using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Visor3D.Services;

namespace Visor3D
{
    /// <summary>
    /// Parte de <see cref="MainWindow"/> con la búsqueda de nodos en la red
    /// local y el arranque del motor 3D.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>Cerrar sesión limpia el chat y cierra el visor sin volver a la ventana antigua.</summary>
        private void CerrarSesionYVolverOficina()
        {
            try { DetenerChatSesion(); } catch { }
            _chatActivo = false;
            // Cerrar sesión cierra el visor; el cierre ordenado detiene Godot,
            // blockchain, LucIA y el portal auxiliar.
            Close();
        }

        private void OnBuscarRedClicked(object sender, RoutedEventArgs e)
        {
            // El motor puede seguir cerrando y liberar el 8912 tarde: se reintenta.
            _ = ActualizarEstadoRedAsync();
        }

        /// <summary>Si el nodo lo detectó la baliza LAN, deja el destino listo para entrar.</summary>
        private void OnUsarNodoRedClicked(object sender, RoutedEventArgs e)
        {
            if (_nodosRedLan.Count == 0) return;
            if (_indiceNodoSeleccionado >= _nodosRedLan.Count) _indiceNodoSeleccionado = 0;

            string ip = _nodosRedLan[_indiceNodoSeleccionado];
            int puerto = _puertosRedLan.TryGetValue(ip, out int p) ? p : DescubrimientoP2P.PuertoGodot;

            ActivarModoLogin();
            TxtUrlAmigo.Text = $"{ip}:{puerto}";
            TxtEstadoRed.Text = _nodosRedLan.Count > 1
                ? $"🔗 Nodo seleccionado ({_indiceNodoSeleccionado + 1}/{_nodosRedLan.Count}): {ip}:{puerto}"
                : $"🔗 Nodo seleccionado: {ip}:{puerto}";
            TxtDetalleRed.Text = "Pulsa «Conectar» para comprobarlo y después inicia sesión.";

            if (_nodosRedLan.Count > 1)
                _indiceNodoSeleccionado = (_indiceNodoSeleccionado + 1) % _nodosRedLan.Count;
        }

        /// <summary>
        /// Refleja en la pantalla previa si el metaverso está vacío u ocupado.
        /// Prioriza la baliza LAN (anfitrión con el mismo puerto UDP que usa Godot)
        /// y, si no hay nadie en la red local, consulta el portal del enlace de
        /// invitación. No abre el WebSocket del juego.
        ///
        /// Se incluye la propia máquina (<c>incluirPropio: true</c>): si un nodo
        /// quedó activo en ESTE equipo (sesión anterior en curso, otra
        /// instalación/versión del visor), su baliza llega con la IP local y, sin
        /// este ajuste, se descartaba y la pantalla mostraba «Metaverso vacío».
        /// </summary>
        private async Task ActualizarEstadoRedAsync()
        {
            if (_consultaRedEnCurso) return;
            _consultaRedEnCurso = true;
            try
            {
                EstadoBusquedaRed(
                    "Buscando nodos en tu red…",
                    "Escuchando la baliza del metaverso (UDP 8912).");
                BtnBuscarRed.IsEnabled = false;
                BtnUsarNodoRed.Visibility = Visibility.Collapsed;

                var lan = await DescubrimientoP2P.BuscarEnLanAsync(incluirPropio: true);
                _nodosRedLan = lan.Nodos;
                _puertosRedLan = lan.Puertos;
                _indiceNodoSeleccionado = 0;

                string enlace = !string.IsNullOrWhiteSpace(_hostInicial) ? _hostInicial : _urlAmigoPendiente;
                bool enlaceResponde = _nodosRedLan.Count == 0 && !string.IsNullOrWhiteSpace(enlace)
                    && await DescubrimientoP2P.ConsultarNodoAsync(enlace);

                if (_nodosRedLan.Count > 0)
                    MostrarRedOcupada();
                else if (enlaceResponde)
                    MostrarEnlaceEnLinea(enlace);
                else
                    MostrarRedVacia(lan.Error);
            }
            catch (Exception ex)
            {
                MostrarRedVacia($"No se pudo completar la búsqueda: {ex.Message}");
            }
            finally
            {
                _consultaRedEnCurso = false;
                BtnBuscarRed.IsEnabled = true;
            }
        }

        private void EstadoBusquedaRed(string estado, string detalle)
        {
            TxtEstadoRed.Text = estado;
            TxtDetalleRed.Text = detalle;
        }

        private void MostrarRedOcupada()
        {
            TxtEstadoRed.Text = _nodosRedLan.Count == 1
                ? "● Metaverso ocupado: 1 nodo en tu red"
                : $"● Metaverso ocupado: {_nodosRedLan.Count} nodos en tu red";
            TxtDetalleRed.Text =
                $"{string.Join(", ", _nodosRedLan)} · Pulsa «Usar este nodo» o pega su enlace al entrar.";
            BtnUsarNodoRed.Visibility = Visibility.Visible;
        }

        private void MostrarEnlaceEnLinea(string enlace)
        {
            TxtEstadoRed.Text = "● El nodo de tu enlace está en línea";
            TxtDetalleRed.Text =
                $"{enlace} responde. Pulsa «Acceder», comprueba el enlace y luego inicia sesión.";
        }

        private void MostrarRedVacia(string? detalle)
        {
            TxtEstadoRed.Text = "○ Metaverso vacío: serás el primero";
            TxtDetalleRed.Text = detalle
                ?? "No se detectaron nodos. Al entrar se creará la sesión y podrás copiar tu enlace para invitar.";
        }

        private void IniciarGodotMain()
        {
            if (_godotHostMain != null) return;
            // Pequeño retraso para asegurar que el panel ya tiene tamaño real
            Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    string godotExe   = GodotConfig.GetGodotExePath();
                    string projectDir = GodotConfig.GetProjectPath();
                    string scenePath  = GodotConfig.GetScenePath();
                    string mainPack   = GodotConfig.GetMainPackPath();

                    // Guardar p2p_session.json para que Godot siempre lo tenga accesible
                    GuardarP2PSessionConfig();

                    // Cuota por ubicación (F9): dejar la firma SMTPLC en user:// antes
                    // de que Godot arranque, para que su recepción la envíe en el alta.
                    await GuardarUbicacionSmtplcAsync();

                    string userArgs = ConstruirArgumentosMotor();

                    _godotHostMain = new GodotHwndHost(godotExe, projectDir, scenePath, userArgs, mainPack);
                    _godotHostMain.GodotExited += OnGodotExited;
                    ContenedorGodotMain.Children.Clear();
                    ContenedorGodotMain.Children.Add(_godotHostMain);
                    BtnReiniciar3D.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"No se pudo incrustar la escena 3D:\n{ex.Message}",
                                    "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Decide el rol del motor: cliente remoto (con destino de sesión) o
        /// anfitrión local, y devuelve los argumentos con los que arrancarlo.
        /// </summary>
        private string ConstruirArgumentosMotor()
        {
            if (string.IsNullOrWhiteSpace(_urlAmigoPendiente))
            {
                string rolLocal = "[Visor3D -> Godot] Rol: SERVIDOR_LOCAL / modo local por defecto (sin destino remoto).";
                LogRol("[Visor3D -> Godot] Sin destino remoto previo. Modo autónomo/local por defecto.");

                // Somos el anfitrión: mostrar la IP LAN + WebSocket 8911 como alternativa local.
                MostrarDireccionAnfitrion();
                LogRol(rolLocal);
                return "";
            }

            // Todos los invitados convergen en el anfitrión raíz. Un enlace
            // reenviado por un invitado es `<portal del invitado>?host=<raíz>`;
            // conectarse al portal del invitado no funciona (su 8911 no
            // escucha) y dejaría al tercer usuario fuera de la sesión.
            string destino = !string.IsNullOrWhiteSpace(_hostSesionUrl)
                ? _hostSesionUrl
                : _urlAmigoPendiente;
            // El túnel exige la invitación de la sesión en el handshake WebSocket.
            string peerUrl = UrlTunelConInvitacion(destino);

            if (TryParseConnectionTarget(destino, out string targetHost, out int targetPort))
            {
                // Inyectar rol explícito de cliente hacia el destino remoto
                string args = $"--client --ip={targetHost} --port={targetPort} --peer-url=\"{peerUrl}\"";
                string rolLog = $"[Visor3D -> Godot] Rol: CLIENTE_REMOTO -> {targetHost}:{targetPort} (peer-url={peerUrl})";
                if (!destino.Equals(_urlAmigoPendiente, StringComparison.OrdinalIgnoreCase))
                    rolLog += $" | raíz de la sesión; el enlace apuntaba a {_urlAmigoPendiente}";

                LogRol($"[Visor3D -> Godot] Parámetros inyectados: {args}");

                if (!EsIpLiteral(targetHost))
                {
                    // Ya no hay ENet: el motor usa WebSocket sobre el túnel (TCP 8911),
                    // así que un nombre de host es un destino válido.
                    LogRol($"[Visor3D -> Godot] Destino por túnel/URL: {targetHost}:{targetPort} (WebSocket TCP 8911).");
                }

                LogRol(rolLog);
                return args;
            }

            string argsUrl = $"--peer-url=\"{peerUrl}\"";
            LogRol($"[Visor3D -> Godot] Parámetros inyectados (URL sin parsear): {argsUrl}");
            LogRol($"[Visor3D -> Godot] Rol: CLIENTE_REMOTO (URL sin parsear: {peerUrl})");
            return argsUrl;
        }

        private static void LogRol(string mensaje)
        {
            Console.WriteLine(mensaje);
            Debug.WriteLine(mensaje);
        }

        /// <summary>El proceso de Godot terminó por su cuenta: avisar y ofrecer reinicio.</summary>
        private void OnGodotExited(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                BtnReiniciar3D.Visibility = Visibility.Visible;
                TxtEstadoP2P.Text = "⚠️ Motor 3D detenido";
                BordeEstadoP2P.Background = new SolidColorBrush(Color.FromRgb(120, 53, 15));
                Log("[Godot] El proceso del motor 3D terminó.");
            });
        }

        /// <summary>Reincrusta Godot sin cerrar el visor (tras un cierre inesperado).</summary>
        private void ReiniciarMotor3D()
        {
            try { _godotHostMain?.StopGodot(); } catch { }
            ContenedorGodotMain.Children.Clear();
            _godotHostMain = null;
            BtnReiniciar3D.Visibility = Visibility.Collapsed;
            TxtEstadoP2P.Text = "🔄 Reiniciando motor 3D…";
            IniciarGodotMain();
        }
    }
}
