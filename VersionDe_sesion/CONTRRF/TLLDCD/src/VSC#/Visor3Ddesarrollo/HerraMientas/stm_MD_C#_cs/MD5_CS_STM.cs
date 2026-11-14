using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Visor3D
{
    /// <summary>
    /// Parte del host con foco, teclado, redimensionado, parada y P/Invoke
    /// (paridad con el bloque extraído de <c>GodotHwndHost.cs</c>).
    /// </summary>
    public partial class GodotHwndHost
    {
        public void ActivateGodotFocus()
        {
            if (_childHwnd == IntPtr.Zero) return;
            try
            {
                AttachGodotThreadInput();
                SetFocus(_childHwnd);
                SetActiveWindow(_childHwnd);
            }
            catch { }
        }

        // ──────────────────────────────────────────────────────────────
        // Keyboard Forwarding (TranslateAcceleratorCore & TabIntoCore)
        // ──────────────────────────────────────────────────────────────

        protected override bool TabIntoCore(TraversalRequest request)
        {
            ActivateGodotFocus();
            return true;
        }

        protected override bool TranslateAcceleratorCore(ref MSG msg, ModifierKeys modifiers)
        {
            // Igual que en OnThreadPreprocessMessage: escribir en el chat manda,
            // salvo que el foco nativo ya esté devuelto a Godot.
            bool escribiendoWpf =
                GetFocus() != _childHwnd &&
                Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase;

            if (_childHwnd != IntPtr.Zero && !escribiendoWpf)
            {
                // Reenviar mensajes de teclado que lleguen por esta vía (fallback).
                // La vía principal es OnThreadPreprocessMessage.
                if (msg.message == WM_KEYDOWN || msg.message == WM_KEYUP ||
                    msg.message == WM_CHAR    || msg.message == WM_SYSKEYDOWN || msg.message == WM_SYSKEYUP)
                {
                    PostMessage(_childHwnd, msg.message, msg.wParam, msg.lParam);
                    if (msg.message == WM_KEYDOWN)
                        ReenviarCaracter(msg.wParam, msg.lParam);
                    return true;
                }
            }
            return base.TranslateAcceleratorCore(ref msg, modifiers);
        }

        // ──────────────────────────────────────────────────────────────
        // Resize & WPF events
        // ──────────────────────────────────────────────────────────────

        public void ResizeToActualPixels()
        {
            if (_hostHwnd == IntPtr.Zero || _childHwnd == IntPtr.Zero) return;
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            var source = PresentationSource.FromVisual(this);
            double dpiX = 1.0, dpiY = 1.0;
            if (source?.CompositionTarget != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            int w = (int)Math.Max(1, ActualWidth  * dpiX);
            int h = (int)Math.Max(1, ActualHeight * dpiY);

            MoveWindow(_hostHwnd, 0, 0, w, h, true);
            MoveWindow(_childHwnd, 0, 0, w, h, true);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            ResizeToActualPixels();
        }

        /// <summary>
        /// Al mover la ventana entre monitores con distinto escalado, WPF cambia el DPI:
        /// el viewport de Godot se recalcula en píxeles físicos con la nueva escala.
        /// </summary>
        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            ResizeToActualPixels();
        }

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);
            ActivateGodotFocus();
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);
            this.Focus();
            ActivateGodotFocus();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            ActivateGodotFocus();
        }

        // ──────────────────────────────────────────────────────────────
        // Stop
        // ──────────────────────────────────────────────────────────────

        public void StopGodot()
        {
            _parando = true;
            _cts?.Cancel();
            try
            {
                if (_godotProcess != null && !_godotProcess.HasExited)
                {
                    _godotProcess.CloseMainWindow();
                    if (!_godotProcess.WaitForExit(1200))
                        _godotProcess.Kill();
                    _godotProcess.Dispose();
                    _godotProcess = null;
                }
            }
            catch { }
        }

        // ──────────────────────────────────────────────────────────────
        // P/Invoke
        // ──────────────────────────────────────────────────────────────

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW")]
        private static extern IntPtr CreateWindowEx(
            int dwExStyle, string lpClassName, string lpWindowName, int dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr Child, IntPtr NewParent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode,
            byte[] lpKeyState, StringBuilder pwszBuff, int cchBuff, uint wFlags,
            IntPtr dwhkl);

        private const int GWL_STYLE   = -16;
        private const int GWL_EXSTYLE = -20;
        private const int GWL_HWNDPARENT = -8;

        private const int WS_VISIBLE      = 0x10000000;
        private const int WS_CHILD        = 0x40000000;
        private const int WS_POPUP        = unchecked((int)0x80000000);
        private const int WS_BORDER       = 0x00800000;
        private const int WS_DLGFRAME     = 0x00400000;
        private const int WS_CAPTION      = WS_BORDER | WS_DLGFRAME;
        private const int WS_THICKFRAME   = 0x00040000;
        private const int WS_CLIPCHILDREN = 0x02000000;

        private const int WS_EX_DLGMODALFRAME  = 0x00000001;
        private const int WS_EX_NOPARENTNOTIFY = 0x00000004;
        private const int WS_EX_WINDOWEDGE    = 0x00000100;
        private const int WS_EX_CLIENTEDGE    = 0x00000200;
        private const int WS_EX_STATICEDGE    = 0x00200000;

        private const int SW_SHOW = 5;

        private const uint SWP_NOSIZE       = 0x0001;
        private const uint SWP_NOMOVE       = 0x0002;
        private const uint SWP_NOZORDER     = 0x0004;
        private const uint SWP_NOACTIVATE   = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW   = 0x0040;

        private const int WM_SETFOCUS    = 0x0007;
        private const int WM_KEYDOWN     = 0x0100;
        private const int WM_KEYUP       = 0x0101;
        private const int WM_CHAR        = 0x0102;
        private const int WM_SYSKEYDOWN  = 0x0104;
        private const int WM_SYSKEYUP    = 0x0105;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_PARENTNOTIFY = 0x0210;
    }
}
