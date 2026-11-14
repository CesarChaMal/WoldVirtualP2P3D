using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Visor3D.Stm
{
    /// <summary>
    /// Ejecución de las órdenes de construcción de LucIA (paridad con
    /// <c>STM_MD3.py</c>): normaliza con <see cref="InterpreteOrden"/> (MD2),
    /// crea la jerarquía con <see cref="Md1Stm.CrearJerarquia"/> y, si se
    /// inyecta, pasa la orden por la red neuronal.
    /// </summary>
    /// <remarks>
    /// Las rutas predeterminadas se inyectan desde el consumidor para respetar
    /// <c>runtime.lc_trabajando</c>; sin inyección quedan vacías y el destino
    /// lo decide quien ejecuta. C# no ejecuta Python: la red se inyecta con
    /// <see cref="IRedNeuronalConstruccion"/> y, si no se inyecta, la creación
    /// de carpetas/archivos continúa y se informa la advertencia.
    /// </remarks>
    public static class Md3Stm
    {
        /// <summary>Tercera acción: sin archivos de momento.</summary>
        public static IReadOnlyList<string> ArchivosMtbgt { get; } = Array.Empty<string>();

        /// <summary>Modo limpio: crea solo carpetas, pero conserva el cálculo de la red.</summary>
        public const bool UsarRedNeuronal = true;

        /// <summary>Comando de la secuencia base (carpetas con red y archivos).</summary>
        public const string ComandoBase = "crear base";

        /// <summary>Comandos soportados, en el orden de MD2.</summary>
        public static IReadOnlyList<string> Comandos => Md2Stm.Comandos;

        private static readonly object Candado = new();
        private static ConstructorLucIA? _constructor;

        /// <summary>
        /// Constructor compartido (carga perezosa de la red y de la máquina).
        /// Como en Python, los argumentos solo se aplican en la primera llamada.
        /// </summary>
        public static ConstructorLucIA ObtenerConstructor(
            IReadOnlyDictionary<string, string>? rutas = null,
            bool usarRed = UsarRedNeuronal,
            IRedNeuronalConstruccion? redNeuronal = null,
            IMaquinaConstruccion? maquina = null,
            Func<IReadOnlyDictionary<string, string>>? proveedorRutas = null)
        {
            if (_constructor is not null)
                return _constructor;

            lock (Candado)
            {
                _constructor ??= new ConstructorLucIA(
                    maquina: maquina,
                    rutas: rutas,
                    usarRed: usarRed,
                    redNeuronal: redNeuronal,
                    proveedorRutas: proveedorRutas);
            }

            return _constructor;
        }

        /// <summary>Atajo: interpreta y ejecuta una orden con el constructor compartido.</summary>
        public static ResultadoConstruccion EjecutarOrden(string? orden)
            => ObtenerConstructor().Ejecutar(orden);

        /// <summary>Atajo: interpreta y ejecuta una orden (forma diccionario).</summary>
        public static ResultadoConstruccion EjecutarOrden(
            IReadOnlyDictionary<string, object?> orden)
            => ObtenerConstructor().Ejecutar(orden);

        /// <summary>Atajo programático de <c>crear carpeta</c>.</summary>
        public static ResultadoConstruccion CrearCarpeta(
            string? ruta = null,
            string? nombre = null,
            IEnumerable<string>? subcarpetas = null,
            IEnumerable<string>? archivos = null)
            => ObtenerConstructor().CrearCarpeta(ruta, nombre, subcarpetas, archivos);

        /// <summary>Alias con los valores predeterminados del comando.</summary>
        public static ResultadoConstruccion ComandoCrearCarpeta(
            string? ruta = null,
            string? nombre = null,
            IEnumerable<string>? subcarpetas = null)
            => ObtenerConstructor().ComandoCrearCarpeta(ruta, nombre, subcarpetas);

        /// <summary>Atajo de <c>crear red neuronal</c>.</summary>
        public static ResultadoConstruccion CrearRedNeuronal(
            string? ruta = null,
            string? nombre = null,
            IEnumerable<string>? subcarpetas = null)
            => ObtenerConstructor().CrearRedNeuronal(ruta, nombre, subcarpetas);

        /// <summary>Alias de <c>crear red neuronal</c>.</summary>
        public static ResultadoConstruccion ComandoCrearRedNeuronal(
            string? ruta = null,
            string? nombre = null,
            IEnumerable<string>? subcarpetas = null)
            => ObtenerConstructor().ComandoCrearRedNeuronal(ruta, nombre, subcarpetas);

        /// <summary>Atajo de <c>crear mtbgt</c>.</summary>
        public static ResultadoConstruccion CrearMtbgt(
            string? ruta = null,
            IEnumerable<string>? subcarpetas = null,
            IEnumerable<string>? archivos = null)
            => ObtenerConstructor().CrearMtbgt(ruta, subcarpetas, archivos);

        /// <summary>Alias de <c>crear mtbgt</c>.</summary>
        public static ResultadoConstruccion ComandoCrearMtbgt(
            string? ruta = null,
            IEnumerable<string>? subcarpetas = null,
            IEnumerable<string>? archivos = null)
            => ObtenerConstructor().ComandoCrearMtbgt(ruta, subcarpetas, archivos);

        /// <summary>Atajo de <c>crear archivo</c>.</summary>
        public static ResultadoConstruccion CrearArchivo(
            string? ruta = null,
            string? nombre = null,
            string? lenguaje = null,
            IEnumerable<string>? archivos = null)
            => ObtenerConstructor().CrearArchivo(ruta, nombre, lenguaje, archivos);

        /// <summary>Alias de <c>crear archivo</c>.</summary>
        public static ResultadoConstruccion ComandoCrearArchivo(
            string? ruta = null,
            string? nombre = null,
            string? lenguaje = null,
            IEnumerable<string>? archivos = null)
            => ObtenerConstructor().ComandoCrearArchivo(ruta, nombre, lenguaje, archivos);

        /// <summary>Atajo de la secuencia completa.</summary>
        public static ResultadoConstruccion CrearTodo()
            => ObtenerConstructor().CrearTodo();

        /// <summary>Alias de la secuencia completa.</summary>
        public static ResultadoConstruccion ComandoCrearTodo()
            => ObtenerConstructor().ComandoCrearTodo();

        /// <summary>Atajo de la secuencia base.</summary>
        public static ResultadoConstruccion CrearBase()
            => ObtenerConstructor().CrearBase();

        /// <summary>Alias de la secuencia base.</summary>
        public static ResultadoConstruccion ComandoCrearBase()
            => ObtenerConstructor().ComandoCrearBase();
    }

    /// <summary>
    /// Máquina de comandos que ejecuta las órdenes: crea carpetas (MD1) y
    /// archivos vacíos. Equivale a la fachada <c>RFPRMN</c> de Python.
    /// </summary>
    public interface IMaquinaConstruccion : ICreadorCarpetas
    {
        /// <summary>Versión de la máquina (se informa en <c>rfprmn.version</c>).</summary>
        string Version { get; }

        /// <summary>Crea (si falta) el archivo vacío <c>ruta/nombre(.ext)</c>.</summary>
        string CrearArchivo(string ruta, string nombre, string lenguaje = "");
    }

    /// <summary>
    /// Máquina por defecto: escribe en disco con <see cref="Directory"/> y
    /// <see cref="File"/>. Es la que se usa cuando no se inyecta otra.
    /// </summary>
    public sealed class MaquinaConstruccionSistema : IMaquinaConstruccion
    {
        /// <summary>Instancia compartida (sin estado).</summary>
        public static MaquinaConstruccionSistema Instancia { get; } = new();

        /// <summary>Versión de la máquina; vacía como el <c>__version__</c> ausente de Python.</summary>
        public string Version => string.Empty;

        /// <inheritdoc />
        public string CrearCarpetaEn(string rutaBase, string nombre)
        {
            string destino = Path.Combine(rutaBase, nombre);
            Directory.CreateDirectory(destino);
            return destino;
        }

        /// <inheritdoc />
        public string CrearArchivo(string ruta, string nombre, string lenguaje = "")
        {
            string limpio = (nombre ?? string.Empty).Trim();
            if (limpio.Length == 0 || limpio is "." or ".." ||
                Path.GetFileName(limpio) != limpio)
            {
                throw new ArgumentException(
                    "Nombre de archivo no válido: " + nombre, nameof(nombre));
            }

            limpio = ConstructorLucIA.Operaciones.ResolverExtension(limpio, lenguaje);
            string destino = Path.Combine(Path.GetFullPath(ruta), limpio);
            Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
            if (!File.Exists(destino))
            {
                using (File.Create(destino))
                {
                }
            }

            return destino;
        }
    }

    /// <summary>
    /// Proveedor de la red neuronal de construcción. En Python es
    /// <c>LCBS_NEURONAL</c>; en C# se inyecta para no depender del intérprete.
    /// </summary>
    public interface IRedNeuronalConstruccion
    {
        /// <summary>
        /// Evalúa la orden y devuelve la red general y, si aplica, las
        /// métricas de la neurona de entrada.
        /// </summary>
        EvaluacionRed Evaluar(string raiz, string nombre, IReadOnlyList<string> niveles);
    }

    /// <summary>Resultado de evaluar una orden en la red neuronal.</summary>
    public sealed record EvaluacionRed(
        RedGeneralResultado RedGeneral,
        NeuronaEntradaMetricas? Metricas);

    /// <summary>Resultado de la red general (<c>LCBS_NEURONAL</c>).</summary>
    public sealed record RedGeneralResultado
    {
        /// <summary><c>true</c> si la red pudo calcular.</summary>
        public bool Ok { get; init; }

        /// <summary>Módulo que evaluó la orden.</summary>
        public string Modulo { get; init; } = "LCBS_NEURONAL";

        /// <summary>Error, vacío si todo fue bien.</summary>
        public string Error { get; init; } = string.Empty;

        /// <summary>Versión de la red, si la informa.</summary>
        public string Version { get; init; } = string.Empty;

        /// <summary><c>true</c> si la red se omitió por modo solo carpetas.</summary>
        public bool Omitida { get; init; }

        /// <summary>Motivo de la omisión.</summary>
        public string Motivo { get; init; } = string.Empty;

        /// <summary>Familias de la red.</summary>
        public IReadOnlyList<string> Familias { get; init; } = Array.Empty<string>();

        /// <summary>Salud por familia.</summary>
        public IReadOnlyDictionary<string, bool> Salud { get; init; } =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        /// <summary>Acción calculada por la red.</summary>
        public IReadOnlyList<double> Accion { get; init; } = Array.Empty<double>();

        /// <summary>Diagnóstico escalar de la red.</summary>
        public IReadOnlyDictionary<string, object?> Diagnostico { get; init; } =
            new Dictionary<string, object?>(StringComparer.Ordinal);

        /// <summary>Vista con las claves del módulo Python (<c>red_general</c>).</summary>
        public IReadOnlyDictionary<string, object?> ToDiccionario()
        {
            var datos = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = Ok,
                ["modulo"] = Modulo,
            };

            if (Omitida)
            {
                datos["omitida"] = true;
                datos["motivo"] = Motivo;
            }

            if (Error.Length > 0)
                datos["error"] = Error;
            if (Version.Length > 0)
                datos["version"] = Version;
            if (Familias.Count > 0)
                datos["familias"] = Familias;
            if (Salud.Count > 0)
                datos["salud"] = Salud;
            if (Accion.Count > 0)
                datos["accion"] = Accion;
            if (Diagnostico.Count > 0)
                datos["diagnostico"] = Diagnostico;

            return datos;
        }
    }

    /// <summary>Métricas de la neurona de entrada (<c>neurona_entrada</c>).</summary>
    public sealed record NeuronaEntradaMetricas
    {
        /// <summary>Familia de la neurona.</summary>
        public string Familia { get; init; } = "ENRN.EN2_RN";

        /// <summary>Nombre de la neurona.</summary>
        public string Neurona { get; init; } = "NeuronaEntradaXavier";

        /// <summary>Dimensión de entrada.</summary>
        public int DimEntrada { get; init; } = Md1Stm.DimEntrada;

        /// <summary>Dimensión de salida.</summary>
        public int DimSalida { get; init; } = Md1Stm.DimSalida;

        /// <summary>Impulso creativo, si se calculó.</summary>
        public double? Impulso { get; init; }

        /// <summary>Entropía del texto de la orden, si se calculó.</summary>
        public double? Entropia { get; init; }

        /// <summary>Similitud del nombre con cada nivel.</summary>
        public IReadOnlyDictionary<string, double>? Similitudes { get; init; }

        /// <summary>Afinidad máxima entre niveles.</summary>
        public double? AfinidadMaxima { get; init; }

        /// <summary>Error de las métricas, vacío si todo fue bien.</summary>
        public string Error { get; init; } = string.Empty;

        /// <summary>Vista con las claves del módulo Python.</summary>
        public IReadOnlyDictionary<string, object?> ToDiccionario()
        {
            var datos = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["familia"] = Familia,
                ["neurona"] = Neurona,
                ["dim_entrada"] = DimEntrada,
                ["dim_salida"] = DimSalida,
            };

            if (Impulso.HasValue)
                datos["impulso"] = Impulso.Value;
            if (Entropia.HasValue)
                datos["entropia"] = Entropia.Value;
            if (Similitudes is not null && Similitudes.Count > 0)
                datos["similitudes"] = Similitudes;
            if (AfinidadMaxima.HasValue)
                datos["afinidad_maxima"] = AfinidadMaxima.Value;
            if (Error.Length > 0)
                datos["error"] = Error;

            return datos;
        }
    }

    /// <summary>Paso de la traza de creación (carpeta o archivo).</summary>
    public readonly record struct PasoConstruccion(
        int Paso,
        string? Carpeta,
        string? Archivo,
        string Ruta)
    {
        /// <summary>Vista con las claves del módulo Python.</summary>
        public IReadOnlyDictionary<string, object?> ToDiccionario()
        {
            var datos = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["paso"] = Paso,
                ["ruta"] = Ruta,
            };

            if (Carpeta is not null)
                datos["carpeta"] = Carpeta;
            if (Archivo is not null)
                datos["archivo"] = Archivo;

            return datos;
        }
    }

    /// <summary>Resultado tipado de una orden de construcción.</summary>
    public sealed record ResultadoConstruccion
    {
        /// <summary><c>true</c> si la orden se ejecutó sin errores.</summary>
        public bool Ok { get; init; }

        /// <summary>Comando ejecutado.</summary>
        public string Comando { get; init; } = Md2Stm.ComandoCarpeta;

        /// <summary>Ruta base normalizada.</summary>
        public string Ruta { get; init; } = string.Empty;

        /// <summary>Nombre de la carpeta o archivo.</summary>
        public string Nombre { get; init; } = string.Empty;

        /// <summary>Raíz realmente creada.</summary>
        public string Raiz { get; init; } = string.Empty;

        /// <summary>Error de la operación, vacío si no hubo.</summary>
        public string Error { get; init; } = string.Empty;

        /// <summary>Advertencia no bloqueante (por ejemplo, red no disponible).</summary>
        public string Advertencia { get; init; } = string.Empty;

        /// <summary>Subcarpetas solicitadas.</summary>
        public IReadOnlyList<string> Subcarpetas { get; init; } = Array.Empty<string>();

        /// <summary>Carpetas creadas.</summary>
        public IReadOnlyList<string> Carpetas { get; init; } = Array.Empty<string>();

        /// <summary>Archivos creados.</summary>
        public IReadOnlyList<string> Archivos { get; init; } = Array.Empty<string>();

        /// <summary>Errores parciales de la creación de archivos.</summary>
        public IReadOnlyList<string> Errores { get; init; } = Array.Empty<string>();

        /// <summary>Traza de pasos.</summary>
        public IReadOnlyList<PasoConstruccion> Pasos { get; init; } =
            Array.Empty<PasoConstruccion>();

        /// <summary>Conteo de archivos por extensión.</summary>
        public IReadOnlyDictionary<string, int> Lenguajes { get; init; } =
            new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Resultado de la red general, si se evaluó.</summary>
        public RedGeneralResultado? RedGeneral { get; init; }

        /// <summary>Métricas de la neurona de entrada, si se evaluó.</summary>
        public NeuronaEntradaMetricas? NeuronaEntrada { get; init; }

        /// <summary>Acciones de una secuencia (<c>crear todo</c>, <c>crear base</c>).</summary>
        public IReadOnlyList<ResultadoConstruccion> Acciones { get; init; } =
            Array.Empty<ResultadoConstruccion>();

        /// <summary>Ruta final de <c>MTBGT</c>, si aplica.</summary>
        public string? RutaMtbgt { get; init; }

        /// <summary>Extensión filtrada en <c>crear archivo</c>, si aplica.</summary>
        public string? Extension { get; init; }

        /// <summary>Lenguaje solicitado en <c>crear archivo</c>.</summary>
        public string Lenguaje { get; init; } = string.Empty;

        /// <summary>Versión de la máquina de comandos.</summary>
        public string VersionMaquina { get; init; } = string.Empty;

        /// <summary>Número de elementos creados (raíz + subcarpetas o archivos).</summary>
        public int Total { get; init; }

        /// <summary>Vista con las claves del módulo Python.</summary>
        public IReadOnlyDictionary<string, object?> ToDiccionario()
        {
            var datos = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = Ok,
                ["comando"] = Comando,
                ["ruta"] = Ruta,
                ["nombre"] = Nombre,
                ["raiz"] = Raiz,
                ["total"] = Total,
                ["subcarpetas"] = Subcarpetas,
                ["carpetas"] = Carpetas,
                ["archivos"] = Archivos,
                ["pasos"] = Pasos.Select(paso => paso.ToDiccionario()).ToArray(),
                ["rfprmn"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["version"] = VersionMaquina,
                },
            };

            if (Comando == Md2Stm.ComandoArchivo || Lenguajes.Count > 0)
                datos["lenguajes"] = Lenguajes;
            if (RedGeneral is not null)
                datos["red_general"] = RedGeneral.ToDiccionario();
            if (NeuronaEntrada is not null)
                datos["neurona_entrada"] = NeuronaEntrada.ToDiccionario();
            if (RutaMtbgt is not null)
                datos["ruta_mtbgt"] = RutaMtbgt;
            if (Extension is not null)
                datos["extension"] = Extension;
            if (Comando == Md2Stm.ComandoArchivo)
                datos["lenguaje"] = Lenguaje;
            if (Error.Length > 0)
                datos["error"] = Error;
            if (Advertencia.Length > 0)
                datos["advertencia"] = Advertencia;
            if (Errores.Count > 0)
                datos["errores"] = Errores;
            if (Acciones.Count > 0)
                datos["acciones"] = Acciones
                    .Select(accion => accion.ToDiccionario())
                    .ToArray();

            return datos;
        }

        /// <summary>Resultado serializado como JSON.</summary>
        public string ToJson() => JsonSerializer.Serialize(ToDiccionario());

        public override string ToString()
            => "ResultadoConstruccion(comando=\"" + Comando + "\", ruta=\"" + Ruta +
               "\", ok=" + Ok + ", total=" + Total + ")";
    }

    /// <summary>Ejecuta las órdenes de construcción de LucIA.</summary>
    /// <param name="rutaRed">Directorio de <c>RedNeuronal</c> (informativo).</param>
    /// <param name="maquina">Máquina de comandos ya construida (útil en pruebas).</param>
    /// <param name="rutas">Rutas de cada comando, inyectadas por el consumidor.</param>
    /// <param name="usarRed">Si es <c>false</c> crea solo carpetas y omite la red.</param>
    /// <param name="redNeuronal">Red inyectada; sin ella se informa la advertencia.</param>
    /// <param name="proveedorRutas">
    /// Proveedor de rutas que se consulta en cada ejecución, de modo que
    /// <c>runtime.lc_trabajando</c> se aplique sin recrear la clase.
    /// </param>
    public sealed class ConstructorLucIA
    {
        /// <summary>Funciones puras compartidas de rutas, jerarquía y archivos.</summary>
        public static class Operaciones
        {
            private const int CapacidadCache = 8;

            private static readonly Dictionary<string, Lazy<IReadOnlyList<string>>>
                CacheSubcarpetas = new(StringComparer.OrdinalIgnoreCase);

            private static readonly Dictionary<string, Lazy<IReadOnlyList<string>>>
                CacheArchivos = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>Extensiones de código que el constructor recrea vacías.</summary>
            public static FrozenSet<string> ExtensionesCodigo { get; } =
                new[]
                {
                    ".py", ".cs", ".gd", ".gdshader", ".tscn", ".tres",
                    ".xaml", ".csproj", ".html", ".mjs", ".js", ".ts",
                    ".sh", ".bat", ".ps1", ".cmd",
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

            /// <summary>Alias lenguaje → extensión (resolución de <c>crear archivo</c>).</summary>
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

            /// <summary>Limpia, expande variables e inicio (<c>~</c>) y resuelve la ruta.</summary>
            public static string NormalizarRuta(string? ruta, string predeterminada = "")
            {
                string texto = (ruta ?? string.Empty).Trim().Trim('"', '\'');
                if (texto.Length == 0)
                    texto = predeterminada;

                texto = Environment.ExpandEnvironmentVariables(texto);
                if (texto.StartsWith("~", StringComparison.Ordinal))
                {
                    string inicio = Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile);
                    texto = texto.Length == 1
                        ? inicio
                        : Path.Combine(inicio, texto.Substring(1).TrimStart('/', '\\'));
                }

                return Path.GetFullPath(texto.Length == 0 ? "." : texto);
            }

            /// <summary>Jerarquía de la fuente, descubierta una vez por proceso.</summary>
            public static IReadOnlyList<string> SubcarpetasFuente(string fuente)
                => Cachear(CacheSubcarpetas, fuente,
                    () => Md1Stm.DescubrirSubcarpetasSrc(fuente));

            /// <summary>Archivos de código de la fuente, descubiertos una vez por proceso.</summary>
            public static IReadOnlyList<string> ArchivosFuente(string fuente)
                => Cachear(CacheArchivos, fuente, () => DescubrirArchivos(fuente));

            /// <summary>Convierte <paramref name="subcarpetas"/> en una lista de nombres.</summary>
            public static IReadOnlyList<string> NormalizarSubcarpetas(
                IEnumerable<string>? subcarpetas,
                string nombre,
                string fuente)
            {
                if (subcarpetas is null)
                {
                    if (string.Equals(nombre, Md2Stm.NombrePredeterminado,
                                      StringComparison.OrdinalIgnoreCase))
                    {
                        return SubcarpetasFuente(fuente);
                    }

                    return Array.Empty<string>();
                }

                var salida = new List<string>();
                foreach (string item in subcarpetas)
                {
                    string limpio = (item ?? string.Empty).Trim();
                    if (limpio.Length > 0)
                        salida.Add(limpio);
                }

                return salida;
            }

            /// <summary>Nombres de primer nivel, sin repetir y en orden.</summary>
            public static IReadOnlyList<string> PrimerNivel(IEnumerable<string> subcarpetas)
            {
                var vistos = new HashSet<string>(StringComparer.Ordinal);
                var salida = new List<string>();
                foreach (string sub in subcarpetas)
                {
                    string normalizado = (sub ?? string.Empty)
                        .Replace('\\', '/').Trim();
                    string primero = normalizado.Split('/')
                        .FirstOrDefault(parte => parte.Length > 0) ?? string.Empty;
                    if (primero.Length > 0 && vistos.Add(primero))
                        salida.Add(primero);
                }

                return salida;
            }

            /// <summary>Traza de pasos por nivel (no por cada ruta anidada).</summary>
            public static IReadOnlyList<PasoConstruccion> ResumirPasos(
                string raiz,
                IEnumerable<string> niveles)
            {
                var pasos = new List<PasoConstruccion>
                {
                    new(1, Path.GetFileName(raiz), null, raiz),
                };

                int numero = 2;
                foreach (string nivel in niveles)
                {
                    pasos.Add(new PasoConstruccion(
                        numero++, nivel, null,
                        Path.Combine(raiz, nivel.Replace('\\', '/'))));
                }

                return pasos;
            }

            /// <summary>Crea los archivos vacíos indicados dentro de la raíz.</summary>
            public static List<string> CrearArchivos(
                IMaquinaConstruccion maquina,
                string raiz,
                IEnumerable<string> archivos)
            {
                ArgumentNullException.ThrowIfNull(maquina);
                var creados = new List<string>();
                foreach (string nombre in archivos)
                {
                    creados.Add(maquina.CrearArchivo(raiz, nombre));
                }

                return creados;
            }

            /// <summary>Añade la extensión del lenguaje si el nombre no la lleva.</summary>
            public static string ResolverExtension(string nombre, string lenguaje = "")
            {
                string limpio = (nombre ?? string.Empty).Trim();
                string pedido = (lenguaje ?? string.Empty).Trim().ToLowerInvariant();
                if (pedido.Length == 0)
                    return limpio;

                string extension = ExtensionDeLenguaje(pedido);
                return limpio.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                    ? limpio
                    : limpio + extension;
            }

            /// <summary>Extensión asociada a un lenguaje (por defecto <c>.lenguaje</c>).</summary>
            public static string ExtensionDeLenguaje(string lenguaje)
            {
                string clave = (lenguaje ?? string.Empty).Trim().ToLowerInvariant();
                return ExtensionesPorLenguaje.TryGetValue(clave, out string? extension)
                    ? extension
                    : "." + clave;
            }

            /// <summary>Cuenta archivos por extensión, ordenado por cantidad y nombre.</summary>
            public static IReadOnlyDictionary<string, int> ContarLenguajes(
                IEnumerable<string> archivos)
            {
                var conteo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (string archivo in archivos)
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

            /// <summary>Partes de una ruta relativa, o <c>null</c> si es absoluta o insegura.</summary>
            public static IReadOnlyList<string>? PartesValidas(string relativa)
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

            private static IReadOnlyList<string> Cachear(
                Dictionary<string, Lazy<IReadOnlyList<string>>> cache,
                string clave,
                Func<IReadOnlyList<string>> calcular)
            {
                lock (cache)
                {
                    if (cache.TryGetValue(clave, out Lazy<IReadOnlyList<string>>? existente))
                        return existente.Value;

                    if (cache.Count >= CapacidadCache)
                        cache.Clear();

                    var nuevo = new Lazy<IReadOnlyList<string>>(
                        calcular, isThreadSafe: true);
                    cache[clave] = nuevo;
                    return nuevo.Value;
                }
            }

            private static IReadOnlyList<string> DescubrirArchivos(string fuente)
            {
                var encontrados = new List<string>();
                string raiz;
                try
                {
                    raiz = Path.GetFullPath(fuente);
                }
                catch (ArgumentException)
                {
                    return Array.Empty<string>();
                }

                if (!Directory.Exists(raiz))
                    return Array.Empty<string>();

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
                        if (extension.Length == 0 ||
                            !ExtensionesCodigo.Contains(extension.ToLowerInvariant()))
                        {
                            continue;
                        }

                        encontrados.Add(
                            Path.GetRelativePath(raiz, fichero).Replace('\\', '/'));
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

                encontrados.Sort(StringComparer.Ordinal);
                return encontrados;
            }
        }

        /// <summary>Comandos soportados (paridad con <see cref="Md2Stm.Comandos"/>).</summary>
        public static IReadOnlyList<string> Comandos => Md2Stm.Comandos;

        private readonly IMaquinaConstruccion _maquina;
        private readonly IReadOnlyDictionary<string, string> _rutas;
        private readonly Func<IReadOnlyDictionary<string, string>>? _proveedorRutas;
        private readonly bool _usarRed;
        private readonly IRedNeuronalConstruccion? _redNeuronal;

        public ConstructorLucIA(
            string? rutaRed = null,
            IMaquinaConstruccion? maquina = null,
            IReadOnlyDictionary<string, string>? rutas = null,
            bool usarRed = Md3Stm.UsarRedNeuronal,
            IRedNeuronalConstruccion? redNeuronal = null,
            Func<IReadOnlyDictionary<string, string>>? proveedorRutas = null)
        {
            RutaRed = rutaRed;
            _maquina = maquina ?? MaquinaConstruccionSistema.Instancia;
            _rutas = rutas is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(rutas, StringComparer.Ordinal);
            _proveedorRutas = proveedorRutas;
            _usarRed = usarRed;
            _redNeuronal = redNeuronal;
        }

        /// <summary>Directorio de <c>RedNeuronal</c>, si se indicó.</summary>
        public string? RutaRed { get; }

        /// <summary>Máquina de comandos en uso.</summary>
        public IMaquinaConstruccion Maquina => _maquina;

        /// <summary>Si la red neuronal está activada.</summary>
        public bool UsarRed => _usarRed;

        /// <summary>Rutas actuales de cada comando (consulta el proveedor si existe).</summary>
        public IReadOnlyDictionary<string, string> Rutas
            => _proveedorRutas?.Invoke() ?? _rutas;

        // ------------------------------------------------------------------
        // Red neuronal
        // ------------------------------------------------------------------

        private EvaluacionRed EvaluarOrden(
            string raiz,
            string nombre,
            IReadOnlyList<string> niveles)
        {
            if (_redNeuronal is null)
            {
                const string error =
                    "red neuronal no disponible en C#: inyecta " +
                    "IRedNeuronalConstruccion para evaluar la orden";
                return new EvaluacionRed(
                    new RedGeneralResultado { Ok = false, Error = error },
                    new NeuronaEntradaMetricas { Error = error });
            }

            return _redNeuronal.Evaluar(raiz, nombre, niveles);
        }

        private static RedGeneralResultado RedOmitida() => new()
        {
            Ok = true,
            Omitida = true,
            Motivo = "modo solo carpetas (red desactivada)",
        };

        // ------------------------------------------------------------------
        // Carpetas
        // ------------------------------------------------------------------

        private ResultadoConstruccion CrearSoloCarpetas(
            string baseRuta,
            string nombre,
            IReadOnlyList<string> subcarpetas,
            IReadOnlyList<string> niveles)
        {
            ResultadoCreacion jerarquia = Md1Stm.CrearJerarquia(
                baseRuta, nombre, subcarpetas, _maquina);

            IReadOnlyList<PasoConstruccion> pasos = jerarquia.Ok
                ? Operaciones.ResumirPasos(jerarquia.Raiz, niveles)
                : jerarquia.Pasos
                    .Select(paso => new PasoConstruccion(
                        paso.Paso, paso.Carpeta, null, paso.Ruta))
                    .ToArray();

            return new ResultadoConstruccion
            {
                Ok = jerarquia.Ok,
                Error = jerarquia.Error,
                Raiz = jerarquia.Raiz,
                Carpetas = jerarquia.Carpetas,
                Pasos = pasos,
                Total = jerarquia.Total,
                Ruta = baseRuta,
                Nombre = nombre,
                Subcarpetas = subcarpetas,
            };
        }

        private ResultadoConstruccion CrearJerarquiaArchivos(
            string baseRuta,
            string raiz,
            IEnumerable<string> archivos)
        {
            string raizRuta = string.IsNullOrWhiteSpace(raiz)
                ? baseRuta
                : Path.Combine(baseRuta, raiz.Trim());
            var creados = new List<string>();
            var pasos = new List<PasoConstruccion>();
            var errores = new List<string>();

            try
            {
                Directory.CreateDirectory(raizRuta);
            }
            catch (Exception error) when (
                error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return new ResultadoConstruccion
                {
                    Ok = false,
                    Raiz = raizRuta,
                    Total = 0,
                    Error = error.GetType().Name + ": " + error.Message,
                };
            }

            string raizAbsoluta = Path.GetFullPath(raizRuta);
            int numero = 1;
            foreach (string relativa in archivos)
            {
                IReadOnlyList<string>? partes = Operaciones.PartesValidas(relativa);
                if (partes is null)
                {
                    errores.Add("\"" + relativa + "\": ruta no válida");
                    continue;
                }

                string destino = Path.Combine(
                    new[] { raizAbsoluta }.Concat(partes).ToArray());
                if (!destino.StartsWith(
                        raizAbsoluta + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                {
                    errores.Add("\"" + relativa + "\": ruta fuera de la raíz");
                    continue;
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
                    _maquina.CrearArchivo(Path.GetDirectoryName(destino)!, partes[^1]);
                }
                catch (Exception error) when (
                    error is IOException or UnauthorizedAccessException or
                    ArgumentException or NotSupportedException)
                {
                    errores.Add("\"" + relativa + "\": " + error.Message);
                    continue;
                }

                creados.Add(destino);
                pasos.Add(new PasoConstruccion(numero++, null, relativa, destino));
            }

            return new ResultadoConstruccion
            {
                Ok = errores.Count == 0,
                Raiz = raizRuta,
                Archivos = creados,
                Total = creados.Count,
                Pasos = pasos,
                Errores = errores,
            };
        }

        private ResultadoConstruccion CrearArchivoUno(
            string baseRuta,
            string nombre,
            string lenguaje)
        {
            try
            {
                string objetivo = _maquina.CrearArchivo(baseRuta, nombre, lenguaje);
                return new ResultadoConstruccion
                {
                    Ok = true,
                    Raiz = baseRuta,
                    Archivos = new[] { objetivo },
                    Total = 1,
                    Pasos = new[]
                    {
                        new PasoConstruccion(1, null, nombre, objetivo),
                    },
                };
            }
            catch (Exception error) when (
                error is IOException or UnauthorizedAccessException or
                ArgumentException or NotSupportedException)
            {
                return new ResultadoConstruccion
                {
                    Ok = false,
                    Raiz = baseRuta,
                    Total = 0,
                    Error = error.GetType().Name + ": " + error.Message,
                };
            }
        }

        // ------------------------------------------------------------------
        // API pública
        // ------------------------------------------------------------------

        /// <summary>
        /// Crea <c>ruta/nombre</c> y, si es <c>src</c>, su jerarquía completa.
        /// Sin <c>ruta</c> usa la ruta inyectada y sin <c>nombre</c> usa
        /// <see cref="Md2Stm.NombrePredeterminado"/>. Si <c>subcarpetas</c> es
        /// <c>null</c> y el nombre es <c>src</c> se descubre la jerarquía de
        /// <c>TLLDCD/src</c>; en otro caso solo la raíz. <c>archivos</c> crea
        /// ficheros vacíos dentro de la raíz.
        /// </summary>
        public ResultadoConstruccion CrearCarpeta(
            string? ruta = null,
            string? nombre = null,
            IEnumerable<string>? subcarpetas = null,
            IEnumerable<string>? archivos = null)
        {
            string baseTexto = Operaciones.NormalizarRuta(
                ruta, RutaPara(Md2Stm.ComandoCarpeta));
            string nombreLimpio = string.IsNullOrWhiteSpace(nombre)
                ? Md2Stm.NombrePredeterminado
                : nombre.Trim();
            IReadOnlyList<string> subs = Operaciones.NormalizarSubcarpetas(
                subcarpetas, nombreLimpio, Md1Stm.RutaFuentePredeterminada);
            IReadOnlyList<string> niveles = Operaciones.PrimerNivel(subs);

            ResultadoConstruccion resultado = CrearSoloCarpetas(
                baseTexto, nombreLimpio, subs, niveles);

            RedGeneralResultado red;
            NeuronaEntradaMetricas? metricas = null;
            if (_usarRed)
            {
                try
                {
                    EvaluacionRed evaluacion = EvaluarOrden(
                        baseTexto, nombreLimpio, niveles);
                    red = evaluacion.RedGeneral;
                    metricas = evaluacion.Metricas;
                }
                catch (Exception error)
                {
                    red = new RedGeneralResultado
                    {
                        Ok = false,
                        Error = error.GetType().Name + ": " + error.Message,
                    };
                }
            }
            else
            {
                red = RedOmitida();
            }

            resultado = resultado with
            {
                Comando = Md2Stm.ComandoCarpeta,
                Ruta = baseTexto,
                Nombre = nombreLimpio,
                Subcarpetas = subs,
                RedGeneral = red,
                NeuronaEntrada = metricas,
                VersionMaquina = _maquina.Version,
            };

            var archivosCreados = new List<string>();
            if (archivos is not null && resultado.Ok)
            {
                try
                {
                    archivosCreados = Operaciones.CrearArchivos(
                        _maquina, resultado.Raiz, archivos);
                }
                catch (Exception error)
                {
                    resultado = resultado with
                    {
                        Ok = false,
                        Error = error.GetType().Name + ": " + error.Message,
                    };
                }
            }

            resultado = resultado with { Archivos = archivosCreados };
            if (!red.Ok)
            {
                resultado = resultado with
                {
                    Advertencia = "La red general no pudo calcular; " +
                                  "la jerarquía se creó igualmente.",
                };
            }

            return resultado;
        }

        /// <summary>Segunda acción: crea <c>LC_STM/RedNeuronal</c> con sus seis familias.</summary>
        public ResultadoConstruccion CrearRedNeuronal(
            string? ruta = null,
            string? nombre = null,
            IEnumerable<string>? subcarpetas = null)
        {
            string rutaFinal = string.IsNullOrWhiteSpace(ruta)
                ? RutaPara(Md2Stm.ComandoRed)
                : ruta!;
            string nombreFinal = string.IsNullOrWhiteSpace(nombre)
                ? Md2Stm.NombreRedPredeterminado
                : nombre!;

            return CrearCarpeta(
                rutaFinal,
                nombreFinal,
                subcarpetas ?? Md2Stm.SubcarpetasRed) with
            {
                Comando = Md2Stm.ComandoRed,
            };
        }

        /// <summary>Alias de la segunda acción con sus valores predeterminados.</summary>
        public ResultadoConstruccion ComandoCrearRedNeuronal(
            string? ruta = null,
            string? nombre = null,
            IEnumerable<string>? subcarpetas = null)
            => CrearRedNeuronal(ruta, nombre, subcarpetas);

        /// <summary>Tercera acción: <c>MTBGT</c> con sus carpetas (sin archivos por ahora).</summary>
        public ResultadoConstruccion CrearMtbgt(
            string? ruta = null,
            IEnumerable<string>? subcarpetas = null,
            IEnumerable<string>? archivos = null)
        {
            string root = Operaciones.NormalizarRuta(
                ruta, RutaPara(Md2Stm.ComandoMtbgt));
            string? padre = Path.GetDirectoryName(root);
            string nombre = Path.GetFileName(root);
            if (string.IsNullOrEmpty(padre) || string.IsNullOrEmpty(nombre))
            {
                return new ResultadoConstruccion
                {
                    Ok = false,
                    Comando = Md2Stm.ComandoMtbgt,
                    Ruta = root,
                    Error = "Ruta de MTBGT no válida: " + root,
                };
            }

            return CrearCarpeta(
                padre,
                nombre,
                subcarpetas ?? Md2Stm.SubcarpetasMtbgt,
                archivos ?? Md3Stm.ArchivosMtbgt) with
            {
                Comando = Md2Stm.ComandoMtbgt,
                RutaMtbgt = root,
            };
        }

        /// <summary>Alias de la tercera acción con sus valores predeterminados.</summary>
        public ResultadoConstruccion ComandoCrearMtbgt(
            string? ruta = null,
            IEnumerable<string>? subcarpetas = null,
            IEnumerable<string>? archivos = null)
            => CrearMtbgt(ruta, subcarpetas, archivos);

        /// <summary>
        /// Cuarta acción: archivo suelto o jerarquía vacía de código.
        /// Sin <c>nombre</c> ni <c>lenguaje</c> recrea la jerarquía de código de
        /// la fuente bajo <c>ruta/src</c>. Con <c>nombre</c> o <c>lenguaje</c>
        /// crea un único archivo vacío; con <c>archivos</c> crea exactamente
        /// esas rutas relativas. Si solo se indica <c>lenguaje</c>, filtra la
        /// jerarquía a esa extensión (por ejemplo <c>csharp</c> para los <c>.cs</c>).
        /// </summary>
        public ResultadoConstruccion CrearArchivo(
            string? ruta = null,
            string? nombre = null,
            string? lenguaje = null,
            IEnumerable<string>? archivos = null)
        {
            string baseTexto = Operaciones.NormalizarRuta(
                ruta, RutaPara(Md2Stm.ComandoArchivo));
            string nombreLimpio = (nombre ?? string.Empty).Trim();
            string lenguajeLimpio = (lenguaje ?? string.Empty).Trim();
            string raiz = nombreLimpio.Length > 0
                ? nombreLimpio
                : Md2Stm.NombrePredeterminado;

            string extension = string.Empty;
            if (lenguajeLimpio.Length > 0 && nombreLimpio.Length == 0)
                extension = Operaciones.ExtensionDeLenguaje(lenguajeLimpio);

            ResultadoConstruccion resultado;
            if (extension.Length > 0)
            {
                IReadOnlyList<string> filtrados = Operaciones
                    .ArchivosFuente(Md1Stm.RutaFuentePredeterminada)
                    .Where(archivo => archivo.EndsWith(
                        extension, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                resultado = CrearJerarquiaArchivos(baseTexto, raiz, filtrados) with
                {
                    Extension = extension,
                };
            }
            else if (archivos is not null)
            {
                resultado = CrearJerarquiaArchivos(baseTexto, raiz, archivos);
            }
            else if (lenguajeLimpio.Length > 0 ||
                     (nombreLimpio.Length > 0 &&
                      Path.GetExtension(nombreLimpio).Length > 0))
            {
                string nombreFinal = nombreLimpio.Length > 0
                    ? nombreLimpio
                    : Operaciones.NombreArchivoPredeterminado;
                resultado = CrearArchivoUno(baseTexto, nombreFinal, lenguajeLimpio);
            }
            else
            {
                resultado = CrearJerarquiaArchivos(
                    baseTexto, raiz,
                    Operaciones.ArchivosFuente(Md1Stm.RutaFuentePredeterminada));
            }

            resultado = resultado with
            {
                Comando = Md2Stm.ComandoArchivo,
                Ruta = baseTexto,
                Nombre = raiz,
                Lenguaje = lenguajeLimpio,
                VersionMaquina = _maquina.Version,
            };

            if (resultado.Lenguajes.Count == 0)
            {
                resultado = resultado with
                {
                    Lenguajes = Operaciones.ContarLenguajes(resultado.Archivos),
                };
            }

            if (!resultado.Ok && resultado.Error.Length == 0)
            {
                resultado = resultado with
                {
                    Error = resultado.Errores.Count > 0
                        ? string.Join("; ", resultado.Errores)
                        : "no se pudieron crear todos los archivos",
                };
            }

            return resultado;
        }

        /// <summary>Alias de la cuarta acción con sus valores predeterminados.</summary>
        public ResultadoConstruccion ComandoCrearArchivo(
            string? ruta = null,
            string? nombre = null,
            string? lenguaje = null,
            IEnumerable<string>? archivos = null)
            => CrearArchivo(ruta, nombre, lenguaje, archivos);

        /// <summary>Secuencia completa: <c>src</c>, <c>RedNeuronal</c>, <c>MTBGT</c> y archivos.</summary>
        public ResultadoConstruccion CrearTodo()
        {
            var acciones = new[]
            {
                CrearCarpeta(),
                CrearRedNeuronal(),
                CrearMtbgt(),
                CrearArchivo(),
            };

            return new ResultadoConstruccion
            {
                Ok = acciones.All(accion => accion.Ok),
                Comando = Md2Stm.ComandoTodo,
                Acciones = acciones,
                Total = acciones.Sum(accion => accion.Total),
                VersionMaquina = _maquina.Version,
            };
        }

        /// <summary>Secuencia base: <c>src</c> con su red y después los archivos vacíos.</summary>
        public ResultadoConstruccion CrearBase()
        {
            var acciones = new[]
            {
                CrearCarpeta(),
                CrearArchivo(),
            };

            return new ResultadoConstruccion
            {
                Ok = acciones.All(accion => accion.Ok),
                Comando = Md3Stm.ComandoBase,
                Acciones = acciones,
                Total = acciones.Sum(accion => accion.Total),
                VersionMaquina = _maquina.Version,
            };
        }

        /// <summary>Alias de la secuencia base.</summary>
        public ResultadoConstruccion ComandoCrearBase() => CrearBase();

        /// <summary>Alias de la secuencia completa.</summary>
        public ResultadoConstruccion ComandoCrearTodo() => CrearTodo();

        /// <summary>Alias del comando con la ruta y el nombre predeterminados.</summary>
        public ResultadoConstruccion ComandoCrearCarpeta(
            string? ruta = null,
            string? nombre = null,
            IEnumerable<string>? subcarpetas = null)
            => CrearCarpeta(ruta, nombre, subcarpetas);

        /// <summary>Interpreta y ejecuta una orden completa de la máquina de comandos.</summary>
        public ResultadoConstruccion Ejecutar(string? orden)
            => Ejecutar(new InterpreteOrden(RutasParaInterprete()).Interpretar(orden));

        /// <summary>Interpreta y ejecuta una orden en forma de diccionario.</summary>
        public ResultadoConstruccion Ejecutar(
            IReadOnlyDictionary<string, object?> orden)
            => Ejecutar(new InterpreteOrden(RutasParaInterprete()).Interpretar(orden));

        private ResultadoConstruccion Ejecutar(OrdenNormalizada datos)
        {
            if (!datos.Ok)
            {
                return new ResultadoConstruccion
                {
                    Ok = false,
                    Comando = datos.Comando,
                    Error = datos.Error,
                };
            }

            string comando = datos.Comando;
            if (!Comandos.Contains(comando, StringComparer.Ordinal))
            {
                return new ResultadoConstruccion
                {
                    Ok = false,
                    Comando = comando,
                    Error = "Comando no soportado: \"" + comando +
                            "\". Disponibles: " + string.Join(", ", Comandos),
                };
            }

            if (comando == Md2Stm.ComandoTodo)
                return CrearTodo();

            if (comando == Md2Stm.ComandoArchivo)
            {
                return CrearArchivo(
                    Elegir(datos.Ruta, RutaPara(Md2Stm.ComandoArchivo)),
                    datos.Nombre,
                    datos.Lenguaje,
                    datos.Archivos);
            }

            if (comando == Md2Stm.ComandoMtbgt)
            {
                return CrearMtbgt(
                    Elegir(datos.Ruta, RutaPara(Md2Stm.ComandoMtbgt)),
                    datos.Subcarpetas);
            }

            if (comando == Md2Stm.ComandoRed)
            {
                return CrearRedNeuronal(
                    Elegir(datos.Ruta, RutaPara(Md2Stm.ComandoRed)),
                    Elegir(datos.Nombre, Md2Stm.NombreRedPredeterminado),
                    datos.Subcarpetas);
            }

            return CrearCarpeta(
                Elegir(datos.Ruta, RutaPara(Md2Stm.ComandoCarpeta)),
                Elegir(datos.Nombre, Md2Stm.NombrePredeterminado),
                datos.Subcarpetas);
        }

        private Dictionary<string, string?> RutasParaInterprete()
        {
            var copia = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> par in Rutas)
                copia[par.Key] = par.Value;
            return copia;
        }

        private string RutaPara(string comando)
            => Rutas.TryGetValue(comando, out string? ruta) ? ruta : string.Empty;

        private static string Elegir(string? valor, string predeterminado)
            => string.IsNullOrEmpty(valor) ? predeterminado : valor;

        public override string ToString()
            => "ConstructorLucIA(rutas=" + Rutas.Count + ", usarRed=" + _usarRed + ")";
    }
}
