using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Visor3D
{
    /// <summary>
    /// Parte del host con el bombeo Win32 (devolver el foco a Godot en
    /// <c>WM_SETFOCUS</c>, clics y <c>WM_PARENTNOTIFY</c>) y con el lanzamiento
    /// del proceso Godot y la espera de su ventana para incrustarla.
    /// </summary>
    public partial class GodotHwndHost
    {
        private const int EsperaVentanaNormalMs = 12_000;
        private const int EsperaVentanaEditorMs = 30_000;

        // ──────────────────────────────────────────────────────────────
        // WndProc — restaurar foco en WM_SETFOCUS y clics
        // ──────────────────────────────────────────────────────────────

        protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_SETFOCUS:
                    // La ventana contenedora recibió foco: pasárselo a Godot
                    if (_childHwnd != IntPtr.Zero)
                    {
                        SetFocus(_childHwnd);
                        handled = true;
                    }
                    break;

                case WM_LBUTTONDOWN:
                case WM_RBUTTONDOWN:
                case WM_MBUTTONDOWN:
                    // Cualquier clic dentro del host: restaurar foco a Godot
                    ActivateGodotFocus();
                    break;

                case WM_PARENTNOTIFY:
                {
                    // Los clics sobre el área 3D los recibe directamente el HWND
                    // hijo de Godot; el padre se entera por esta notificación.
                    // Es la vía que devuelve el teclado a Godot tras usar el chat.
                    int evento = (int)(wParam.ToInt64() & 0xFFFF);
                    if (evento is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN)
                        ActivateGodotFocus();
                    break;
                }
            }

            return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
        }

        // ──────────────────────────────────────────────────────────────
        // Godot process management
        // ──────────────────────────────────────────────────────────────

        private void LaunchGodotAsync()
        {
            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            Task.Run(() =>
            {
                try
                {
                    string args = ConstruirArgumentos();

                    var psi = new ProcessStartInfo
                    {
                        FileName         = _godotExePath,
                        Arguments        = args,
                        UseShellExecute  = false,
                        CreateNoWindow   = false
                    };

                    // La caché de miniaturas del editor va a %LOCALAPPDATA%\Godot.
                    // Redirigirla a una carpeta propia evita los errores
                    // "Can't save PNG / Check user write permissions".
                    if (_editorMode)
                        RedirigirCacheEditor(psi);

                    Console.WriteLine($"[GodotHwndHost] Lanzando proceso Godot: {_godotExePath}");
                    Console.WriteLine($"[GodotHwndHost] Mundo: {(_mainPack ?? _projectPath)}");
                    Console.WriteLine($"[GodotHwndHost] Argumentos completos pasados al motor: {args}");
                    Console.WriteLine($"[GodotHwndHost] Args de red (tras '--'): {(string.IsNullOrEmpty(_userArgs) ? "(ninguno -> SERVIDOR_LOCAL por defecto)" : _userArgs)}");
                    Debug.WriteLine($"[GodotHwndHost] Lanzando proceso Godot: {_godotExePath} {args}");

                    Process? proceso = Process.Start(psi);
                    if (proceso is null)
                    {
                        Console.WriteLine("[GodotHwndHost] ERROR: No se pudo iniciar el proceso de Godot.");
                        return;
                    }
                    _godotProcess = proceso;

                    try
                    {
                        proceso.EnableRaisingEvents = true;
                        proceso.Exited += (_, _) =>
                        {
                            if (_parando) return;
                            try { GodotExited?.Invoke(this, EventArgs.Empty); } catch { }
                        };
                    }
                    catch { /* si no se puede observar el proceso, seguimos igual */ }

                    IntPtr hwnd = EsperarVentanaGodot(proceso, token);
                    if (hwnd == IntPtr.Zero || token.IsCancellationRequested) return;

                    _childHwnd = hwnd;

                    // Volver al hilo UI para embeber
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (_hostHwnd != IntPtr.Zero && _childHwnd != IntPtr.Zero)
                            EmbedChildWindow();
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GodotHwndHost] Error: {ex.Message}");
                }
            }, token);
        }

        private string ConstruirArgumentos()
        {
            string args;
            if (_editorMode)
            {
                // Editor Godot vacío incrustado en el visor.
                args = $"--editor --path \"{_projectPath}\"";
            }
            else if (!string.IsNullOrEmpty(_mainPack))
            {
                // Mundo empaquetado: no hay carpeta de proyecto que cargar.
                args = $"--main-pack \"{_mainPack}\"";
            }
            else
            {
                args = $"--path \"{_projectPath}\"";
                if (!string.IsNullOrEmpty(_scenePath))
                    args += $" \"{_scenePath}\"";
            }
            if (!string.IsNullOrEmpty(_userArgs))
                args += $" -- {_userArgs}";
            return args;
        }

        private static void RedirigirCacheEditor(ProcessStartInfo psi)
        {
            string cache = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WoldVirtualP2P3D", "cache_editor");
            Directory.CreateDirectory(Path.Combine(cache, "Godot"));
            psi.Environment["LOCALAPPDATA"] = cache;
        }

        private IntPtr EsperarVentanaGodot(Process proceso, CancellationToken token)
        {
            int limite = _editorMode ? EsperaVentanaEditorMs : EsperaVentanaNormalMs;
            int pid = proceso.Id;
            int transcurrido = 0;

            while (!token.IsCancellationRequested && !proceso.HasExited && transcurrido < limite)
            {
                Thread.Sleep(100);
                transcurrido += 100;
                proceso.Refresh();

                IntPtr hwnd = proceso.MainWindowHandle;
                if (hwnd != IntPtr.Zero) return hwnd;

                if (transcurrido % 500 == 0)
                {
                    hwnd = FindGodotWindow(pid);
                    if (hwnd != IntPtr.Zero) return hwnd;
                }
            }
            return IntPtr.Zero;
        }
    }
}
