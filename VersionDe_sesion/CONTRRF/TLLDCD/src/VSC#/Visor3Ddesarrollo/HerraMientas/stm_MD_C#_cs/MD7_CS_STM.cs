using System;
using System.Windows;

namespace Visor3D
{
    /// <summary>
    /// Parte del host con el embedding de la ventana de Godot (reparentado y
    /// estilos), el modo editor, el reposicionado por DPI y la unión de colas
    /// de input entre los hilos WPF y Godot.
    /// </summary>
    public partial class GodotHwndHost
    {
        private static IntPtr FindGodotWindow(int pid)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((hWnd, _) =>
            {
                GetWindowThreadProcessId(hWnd, out uint wndPid);
                if (wndPid == (uint)pid && IsWindowVisible(hWnd))
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        // ──────────────────────────────────────────────────────────────
        // Embedding & Focus Management
        // ──────────────────────────────────────────────────────────────

        private void EmbedChildWindow()
        {
            if (_childHwnd == IntPtr.Zero || _hostHwnd == IntPtr.Zero) return;

            if (_editorMode)
            {
                EmbedEditorWindow();
                return;
            }

            // Reparentar
            SetParent(_childHwnd, _hostHwnd);

            // Quitar decoraciones
            int style = GetWindowLong(_childHwnd, GWL_STYLE);
            style &= ~WS_CAPTION & ~WS_BORDER & ~WS_POPUP & ~WS_THICKFRAME;
            style |= WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN;
            SetWindowLong(_childHwnd, GWL_STYLE, style);

            int exStyle = GetWindowLong(_childHwnd, GWL_EXSTYLE);
            exStyle &= ~WS_EX_DLGMODALFRAME & ~WS_EX_CLIENTEDGE & ~WS_EX_STATICEDGE & ~WS_EX_WINDOWEDGE;
            // Permite que el host reciba WM_PARENTNOTIFY de los clics en Godot.
            exStyle &= ~WS_EX_NOPARENTNOTIFY;
            SetWindowLong(_childHwnd, GWL_EXSTYLE, exStyle);

            SetWindowPos(_childHwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

            ShowWindow(_childHwnd, SW_SHOW);
            ResizeToActualPixels();

            // Adjuntar colas de input con los thread IDs CORRECTOS de cada ventana
            AttachGodotThreadInput();
            ActivateGodotFocus();
        }

        private void EmbedEditorWindow()
        {
            // El editor de Godot no soporta SetParent (crashea). Se deja como
            // ventana propia sin bordes, con owner el visor y posicionada sobre
            // el contenedor: visualmente queda dentro del visor.
            IntPtr dueno = GetParent(_hostHwnd);
            if (dueno != IntPtr.Zero)
                SetWindowLongPtr(_childHwnd, GWL_HWNDPARENT, dueno);

            int estilo = GetWindowLong(_childHwnd, GWL_STYLE);
            estilo &= ~WS_CAPTION & ~WS_BORDER & ~WS_POPUP & ~WS_THICKFRAME;
            estilo |= WS_VISIBLE | WS_CLIPCHILDREN;
            SetWindowLong(_childHwnd, GWL_STYLE, estilo);
            SetWindowPos(_childHwnd, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            PosicionarEditorEnHost();
            ShowWindow(_childHwnd, SW_SHOW);
        }

        private void PosicionarEditorEnHost()
        {
            if (!_editorMode || _childHwnd == IntPtr.Zero || _hostHwnd == IntPtr.Zero)
                return;
            if (!GetWindowRect(_hostHwnd, out RECT destino))
                return;
            SetWindowPos(_childHwnd, IntPtr.Zero,
                destino.Left, destino.Top,
                Math.Max(1, destino.Right - destino.Left),
                Math.Max(1, destino.Bottom - destino.Top),
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        protected override void OnWindowPositionChanged(Rect rcBoundingBox)
        {
            base.OnWindowPositionChanged(rcBoundingBox);
            PosicionarEditorEnHost();
        }

        private void AttachGodotThreadInput()
        {
            if (_childHwnd == IntPtr.Zero) return;
            try
            {
                // Thread del lado WPF: el del _hostHwnd (ventana contenedora)
                // Thread del lado Godot: el del _childHwnd
                // Usar GetWindowThreadProcessId de cada ventana, NO GetCurrentThreadId(),
                // para obtener el thread correcto incluso si hay marshaling de hilos.
                uint wpfThread   = _hostHwnd != IntPtr.Zero
                    ? GetWindowThreadProcessId(_hostHwnd, out _)
                    : GetCurrentThreadId();
                uint godotThread = GetWindowThreadProcessId(_childHwnd, out _);

                if (godotThread != 0 && wpfThread != 0 && godotThread != wpfThread)
                {
                    AttachThreadInput(wpfThread, godotThread, true);
                }
            }
            catch { }
        }
    }
}
