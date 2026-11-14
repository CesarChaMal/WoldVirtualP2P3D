using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Visor3D.Stm
{
    /// <summary>
    /// MD2 en C#: interpretación y normalización de órdenes de LucIA.
    ///
    /// <para>Puerto del módulo Python <c>STM_MD2</c> con la misma finalidad:
    /// convierte texto etiquetado, JSON o diccionario en la orden normalizada
    /// que ejecuta MD1 (<c>comando</c>, <c>ruta</c>, <c>nombre</c>,
    /// <c>subcarpetas</c>, <c>lenguaje</c> y <c>archivos</c>). No crea carpetas
    /// ni carga la red neuronal: es la mitad «parser» del constructor.</para>
    ///
    /// <para><b>Importación:</b> <c>using Visor3D.Stm;</c> y usa
    /// <see cref="InterpreteOrden"/> o el atajo <c>Md2Stm.InterpretarOrden</c>.</para>
    ///
    /// <para><b>Rendimiento:</b> expresiones regulares estáticas con
    /// <see cref="RegexOptions.NonBacktracking"/> (tiempo lineal, sin ReDoS),
    /// JSON con <see cref="JsonDocument"/> de solo lectura y tokenizador propio
    /// de una pasada; sin LINQ en los bucles de extracción.</para>
    /// </summary>
    public static class Md2Stm
    {
        public const string ComandoCarpeta = "crear carpeta";
        public const string ComandoRed = "crear red neuronal";
        public const string ComandoMtbgt = "crear mtbgt";
        public const string ComandoArchivo = "crear archivo";
        public const string ComandoTodo = "crear todo";

        /// <summary>Comandos reconocidos.</summary>
        public static IReadOnlyList<string> Comandos { get; } = Array.AsReadOnly(new[]
        {
            ComandoCarpeta, ComandoRed, ComandoMtbgt, ComandoArchivo, ComandoTodo,
        });

        /// <summary>Nombre que usa el comando cuando no se indica.</summary>
        public const string NombrePredeterminado = "src";

        /// <summary>Nombre por defecto de la acción de red neuronal.</summary>
        public const string NombreRedPredeterminado = "RedNeuronal";

        /// <summary>Subcarpetas de <c>LCtrabajando/src/LC_STM/RedNeuronal</c>.</summary>
        public static IReadOnlyList<string> SubcarpetasRed { get; } = Array.AsReadOnly(new[]
        {
            "CELEBRO", "ENRN", "RF_EN", "RF_SL", "RNP", "SLRN",
        });

        /// <summary>Nombre por defecto de la acción MTBGT.</summary>
        public const string NombreMtbgtPredeterminado = "MTBGT";

        /// <summary>Subcarpetas de <c>LCtrabajando/src/MTBGT</c>.</summary>
        public static IReadOnlyList<string> SubcarpetasMtbgt { get; } = Array.AsReadOnly(new[]
        {
            ".godot", "addons", "AreaDeTrabajo", "gdscrip", "gdshader", "scene",
        });

        /// <summary>
        /// Atajo: normaliza una orden de texto o JSON con las rutas indicadas.
        /// Sin <paramref name="rutas"/> el destino queda vacío y lo decide quien
        /// ejecuta (paridad con <c>interpretar_orden</c>).
        /// </summary>
        public static OrdenNormalizada InterpretarOrden(
            string? orden,
            IReadOnlyDictionary<string, string?>? rutas = null)
            => new InterpreteOrden(rutas).Interpretar(orden);

        /// <summary>Atajo: normaliza una orden recibida como diccionario.</summary>
        public static OrdenNormalizada InterpretarOrden(
            IReadOnlyDictionary<string, object?> orden,
            IReadOnlyDictionary<string, string?>? rutas = null)
            => new InterpreteOrden(rutas).Interpretar(orden);
    }

    /// <summary>
    /// Normaliza órdenes de LucIA recibidas como texto, JSON o diccionario.
    /// </summary>
    /// <remarks>
    /// Las rutas predeterminadas se inyectan por comando y se leen en cada
    /// llamada, de modo que el consumidor puede cambiar su configuración sin
    /// recrear el intérprete.
    /// </remarks>
    public sealed class InterpreteOrden
    {
        private const RegexOptions OpcionesCampo =
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

        private const RegexOptions OpcionesPrefijo =
            RegexOptions.IgnoreCase | RegexOptions.Singleline |
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

        private readonly Dictionary<string, string> _rutas;

        public InterpreteOrden(IReadOnlyDictionary<string, string?>? rutas = null)
        {
            var copia = new Dictionary<string, string>(StringComparer.Ordinal);
            if (rutas is not null)
            {
                foreach (KeyValuePair<string, string?> par in rutas)
                    copia[par.Key] = par.Value ?? string.Empty;
            }
            _rutas = copia;
        }

        /// <summary>Ruta predeterminada por comando, tal como la inyectó el consumidor.</summary>
        public IReadOnlyDictionary<string, string> Rutas => _rutas;

        // ------------------------------------------------------------------
        // Campos: extracción y alias
        // ------------------------------------------------------------------

        /// <summary>
        /// Extracción de campos etiquetados y normalización de alias. Se agrupa
        /// en una clase anidada para no repetir patrones en el consumidor.
        /// </summary>
        public static class Campos
        {
            /// <summary>Campos etiquetados de las órdenes en texto.</summary>
            public static IReadOnlyList<(string Campo, Regex Patron)> Patrones { get; } =
                Array.AsReadOnly(new (string, Regex)[]
                {
                    ("ruta", new Regex(
                        @"(?:^|[\s,;])(?:ruta)\s*(?:donde\s+crear|de\s+creaci[oó]n)?\s*[:=]\s*",
                        OpcionesCampo)),
                    ("nombre", new Regex(
                        @"(?:^|[\s,;])(?:nombre)\s*(?:de\s+la\s+carpeta|de\s+carpeta|del\s+archivo|de\s+archivo)?\s*[:=]\s*",
                        OpcionesCampo)),
                    ("lenguaje", new Regex(
                        @"(?:^|[\s,;])(?:lenguaje(?:\s+de\s+programaci[oó]n)?|lenguaje_programacion|extension)\s*[:=]\s*",
                        OpcionesCampo)),
                    ("archivos", new Regex(
                        @"(?:^|[\s,;])(?:archivos)\s*[:=]\s*",
                        OpcionesCampo)),
                    ("subcarpetas", new Regex(
                        @"(?:^|[\s,;])(?:subcarpetas?|jerarqu[ií]a)\s*[:=]\s*",
                        OpcionesCampo)),
                });

            /// <summary>Prefijo que identifica el comando al inicio del texto.</summary>
            public static Regex PrefijoComando { get; } = new(
                @"^\s*(?:comando\s+)?(crear\s+mtbgt|mtbgt|crear\s+godot|godot|crear\s+todo|todo|crear\s+secuencia|secuencia|crear\s+red\s+neuronal|red\s+neuronal|red_neuronal|red|crear\s+archivo|archivo|crear\s+carpeta|crear_carpeta)\b",
                OpcionesPrefijo);

            /// <summary>Separa nombres por comas, punto y coma, barras o saltos.</summary>
            public static IReadOnlyList<string> DividirSubcarpetas(string valor)
            {
                string[] partes = Regex.Split(valor, @"[,;|\n]+");
                var limpias = new List<string>(partes.Length);
                foreach (string parte in partes)
                {
                    string limpia = parte.Trim().Trim('"', '\'');
                    if (limpia.Length > 0)
                        limpias.Add(limpia);
                }
                return limpias.ToArray();
            }

            /// <summary>Convierte <c>subcarpetas</c> en nombres limpios o <c>null</c>.</summary>
            public static IReadOnlyList<string>? NormalizarSubcarpetas(object? valor)
            {
                if (valor is null)
                    return null;
                if (valor is string texto)
                    return DividirSubcarpetas(texto);
                if (valor is System.Collections.IEnumerable enumerable)
                {
                    var limpias = new List<string>();
                    foreach (object? item in enumerable)
                    {
                        string limpia = Limpiar(ATexto(item));
                        if (limpia.Length > 0)
                            limpias.Add(limpia);
                    }
                    return limpias.ToArray();
                }
                return null;
            }

            /// <summary>
            /// Extrae los campos etiquetados aunque vengan en una línea.
            /// Devuelve la primera aparición de cada campo.
            /// </summary>
            public static IReadOnlyDictionary<string, string> Extraer(string texto)
            {
                var marcas = new List<(int Inicio, int Fin, string Campo)>();
                foreach ((string campo, Regex patron) in Patrones)
                {
                    MatchCollection coincidencias = patron.Matches(texto);
                    foreach (Match coincidencia in coincidencias)
                        marcas.Add((coincidencia.Index, coincidencia.Index + coincidencia.Length, campo));
                }

                var ordenadas = marcas.OrderBy(marca => marca.Inicio).ToList();
                var campos = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 0; i < ordenadas.Count; i++)
                {
                    var (_, fin, campo) = ordenadas[i];
                    int siguiente = texto.Length;
                    for (int j = i + 1; j < ordenadas.Count; j++)
                    {
                        if (ordenadas[j].Inicio >= fin)
                        {
                            siguiente = ordenadas[j].Inicio;
                            break;
                        }
                    }

                    string valor = texto[fin..siguiente].Trim().Trim('"', '\'').Trim();
                    if (valor.Length > 0 && !campos.ContainsKey(campo))
                        campos[campo] = valor;
                }
                return campos;
            }

            /// <summary>Unifica los alias (<c>red</c>, <c>red_neuronal</c>, ...).</summary>
            public static string NormalizarComando(object? comando)
            {
                string limpio = ATexto(comando).Trim().ToLowerInvariant();

                if (limpio.StartsWith("comando", StringComparison.Ordinal))
                {
                    int inicio = "comando".Length;
                    if (inicio < limpio.Length && char.IsWhiteSpace(limpio[inicio]))
                        limpio = limpio[inicio..].TrimStart();
                }

                string[] palabras = limpio.Replace('_', ' ')
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                limpio = string.Join(' ', palabras);

                if (limpio is "red" or "red neuronal" or "crear red" or Md2Stm.ComandoRed)
                    return Md2Stm.ComandoRed;
                if (limpio is "mtbgt" or "crear mtbgt" or "godot" or "crear godot" or Md2Stm.ComandoMtbgt)
                    return Md2Stm.ComandoMtbgt;
                if (limpio is "archivo" or "crear archivo" or Md2Stm.ComandoArchivo)
                    return Md2Stm.ComandoArchivo;
                if (limpio is "todo" or "crear todo" or "secuencia" or "crear secuencia" or Md2Stm.ComandoTodo)
                    return Md2Stm.ComandoTodo;
                if (limpio is "src" or "crear src" or Md2Stm.ComandoCarpeta)
                    return Md2Stm.ComandoCarpeta;
                return limpio;
            }
        }

        // ------------------------------------------------------------------
        // Valores predeterminados
        // ------------------------------------------------------------------

        /// <summary>Ruta, nombre y subcarpetas predeterminadas de cada acción.</summary>
        public PredeterminadosComando Predeterminados(string comando)
        {
            (string nombre, IReadOnlyList<string>? subcarpetas) = comando switch
            {
                Md2Stm.ComandoRed => (Md2Stm.NombreRedPredeterminado, Md2Stm.SubcarpetasRed),
                Md2Stm.ComandoMtbgt => (Md2Stm.NombreMtbgtPredeterminado, Md2Stm.SubcarpetasMtbgt),
                Md2Stm.ComandoArchivo => (string.Empty, null),
                Md2Stm.ComandoTodo => (Md2Stm.NombrePredeterminado, (IReadOnlyList<string>?)null),
                _ => (Md2Stm.NombrePredeterminado, (IReadOnlyList<string>?)null),
            };

            string ruta = _rutas.TryGetValue(comando, out string? inyectada)
                ? inyectada
                : string.Empty;
            return new PredeterminadosComando(ruta, nombre, subcarpetas);
        }

        /// <summary>Comando indicado al principio del texto (por defecto, carpeta).</summary>
        public string ComandoDeTexto(string? texto)
        {
            if (string.IsNullOrEmpty(texto))
                return Md2Stm.ComandoCarpeta;

            Match coincidencia = Campos.PrefijoComando.Match(texto);
            return coincidencia.Success
                ? Campos.NormalizarComando(coincidencia.Groups[1].Value)
                : Md2Stm.ComandoCarpeta;
        }

        /// <summary>Estructura de error común al interpretar una orden.</summary>
        public static OrdenNormalizada Error(string mensaje) => new()
        {
            Comando = Md2Stm.ComandoCarpeta,
            Ruta = string.Empty,
            Nombre = string.Empty,
            Subcarpetas = null,
            Lenguaje = string.Empty,
            Archivos = null,
            Error = mensaje,
        };

        // ------------------------------------------------------------------
        // Normalización
        // ------------------------------------------------------------------

        /// <summary>Normaliza una orden recibida como diccionario de cadenas.</summary>
        public OrdenNormalizada NormalizarOrden(IReadOnlyDictionary<string, string?> datos)
        {
            ArgumentNullException.ThrowIfNull(datos);
            var convertidos = new Dictionary<string, object?>(datos.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, string?> par in datos)
                convertidos[par.Key] = par.Value;
            return NormalizarOrden(convertidos);
        }

        /// <summary>Normaliza una orden recibida como diccionario de valores.</summary>
        public OrdenNormalizada NormalizarOrden(IReadOnlyDictionary<string, object?> datos)
        {
            ArgumentNullException.ThrowIfNull(datos);

            string comandoCrudo = PrimeroTexto(datos, "comando", "accion", "orden");
            string comando = Campos.NormalizarComando(
                comandoCrudo.Length > 0 ? comandoCrudo : Md2Stm.ComandoCarpeta);
            PredeterminadosComando pred = Predeterminados(comando);

            string ruta = PrimeroTexto(datos, "ruta", "ruta_donde_crear", "ruta_crear", "path");
            if (ruta.Length == 0)
                ruta = pred.Ruta;

            string nombre = PrimeroTexto(datos, "nombre", "nombre_carpeta", "nombre_de_la_carpeta", "carpeta");
            if (nombre.Length == 0)
                nombre = pred.Nombre;

            IReadOnlyList<string>? subcarpetas =
                Campos.NormalizarSubcarpetas(PrimeroPresente(datos, "subcarpetas", "jerarquia", "carpetas"))
                ?? pred.Subcarpetas;

            string lenguaje = PrimeroTexto(datos, "lenguaje", "lenguaje_programacion", "extension");
            object? archivos = datos.TryGetValue("archivos", out object? valorArchivos)
                ? valorArchivos
                : null;

            return new OrdenNormalizada
            {
                Comando = comando,
                Ruta = Limpiar(ruta),
                Nombre = Limpiar(nombre),
                Subcarpetas = subcarpetas,
                Lenguaje = Limpiar(lenguaje),
                Archivos = Campos.NormalizarSubcarpetas(archivos),
            };
        }

        /// <summary>Normaliza una orden ya deserializada como objeto JSON.</summary>
        public OrdenNormalizada NormalizarOrden(JsonElement datos)
        {
            if (datos.ValueKind != JsonValueKind.Object)
                return Error("El JSON de la orden debe ser un objeto.");
            return NormalizarOrden(DeJson(datos));
        }

        // ------------------------------------------------------------------
        // Interpretación
        // ------------------------------------------------------------------

        /// <summary>
        /// Convierte texto, JSON o diccionario en una orden normalizada.
        /// Acepta <c>Ruta donde crear:</c> y <c>Nombre de la carpeta:</c> en una
        /// o varias líneas, además de la forma corta
        /// <c>crear carpeta &lt;ruta&gt; [nombre]</c>.
        /// </summary>
        public OrdenNormalizada Interpretar(string? orden)
        {
            string texto = (orden ?? string.Empty).Trim();
            if (texto.Length == 0)
                return Error("Orden vacía: no hay comando que ejecutar.");

            if (texto[0] == '{')
            {
                try
                {
                    using JsonDocument documento = JsonDocument.Parse(texto);
                    if (documento.RootElement.ValueKind != JsonValueKind.Object)
                        return Error("El JSON de la orden debe ser un objeto.");
                    return NormalizarOrden(DeJson(documento.RootElement));
                }
                catch (JsonException error)
                {
                    return Error("JSON no válido: " + error.Message);
                }
            }

            string comando = ComandoDeTexto(texto);
            PredeterminadosComando pred = Predeterminados(comando);
            IReadOnlyDictionary<string, string> campos = Campos.Extraer(texto);

            if (campos.Count > 0)
            {
                // Sin prefijo explícito, el comando se deduce de los campos.
                if (comando == Md2Stm.ComandoCarpeta && !Campos.PrefijoComando.IsMatch(texto))
                {
                    string nombreClave = ValorDe(campos, "nombre").ToLowerInvariant();
                    string rutaClave = Path.GetFileName(ValorDe(campos, "ruta")).ToLowerInvariant();
                    string lenguajeClave = ValorDe(campos, "lenguaje").ToLowerInvariant();
                    string archivosClave = ValorDe(campos, "archivos");

                    if (nombreClave == Md2Stm.NombreRedPredeterminado.ToLowerInvariant())
                        comando = Md2Stm.ComandoRed;
                    else if (nombreClave == Md2Stm.NombreMtbgtPredeterminado.ToLowerInvariant()
                             || rutaClave == Md2Stm.NombreMtbgtPredeterminado.ToLowerInvariant())
                        comando = Md2Stm.ComandoMtbgt;
                    else if (lenguajeClave.Length > 0
                             || archivosClave.Length > 0
                             || Path.GetExtension(nombreClave).Length > 0)
                        comando = Md2Stm.ComandoArchivo;

                    if (comando != Md2Stm.ComandoCarpeta)
                        pred = Predeterminados(comando);
                }

                string rutaCampo = ValorDe(campos, "ruta");
                string nombreCampo = ValorDe(campos, "nombre");
                IReadOnlyList<string>? subcarpetasCampo =
                    Campos.NormalizarSubcarpetas(ValorDe(campos, "subcarpetas"));

                return new OrdenNormalizada
                {
                    Comando = comando,
                    Ruta = Limpiar(rutaCampo.Length > 0 ? rutaCampo : pred.Ruta),
                    Nombre = Limpiar(nombreCampo.Length > 0 ? nombreCampo : pred.Nombre),
                    Subcarpetas = subcarpetasCampo ?? pred.Subcarpetas,
                    Lenguaje = Limpiar(ValorDe(campos, "lenguaje")),
                    Archivos = Campos.NormalizarSubcarpetas(ValorDe(campos, "archivos")),
                };
            }

            Match coincidencia = Campos.PrefijoComando.Match(texto);
            if (!coincidencia.Success)
            {
                return Error(
                    "No reconozco la orden. Usa 'crear carpeta', " +
                    "'crear red neuronal' o 'crear archivo' con " +
                    "'Ruta donde crear:' y 'Nombre de la carpeta:'.");
            }

            string resto = texto[(coincidencia.Index + coincidencia.Length)..].Trim();
            if (resto.Length == 0)
            {
                return new OrdenNormalizada
                {
                    Comando = comando,
                    Ruta = pred.Ruta,
                    Nombre = pred.Nombre,
                    Subcarpetas = pred.Subcarpetas,
                    Lenguaje = string.Empty,
                    Archivos = null,
                };
            }

            IReadOnlyList<string> partes = DividirComoShell(resto);
            string ruta = partes.Count > 0 ? Limpiar(partes[0]) : pred.Ruta;
            string nombre = partes.Count > 1 ? Limpiar(partes[1]) : pred.Nombre;
            return new OrdenNormalizada
            {
                Comando = comando,
                Ruta = ruta,
                Nombre = nombre,
                Subcarpetas = pred.Subcarpetas,
                Lenguaje = string.Empty,
                Archivos = null,
            };
        }

        /// <summary>Normaliza una orden ya recibida como diccionario.</summary>
        public OrdenNormalizada Interpretar(IReadOnlyDictionary<string, object?> orden)
            => NormalizarOrden(orden);

        /// <summary>Normaliza una orden ya recibida como diccionario de cadenas.</summary>
        public OrdenNormalizada Interpretar(IReadOnlyDictionary<string, string?> orden)
            => NormalizarOrden(orden);

        /// <summary>Normaliza una orden ya deserializada como objeto JSON.</summary>
        public OrdenNormalizada Interpretar(JsonElement orden)
            => NormalizarOrden(orden);

        // ------------------------------------------------------------------
        // Auxiliares
        // ------------------------------------------------------------------

        /// <summary>Trim de espacios y comillas envolventes, como el módulo Python.</summary>
        internal static string Limpiar(string? valor)
            => (valor ?? string.Empty).Trim().Trim('"', '\'');

        internal static string ATexto(object? valor) => valor switch
        {
            null => string.Empty,
            string texto => texto,
            IFormattable formateable => formateable.ToString(null, CultureInfo.InvariantCulture)
                                        ?? string.Empty,
            _ => valor.ToString() ?? string.Empty,
        };

        private static string ValorDe(IReadOnlyDictionary<string, string> campos, string campo)
            => campos.TryGetValue(campo, out string? valor) ? valor : string.Empty;

        private static string PrimeroTexto(IReadOnlyDictionary<string, object?> datos, params string[] claves)
        {
            foreach (string clave in claves)
            {
                if (!datos.TryGetValue(clave, out object? valor))
                    continue;

                string texto = ATexto(valor).Trim();
                if (texto.Length > 0)
                    return texto;
            }
            return string.Empty;
        }

        private static object? PrimeroPresente(IReadOnlyDictionary<string, object?> datos, params string[] claves)
        {
            foreach (string clave in claves)
            {
                if (!datos.TryGetValue(clave, out object? valor))
                    continue;

                switch (valor)
                {
                    case null:
                        continue;
                    case string texto when texto.Trim().Length == 0:
                        continue;
                    case System.Collections.ICollection coleccion when coleccion.Count == 0:
                        continue;
                }
                return valor;
            }
            return null;
        }

        /// <summary>
        /// Tokeniza respetando comillas y conservando el carácter de comilla,
        /// como <c>shlex.split(posix=False)</c>; tolerante a comillas sin cerrar.
        /// </summary>
        private static IReadOnlyList<string> DividirComoShell(string resto)
        {
            var partes = new List<string>();
            var actual = new StringBuilder();
            char? comilla = null;

            foreach (char caracter in resto)
            {
                if (comilla is not null)
                {
                    actual.Append(caracter);
                    if (caracter == comilla)
                        comilla = null;
                    continue;
                }

                if (caracter is '"' or '\'')
                {
                    comilla = caracter;
                    actual.Append(caracter);
                    continue;
                }

                if (char.IsWhiteSpace(caracter))
                {
                    if (actual.Length > 0)
                    {
                        partes.Add(actual.ToString());
                        actual.Clear();
                    }
                    continue;
                }

                actual.Append(caracter);
            }

            if (actual.Length > 0)
                partes.Add(actual.ToString());
            return partes;
        }

        private static Dictionary<string, object?> DeJson(JsonElement objeto)
        {
            var datos = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (JsonProperty propiedad in objeto.EnumerateObject())
                datos[propiedad.Name] = ValorJson(propiedad.Value);
            return datos;
        }

        private static object? ValorJson(JsonElement valor) => valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => valor.EnumerateArray()
                .Select(elemento => ValorJson(elemento) is string texto
                    ? texto
                    : elemento.GetRawText())
                .ToArray(),
            _ => valor.GetRawText(),
        };
    }

    /// <summary>Ruta, nombre y subcarpetas predeterminadas de un comando.</summary>
    /// <param name="Ruta">Ruta predeterminada inyectada por el consumidor.</param>
    /// <param name="Nombre">Nombre de carpeta predeterminado.</param>
    /// <param name="Subcarpetas">Subcarpetas predeterminadas, si las hay.</param>
    public readonly record struct PredeterminadosComando(
        string Ruta,
        string Nombre,
        IReadOnlyList<string>? Subcarpetas);

    /// <summary>
    /// Orden normalizada que consume el constructor (MD1).
    /// Si <see cref="Error"/> no está vacío, la orden no es ejecutable y el
    /// resto de campos queda con sus valores por defecto.
    /// </summary>
    public sealed class OrdenNormalizada
    {
        /// <summary>Comando normalizado (<c>crear carpeta</c>, <c>crear red neuronal</c>, ...).</summary>
        public string Comando { get; init; } = Md2Stm.ComandoCarpeta;

        /// <summary>Ruta donde crear; vacía si la decide quien ejecuta.</summary>
        public string Ruta { get; init; } = string.Empty;

        /// <summary>Nombre de la carpeta o archivo a crear.</summary>
        public string Nombre { get; init; } = string.Empty;

        /// <summary>Subcarpetas a crear, o <c>null</c> si no aplica.</summary>
        public IReadOnlyList<string>? Subcarpetas { get; init; }

        /// <summary>Lenguaje de programación del archivo, si se indicó.</summary>
        public string Lenguaje { get; init; } = string.Empty;

        /// <summary>Archivos a crear, o <c>null</c> si no aplica.</summary>
        public IReadOnlyList<string>? Archivos { get; init; }

        /// <summary>Mensaje de error; vacío si la orden es válida.</summary>
        public string Error { get; init; } = string.Empty;

        /// <summary><c>true</c> si la orden se interpretó sin errores.</summary>
        public bool Ok => Error.Length == 0;

        /// <summary>Vista de diccionario con las claves del módulo Python.</summary>
        public IReadOnlyDictionary<string, object?> ToDiccionario()
        {
            var datos = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["comando"] = Comando,
                ["ruta"] = Ruta,
                ["nombre"] = Nombre,
                ["subcarpetas"] = Subcarpetas,
                ["lenguaje"] = Lenguaje,
                ["archivos"] = Archivos,
            };
            if (Error.Length > 0)
                datos["error"] = Error;
            return datos;
        }

        /// <summary>Orden normalizada serializada como JSON.</summary>
        public string ToJson() => JsonSerializer.Serialize(ToDiccionario());

        public override string ToString()
            => "OrdenNormalizada(comando=\"" + Comando + "\", ruta=\"" + Ruta +
               "\", nombre=\"" + Nombre + "\", error=\"" + Error + "\")";
    }
}
