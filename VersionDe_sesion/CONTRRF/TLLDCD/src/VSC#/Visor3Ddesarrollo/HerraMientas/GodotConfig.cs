using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Visor3D
{
    public static class GodotConfig
    {
        private const string GodotName = "Godot_v4.7.2-stable_win64.exe";

        private static string ExeDir =>
            Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName
                ?? AppDomain.CurrentDomain.BaseDirectory)
            ?? AppDomain.CurrentDomain.BaseDirectory;

        public static string GetGodotExePath()
        {
            // 1. Recursos extraidos al LocalAppData (exe standalone)
            if (File.Exists(AppExtractor.GodotExePath)) return AppExtractor.GodotExePath;

            // 2. Junto al exe publicado
            string nextToExe = Path.Combine(ExeDir, "Servidor3D", GodotName);
            if (File.Exists(nextToExe)) return nextToExe;

            // 3. Junto al proyecto Godot del arbol vivo (TLLDCD/src)
            string? proyecto = RutasVisor.GodotProyecto;
            if (!string.IsNullOrEmpty(proyecto))
            {
                string? srcDir = Directory.GetParent(proyecto)?.Parent?.FullName;

                foreach (string cand in new[]
                {
                    Path.Combine(proyecto, "Servidor3D", GodotName),
                    Path.Combine(proyecto, GodotName),
                    srcDir != null ? Path.Combine(srcDir, "VSC#", "Servidor3D", GodotName) : "",
                })
                {
                    if (!string.IsNullOrWhiteSpace(cand) && File.Exists(cand)) return cand;
                }
            }

            // 4. Busqueda relativa subiendo carpetas
            string? current = ExeDir;
            for (int i = 0; i < 10 && current != null; i++)
            {
                foreach (string rel in new[]
                {
                    Path.Combine("Servidor3D", GodotName),
                    Path.Combine("src", "VSC#", "Servidor3D", GodotName),
                    Path.Combine("MTBGT", "Servidor3D", GodotName),
                })
                {
                    string cand = Path.Combine(current, rel);
                    if (File.Exists(cand)) return cand;
                }
                current = Directory.GetParent(current)?.FullName;
            }

            // 5. Cualquier runtime instalado (la version mas reciente primero)
            string runtimeRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WoldVirtualP2P3D", "runtime");
            try
            {
                if (Directory.Exists(runtimeRoot))
                {
                    foreach (string version in Directory.GetDirectories(runtimeRoot)
                        .OrderByDescending(d => Path.GetFileName(d)))
                    {
                        string cand = Path.Combine(version, "Servidor3D", GodotName);
                        if (File.Exists(cand)) return cand;
                    }
                }
            }
            catch { }

            return "";
        }

        public static string GetProjectPath()
        {
            // 1. Proyecto del arbol vivo (TLLDCD/src): es la configuracion actual
            string? vivo = RutasVisor.GodotProyecto;
            if (!string.IsNullOrEmpty(vivo) && Directory.Exists(vivo) &&
                File.Exists(Path.Combine(vivo, "project.godot")))
                return vivo;

            // 2. Recursos extraidos al LocalAppData
            if (Directory.Exists(AppExtractor.ProjectPath) &&
                File.Exists(Path.Combine(AppExtractor.ProjectPath, "project.godot")))
                return AppExtractor.ProjectPath;

            // 3. Junto al exe publicado
            string nextToExe = Path.Combine(ExeDir, "MTBGT", "WoldVirtualP2P3D");
            if (File.Exists(Path.Combine(nextToExe, "project.godot")))
                return nextToExe;

            // 4. Busqueda relativa
            string? current = ExeDir;
            for (int i = 0; i < 10 && current != null; i++)
            {
                foreach (string rel in new[]
                {
                    Path.Combine("MTBGT", "WoldVirtualP2P3D"),
                    Path.Combine("src", "MTBGT", "WoldVirtualP2P3D"),
                })
                {
                    string cand = Path.Combine(current, rel);
                    if (File.Exists(Path.Combine(cand, "project.godot"))) return cand;
                }
                current = Directory.GetParent(current)?.FullName;
            }

            return "";
        }

        public static string GetScenePath() => "res://WoldVirtual3DP2P.tscn";

        /// <summary>
        /// Mundo empaquetado (PCK) para lanzar Godot con <c>--main-pack</c>.
        /// Primero el que extrae <see cref="AppExtractor"/>; si no, junto al .exe
        /// o subiendo carpetas (modo desarrollo). Devuelve "" si no hay PCK.
        /// </summary>
        public static string GetMainPackPath()
        {
            // 1. Paquete volátil (embebido en el .exe, materializado en %TEMP%)
            string? volatil = Visor3D.CargaVolatil.VolatilePackage.PckVolatilPath;
            if (!string.IsNullOrEmpty(volatil) && File.Exists(volatil)) return volatil;

            if (File.Exists(AppExtractor.PckPath)) return AppExtractor.PckPath;

            string nextToExe = Path.Combine(ExeDir, "wvp3d.pck");
            if (File.Exists(nextToExe)) return nextToExe;

            // 2. Junto al proyecto del arbol vivo
            string proyecto = GetProjectPath();
            if (!string.IsNullOrEmpty(proyecto))
            {
                string juntoProyecto = Path.Combine(proyecto, "wvp3d.pck");
                if (File.Exists(juntoProyecto)) return juntoProyecto;
            }

            // 3. Subiendo carpetas
            string? current = ExeDir;
            for (int i = 0; i < 10 && current != null; i++)
            {
                string c1 = Path.Combine(current, "wvp3d.pck");
                if (File.Exists(c1)) return c1;

                string c2 = Path.Combine(current, "src", "VSC#", "wvp3d.pck");
                if (File.Exists(c2)) return c2;

                string c3 = Path.Combine(current, "VSC#", "wvp3d.pck");
                if (File.Exists(c3)) return c3;

                current = Directory.GetParent(current)?.FullName;
            }
            return "";
        }
    }
}
