using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Visor3D.Stm;

namespace Visor3D
{
    /// <summary>
    /// Host Win32 que incrusta Godot 4 dentro de WPF via SetParent.
    /// Gestiona el foco del teclado de forma robusta:
    ///   • <c>ComponentDispatcher.ThreadPreprocessMessage</c>: intercepta
    ///     WM_KEYDOWN/UP globalmente antes de que WPF los procese como
    ///     aceleradores (incluidas las flechas de movimiento del avatar).
    ///   • <c>AttachThreadInput</c>: vincula los threads UI-WPF y Godot usando
    ///     los IDs correctos de ambas ventanas.
    ///   • <c>WndProc</c>: restaura el foco automáticamente cuando la ventana
    ///     contenedora recibe WM_SETFOCUS o clics de ratón.
    /// </summary>
    public partial class GodotHwndHost : HwndHost
    {
        private IntPtr _childHwnd = IntPtr.Zero;
        private IntPtr _hostHwnd  = IntPtr.Zero;
        private Process? _godotProcess;
        private CancellationTokenSource? _cts;
        private volatile bool _parando;

        /// <summary>Se dispara si el proceso de Godot termina sin que lo hayamos parado nosotros.</summary>
        public event EventHandler? GodotExited;

        private readonly string? _godotExePath;
        private readonly string? _projectPath;
        private readonly string? _scenePath;
        private readonly string? _userArgs;
        private readonly string? _mainPack;
        private readonly bool _editorMode;

        // Flag para el hook global de teclado
        private bool _preprocessHookRegistrado = false;

        public GodotHwndHost(IntPtr childHwnd)
        {
            _childHwnd = childHwnd;
        }

        public GodotHwndHost(string godotExePath, string projectPath, string scenePath = "", string userArgs = "", string? mainPack = null, bool editorMode = false)
        {
            _godotExePath = godotExePath;
            _projectPath  = projectPath;
            _scenePath    = scenePath;
            _userArgs     = userArgs;
            _mainPack     = mainPack;
            _editorMode   = editorMode;
        }

        public IntPtr ChildHwnd => _childHwnd;

        // ──────────────────────────────────────────────────────────────
        // HwndHost overrides
        // ──────────────────────────────────────────────────────────────

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            // Crear ventana contenedora Win32 "static"
            _hostHwnd = CreateWindowEx(
                0, "static", "",
                WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN,
                0, 0, 1, 1,
                hwndParent.Handle,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            if (_hostHwnd == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

            this.Focusable = true;

            // ── Registrar hook global de teclado ────────────────────────────
            // Las teclas de flecha son aceleradores WPF y no llegan a
            // TranslateAcceleratorCore. El hook ThreadPreprocessMessage las
            // intercepta antes de cualquier procesamiento WPF.
            if (!_preprocessHookRegistrado)
            {
                ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;
                _preprocessHookRegistrado = true;
            }

            if (_childHwnd != IntPtr.Zero)
            {
                EmbedChildWindow();
            }
            else if (!string.IsNullOrEmpty(_godotExePath))
            {
                LaunchGodotAsync();
            }

            return new HandleRef(this, _hostHwnd);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            // Desregistrar hook para evitar memory leak
            if (_preprocessHookRegistrado)
            {
                ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;
                _preprocessHookRegistrado = false;
            }

            _cts?.Cancel();

            if (_hostHwnd != IntPtr.Zero)
            {
                DestroyWindow(_hostHwnd);
                _hostHwnd = IntPtr.Zero;
            }

            StopGodot();
        }

        // ──────────────────────────────────────────────────────────────
        // Hook global de teclado (captura flechas antes de WPF)
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Se llama para CADA mensaje del hilo UI antes de que WPF lo procese.
        /// Si el HwndHost (o Godot) tiene el foco y el mensaje es de teclado,
        /// lo reenviamos a Godot y lo marcamos como handled para que WPF no lo
        /// procese como acelerador (flechas de movimiento del avatar, etc.).
        /// </summary>
        private void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            if (handled) return;
            if (_childHwnd == IntPtr.Zero) return;

            // Solo procesar mensajes de teclado
            if (msg.message != WM_KEYDOWN && msg.message != WM_KEYUP &&
                msg.message != WM_SYSKEYDOWN && msg.message != WM_SYSKEYUP)
                return;

            // Comprobar que el foco está en nuestra ventana o en Godot
            IntPtr focusHwnd = GetFocus();
            bool godotTieneFoco = focusHwnd == _childHwnd || focusHwnd == _hostHwnd ||
                                  IsChildOf(_hostHwnd, focusHwnd);

            // Si el foco nativo ya está en Godot, los mensajes deben seguir su
            // curso normal: Windows genera WM_CHAR para el LineEdit del chat.
            if (godotTieneFoco)
                return;

            // Si el usuario escribe en el chat (u otro TextBox WPF), las teclas
            // son para el texto: no deben mover al avatar ni saltar.
            if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase)
                return;

            // WPF conserva el foco lógico tras usar un control: devolvérselo a
            // Godot para que el siguiente mensaje ya llegue nativo.
            if (IsKeyboardFocusWithin)
                SetFocus(_childHwnd);
        }

        /// <summary>
        /// Genera el WM_CHAR que WPF no traduce al interceptar WM_KEYDOWN.
        /// Sin esto el avatar se mueve pero el LineEdit del chat no recibe letras.
        /// </summary>
        private void ReenviarCaracter(IntPtr virtualKey, IntPtr lParam)
        {
            try
            {
                var estado = new byte[256];
                GetKeyboardState(estado);
                uint scan = (uint)((long)lParam >> 16) & 0xFF;
                var buffer = new StringBuilder(4);
                uint hilo = GetWindowThreadProcessId(_childHwnd, out _);
                IntPtr distribucion = GetKeyboardLayout(hilo);
                int cuantos = ToUnicodeEx((uint)virtualKey, scan, estado, buffer,
                    buffer.Capacity, 0, distribucion);
                if (cuantos > 0)
                    PostMessage(_childHwnd, WM_CHAR, (IntPtr)buffer[0], lParam);
            }
            catch { }
        }

        /// <summary>Devuelve true si <paramref name="child"/> es descendiente de <paramref name="parent"/>.</summary>
        private static bool IsChildOf(IntPtr parent, IntPtr child)
        {
            if (child == IntPtr.Zero || parent == IntPtr.Zero) return false;
            IntPtr current = GetParent(child);
            while (current != IntPtr.Zero)
            {
                if (current == parent) return true;
                current = GetParent(current);
            }
            return false;
        }
    }
}
