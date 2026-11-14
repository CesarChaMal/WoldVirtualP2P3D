using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Visor3D.Stm
{
    /// <summary>
    /// Archivos de código vacíos para LucIA (paridad con <c>STM_MD4.py</c>):
    /// descubre los lenguajes presentes en <c>TLLDCD/src</c> y crea archivos
    /// vacíos con la misma jerarquía que <see cref="Md1Stm"/>.
    /// </summary>
    /// <remarks>
    /// El comando <c>crear archivo</c> puede crear un archivo suelto
    /// <c>ruta/nombre(.extensión)</c> o recrear vacía toda la jerarquía de
    /// código de la fuente. Módulo independiente: no crea carpetas ni evalúa
    /// la red neuronal.
    /// </remarks>
    public static class Md4Stm
    {
        /// <summary>Extensiones de código que el constructor recrea vacías.</summary>
        public static FrozenSet<string> ExtensionesCodigo { get; } =
            new[]
            {
                ".py", ".cs", ".gd", ".gdshader", ".tscn", ".tres",
                ".xaml", ".csproj", ".html", ".mjs", ".js", ".ts",
                ".sh", ".bat", ".ps1", ".cmd",
            }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        /// <summary>Lenguaje asociado a cada extensión (informes y diagnósticos).</summary>
        public static FrozenDictionary<string, string> LenguajesPorExtension { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [".py"] = "python",
                [".cs"] = "csharp",
                [".gd"] = "gdscript",
                [".gdshader"] = "gdshader",
                [".tscn"] = "godot",
                [".tres"] = "godot",
                [".xaml"] = "xaml",
                [".csproj"] = "csharp",
                [".html"] = "html",
                [".mjs"] = "javascript",
                [".js"] = "javascript",
                [".ts"] = "typescript",
                [".sh"] = "shell",
                [".bat"] = "batch",
                [".ps1"] = "powershell",
                [".cmd"] = "batch",
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        /// <summary>Alias de lenguaje → extensión (resolución de <c>crear archivo</c>).</summary>
        public static FrozenDictionary<string, string> ExtensionesPorLenguaje { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["python"] = ".py", ["py"] = ".py",
                ["csharp"] = ".cs", ["cs"] = ".cs", ["c#"] = ".cs",
                ["gdscript"] = ".gd", ["gd"] = ".gd", ["godot"] = ".gd",
                ["gdshader"] = ".gdshader", ["shader"] = ".gdshader",
                ["xaml"] = ".xaml",
                ["html"] = ".html",
                ["javascript"] = ".js", ["js"] = ".js", ["mjs"] = ".mjs",
                ["typescript"] = ".ts", ["ts"] = ".ts",
                ["shell"] = ".sh", ["sh"] = ".sh",
                ["batch"] = ".bat", ["bat"] = ".bat",
                ["powershell"] = ".ps1", ["ps1"] = ".ps1",
                ["cmd"] = ".cmd",
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        /// <summary>Nombre por defecto cuando se pide un archivo sin nombre.</summary>
        public const string NombreArchivoPredeterminado = "archivo";

        /// <summary>
        /// Recorre la fuente y devuelve los archivos de código, sin cachés.
        /// Las rutas devueltas son absolutas y el orden es estable (carpetas
        /// ordenadas), aunque no alfabético global.
        /// </summary>
        public static IReadOnlyList<string> RutasProgramacion(
            string? rutaFuente = null,
            IEnumerable<string>? extensiones = null)
        {
            string fuente = rutaFuente ?? Md1Stm.RutaFuentePredeterminada;
            FrozenSet<string> deseadas = NormalizarExtensiones(
                extensiones, Md4Stm.ExtensionesCodigo);
            return RecorrerCodigo(fuente, deseadas);
        }

        /// <summary>
        /// Cuenta los archivos de código por extensión en <c>TLLDCD/src</c>.
        /// Resultado ordenado por cantidad descendente y extensión ascendente.
        /// </summary>
        public static IReadOnlyDictionary<string, int> DescubrirLenguajesFuente(
            string? rutaFuente = null,
            IEnumerable<string>? extensiones = null)
        {
            var conteo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string archivo in RutasProgramacion(rutaFuente, extensiones))
            {
                string extension = Path.GetExtension(archivo).ToLowerInvariant();
                if (extension.Length == 0)
                    continue;

                conteo[extension] = conteo.TryGetValue(extension, out int total)
                    ? total + 1
                    : 1;
            }

            var ordenado = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> par in conteo
                         .OrderByDescending(par => par.Value)
                         .ThenBy(par => par.Key, StringComparer.Ordinal))
            {
                ordenado[par.Key] = par.Value;
            }

            return ordenado;
        }

        /// <summary>
        /// Rutas relativas (separador <c>/</c>) de los archivos de código de la
        /// fuente, ordenadas alfabéticamente. Si la fuente falla, devuelve vacío.
        /// </summary>
        public static IReadOnlyList<string> DescubrirArchivosFuente(
            string? rutaFuente = null,
            IEnumerable<string>? extensiones = null)
        {
            string fuente = rutaFuente ?? Md1Stm.RutaFuentePredeterminada;
            try
            {
                string raiz = Path.GetFullPath(fuente);
                var relativas = new List<string>();
                foreach (string archivo in RutasProgramacion(raiz, extensiones))
                {
                    relativas.Add(
                        Path.GetRelativePath(raiz, archivo).Replace('\\', '/'));
                }

                relativas.Sort(StringComparer.Ordinal);
                return relativas;
            }
            catch (Exception error) when (
                error is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException)
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>Añade la extensión del lenguaje si el nombre no la lleva.</summary>
        public static string ResolverExtension(string nombre, string lenguaje = "")
        {
            string limpio = (nombre ?? string.Empty).Trim();
            string pedido = (lenguaje ?? string.Empty).Trim().ToLowerInvariant();
            if (pedido.Length == 0)
                return limpio;

            string extension = ExtensionesPorLenguaje.TryGetValue(
                pedido, out string? encontrada) ? encontrada : "." + pedido;
            if (extension.Length > 0 &&
                !limpio.EndsWith(extension, StringComparison.Ordinal))
            {
                return limpio + extension;
            }

            return limpio;
        }

        /// <summary>
        /// Crea un archivo vacío <c>ruta/nombre(.ext)</c> y devuelve su ruta.
        /// Si se pasa <paramref name="maquina"/> se delega en su
        /// <see cref="ICreadorArchivos.CrearArchivo"/>; si no, se crea en disco.
        /// </summary>
        public static string CrearArchivoVacio(
            string ruta,
            string nombre,
            string lenguaje = "",
            ICreadorArchivos? maquina = null)
        {
            string limpio = (nombre ?? string.Empty).Trim();
            if (limpio.Length == 0 || limpio is "." or ".." ||
                Path.GetFileName(limpio) != limpio)
            {
                throw new ArgumentException(
                    "Nombre de archivo no válido: '" + nombre + "'",
                    nameof(nombre));
            }

            if (maquina is not null)
                return maquina.CrearArchivo(ruta, limpio, lenguaje ?? string.Empty);

            string conExtension = ResolverExtension(limpio, lenguaje ?? string.Empty);
            string destino = Path.GetFullPath(
                Path.Combine(ExpandirInicio(ruta), conExtension));
            Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
            if (!File.Exists(destino))
            {
                using (File.Create(destino))
                {
                }
            }

            return destino;
        }

        /// <summary>
        /// Crea <c>base/raiz</c> y los archivos relativos dentro; devuelve la
        /// traza. Las rutas absolutas, con <c>.</c>/<c>..</c> o sin extensión
        /// se rechazan sin detener el resto.
        /// </summary>
        public static ResultadoArchivos CrearJerarquiaArchivos(
            string baseRuta,
            string raiz,
            IEnumerable<string> archivos,
            ICreadorArchivos? maquina = null)
        {
            string raizTexto = (raiz ?? string.Empty).Trim();
            string raizRuta = raizTexto.Length == 0
                ? baseRuta
                : Path.Combine(baseRuta, raizTexto);
            var creados = new List<string>();
            var pasos = new List<PasoArchivo>();
            var errores = new List<string>();

            try
            {
                Directory.CreateDirectory(raizRuta);
            }
            catch (Exception error) when (
                error is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException)
            {
                return new ResultadoArchivos
                {
                    Ok = false,
                    Raiz = raizRuta,
                    Error = error.GetType().Name + ": " + error.Message,
                };
            }

            string raizAbsoluta = Path.GetFullPath(raizRuta);
            int numero = 1;
            foreach (string relativa in archivos)
            {
                IReadOnlyList<string>? partes = PartesValidas(relativa);
                if (partes is null)
                {
                    errores.Add("'" + relativa + "': ruta no válida");
                    continue;
                }

                string destino = Path.Combine(
                    new[] { raizAbsoluta }.Concat(partes).ToArray());
                if (!destino.StartsWith(
                        raizAbsoluta + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                {
                    errores.Add("'" + relativa + "': ruta fuera de la raíz");
                    continue;
                }

                try
                {
                    string carpeta = Path.GetDirectoryName(destino)!;
                    Directory.CreateDirectory(carpeta);
                    if (maquina is not null)
                        maquina.CrearArchivo(carpeta, partes[^1]);
                    else
                        CrearArchivoVacio(carpeta, partes[^1]);
                }
                catch (Exception error) when (
                    error is IOException or UnauthorizedAccessException or
                    ArgumentException or NotSupportedException)
                {
                    errores.Add("'" + relativa + "': " + error.Message);
                    continue;
                }

                creados.Add(destino);
                pasos.Add(new PasoArchivo(numero++, relativa, destino));
            }

            return new ResultadoArchivos
            {
                Ok = errores.Count == 0,
                Raiz = raizRuta,
                Archivos = creados,
                Total = creados.Count,
                Pasos = pasos,
                Errores = errores,
            };
        }

        // ------------------------------------------------------------------
        // Internos compartidos
        // ------------------------------------------------------------------

        private static FrozenSet<string> NormalizarExtensiones(
            IEnumerable<string>? extensiones,
            FrozenSet<string> predeterminadas)
        {
            if (extensiones is null)
                return predeterminadas;

            string[] lista = extensiones
                .Where(extension => !string.IsNullOrWhiteSpace(extension))
                .Select(extension => extension.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (lista.Length == 0)
                return predeterminadas;

            return lista.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
        }

        private static List<string> RecorrerCodigo(
            string rutaFuente,
            FrozenSet<string> deseadas)
        {
            var encontrados = new List<string>();
            string raiz;
            try
            {
                raiz = Path.GetFullPath(
                    rutaFuente ?? Md1Stm.RutaFuentePredeterminada);
            }
            catch (Exception error) when (
                error is ArgumentException or NotSupportedException)
            {
                return encontrados;
            }

            if (!Directory.Exists(raiz))
                return encontrados;

            IReadOnlySet<string> excluidas = Md1Stm.CarpetasExcluidas;
            var pila = new Stack<string>();
            pila.Push(raiz);

            while (pila.Count > 0)
            {
                string actual = pila.Pop();
                string[] carpetas;
                string[] ficheros;
                try
                {
                    carpetas = Directory.GetDirectories(actual);
                    ficheros = Directory.GetFiles(actual);
                }
                catch (Exception error) when (
                    error is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (string fichero in ficheros)
                {
                    string extension = Path.GetExtension(fichero);
                    if (extension.Length > 0 &&
                        deseadas.Contains(extension.ToLowerInvariant()))
                    {
                        encontrados.Add(fichero);
                    }
                }

                Array.Sort(carpetas, StringComparer.Ordinal);
                for (int i = carpetas.Length - 1; i >= 0; i--)
                {
                    string carpeta = carpetas[i];
                    if (excluidas.Contains(Path.GetFileName(carpeta)))
                        continue;

                    try
                    {
                        if ((File.GetAttributes(carpeta) &
                             FileAttributes.ReparsePoint) != 0)
                        {
                            continue;
                        }
                    }
                    catch (Exception error) when (
                        error is IOException or UnauthorizedAccessException)
                    {
                        continue;
                    }

                    pila.Push(carpeta);
                }
            }

            return encontrados;
        }

        private static IReadOnlyList<string>? PartesValidas(string relativa)
        {
            string normalizado = (relativa ?? string.Empty)
                .Replace('\\', '/').Trim();
            if (normalizado.Length == 0 ||
                Path.IsPathRooted(normalizado) ||
                normalizado.StartsWith("/", StringComparison.Ordinal))
            {
                return null;
            }

            string[] partes = normalizado.Split(
                '/', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length == 0)
                return null;

            foreach (string parte in partes)
            {
                if (parte is "." or "..")
                    return null;
            }

            return Path.GetExtension(partes[^1]).Length == 0 ? null : partes;
        }

        private static string ExpandirInicio(string? ruta)
        {
            string texto = Environment.ExpandEnvironmentVariables(
                ruta ?? string.Empty);
            if (texto.StartsWith("~", StringComparison.Ordinal))
            {
                string inicio = Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile);
                texto = texto.Length == 1
                    ? inicio
                    : Path.Combine(inicio, texto.Substring(1).TrimStart('/', '\\'));
            }

            return texto;
        }
    }

    /// <summary>Máquina capaz de crear un archivo vacío (fachada RFPRMN).</summary>
    public interface ICreadorArchivos
    {
        /// <summary>Crea (si falta) el archivo vacío y devuelve su ruta.</summary>
        string CrearArchivo(string ruta, string nombre, string lenguaje = "");
    }

    /// <summary>Paso de la traza de creación de archivos.</summary>
    public readonly record struct PasoArchivo(int Paso, string Archivo, string Ruta)
    {
        /// <summary>Vista con las claves del módulo Python.</summary>
        public IReadOnlyDictionary<string, object?> ToDiccionario()
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["paso"] = Paso,
                ["archivo"] = Archivo,
                ["ruta"] = Ruta,
            };
        }
    }

    /// <summary>Resultado de crear una jerarquía de archivos vacíos.</summary>
    public sealed record ResultadoArchivos
    {
        /// <summary><c>true</c> si se crearon todos los archivos.</summary>
        public bool Ok { get; init; }

        /// <summary>Raíz creada (<c>base/raiz</c>).</summary>
        public string Raiz { get; init; } = string.Empty;

        /// <summary>Rutas absolutas de los archivos creados.</summary>
        public IReadOnlyList<string> Archivos { get; init; } = Array.Empty<string>();

        /// <summary>Número de archivos creados.</summary>
        public int Total { get; init; }

        /// <summary>Traza de pasos.</summary>
        public IReadOnlyList<PasoArchivo> Pasos { get; init; } = Array.Empty<PasoArchivo>();

        /// <summary>Errores parciales, uno por archivo rechazado o fallido.</summary>
        public IReadOnlyList<string> Errores { get; init; } = Array.Empty<string>();

        /// <summary>Error global (por ejemplo, no se pudo crear la raíz).</summary>
        public string Error { get; init; } = string.Empty;

        /// <summary>Vista con las claves del módulo Python.</summary>
        public IReadOnlyDictionary<string, object?> ToDiccionario()
        {
            var datos = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = Ok,
                ["raiz"] = Raiz,
                ["archivos"] = Archivos,
                ["total"] = Total,
                ["pasos"] = Pasos.Select(paso => paso.ToDiccionario()).ToArray(),
                ["errores"] = Errores,
            };

            if (Error.Length > 0)
                datos["error"] = Error;

            return datos;
        }

        /// <summary>Resultado serializado como JSON.</summary>
        public string ToJson() => JsonSerializer.Serialize(ToDiccionario());

        public override string ToString()
            => "ResultadoArchivos(raiz=\"" + Raiz + "\", ok=" + Ok +
               ", total=" + Total + ")";
    }

    /// <summary>
    /// Descubre y crea archivos de código vacíos con la jerarquía de la fuente.
    /// </summary>
    /// <param name="rutaFuente">Fuente a descubrir; por defecto <c>TLLDCD/src</c>.</param>
    /// <param name="maquina">Máquina que crea archivos; sin ella se escribe en disco.</param>
    /// <param name="extensiones">Extensiones a considerar; sin ellas, las de código.</param>
    public sealed class ConstructorArchivos
    {
        /// <summary>Funciones puras de validación, resolución y creación.</summary>
        public static class Operaciones
        {
            /// <summary><c>true</c> si el nombre es un nombre de archivo simple.</summary>
            public static bool ValidarNombre(string? nombre)
            {
                string limpio = (nombre ?? string.Empty).Trim();
                return limpio.Length > 0 &&
                       limpio is not "." and not ".." &&
                       Path.GetFileName(limpio) == limpio;
            }

            /// <summary>Añade la extensión del lenguaje si el nombre no la lleva.</summary>
            public static string ResolverExtension(string nombre, string lenguaje = "")
                => Md4Stm.ResolverExtension(nombre, lenguaje);

            /// <summary>Crea la jerarquía de archivos bajo <c>base/raiz</c>.</summary>
            public static ResultadoArchivos CrearJerarquia(
                string baseRuta,
                string raiz,
                IEnumerable<string> archivos,
                ICreadorArchivos? maquina = null)
                => Md4Stm.CrearJerarquiaArchivos(
                    baseRuta, raiz, archivos, maquina);
        }

        private readonly Lazy<IReadOnlyDictionary<string, int>> _lenguajes;
        private readonly Lazy<IReadOnlyList<string>> _archivos;

        public ConstructorArchivos(
            string? rutaFuente = null,
            ICreadorArchivos? maquina = null,
            IEnumerable<string>? extensiones = null)
        {
            RutaFuente = Path.GetFullPath(
                rutaFuente ?? Md1Stm.RutaFuentePredeterminada);
            Maquina = maquina;

            string[] lista = extensiones is null
                ? Array.Empty<string>()
                : extensiones
                    .Where(extension => !string.IsNullOrWhiteSpace(extension))
                    .Select(extension => extension.Trim().ToLowerInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            Extensiones = lista.Length > 0
                ? Array.AsReadOnly(lista)
                : Md4Stm.ExtensionesCodigo.ToArray();

            _lenguajes = new Lazy<IReadOnlyDictionary<string, int>>(
                () => Md4Stm.DescubrirLenguajesFuente(RutaFuente, Extensiones),
                isThreadSafe: true);
            _archivos = new Lazy<IReadOnlyList<string>>(
                () => Md4Stm.DescubrirArchivosFuente(RutaFuente, Extensiones),
                isThreadSafe: true);
        }

        /// <summary>Fuente usada para descubrir archivos.</summary>
        public string RutaFuente { get; }

        /// <summary>Máquina inyectada, si la hay.</summary>
        public ICreadorArchivos? Maquina { get; }

        /// <summary>Extensiones consideradas (normalizadas a minúsculas).</summary>
        public IReadOnlyList<string> Extensiones { get; }

        /// <summary>Lenguajes detectados en la fuente (extensión → número de archivos).</summary>
        public IReadOnlyDictionary<string, int> Lenguajes => _lenguajes.Value;

        /// <summary>Jerarquía de archivos de código de la fuente (relativa y ordenada).</summary>
        public IReadOnlyList<string> ArchivosFuente() => _archivos.Value;

        /// <summary>Recrea vacía la jerarquía de código bajo <c>rutaBase/raiz</c>.</summary>
        public ResultadoArchivos Crear(
            string? rutaBase = null,
            string raiz = "src",
            IEnumerable<string>? archivos = null)
        {
            IReadOnlyList<string> rutas = archivos is null
                ? ArchivosFuente()
                : archivos.ToArray();
            return Md4Stm.CrearJerarquiaArchivos(
                rutaBase ?? string.Empty, raiz, rutas, Maquina);
        }

        public override string ToString()
            => "ConstructorArchivos(fuente=\"" + RutaFuente + "\", extensiones=" +
               Extensiones.Count + ")";
    }
}
