using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Visor3D.Chat;
using Visor3D.RedP2P;
using Visor3D.Seguridad;
using Visor3D.Services;
using Visor3D.Update;

namespace Visor3D
{
    /// <summary>
    /// Parte de <see cref="MainWindow"/> con el estado de arranque del visor:
    /// versión y rutas del perfil, servicios (WebNodo, blockchain, LucIA, chat,
    /// P2P y túneles), construcción de la ventana y ciclo de vida.
    /// </summary>
    public partial class MainWindow : Window
    {
        // ── Versión y rutas del perfil de usuario ─────────────────────

        // Versión local, leída del propio ensamblado (definida en Visor3D.csproj <Version>).
        private static readonly string CurrentVersion = ObtenerVersionLocal();

        // Carpeta de datos del usuario en el perfil local del SO (%LocalAppData%\WoldVirtualP2P3D\).
        // Es estable entre versiones: AppExtractor sólo borra runtime\<versión>.
        private static readonly string TestUserDataDir = PerfilUsuario.BaseDir;

        private static readonly string CredentialsPath = PerfilUsuario.CredencialesPath;

        private static readonly string UserProfilePath = PerfilUsuario.PerfilPath;

        // ── Motor 3D, red P2P y túnel del portal ─────────────────────

        private GodotHwndHost? _godotHostMain;
        private RedP2PServicio? _p2pServicio;
        private Process? _tunnelProcess;
        private const string TunelWebNodoSlot = "webnodo";   // slot del PID persistido para el túnel del portal
        private string _urlAmigoPendiente = "";
        private string _ipsLanFallback = "";
        private string _urlPublica = "";
        private string _enlaceCompartible = "";
        private string _hostSesionUrl = "";
        private List<string> _nodosRedLan = new();
        private Dictionary<string, int> _puertosRedLan = new(StringComparer.OrdinalIgnoreCase);
        private int _indiceNodoSeleccionado = 0;
        private bool _consultaRedEnCurso;
        private string _hostInicial = "";
        private string _tokenInvitacion = "";
        private readonly DateTime _arranqueUtc = DateTime.UtcNow;
        private ReleaseInfo? _updateInfo;
        private bool _descargandoActualizacion;
        private CancellationTokenSource? _ctsDescarga;
        private DispatcherTimer? _timerMigracion;

        // ── Validación de conexión con túneles SSH (LucIA como host) ─────
        private DispatcherTimer? _timerValidacionTunel;
        private bool _validacionTunelEnCurso = false;
        private string _rutaDatosGodot = "";  // Ruta al directorio user:// de Godot
        private string _rutaProyectoIsla3D = ""; // Proyecto elegido por el creador

        // ── Servidor Oficial Blockchain & Cuentas (STM_BKCH puerto 8545) ─
        private readonly ServidorBlockchainOficial _servidorBlockchain = new();
        private readonly ServidorLucIAOficial _servidorLucia = new();
        private Process? _webNodoHelper;
        private GodotHwndHost? _constructorHost;
        private DispatcherTimer? _timerLucia;
        private bool _iniciandoLucia;

        // Equipo sin nada de lo necesario: se abre el ciclo de registro completo
        // sin pasar por la pantalla de bienvenida (sólo una vez por ejecución).
        private bool _equipoVacio;
        private bool _registroAutoLanzado;

        // ── Chat del metaverso (texto + voz + webcam) ─────────────────
        private ChatRelayServer? _chatServer;
        private ChatClient? _chatClient;
        private ChatVoice? _chatVoice;
        private ChatWebcam? _chatWebcam;
        private DispatcherTimer? _timerChat;
        private string _chatUsuario = "";
        private bool _chatActivo;
        private bool _chatConectando;
        private bool _chatAbierto;
        private int _chatNoLeidos;
        private DateTime _chatProximoIntento = DateTime.MinValue;
        private readonly Dictionary<int, string> _nombresChat = new();
        private readonly Dictionary<int, (Border Marco, Image Imagen, TextBlock Nombre)> _tilesChat = new();
        private readonly Dictionary<int, DateTime> _ultimoVideoChat = new();

        // ── Amigos y mensajes privados ────────────────────────────────
        private string _chatUid = "";
        private FriendsStore? _amigos;
        private readonly Dictionary<int, string> _uidPorId = new();
        private readonly HashSet<string> _uidsEnLinea = new();
        private readonly HashSet<string> _reenviosAmistad = new();
        private readonly Dictionary<string, PrivateChatWindow> _ventanasPrivadas = new();
        private readonly Dictionary<string, int> _privadosNoLeidos = new();
        private readonly Dictionary<string, List<MensajePrivado>> _historialPrivado = new();

        public MainWindow(string? initialConnectionUrl = null, bool equipoVacio = false)
        {
            InitializeComponent();
            _equipoVacio = equipoVacio;

            AplicarEnlaceInicial(initialConnectionUrl);

            Loaded += OnLoaded;
            SizeChanged += (s, e) => AjustarBarraResponsive();
            Closing += (s, e) => DetenerServiciosEnCierre();

            RegistrarManejadoresUi();
            IniciarVigilantes();

            _rutaDatosGodot = ResolverRutaDatosGodot();
        }

        /// <summary>
        /// Aplica el enlace de sesión recibido por línea de comandos: fija el
        /// anfitrión raíz y adopta el token de invitación, si lo trae.
        /// </summary>
        private void AplicarEnlaceInicial(string? initialConnectionUrl)
        {
            if (string.IsNullOrWhiteSpace(initialConnectionUrl))
                return;

            // El enlace puede traer ?host=<anfitrión raíz> y la invitación de la
            // sesión. Todos los invitados deben converger en ese anfitrión.
            _urlAmigoPendiente = NormalizarUrlSesion(
                initialConnectionUrl.Trim(), out string? raiz, out string? token);
            _hostSesionUrl = raiz ?? _urlAmigoPendiente;
            _hostInicial = _hostSesionUrl;
            AdoptarTokenInvitacion(token);

            string detalle =
                $"[MainWindow] Enlace de sesión: {_urlAmigoPendiente} (raíz: {_hostSesionUrl}, invitación: {(string.IsNullOrEmpty(_tokenInvitacion) ? "—" : "sí")})";
            Debug.WriteLine(detalle);
            Console.WriteLine(detalle);
        }

        /// <summary>Detiene servicios, túneles y temporizadores al cerrar la ventana.</summary>
        private void DetenerServiciosEnCierre()
        {
            DetenerChatSesion();
            _servidorBlockchain.Detener();
            _servidorLucia.Detener();
            try { _constructorHost?.StopGodot(); } catch { }
            _godotHostMain?.StopGodot();
            _p2pServicio?.Dispose();
            DetenerTunelWebNodo();
            _timerValidacionTunel?.Stop();
            _timerLucia?.Stop();
            try { _webNodoHelper?.Kill(entireProcessTree: true); } catch { }
        }

        /// <summary>Conecta los manejadores de la UI de bienvenida, login, P2P, chat y amigos.</summary>
        private void RegistrarManejadoresUi()
        {
            // Navegación de bienvenida oficial (Lucía 3D)
            BtnCrearCuenta.Click += OnCrearCuentaClicked;
            BtnOficinaLucia.Click += (s, e) => AbrirOficinaLucia();
            BtnOficinaAvatar.Click += (s, e) => AbrirOficinaConAvatar();
            BtnInvitarLucia.Click += (s, e) => InvitarLuciaIsla();
            BtnConstruir.Click += (s, e) => AbrirConstructorGodot();
            BtnUsarConstruccion.Click += (s, e) => UsarConstruccionEnIsla();
            BtnCerrarConstructor.Click += (s, e) => CerrarConstructorGodot();
            BtnImportarCuenta.Click += (s, e) => ImportarCuentaDialogo();
            BtnVolverLucia.Click += (s, e) => VolverALuciaRecepcion();
            BtnAcceder.Click += OnAccederClicked;
            BtnVolverBienvenida.Click += OnVolverBienvenidaClicked;
            BtnMigrarCuenta.Click += OnMigrarCuentaClicked;

            // Login
            BtnCerrarSesion.Click += (s, e) =>
            {
                Visor3D.Seguridad.EstadoGlobal.Actualizar();
                CerrarSesionYVolverOficina();
            };

            // P2P Interacción
            BtnCopiarUrlP2P.Click += OnCopiarUrlP2PClicked;
            TxtUrlP2P.MouseLeftButtonDown += (s, e) => OnCopiarUrlP2PClicked(s, e);
            BtnConectarAmigo.Click += OnConectarAmigoClicked;
            BtnBuscarRed.Click += OnBuscarRedClicked;
            BtnUsarNodoRed.Click += OnUsarNodoRedClicked;

            // WebNodo — portal de descarga y conexión para amigos en el metaverso
            BtnWebNodo.Click += OnWebNodoClicked;

            // Chat del metaverso (texto, voz y webcam)
            BtnChat.Click += (s, e) => AlternarPanelChat();
            BtnChatCerrar.Click += (s, e) => CerrarPanelChat();
            BtnChatEnviar.Click += (s, e) => EnviarMensajeChat();
            BtnChatMic.Click += (s, e) => AlternarMicrofonoChat();
            BtnChatCam.Click += (s, e) => AlternarCamaraChat();
            TxtChat.KeyDown += OnChatTextBoxKeyDown;
            BtnAmigos.Click += (s, e) => AlternarPanelAmigos();
            BtnCopiarCodigo.Click += (s, e) => CopiarCodigoAmigo();
            BtnAgregarAmigo.Click += (s, e) => EnviarSolicitudPorCodigo();
            TxtCodigoAmigo.KeyDown += OnCodigoAmigoKeyDown;

            // Si Godot se cae, se puede reincrustar sin cerrar el visor
            BtnReiniciar3D.Click += (s, e) => ReiniciarMotor3D();

            // Copiar diagnóstico al portapapeles
            BtnDiagnostico.Click += OnDiagnosticoClicked;
        }

        /// <summary>Arranca los temporizadores de migración, túneles y vigilancia de LucIA.</summary>
        private void IniciarVigilantes()
        {
            // Vigilar si Godot migró el anfitrión (otro peer asumió la sesión)
            _timerMigracion = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(4)
            };
            _timerMigracion.Tick += (s, e) => ComprobarHostMigrado();
            _timerMigracion.Start();

            // Vigilar solicitudes de validación de túnel SSH desde LucIA (Godot)
            _timerValidacionTunel = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)  // Poll cada segundo
            };
            _timerValidacionTunel.Tick += (s, e) => PollSolicitudValidacionTunel();
            _timerValidacionTunel.Start();

            // Vigilar el cerebro de LucIA: si se cae (o lo cierra el .bat),
            // el visor lo vuelve a levantar solo.
            _timerLucia = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(6)
            };
            _timerLucia.Tick += async (s, e) =>
            {
                if (_iniciandoLucia || _servidorLucia.EstaListo()) return;
                _iniciandoLucia = true;
                try { await IniciarServidorLuciaAsync(); }
                finally { _iniciandoLucia = false; }
            };
            _timerLucia.Start();
        }

        /// <summary>Directorio <c>user://</c> de Godot: el del perfil si existe, o el estándar de Godot.</summary>
        private static string ResolverRutaDatosGodot()
            => Directory.Exists(PerfilUsuario.GodotUserDataDir)
                ? PerfilUsuario.GodotUserDataDir
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Godot", "app_userdata", "WoldVirtualP2P3D");

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Lo primero: quitar la pantalla antigua antes de cualquier await.
            MostrarPanelMundo3D();

            await IniciarWebNodoAsync();
            ActualizarEstadoWebNodo();

            ActualizarEstadoCuentaUI();
            _ = IniciarServidorBlockchainAsync();
            _ = IniciarServidorLuciaAsync();

            // Arranque directo en el despacho: no mostrar la antigua ventana
            // de bienvenida ni siquiera durante un frame.
            MostrarPanelMundo3D();

            // Si veníamos de un enlace de invitación, se restaura el anfitrión raíz.
            if (!string.IsNullOrEmpty(_hostInicial)) _hostSesionUrl = _hostInicial;
            if (!string.IsNullOrWhiteSpace(_urlAmigoPendiente))
            {
                TxtUrlAmigo.Text = _urlAmigoPendiente;
                TxtEstadoP2P.Text = $"🔗 Destino amigo fijado: {_urlAmigoPendiente}";
            }

            // Iniciar búsqueda de actualizaciones en segundo plano
            _ = Task.Run(CheckUpdatesAsync);

            // La ventana ya está en pie: el recolector puede purgar los runtimes
            // anteriores en el próximo arranque.
            RuntimeCleaner.MarcarArranqueCorrecto();

            // La nueva experiencia empieza siempre en la oficina de LucIA:
            // recepción, registro, conexión de túnel y avatares viven allí.
            _ = Dispatcher.BeginInvoke(new Action(AbrirOficinaLucia));
        }

        /// <summary>Muestra el panel del mundo 3D y oculta la bienvenida antigua.</summary>
        private void MostrarPanelMundo3D()
        {
            PanelBienvenida.Visibility = Visibility.Collapsed;
            BarraInferiorLogin.Visibility = Visibility.Collapsed;
            PanelMundo3D.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Arranca WebNodo: primero en un hilo MTA (HttpListener no funciona en
        /// el hilo STA de WPF); si falla, recurre al proceso externo.
        /// </summary>
        private async Task IniciarWebNodoAsync()
        {
            // HttpListener debe crearse en un hilo MTA; en el hilo STA de WPF
            // devuelve "Controlador no válido" y WebNodo no abre.
            if (!App.WebNodo.IsRunning)
                await Task.Run(() => App.WebNodo.Start());
            if (!App.WebNodo.IsRunning)
                await IniciarWebNodoExternoAsync();
        }

        /// <summary>Refleja en la barra inferior el estado real de WebNodo.</summary>
        private void ActualizarEstadoWebNodo()
        {
            bool activo = App.WebNodo.IsRunning;
            BtnWebNodo.Content = activo ? "🌐 WebNodo" : "🌐 WebNodo (Error)";
            BtnWebNodo.ToolTip = activo
                ? $"WebNodo local: http://localhost:{App.WebNodo.ActivePort}/"
                : $"WebNodo no disponible: {App.WebNodo.StartError ?? "sin detalle"}";
            VisorLog.Info(activo
                ? $"[WebNodo] Portal listo en http://localhost:{App.WebNodo.ActivePort}/"
                : $"[WebNodo] No se pudo abrir: {App.WebNodo.StartError ?? "sin detalle"}");
        }

        /// <summary>
        /// Subrutina de arranque: en la versión oficial, la anfitriona Lucía 3D
        /// gestiona la bienvenida y el registro inmutable en el metaverso.
        /// </summary>
        private void AbrirRegistroInicial()
        {
            if (_registroAutoLanzado || PerfilUsuario.TieneIdentidad) return;
            _registroAutoLanzado = true;

            Log("[cuenta] Equipo sin cuenta previa: la anfitriona Lucía 3D asume la bienvenida y registro en el metaverso.");
            ActualizarEstadoCuentaUI();
        }

        private void Log(string message)
        {
            VisorLog.Info(message);
            try
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WoldVirtualP2P3D");
                Directory.CreateDirectory(logDir);

                File.AppendAllText(
                    Path.Combine(logDir, "update_debug.log"),
                    $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
                Console.WriteLine($"[Updater] {message}");
            }
            catch { }
        }
    }
}
