using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;

namespace Visor3D.Stm
{
    /// <summary>
    /// MD1 en C#: constructor de jerarquías de carpetas para LucIA.
    ///
    /// <para>Puerto del módulo Python <c>STM_MD1</c> con el mismo contrato:</para>
    /// <list type="bullet">
    ///   <item>descubre toda la jerarquía de carpetas de <c>TLLDCD/src</c>;</item>
    ///   <item>localiza (no ejecuta) la red neuronal ENRN y la fachada LCBS;</item>
    ///   <item>crea la raíz y sus subcarpetas paso a paso, devolviendo la traza.</item>
    /// </list>
    ///
    /// <para><b>Importación:</b> el archivo queda bajo el proyecto <c>Visor3D.csproj</c>
    /// (<c>EnableDefaultCompileItems=true</c>), así que se compila solo. En cualquier
    /// otro archivo basta con:</para>
    /// <code>
    /// using Visor3D.Stm;
    ///
    /// var resultado = Md1Stm.CrearJerarquia(@"E:\ruta", "LC", Md1Stm.SubcarpetasSrc);
    /// </code>
    ///
    /// <para><b>Rendimiento:</b> el descubrimiento se cachea con <see cref="Lazy{T}"/>,
    /// las carpetas excluidas se consultan en un <see cref="FrozenSet{T}"/> y el
    /// recorrido usa una pila que nunca entra en cachés ni en puntos de reanálisis.</para>
    /// </summary>
    public static class Md1Stm
    {
        /// <summary>Dimensión de entrada de la red neuronal asociada al constructor.</summary>
        public const int DimEntrada = 64;

        /// <summary>Dimensión de salida de la red neuronal asociada al constructor.</summary>
        public const int DimSalida = 32;

        /// <summary>Nombre de carpeta que se crea cuando no se indica otro.</summary>
        public const string NombrePredeterminado = "LC";

        /// <summary>Carpetas de caché/metadatos que el descubrimiento omite.</summary>
        private static readonly FrozenSet<string> Excluidas = new[]
        {
            "__pycache__", "pycache", ".git", ".godot", ".venv", "venv",
            "node_modules", ".pytest_cache", ".ruff_cache",
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        /// <summary>Jerarquía de respaldo si la fuente no está disponible.</summary>
        private static readonly IReadOnlyList<string> Base = Array.AsReadOnly(new[]
        {
            "LC_STM", "MTBGT", "SBSTM", "STMGNRL", "STMIPFS", "STM_BKCH",
            "STM_CH", "STM_DOC", "STM_HRTS", "STM_IA", "STM_JSON", "STM_SGR",
            "tests", "VSC#",
        });

        private static readonly string RutaRedRelativa = Path.Combine(
            "CONTRUCCION", "Refactorizacion", "src", "LC_STM", "RedNeuronal");

        private static readonly Lazy<string> FuentePredeterminada = new(ResolverFuente, true);
        private static readonly Lazy<IReadOnlyList<string>> JerarquiaSrc = new(
            () => DescubrirSubcarpetasSrc(), true);

        /// <summary>Carpetas excluidas del descubrimiento (consulta O(1)).</summary>
        public static IReadOnlySet<string> CarpetasExcluidas => Excluidas;

        /// <summary>Jerarquía de respaldo cuando no se encuentra la fuente real.</summary>
        public static IReadOnlyList<string> SubcarpetasSrcBase => Base;

        /// <summary>
        /// <c>TLLDCD/src</c> resuelto desde <c>LC_SRC</c> o subiendo desde el ejecutable
        /// y el directorio actual. Se calcula una sola vez.
        /// </summary>
        public static string RutaFuentePredeterminada => FuentePredeterminada.Value;

        /// <summary>
        /// Jerarquía completa de la fuente predeterminada, descubierta una sola vez
        /// y reutilizada en llamadas posteriores.
        /// </summary>
        public static IReadOnlyList<string> SubcarpetasSrc => JerarquiaSrc.Value;

        // ------------------------------------------------------------------
        // Descubrimiento de carpetas
        // ------------------------------------------------------------------

        /// <summary>
        /// Devuelve toda la jerarquía de carpetas de <paramref name="rutaFuente"/>
        /// (o de la fuente predeterminada), en orden de recorrido previo.
        /// Solo carpetas, rutas relativas con <c>/</c>. Si la fuente no existe
        /// devuelve <see cref="SubcarpetasSrcBase"/>.
        /// </summary>
        public static IReadOnlyList<string> DescubrirSubcarpetasSrc(string? rutaFuente = null)
        {
            string baseAbs;
            try
            {
                baseAbs = Path.GetFullPath(rutaFuente ?? RutaFuentePredeterminada);
            }
            catch (Exception error) when (EsErrorDeRuta(error))
            {
                return Base;
            }

            if (!Directory.Exists(baseAbs))
                return Base;

            var rutas = new List<string>();
            var pendientes = new Stack<string>();
            pendientes.Push(baseAbs);

            while (pendientes.Count > 0)
            {
                string actual = pendientes.Pop();
                if (!string.Equals(actual, baseAbs, StringComparison.Ordinal))
                    rutas.Add(Relativa(baseAbs, actual));

                string[] hijas;
                try
                {
                    hijas = Directory.GetDirectories(actual);
                }
                catch (Exception error) when (EsErrorDeIo(error))
                {
                    continue;
                }

                Array.Sort(hijas, StringComparer.Ordinal);

                // Se apilan al revés para que la pila entregue orden ascendente.
                for (int i = hijas.Length - 1; i >= 0; i--)
                {
                    string hija = hijas[i];
                    if (Excluidas.Contains(Path.GetFileName(hija)))
                        continue;
                    if (!EntrarEn(hija))
                        continue;
                    pendientes.Push(hija);
                }
            }

            return rutas;
        }

        /// <summary>Alias semántico de <see cref="DescubrirSubcarpetasSrc"/>.</summary>
        public static IReadOnlyList<string> DescubrirJerarquiaSrc(string? rutaFuente = null)
            => DescubrirSubcarpetasSrc(rutaFuente);

        // ------------------------------------------------------------------
        // Localización de la red neuronal
        // ------------------------------------------------------------------

        /// <summary>
        /// Devuelve la carpeta que contiene <c>ENRN/EN2_RN.py</c> o lanza
        /// <see cref="FileNotFoundException"/> si no existe.
        /// </summary>
        public static string LocalizarRedNeuronal()
        {
            return TryLocalizarRedNeuronal(out string? ruta)
                ? ruta
                : throw new FileNotFoundException(
                    "No encuentro RedNeuronal/ENRN/EN2_RN.py. Define LC_RED_NEURONAL " +
                    "o revisa la ruta de " + AppContext.BaseDirectory);
        }

        /// <summary>
        /// Intenta localizar la carpeta de la red neuronal sin lanzar excepciones.
        /// </summary>
        public static bool TryLocalizarRedNeuronal(
            [NotNullWhen(true)] out string? ruta)
            => TryLocalizarRedNeuronal(null, out ruta);

        /// <summary>
        /// Devuelve <c>LCBS_NEURONAL.py</c> (fachada general de la red) o lanza
        /// <see cref="FileNotFoundException"/> si no existe.
        /// </summary>
        public static string LocalizarLcbsNeuronal()
        {
            return TryLocalizarLcbsNeuronal(out string? ruta)
                ? ruta
                : throw new FileNotFoundException(
                    "No encuentro el módulo general de la red (LCBS_NEURONAL.py). " +
                    "Define LC_LCBS_NEURONAL o revisa la ruta de " + AppContext.BaseDirectory);
        }

        /// <summary>
        /// Intenta localizar <c>LCBS_NEURONAL.py</c> sin lanzar excepciones.
        /// </summary>
        public static bool TryLocalizarLcbsNeuronal(
            [NotNullWhen(true)] out string? ruta)
        {
            string? entorno = Environment.GetEnvironmentVariable("LC_LCBS_NEURONAL")?.Trim();
            if (!string.IsNullOrEmpty(entorno))
            {
                string candidata = Environment.ExpandEnvironmentVariables(entorno);
                if (File.Exists(candidata))
                {
                    ruta = Path.GetFullPath(candidata);
                    return true;
                }
            }

            if (!TryLocalizarRedNeuronal(out string? red))
            {
                ruta = null;
                return false;
            }

            string? carpeta = Path.GetDirectoryName(red);
            if (carpeta is not null)
            {
                string lcbs = Path.Combine(carpeta, "LCBS_NEURONAL.py");
                if (File.Exists(lcbs))
                {
                    ruta = lcbs;
                    return true;
                }
            }

            ruta = null;
            return false;
        }

        internal static bool TryLocalizarRedNeuronal(
            string? rutaRed,
            [NotNullWhen(true)] out string? ruta)
        {
            IEnumerable<string> candidatos = string.IsNullOrWhiteSpace(rutaRed)
                ? EnumerarCandidatosRed()
                : new[] { Environment.ExpandEnvironmentVariables(rutaRed.Trim()) };

            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidato in candidatos)
            {
                string resuelto;
                try
                {
                    resuelto = Path.GetFullPath(candidato);
                }
                catch (Exception error) when (EsErrorDeRuta(error))
                {
                    continue;
                }

                if (!vistos.Add(resuelto))
                    continue;

                if (File.Exists(Path.Combine(resuelto, "ENRN", "EN2_RN.py")))
                {
                    ruta = resuelto;
                    return true;
                }
            }

            ruta = null;
            return false;
        }

        // ------------------------------------------------------------------
        // Paridad con el módulo Python (funciones adicionales)
        // ------------------------------------------------------------------

        /// <summary>
        /// Resuelve el módulo EN2_RN (lo que en Python sería
        /// <c>cargar_modulo_red</c>) y devuelve la ruta de <c>EN2_RN.py</c>,
        /// o <c>null</c> si no existe. C# no ejecuta Python: solo carga la
        /// definición resuelta.
        /// </summary>
        public static string? CargarModuloRed(string? rutaRed = null)
        {
            if (!TryLocalizarRedNeuronal(rutaRed, out string? red))
                return null;

            string en2 = Path.Combine(red, "ENRN", "EN2_RN.py");
            return File.Exists(en2) ? en2 : null;
        }

        /// <summary>
        /// Resuelve <c>LCBS_NEURONAL.py</c> (equivalente a
        /// <c>cargar_lcbs_neuronal</c>) y devuelve su ruta, o <c>null</c>.
        /// Acepta una ruta explícita; si es inválida devuelve <c>null</c>
        /// en lugar de lanzar.
        /// </summary>
        public static string? CargarLcbsNeuronal(string? ruta = null)
        {
            if (!string.IsNullOrWhiteSpace(ruta))
            {
                string candidata = Environment.ExpandEnvironmentVariables(ruta.Trim());
                return File.Exists(candidata) ? Path.GetFullPath(candidata) : null;
            }

            return TryLocalizarLcbsNeuronal(out string? lcbs) ? lcbs : null;
        }

        /// <summary>
        /// Clase constructiva de referencia (paridad con
        /// <c>obtener_clase_constructiva</c>). En C# la especialización por
        /// herencia dinámica no existe: se devuelve el tipo que los
        /// consumidores deben instanciar.
        /// </summary>
        public static Type ObtenerClaseConstructiva() => typeof(ConstructorJerarquia);

        /// <summary>
        /// Ámbito que silencia la consola mientras dure, equivalente al gestor
        /// de contexto <c>silencio</c> de Python:
        /// <c>using (Md1Stm.Silencio()) { ... }</c>.
        /// </summary>
        public static AmbitoSilencio Silencio() => AmbitoSilencio.Activar();

        /// <summary>Ejecuta <paramref name="accion"/> con la consola silenciada.</summary>
        public static void EnSilencio(Action accion)
        {
            ArgumentNullException.ThrowIfNull(accion);
            using (Silencio())
                accion();
        }

        // ------------------------------------------------------------------
        // Creación de la jerarquía
        // ------------------------------------------------------------------

        /// <summary>
        /// Crea la raíz <paramref name="nombre"/> dentro de <paramref name="rutaBase"/>
        /// y cada subcarpeta, y devuelve la traza de pasos.
        /// </summary>
        /// <param name="creador">
        /// Mecanismo de creación; por defecto escribe en disco. Permite inyectar
        /// un creador simulado o remoto sin tocar el resto del módulo.
        /// </param>
        /// <remarks>
        /// Nunca lanza por errores de sistema de archivos: devuelve
        /// <see cref="ResultadoCreacion.Ok"/> en <c>false</c> con el error,
        /// la raíz alcanzada y las carpetas creadas hasta el fallo.
        /// </remarks>
        public static ResultadoCreacion CrearJerarquia(
            string rutaBase,
            string nombre,
            IEnumerable<string>? subcarpetas = null,
            ICreadorCarpetas? creador = null)
        {
            ArgumentNullException.ThrowIfNull(rutaBase);
            ArgumentNullException.ThrowIfNull(nombre);

            creador ??= ConstructorJerarquia.Operaciones.Instancia;

            var carpetas = new List<string>();
            var pasos = new List<PasoCreacion>();
            string raiz = string.Empty;
            int numero = 1;

            try
            {
                raiz = AsegurarCarpeta(rutaBase, nombre, creador);
                pasos.Add(new PasoCreacion(numero++, NombreDe(raiz, nombre), raiz));

                if (subcarpetas is not null)
                {
                    foreach (string sub in subcarpetas)
                    {
                        if (string.IsNullOrWhiteSpace(sub))
                            continue;

                        string rutaSub = AsegurarCarpeta(raiz, sub, creador);
                        carpetas.Add(rutaSub);
                        pasos.Add(new PasoCreacion(numero++, sub.Trim(), rutaSub));
                    }
                }

                return new ResultadoCreacion
                {
                    Ok = true,
                    Raiz = raiz,
                    Carpetas = carpetas.ToArray(),
                    Pasos = pasos.ToArray(),
                };
            }
            catch (Exception error) when (EsErrorDeSistemaArchivos(error))
            {
                return new ResultadoCreacion
                {
                    Ok = false,
                    Error = error.GetType().Name + ": " + error.Message,
                    Raiz = raiz,
                    Carpetas = carpetas.ToArray(),
                    Pasos = pasos.ToArray(),
                };
            }
        }

        // ------------------------------------------------------------------
        // Internos compartidos
        // ------------------------------------------------------------------

        /// <summary>
        /// Devuelve <c>base/nombre</c> creándolo si falta. Admite rutas anidadas
        /// (<c>STMGNRL/sub</c>) y crea cada nivel con el creador inyectado.
        /// Rechaza rutas absolutas y segmentos <c>.</c>/<c>..</c> para impedir
        /// que la jerarquía se salga de <paramref name="rutaBase"/>.
        /// </summary>
        private static string AsegurarCarpeta(
            string rutaBase,
            string nombre,
            ICreadorCarpetas creador)
        {
            string limpio = nombre.Trim();
            if (limpio.Length == 0)
                return creador.CrearCarpetaEn(rutaBase, nombre);

            string normalizado = limpio.Replace('\\', '/');
            if (Path.IsPathRooted(limpio) || Path.IsPathRooted(normalizado))
                throw new ArgumentException(
                    "El nombre de carpeta no puede ser una ruta absoluta: " + nombre,
                    nameof(nombre));

            string[] partes = normalizado.Split(
                new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length == 0)
                throw new ArgumentException("Nombre de carpeta vacío.", nameof(nombre));

            for (int i = 0; i < partes.Length; i++)
            {
                if (partes[i] is "." or "..")
                    throw new ArgumentException(
                        "El nombre de carpeta no puede contener segmentos '.' o '..': " + nombre,
                        nameof(nombre));
            }

            string actual = rutaBase;
            for (int i = 0; i < partes.Length; i++)
            {
                string objetivo = Path.Combine(actual, partes[i]);
                if (Directory.Exists(objetivo))
                {
                    actual = objetivo;
                    continue;
                }
                actual = creador.CrearCarpetaEn(actual, partes[i]);
            }

            return actual;
        }

        private static IEnumerable<string> EnumerarCandidatosRed()
        {
            string? entorno = Environment.GetEnvironmentVariable("LC_RED_NEURONAL")?.Trim();
            if (!string.IsNullOrEmpty(entorno))
                yield return Environment.ExpandEnvironmentVariables(entorno);

            var anclas = new[] { AppContext.BaseDirectory, Environment.CurrentDirectory };
            for (int i = 0; i < anclas.Length; i++)
            {
                for (DirectoryInfo? dir = new(anclas[i]); dir is not null; dir = dir.Parent)
                {
                    yield return Path.Combine(dir.FullName, RutaRedRelativa);
                    yield return Path.Combine(dir.FullName, "CONTRRF", RutaRedRelativa);
                    yield return Path.Combine(
                        dir.FullName, "VersionDe_sesion", "CONTRRF", RutaRedRelativa);
                }
            }
        }

        private static string ResolverFuente()
        {
            string? entorno = Environment.GetEnvironmentVariable("LC_SRC")?.Trim();
            if (!string.IsNullOrEmpty(entorno))
            {
                string expandida = Environment.ExpandEnvironmentVariables(entorno);
                if (Directory.Exists(expandida))
                    return Path.GetFullPath(expandida);
            }

            var anclas = new[] { AppContext.BaseDirectory, Environment.CurrentDirectory };
            for (int i = 0; i < anclas.Length; i++)
            {
                for (DirectoryInfo? dir = new(anclas[i]); dir is not null; dir = dir.Parent)
                {
                    string directa = Path.Combine(dir.FullName, "TLLDCD", "src");
                    if (Directory.Exists(directa))
                        return Path.GetFullPath(directa);

                    string anidada = Path.Combine(dir.FullName, "CONTRRF", "TLLDCD", "src");
                    if (Directory.Exists(anidada))
                        return Path.GetFullPath(anidada);
                }
            }

            return Path.GetFullPath(AppContext.BaseDirectory);
        }

        /// <summary>No se entra en enlaces/junctions para evitar ciclos y rutas remotas.</summary>
        private static bool EntrarEn(string ruta)
        {
            try
            {
                return (File.GetAttributes(ruta) & FileAttributes.ReparsePoint) == 0;
            }
            catch (Exception error) when (EsErrorDeIo(error))
            {
                return false;
            }
        }

        private static string Relativa(string baseAbs, string ruta)
            => Path.GetRelativePath(baseAbs, ruta).Replace('\\', '/');

        private static string NombreDe(string ruta, string respaldo)
        {
            string nombre = Path.GetFileName(ruta);
            if (nombre.Length > 0)
                return nombre;

            nombre = Path.GetFileName(
                ruta.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return nombre.Length > 0 ? nombre : respaldo.Trim();
        }

        private static bool EsErrorDeRuta(Exception error)
            => error is ArgumentException or NotSupportedException
                or PathTooLongException or System.Security.SecurityException;

        private static bool EsErrorDeIo(Exception error)
            => error is IOException or UnauthorizedAccessException;

        private static bool EsErrorDeSistemaArchivos(Exception error)
            => error is IOException or UnauthorizedAccessException or ArgumentException
                or NotSupportedException or System.Security.SecurityException;
    }

    /// <summary>Paso individual de una creación de jerarquía.</summary>
    /// <param name="Paso">Número de paso (1 = raíz).</param>
    /// <param name="Carpeta">Nombre solicitado de la carpeta.</param>
    /// <param name="Ruta">Ruta final en disco.</param>
    public readonly record struct PasoCreacion(int Paso, string Carpeta, string Ruta);

    /// <summary>
    /// Ámbito que silencia <see cref="Console"/> (salida y error) hasta que se
    /// libera. Sustituye al gestor de contexto <c>silencio</c> de Python y es
    /// seguro entre hilos: la restauración se hace bajo el mismo candado.
    /// </summary>
    public sealed class AmbitoSilencio : IDisposable
    {
        private static readonly object Candado = new();

        private readonly TextWriter? _salida;
        private readonly TextWriter? _error;
        private bool _restaurado;

        private AmbitoSilencio(TextWriter? salida, TextWriter? error)
        {
            _salida = salida;
            _error = error;
        }

        internal static AmbitoSilencio Activar()
        {
            lock (Candado)
            {
                TextWriter salida = Console.Out;
                TextWriter error = Console.Error;
                Console.SetOut(TextWriter.Null);
                Console.SetError(TextWriter.Null);
                return new AmbitoSilencio(salida, error);
            }
        }

        /// <summary>Restaura la salida y el error originales una sola vez.</summary>
        public void Dispose()
        {
            lock (Candado)
            {
                if (_restaurado)
                    return;

                if (_salida is not null)
                    Console.SetOut(_salida);
                if (_error is not null)
                    Console.SetError(_error);

                _restaurado = true;
            }
        }
    }

    /// <summary>
    /// Resultado de una creación de jerarquía. Si <see cref="Ok"/> es
    /// <c>false</c>, <see cref="Error"/> describe el fallo y las colecciones
    /// conservan lo creado hasta ese punto.
    /// </summary>
    public sealed class ResultadoCreacion
    {
        /// <summary><c>true</c> si la raíz y todas las subcarpetas quedaron creadas.</summary>
        public bool Ok { get; init; }

        /// <summary>Descripción del fallo cuando <see cref="Ok"/> es <c>false</c>.</summary>
        public string Error { get; init; } = string.Empty;

        /// <summary>Ruta de la raíz creada (vacía si falló antes de crearla).</summary>
        public string Raiz { get; init; } = string.Empty;

        /// <summary>Rutas de las subcarpetas creadas, en orden.</summary>
        public IReadOnlyList<string> Carpetas { get; init; } = Array.Empty<string>();

        /// <summary>Traza de pasos seguidos, incluida la raíz.</summary>
        public IReadOnlyList<PasoCreacion> Pasos { get; init; } = Array.Empty<PasoCreacion>();

        /// <summary>Raíz + subcarpetas creadas.</summary>
        public int Total => (Raiz.Length > 0 ? 1 : 0) + Carpetas.Count;
    }

    /// <summary>
    /// Punto de creación de una carpeta. Lo implementa
    /// <see cref="ConstructorJerarquia.Operaciones"/> y sirve para sustituir el
    /// disco por un creador simulado o remoto sin cambiar la lógica de MD1.
    /// </summary>
    public interface ICreadorCarpetas
    {
        /// <summary>Crea (si falta) <c>rutaBase/nombre</c> y devuelve la ruta final.</summary>
        string CrearCarpetaEn(string rutaBase, string nombre);
    }

    /// <summary>
    /// Fachada de alto nivel de MD1: descubre la jerarquía de la fuente,
    /// localiza (opcionalmente) la red neuronal y crea la jerarquía en disco.
    /// </summary>
    public sealed class ConstructorJerarquia
    {
        /// <summary>
        /// Operaciones de carpeta unificadas en una clase anidada: validación de
        /// nombres, creación de un nivel y creación completa de la jerarquía.
        /// </summary>
        public sealed class Operaciones : ICreadorCarpetas
        {
            /// <summary>Instancia compartida (sin estado, segura entre hilos).</summary>
            public static Operaciones Instancia { get; } = new();

            private Operaciones()
            {
            }

            /// <summary>
            /// Indica si <paramref name="nombre"/> es un único segmento válido
            /// (no vacío, no <c>.</c>/<c>..</c>, no absoluto, sin separadores).
            /// </summary>
            public static bool ValidarNombre(string? nombre)
            {
                string limpio = (nombre ?? string.Empty).Trim();
                return limpio.Length > 0
                    && limpio is not ("." or "..")
                    && !Path.IsPathRooted(limpio)
                    && string.Equals(Path.GetFileName(limpio), limpio, StringComparison.Ordinal);
            }

            /// <summary>Crea (si falta) y devuelve la ruta de la carpeta.</summary>
            public string CrearCarpetaEn(string rutaBase, string nombre)
            {
                string destino = Path.GetFullPath(Path.Combine(rutaBase, nombre.Trim()));
                Directory.CreateDirectory(destino);
                return destino;
            }

            /// <summary>Crea raíz + subcarpetas y devuelve la traza.</summary>
            public static ResultadoCreacion AsegurarJerarquia(
                string rutaBase,
                string nombre,
                IEnumerable<string>? subcarpetas = null)
                => Md1Stm.CrearJerarquia(rutaBase, nombre, subcarpetas, Instancia);
        }

        private readonly Lazy<IReadOnlyList<string>> _descubiertas;
        private readonly IReadOnlyList<string>? _fijadas;
        private readonly string? _rutaRed;

        public ConstructorJerarquia(
            string? rutaFuente = null,
            string nombre = Md1Stm.NombrePredeterminado,
            IEnumerable<string>? subcarpetas = null,
            string? rutaRed = null,
            bool usarRed = true)
        {
            RutaFuente = rutaFuente ?? Md1Stm.RutaFuentePredeterminada;
            Nombre = string.IsNullOrWhiteSpace(nombre) ? Md1Stm.NombrePredeterminado : nombre;
            _fijadas = subcarpetas?.ToArray();
            _rutaRed = rutaRed;
            UsarRed = usarRed;
            _descubiertas = new Lazy<IReadOnlyList<string>>(
                () => Md1Stm.DescubrirSubcarpetasSrc(RutaFuente),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>Fuente de la jerarquía (por defecto <c>TLLDCD/src</c>).</summary>
        public string RutaFuente { get; }

        /// <summary>Nombre de la carpeta raíz a crear.</summary>
        public string Nombre { get; }

        /// <summary>Si se permite resolver la red neuronal.</summary>
        public bool UsarRed { get; }

        /// <summary>Ruta explícita de la red, si se indicó.</summary>
        public string? RutaRed => _rutaRed;

        /// <summary>
        /// Subcarpetas a crear: las indicadas en el constructor o, si no,
        /// la jerarquía descubierta (calculada una sola vez por instancia).
        /// </summary>
        public IReadOnlyList<string> Subcarpetas => _fijadas ?? _descubiertas.Value;

        /// <summary>Devuelve la jerarquía que se creará.</summary>
        public IReadOnlyList<string> Descubrir() => Subcarpetas;

        /// <summary>
        /// Devuelve la carpeta de la red neuronal si existe, o <c>null</c>.
        /// El módulo C# no ejecuta Python: solo resuelve la ruta.
        /// </summary>
        public string? CargarRed()
            => UsarRed && Md1Stm.TryLocalizarRedNeuronal(_rutaRed, out string? ruta)
                ? ruta
                : null;

        /// <summary>
        /// Devuelve <c>LCBS_NEURONAL.py</c> si existe, o <c>null</c>.
        /// </summary>
        public string? CargarLcbs()
            => UsarRed && Md1Stm.TryLocalizarLcbsNeuronal(out string? ruta) ? ruta : null;

        /// <summary>
        /// Crea la jerarquía. Si <paramref name="rutaBase"/> es <c>null</c> usa
        /// <see cref="RutaFuente"/>.
        /// </summary>
        public ResultadoCreacion Crear(string? rutaBase = null)
            => Md1Stm.CrearJerarquia(rutaBase ?? RutaFuente, Nombre, Subcarpetas);

        public override string ToString()
            => "ConstructorJerarquia(nombre=\"" + Nombre + "\", fuente=\"" + RutaFuente + "\")";
    }
}
