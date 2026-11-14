using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Visor3D
{
    /// <summary>
    /// Resuelve las rutas del sistema vivo (TLLDCD/src) para que el visor no
    /// dependa de copias antiguas en Sistema_Principal/PRY. La fuente de verdad
    /// es Rutas.json; las rutas por convención quedan como respaldo.
    /// </summary>
    public static class RutasVisor
    {
        private static readonly Lazy<string?> _srcDir = new(ResolverSrc, true);
        private static readonly Lazy<string?> _godotProyecto = new(ResolverGodotProyecto, true);
        private static readonly Lazy<string?> _python = new(ResolverPython, true);
        private static readonly Lazy<string?> _pythonW = new(ResolverPythonW, true);
        private static readonly Lazy<string?> _servidorLucia = new(ResolverServidorLucia, true);
        private static readonly Lazy<string?> _servidorBlockchain = new(ResolverServidorBlockchain, true);

        /// <summary>Raíz del árbol actual (carpeta que contiene Rutas.json, STM_HRTS y VSC#).</summary>
        public static string? SrcDir => _srcDir.Value;

        /// <summary>Directorio de trabajo para los procesos Python (src).</summary>
        public static string DirTrabajo => SrcDir ?? AppContext.BaseDirectory;

        public static string? GodotProyecto => _godotProyecto.Value;
        public static string? PythonExe => _python.Value;
        public static string? PythonWExe => _pythonW.Value;
        public static string? ServidorLucia => _servidorLucia.Value;
        public static string? ServidorBlockchain => _servidorBlockchain.Value;

        /// <summary>Carpeta HerraMientas: scripts .bat/.ps1 junto al ejecutable o en el árbol.</summary>
        public static string? HerraMientasDir
        {
            get
            {
                if (File.Exists(Path.Combine(AppContext.BaseDirectory, "Iniciar_Lucia_Visor.ps1")))
                    return AppContext.BaseDirectory;

                if (SrcDir != null)
                {
                    string cand = Path.Combine(SrcDir, "VSC#", "Visor3Ddesarrollo", "HerraMientas");
                    if (Directory.Exists(cand)) return cand;
                }

                return null;
            }
        }

        /// <summary>
        /// Propaga PYTHONPATH=src al proceso hijo para que los paquetes del
        /// taller (STM_BKCH, STMGNRL, STM_CH...) se importen sin depender del cwd.
        /// </summary>
        public static void AplicarPythonPath(ProcessStartInfo psi)
        {
            string? src = SrcDir;
            if (string.IsNullOrEmpty(src)) return;

            string actual = psi.Environment.TryGetValue("PYTHONPATH", out string? valor)
                ? (valor ?? "")
                : (Environment.GetEnvironmentVariable("PYTHONPATH") ?? "");

            psi.Environment["PYTHONPATH"] = string.IsNullOrEmpty(actual)
                ? src
                : src + Path.PathSeparator + actual;

            psi.Environment["WVP_SRC_DIR"] = src;
        }

        private static string? Env(string nombre)
        {
            string? valor = Environment.GetEnvironmentVariable(nombre);
            return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
        }

        private static bool EsSrc(string? dir) =>
            !string.IsNullOrWhiteSpace(dir) &&
            File.Exists(Path.Combine(dir!, "Rutas.json")) &&
            Directory.Exists(Path.Combine(dir!, "STM_HRTS")) &&
            Directory.Exists(Path.Combine(dir!, "VSC#"));

        private static string? ResolverSrc()
        {
            foreach (string nombre in new[] { "WVP_SRC_DIR", "WOLDVIRTUAL_SRC_DIR" })
            {
                string? valor = Env(nombre);
                if (EsSrc(valor)) return Path.GetFullPath(valor!);
            }

            string? actual = AppContext.BaseDirectory.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            for (int i = 0; i < 12 && !string.IsNullOrEmpty(actual); i++)
            {
                if (EsSrc(actual)) return actual;

                string bajoSrc = Path.Combine(actual!, "src");
                if (EsSrc(bajoSrc)) return Path.GetFullPath(bajoSrc);

                actual = Directory.GetParent(actual!)?.FullName;
            }

            return null;
        }

        private static string? RutaJson(params string[] claves)
        {
            if (SrcDir == null) return null;

            string archivo = Path.Combine(SrcDir, "Rutas.json");
            if (!File.Exists(archivo)) return null;

            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(archivo));
                JsonElement elemento = doc.RootElement;

                foreach (string clave in claves)
                {
                    if (!elemento.TryGetProperty(clave, out elemento)) return null;
                }

                string? relativa = elemento.GetString();
                if (string.IsNullOrWhiteSpace(relativa)) return null;

                return Path.GetFullPath(Path.Combine(
                    SrcDir, relativa.Replace('/', Path.DirectorySeparatorChar)));
            }
            catch
            {
                return null;
            }
        }

        private static string? ResolverGodotProyecto()
        {
            string? proyecto = RutaJson("runtime", "godot_project");
            if (!string.IsNullOrEmpty(proyecto) && File.Exists(proyecto))
                return Path.GetDirectoryName(proyecto);

            if (SrcDir != null)
            {
                string cand = Path.Combine(SrcDir, "MTBGT", "WoldVirtualP2P3D");
                if (File.Exists(Path.Combine(cand, "project.godot"))) return cand;
            }

            string? actual = AppContext.BaseDirectory;
            for (int i = 0; i < 10 && !string.IsNullOrEmpty(actual); i++)
            {
                foreach (string rel in new[]
                {
                    Path.Combine("MTBGT", "WoldVirtualP2P3D"),
                    Path.Combine("src", "MTBGT", "WoldVirtualP2P3D"),
                })
                {
                    string cand = Path.Combine(actual!, rel);
                    if (File.Exists(Path.Combine(cand, "project.godot"))) return cand;
                }

                actual = Directory.GetParent(actual!)?.FullName;
            }

            return null;
        }

        private static IEnumerable<string> CandidatosPython()
        {
            string? wvpPython = Env("WVP_PYTHON");
            if (wvpPython != null) yield return wvpPython;

            string? wvpPythonExe = Env("WOLDVIRTUAL_PYTHON_EXE");
            if (wvpPythonExe != null) yield return wvpPythonExe;

            if (SrcDir != null)
                yield return Path.Combine(SrcDir, "LC_STM", "python", "python.exe");

            yield return Path.Combine(AppContext.BaseDirectory, "LC_STM", "python", "python.exe");

            string localPrograms = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Python");
            foreach (string version in new[] { "Python315", "Python314", "Python313", "Python312", "Python311" })
                yield return Path.Combine(localPrograms, version, "python.exe");

            foreach (string raiz in new[] { @"C:\Python315", @"C:\Python314", @"C:\Python313", @"C:\Python312", @"C:\Python311" })
                yield return Path.Combine(raiz, "python.exe");
        }

        private static string? BuscarEnPath(string exe)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = exe,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using var proceso = Process.Start(psi);
                if (proceso == null) return null;

                string salida = proceso.StandardOutput.ReadLine() ?? "";
                proceso.WaitForExit(1500);

                string ruta = salida.Trim();
                return !string.IsNullOrWhiteSpace(ruta) && File.Exists(ruta) ? ruta : null;
            }
            catch
            {
                return null;
            }
        }

        private static string? ResolverPython()
        {
            foreach (string cand in CandidatosPython())
                if (!string.IsNullOrWhiteSpace(cand) && File.Exists(cand)) return cand;

            return BuscarEnPath("python");
        }

        private static string? ResolverPythonW()
        {
            string? preferido = Env("WVP_PYTHONW");
            if (preferido != null && File.Exists(preferido)) return preferido;

            string? python = PythonExe;
            if (!string.IsNullOrEmpty(python))
            {
                string hermano = Path.Combine(Path.GetDirectoryName(python) ?? "", "pythonw.exe");
                if (File.Exists(hermano)) return hermano;
            }

            return python ?? BuscarEnPath("pythonw");
        }

        private static string? ResolverServidorLucia()
        {
            string? proyecto = GodotProyecto;
            if (proyecto == null) return null;

            string cand = Path.Combine(
                proyecto, "lucia3D", "GLTF_lucIA", "celebro_3D", "puente_3D", "servidor_lucia.py");
            if (File.Exists(cand)) return cand;

            try
            {
                var encontrados = Directory.GetFiles(proyecto, "servidor_lucia.py", SearchOption.AllDirectories);
                return encontrados.FirstOrDefault(p => p.Contains("GLTF_lucIA", StringComparison.OrdinalIgnoreCase))
                    ?? encontrados.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static string? ResolverServidorBlockchain()
        {
            string? declarado = RutaJson("archivos", "servidor_oficial");
            if (!string.IsNullOrEmpty(declarado) && File.Exists(declarado)) return declarado;

            if (SrcDir != null)
            {
                string actual = Path.Combine(SrcDir, "STM_BKCH", "stm_bkch1", "servidor_oficial.py");
                if (File.Exists(actual)) return actual;

                // Compatibilidad con árboles anteriores a la sección stm_bkch1.
                string legado = Path.Combine(SrcDir, "STM_BKCH", "servidor_oficial.py");
                if (File.Exists(legado)) return legado;
            }

            return null;
        }
    }
}
