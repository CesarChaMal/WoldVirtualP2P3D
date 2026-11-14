using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Visor3D.Services
{
    /// <summary>
    /// Sincroniza automáticamente las rutas Python del proyecto al arrancar el visor.
    /// Ejecuta el motor de sincronización de STM_HRTS sobre el árbol vivo
    /// (TLLDCD/src) usando el Python resuelto por <see cref="RutasVisor"/>.
    ///
    /// Se llama desde App.xaml.cs durante el arranque, antes de mostrar la ventana
    /// principal. La operación es asíncrona y fire-and-forget: si falla no bloquea
    /// el arranque del visor.
    /// </summary>
    public static class RutasSyncService
    {
        // Tiempo máximo de espera antes de cancelar la sincronización (ms)
        private const int TimeoutMs = 15_000;

        /// <summary>
        /// Resultado devuelto por la sincronización de rutas.
        /// </summary>
        public sealed class ResultadoSync
        {
            public bool Ok { get; init; }
            public int Corregidas { get; init; }
            public int Correctas { get; init; }
            public string? Mensaje { get; init; }
            public string? Error { get; init; }
        }

        /// <summary>
        /// Lanza la sincronización de rutas de forma asíncrona y fire-and-forget.
        /// Los resultados se escriben al log del visor; no lanza excepciones.
        /// </summary>
        public static void LanzarEnSegundoPlano()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var r = await SincronizarRutasAsync();
                    if (r.Ok)
                        VisorLog.Info($"[RutasSync] OK — {r.Correctas} correctas, {r.Corregidas} corregidas. {r.Mensaje}");
                    else
                        VisorLog.Info($"[RutasSync] Sin ejecutar: {r.Error}");
                }
                catch (Exception ex)
                {
                    VisorLog.Info($"[RutasSync] Error inesperado: {ex.Message}");
                }
            });
        }

        /// <summary>Intérprete Python resuelto sobre la configuración viva.</summary>
        private static string? EncontrarPythonExe() => RutasVisor.PythonExe;

        /// <summary>
        /// Ejecuta <c>python.exe -m STM_HRTS.utils.add_utils sincronizar --json</c>
        /// usando el Python embebido y devuelve el resultado parseado.
        /// </summary>
        public static async Task<ResultadoSync> SincronizarRutasAsync(
            CancellationToken ct = default)
        {
            string? pythonExe = EncontrarPythonExe();
            if (pythonExe == null)
                return new ResultadoSync { Ok = false, Error = "Python embebido no encontrado." };

            // El módulo STM_HRTS está en src/STM_HRTS; trabajamos desde src/
            string workDir = RutasVisor.DirTrabajo;

            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = "-m STM_HRTS.utils.add_utils sincronizar --json",
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            RutasVisor.AplicarPythonPath(psi);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeoutMs);

            try
            {
                using var proc = Process.Start(psi);
                if (proc == null)
                    return new ResultadoSync { Ok = false, Error = "No se pudo iniciar python.exe." };

                string stdout = await proc.StandardOutput.ReadToEndAsync();
                string stderr = await proc.StandardError.ReadToEndAsync();

                await proc.WaitForExitAsync(cts.Token);

                return ParsearSalida(stdout, stderr, proc.ExitCode);
            }
            catch (OperationCanceledException)
            {
                return new ResultadoSync { Ok = false, Error = "Tiempo de espera agotado (>15 s)." };
            }
            catch (Exception ex)
            {
                return new ResultadoSync { Ok = false, Error = ex.Message };
            }
        }

        private static ResultadoSync ParsearSalida(string stdout, string stderr, int exitCode)
        {
            string? json = ExtraerJson(stdout);
            if (json != null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    int correctas = Contar(root, "ok_sin_cambios", "correctas");
                    int corregidas = Contar(root, "corregidas");
                    int pendientes =
                        Contar(root, "sin_candidato") +
                        Contar(root, "ambiguas") +
                        Contar(root, "errores");

                    bool ok = !root.TryGetProperty("ok", out var okProp) ||
                              okProp.ValueKind != JsonValueKind.False;
                    if (pendientes > 0) ok = false;

                    string mensaje = $"rutas correctas={correctas}, corregidas={corregidas}"
                        + (pendientes > 0 ? $", pendientes={pendientes}" : "");

                    return new ResultadoSync
                    {
                        Ok = ok,
                        Correctas = correctas,
                        Corregidas = corregidas,
                        Mensaje = mensaje,
                        Error = ok ? null : mensaje,
                    };
                }
                catch
                {
                    // JSON no parseable: registrar texto plano como mensaje.
                }
            }

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                return new ResultadoSync
                {
                    Ok = exitCode == 0,
                    Mensaje = stdout.Trim(),
                    Error = exitCode != 0
                        ? (!string.IsNullOrWhiteSpace(stderr) ? stderr.Trim() : stdout.Trim())
                        : null,
                };
            }

            return new ResultadoSync
            {
                Ok = false,
                Error = !string.IsNullOrWhiteSpace(stderr)
                    ? stderr.Trim()
                    : $"Proceso terminó con código {exitCode} sin salida.",
            };
        }

        /// <summary>Recorta la salida hasta el primer objeto JSON completo.</summary>
        private static string? ExtraerJson(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;

            int inicio = texto.IndexOf('{');
            int fin = texto.LastIndexOf('}');
            if (inicio < 0 || fin <= inicio) return null;

            return texto.Substring(inicio, fin - inicio + 1);
        }

        /// <summary>Cuenta elementos de una lista JSON o lee un entero directo.</summary>
        private static int Contar(JsonElement root, params string[] claves)
        {
            foreach (string clave in claves)
            {
                if (!root.TryGetProperty(clave, out var valor)) continue;

                return valor.ValueKind switch
                {
                    JsonValueKind.Array => valor.GetArrayLength(),
                    JsonValueKind.Number => valor.TryGetInt32(out int n) ? n : 0,
                    JsonValueKind.True => 1,
                    _ => 0,
                };
            }
            return 0;
        }
    }
}
