using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Visor3D.Chat;

namespace Visor3D
{
    /// <summary>
    /// Chat del metaverso (texto + voz + webcam) y presencia/amigos.
    ///
    /// Topología pública: el anfitrión raíz retransmite y su relay viaja por la
    /// MISMA URL pública del WebNodo (ruta <c>/chat</c>), de modo que cualquier
    /// usuario de Internet entra sin abrir ni depender de un túnel adicional.
    /// No se admiten rutas locales para los invitados (ni 127.0.0.1 ni
    /// host:8920): mientras el anfitrión no publique su URL, el cliente
    /// reintenta cada 5 s. El loopback queda reservado al propio anfitrión como
    /// puente mientras su túnel público termina de publicarse; en cuanto la URL
    /// aparece, también el anfitrión reconecta al endpoint público.
    /// </summary>
    public partial class MainWindow : Window
    {
        // ═══════════════════════════════════════════════════════════
        // Ciclo de vida del chat
        // ═══════════════════════════════════════════════════════════

        private void IniciarChatSesion(string usuario, string uid)
        {
            DetenerChatSesion();

            _chatUsuario = ChatProtocol.CleanName(usuario);
            _chatUid = ChatProtocol.CleanUid(uid);
            if (_chatUid.Length == 0) _chatUid = ChatProtocol.BuildFallbackUid(_chatUsuario);
            try
            {
                _amigos = FriendsStore.Load(_chatUid);
            }
            catch (Exception ex)
            {
                _amigos = null;
                Log($"[Chat] No se pudo cargar la lista de amigos: {ex.Message}");
            }
            _chatActivo = true;
            _chatProximoIntento = DateTime.MinValue;

            LimpiarPresencia();
            TxtMiCodigo.Text = ChatProtocol.BuildFriendCode(_chatUid);
            TxtEstadoAmigos.Text = "";
            RefrescarAmigos();

            _chatServer = IniciarRelayChat();
            App.WebNodo.ChatEnabled = _chatServer?.IsRunning == true;
            App.WebNodo.ChatLocalPort = _chatServer?.Port ?? 0;

            IniciarVozChat();
            IniciarTemporizadorChat();

            ActualizarEstadoChat();
            _ = ConectarChatAsync();
        }

        /// <summary>Abre el relay del anfitrión; el túnel WebNodo lo publica en /chat.</summary>
        private ChatRelayServer? IniciarRelayChat()
        {
            try
            {
                var relay = new ChatRelayServer();
                relay.Log += mensaje => Log($"[Chat] {mensaje}");
                if (relay.Start(8920, 10))
                    Log($"[Chat] Relay del anfitrión escuchando en el puerto {relay.Port} (ruta pública /chat).");
                else
                    Log("[Chat] No se pudo abrir el relay; este visor solo podrá unirse como cliente.");
                return relay;
            }
            catch (Exception ex)
            {
                Log($"[Chat] Relay no disponible: {ex.Message}");
                return null;
            }
        }

        private void IniciarVozChat()
        {
            _chatVoice = new ChatVoice();
            _chatVoice.MicFrame += OnMicrofonoChat;
            if (!_chatVoice.StartPlayback())
                Log($"[Chat] {_chatVoice.LastError}");
        }

        private void IniciarTemporizadorChat()
        {
            _timerChat = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _timerChat.Tick += (s, e) => MantenerChatSesion();
            _timerChat.Start();
        }

        private void DetenerChatSesion()
        {
            _chatActivo = false;

            CerrarVentanasPrivadas();
            LimpiarPresencia();
            try { _amigos?.Guardar(); } catch { }
            _amigos = null;
            _chatUid = "";

            _timerChat?.Stop();
            _timerChat = null;

            var cliente = _chatClient;
            _chatClient = null;
            try { cliente?.Dispose(); } catch { }

            try { _chatWebcam?.Dispose(); } catch { }
            _chatWebcam = null;

            try { _chatVoice?.Dispose(); } catch { }
            _chatVoice = null;

            try { _chatServer?.Dispose(); } catch { }
            _chatServer = null;

            App.WebNodo.ChatEnabled = false;
            App.WebNodo.ChatLocalPort = 0;

            RestablecerUiChat();
        }

        private void CerrarVentanasPrivadas()
        {
            foreach (var ventana in _ventanasPrivadas.Values.ToArray())
            {
                try { ventana.CerrarPorSesion(); } catch { }
            }
            _ventanasPrivadas.Clear();
            _privadosNoLeidos.Clear();
            _historialPrivado.Clear();
        }

        private void RestablecerUiChat()
        {
            _nombresChat.Clear();
            _tilesChat.Clear();
            _ultimoVideoChat.Clear();
            LimpiarTilesVideo(PanelVideos, MarcoCamLocal);
            ListaChat.Items.Clear();
            ImgCamLocal.Source = null;
            MarcoCamLocal.Visibility = Visibility.Collapsed;
            ActualizarFranjaVideos();
            BarraMic.Value = 0;

            _chatConectando = false;
            _chatNoLeidos = 0;
            _chatAbierto = false;
            PanelChat.Visibility = Visibility.Collapsed;
            ActualizarBotonChat();

            PanelAmigos.Visibility = Visibility.Collapsed;
            ListaAmigos.Children.Clear();
            ListaSolicitudes.Children.Clear();
            ListaEnSesion.Children.Clear();
            LblSolicitudes.Visibility = Visibility.Collapsed;
            LblEnSesion.Visibility = Visibility.Collapsed;
            TxtMiCodigo.Text = "—";
            TxtEstadoAmigos.Text = "";
            TxtCodigoAmigo.Clear();
            BtnAmigos.Content = "👥 Amigos";

            BtnChatMic.Content = "🎙 Voz";
            MarcarBotonChat(BtnChatMic, false);
            BtnChatCam.Content = "📷 Cámara";
            MarcarBotonChat(BtnChatCam, false);
            TxtChatEstado.Text = "Chat desconectado";
        }

        private void LimpiarPresencia()
        {
            _uidPorId.Clear();
            _uidsEnLinea.Clear();
            _reenviosAmistad.Clear();
        }

        // ═══════════════════════════════════════════════════════════
        // Endpoint público (ruta /chat del túnel WebNodo)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Endpoint WebSocket del chat, siempre a través del portal público del
        /// anfitrión. Devuelve null si el anfitrión aún no tiene URL pública: el
        /// llamador reintenta; nunca se cae a una ruta local para invitados.
        /// </summary>
        private string? ResolverEndpointChat()
        {
            string? publica = UrlPublicaAnfitrion();
            if (!string.IsNullOrWhiteSpace(publica))
            {
                var uri = new Uri(publica);
                string esquema = uri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
                string token = TokenSesionP2P;
                string consulta = string.IsNullOrWhiteSpace(token)
                    ? ""
                    : $"?invitacion={Uri.EscapeDataString(token)}";
                return $"{esquema}://{uri.Authority}/chat{consulta}";
            }

            // Único puente local permitido: el anfitrión hablando con su propio
            // relay mientras el túnel público se está publicando. Los invitados
            // jamás usan esta ruta.
            if (EsAnfitrionChat() && _chatServer?.IsRunning == true)
                return $"ws://127.0.0.1:{_chatServer.Port}/chat";

            return null;
        }

        /// <summary>
        /// URL pública del anfitrión. El anfitrión prefiere la URL viva del túnel
        /// WebNodo; el invitado usa la URL de sesión solo si es http/https (una
        /// IP LAN suelta no sirve: no hay ruta pública sin túnel).
        /// </summary>
        private string? UrlPublicaAnfitrion()
        {
            if (EsAnfitrionChat())
            {
                if (!string.IsNullOrWhiteSpace(App.WebNodo.PublicTunnelUrl))
                    return App.WebNodo.PublicTunnelUrl;
                if (EsUrlPublica(_urlPublica))
                    return _urlPublica;
            }

            string anfitrion = !string.IsNullOrWhiteSpace(_hostSesionUrl)
                ? _hostSesionUrl
                : _urlAmigoPendiente;
            return EsUrlPublica(anfitrion) ? anfitrion.Trim() : null;
        }

        private static bool EsUrlPublica(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        /// <summary>
        /// True si este visor es el anfitrión raíz de la sesión: quien no recibió
        /// enlace de invitación, o quien Godot marcó como nuevo anfitrión tras una
        /// migración (su portal coincide con el último session_host.txt).
        /// </summary>
        private bool EsAnfitrionChat()
        {
            if (string.IsNullOrWhiteSpace(_urlAmigoPendiente)) return true;
            if (!string.IsNullOrWhiteSpace(_urlPublica) &&
                _hostSesionUrl.Equals(_urlPublica, StringComparison.OrdinalIgnoreCase))
                return true;

            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Godot", "app_userdata", "WoldVirtualP2P3D", "session_host.txt");
                if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < _arranqueUtc) return false;

                string actual = File.ReadAllText(path).Trim();
                return !string.IsNullOrWhiteSpace(_urlPublica) &&
                       actual.Equals(_urlPublica, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════
        // Conexión
        // ═══════════════════════════════════════════════════════════

        private async Task ConectarChatAsync()
        {
            if (!_chatActivo || _chatConectando) return;

            string? endpoint = ResolverEndpointChat();
            if (endpoint == null)
            {
                _chatProximoIntento = DateTime.UtcNow.AddSeconds(5);
                TxtChatEstado.Text = "🟠 Esperando el enlace público del anfitrión…";
                return;
            }

            var actual = _chatClient;
            if (actual != null && actual.IsConnected &&
                string.Equals(actual.EndPoint, endpoint, StringComparison.OrdinalIgnoreCase))
                return;

            _chatConectando = true;
            try
            {
                if (actual != null)
                {
                    _chatClient = null;
                    try { actual.Dispose(); } catch { }
                }

                // La presencia se limpia antes de crear el cliente: el saludo
                // `welcome` puede encolar `PeerJoined` antes que `Connected`, y
                // limpiar después borraría a los pares ya conectados.
                LimpiarPresencia();
                ActualizarEstadoVentanasPrivadas();
                RefrescarAmigos();

                Log($"[Chat] Conectando a {endpoint}…");
                var cliente = new ChatClient(_chatUsuario, _chatUid);
                SuscribirEventosChat(cliente);
                _chatClient = cliente;

                bool ok = await cliente.ConnectAsync(endpoint);
                if (!ok)
                {
                    if (ReferenceEquals(_chatClient, cliente)) _chatClient = null;
                    try { cliente.Dispose(); } catch { }
                    _chatProximoIntento = DateTime.UtcNow.AddSeconds(5);
                    TxtChatEstado.Text = "🔴 Sin conexión con el anfitrión del chat. Reintentando…";
                    return;
                }

                _chatProximoIntento = DateTime.MinValue;
                await cliente.SendStateAsync(_chatVoice?.IsMicOn == true, _chatWebcam?.IsRunning == true);
            }
            catch (Exception ex)
            {
                Log($"[Chat] Error al conectar: {ex.Message}");
                _chatProximoIntento = DateTime.UtcNow.AddSeconds(5);
            }
            finally
            {
                _chatConectando = false;
            }
        }

        private void SuscribirEventosChat(ChatClient cliente)
        {
            SuscribirEventosConexion(cliente);
            SuscribirEventosPresencia(cliente);
            SuscribirEventosAmistad(cliente);
            SuscribirEventosPrivados(cliente);
        }

        private void SuscribirEventosConexion(ChatClient cliente)
        {
            cliente.Connected += uri => Dispatcher.BeginInvoke(new Action(() =>
            {
                AgregarMensajeSistemaChat("Conectado al chat del metaverso.");
                ActualizarEstadoChat();
            }));

            cliente.Disconnected += motivo => Dispatcher.BeginInvoke(new Action(() =>
            {
                _chatVoice?.Reset();
                LimpiarPresencia();
                ActualizarEstadoVentanasPrivadas();
                RefrescarAmigos();
                AgregarMensajeSistemaChat($"Chat desconectado ({motivo}). Reintentando…");
                ActualizarEstadoChat();
            }));

            cliente.TextReceived += (id, nombre, texto) => Dispatcher.BeginInvoke(new Action(() =>
            {
                _nombresChat[id] = nombre;
                bool propio = id == (_chatClient?.MyId ?? -1);
                AgregarMensajeChat(
                    propio ? "Tú" : ChatProtocol.CleanName(nombre),
                    texto,
                    propio ? Color.FromRgb(45, 212, 191) : Color.FromRgb(0, 217, 255));

                if (!_chatAbierto && !propio)
                {
                    _chatNoLeidos++;
                    ActualizarBotonChat();
                }
            }));

            cliente.SystemReceived += texto => Dispatcher.BeginInvoke(new Action(() =>
            {
                AgregarMensajeSistemaChat(texto);
            }));

            cliente.ErrorReceived += texto => Dispatcher.BeginInvoke(new Action(() =>
            {
                AgregarMensajeSistemaChat("⚠ " + texto);
            }));
        }

        private void SuscribirEventosPresencia(ChatClient cliente)
        {
            cliente.PeerJoined += (id, nombre, uid) => Dispatcher.BeginInvoke(new Action(() =>
            {
                AgregarPresencia(id, nombre, uid);
                ActualizarNombreTile(id);
                ActualizarEstadoChat();
            }));

            cliente.PeerLeft += (id, uid) => Dispatcher.BeginInvoke(new Action(() =>
            {
                QuitarPresencia(id);
                QuitarTileVideo(id);
                _chatVoice?.RemoveRemote(id);
                ActualizarEstadoChat();
            }));

            cliente.PeerState += (id, nombre, audio, video) => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!string.IsNullOrWhiteSpace(nombre)) _nombresChat[id] = nombre;
                ActualizarNombreTile(id);
                if (!video) OcultarTileVideo(id);
                ActualizarEstadoChat();
            }));

            cliente.MediaReceived += (tipo, datos, emisor) =>
            {
                if (tipo == ChatProtocol.KindAudio)
                {
                    _chatVoice?.PlayRemote(emisor, datos);
                    return;
                }

                if (tipo == ChatProtocol.KindVideo)
                {
                    BitmapImage? imagen = DecodificarJpeg(datos);
                    if (imagen == null) return;
                    Dispatcher.BeginInvoke(new Action(() => MostrarTileVideo(emisor, imagen)));
                }
            };
        }

        private void SuscribirEventosAmistad(ChatClient cliente)
        {
            cliente.FriendRequestReceived += (uid, nombre, nota) => Dispatcher.BeginInvoke(new Action(() =>
            {
                string id = ChatProtocol.CleanUid(uid);
                if (id.Length == 0 || _amigos == null) return;

                string limpio = ChatProtocol.CleanName(nombre);
                if (_amigos.EsAmigo(id))
                {
                    // La otra persona puede tener una lista antigua: confirmamos.
                    _ = cliente.SendFriendResponseAsync(id, true);
                    return;
                }

                _amigos.AgregarEntrante(id, limpio);
                _amigos.Guardar();

                string aviso = string.IsNullOrWhiteSpace(nota)
                    ? $"{limpio} te ha enviado una solicitud de amistad."
                    : $"{limpio} te ha enviado una solicitud de amistad: «{nota}»";
                AgregarMensajeSistemaChat(aviso);
                TxtEstadoAmigos.Text = $"Solicitud de {limpio}.";
                if (_chatAbierto) PanelAmigos.Visibility = Visibility.Visible;
                else
                {
                    _chatNoLeidos++;
                    ActualizarBotonChat();
                }
                RefrescarAmigos();
            }));

            cliente.FriendRequestSent += (uid, nombre, destino) => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_amigos == null) return;
                _amigos.ResolverSaliente(destino, uid, nombre);
                _amigos.Guardar();
                TxtEstadoAmigos.Text = string.IsNullOrWhiteSpace(nombre)
                    ? "Solicitud enviada. Esperando respuesta…"
                    : $"Solicitud enviada a {nombre}. Esperando respuesta…";
                RefrescarAmigos();
            }));

            cliente.FriendAccepted += (uid, nombre) => Dispatcher.BeginInvoke(new Action(() =>
            {
                string id = ChatProtocol.CleanUid(uid);
                if (id.Length == 0 || _amigos == null) return;
                if (!TieneSolicitudEnviadaA(id)) return;

                string limpio = ChatProtocol.CleanName(nombre);
                _amigos.QuitarSaliente(id);
                _amigos.QuitarSaliente(ChatProtocol.BuildFriendCode(id));
                _amigos.QuitarEntrante(id);
                _amigos.AgregarAmigo(id, limpio);
                _amigos.Guardar();

                AgregarMensajeSistemaChat($"{limpio} ha aceptado tu solicitud de amistad.");
                TxtEstadoAmigos.Text = $"{limpio} ya es tu amigo.";
                ActualizarEstadoVentanasPrivadas();
                RefrescarAmigos();
            }));

            cliente.FriendDeclined += (uid, nombre) => Dispatcher.BeginInvoke(new Action(() =>
            {
                string id = ChatProtocol.CleanUid(uid);
                if (id.Length == 0 || _amigos == null) return;

                string limpio = ChatProtocol.CleanName(nombre);
                _amigos.QuitarSaliente(id);
                _amigos.QuitarSaliente(ChatProtocol.BuildFriendCode(id));
                _amigos.Guardar();

                AgregarMensajeSistemaChat($"{limpio} ha rechazado tu solicitud de amistad.");
                TxtEstadoAmigos.Text = $"{limpio} ha rechazado la solicitud.";
                RefrescarAmigos();
            }));

            cliente.FriendRequestFailed += (destino, motivo) => Dispatcher.BeginInvoke(new Action(() =>
            {
                string razon = string.IsNullOrWhiteSpace(motivo)
                    ? "No se pudo enviar la solicitud."
                    : motivo;
                TxtEstadoAmigos.Text = "⚠ " + razon;
                AgregarMensajeSistemaChat("⚠ " + razon);
                RefrescarAmigos();
            }));
        }

        private void SuscribirEventosPrivados(ChatClient cliente)
        {
            cliente.DirectMessageReceived += (uid, nombre, texto, ts) => Dispatcher.BeginInvoke(new Action(() =>
            {
                string id = ChatProtocol.CleanUid(uid);
                if (id.Length == 0) return;

                string limpio = ChatProtocol.CleanName(nombre);
                if (_amigos == null || !_amigos.EsAmigo(id))
                {
                    AgregarMensajeSistemaChat(
                        $"{limpio} intentó enviarte un mensaje privado. Añádelo como amigo para responder.");
                    if (!_chatAbierto)
                    {
                        _chatNoLeidos++;
                        ActualizarBotonChat();
                    }
                    return;
                }

                if (_amigos.ActualizarNombreAmigo(id, limpio)) _amigos.Guardar();
                RegistrarHistorialPrivado(id, limpio, texto, propio: false);

                if (_ventanasPrivadas.TryGetValue(id, out var ventana))
                {
                    ventana.AgregarMensaje(limpio, texto, false);
                    if (ventana.IsActive)
                    {
                        ventana.Activate();
                    }
                    else
                    {
                        _privadosNoLeidos[id] = (_privadosNoLeidos.TryGetValue(id, out int n) ? n : 0) + 1;
                        RefrescarAmigos();
                    }
                }
                else
                {
                    _privadosNoLeidos[id] = (_privadosNoLeidos.TryGetValue(id, out int n) ? n : 0) + 1;
                    AgregarMensajeSistemaChat($"Nuevo mensaje privado de {limpio}.");
                    if (!_chatAbierto)
                    {
                        _chatNoLeidos++;
                        ActualizarBotonChat();
                    }
                    RefrescarAmigos();
                }
            }));

            cliente.DirectMessageFailed += (uid, motivo) => Dispatcher.BeginInvoke(new Action(() =>
            {
                string razon = string.IsNullOrWhiteSpace(motivo)
                    ? "No se pudo entregar el mensaje privado."
                    : motivo;
                string id = ChatProtocol.CleanUid(uid);
                if (_ventanasPrivadas.TryGetValue(id, out var ventana))
                    ventana.AgregarSistema("No se pudo entregar: " + razon);
                else
                    AgregarMensajeSistemaChat("⚠ " + razon);
            }));
        }

        /// <summary>
        /// Reconexión y caducidad de vídeos; se ejecuta cada 2 s en el hilo de UI.
        /// Si el endpoint esperado cambia (el túnel publica su URL), reconecta.
        /// </summary>
        private void MantenerChatSesion()
        {
            if (!_chatActivo) return;

            foreach (var par in _ultimoVideoChat.ToArray())
            {
                if ((DateTime.UtcNow - par.Value).TotalSeconds < 6) continue;
                if (_tilesChat.TryGetValue(par.Key, out var tile))
                    tile.Marco.Visibility = Visibility.Collapsed;
                _ultimoVideoChat.Remove(par.Key);
            }

            if (_chatVoice?.IsMicOn == true)
                BarraMic.Value = Math.Min(100, _chatVoice.Level * 140);
            else if (BarraMic.Value > 0)
                BarraMic.Value = 0;

            ActualizarEstadoChat();

            if (_chatConectando || DateTime.UtcNow < _chatProximoIntento) return;

            string? esperado = ResolverEndpointChat();
            if (esperado == null)
            {
                _ = ConectarChatAsync();
                return;
            }

            if (_chatClient?.IsConnected == true &&
                string.Equals(_chatClient.EndPoint, esperado, StringComparison.OrdinalIgnoreCase))
                return;

            _ = ConectarChatAsync();
        }

        // ═══════════════════════════════════════════════════════════
        // Panel de chat
        // ═══════════════════════════════════════════════════════════

        private void ActualizarEstadoChat()
        {
            if (!_chatActivo)
            {
                TxtChatEstado.Text = "Chat desconectado";
                return;
            }

            var texto = new StringBuilder();
            if (_chatClient?.IsConnected == true)
                texto.Append($"🟢 Conectado como {_chatUsuario} (id {_chatClient.MyId})");
            else
                texto.Append("🔴 Sin conexión con el anfitrión del chat");

            if (_chatServer?.IsRunning == true)
                texto.Append($" · relay :{_chatServer.Port} · {_chatServer.ClientCount} conectados");
            if (_chatVoice?.IsMicOn == true)
                texto.Append(" · 🎙 micrófono activo");
            if (_chatWebcam?.IsRunning == true)
                texto.Append(" · 📷 cámara activa");

            TxtChatEstado.Text = texto.ToString();
        }

        private void AlternarPanelChat()
        {
            if (_chatAbierto) CerrarPanelChat();
            else AbrirPanelChat();
        }

        private void AbrirPanelChat()
        {
            if (_chatAbierto) return;

            _chatAbierto = true;
            PanelChat.Visibility = Visibility.Visible;
            _chatNoLeidos = 0;
            ActualizarBotonChat();
            ActualizarEstadoChat();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                TxtChat.Focus();
                TxtChat.CaretIndex = TxtChat.Text.Length;
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void CerrarPanelChat()
        {
            if (!_chatAbierto) return;

            _chatAbierto = false;
            PanelChat.Visibility = Visibility.Collapsed;
            ActualizarBotonChat();
            _godotHostMain?.ActivateGodotFocus();
        }

        private void ActualizarBotonChat()
        {
            BtnChat.Content = _chatNoLeidos > 0 ? $"💬 Chat ({_chatNoLeidos})" : "💬 Chat";
        }

        // ═══════════════════════════════════════════════════════════
        // Amigos: presencia, solicitudes y chat privado
        // ═══════════════════════════════════════════════════════════

        private void AgregarPresencia(int id, string nombre, string uid)
        {
            _nombresChat[id] = nombre;
            string limpio = ChatProtocol.CleanUid(uid);
            if (limpio.Length > 0) _uidPorId[id] = limpio;
            RecalcularPresencia();
        }

        private void QuitarPresencia(int id)
        {
            _nombresChat.Remove(id);
            _uidPorId.Remove(id);
            RecalcularPresencia();
        }

        private void RecalcularPresencia()
        {
            var antes = new HashSet<string>(_uidsEnLinea);
            _uidsEnLinea.Clear();
            foreach (var uid in _uidPorId.Values)
            {
                if (uid != _chatUid) _uidsEnLinea.Add(uid);
            }

            ActualizarEstadoVentanasPrivadas();
            if (!antes.SetEquals(_uidsEnLinea)) RefrescarAmigos();
            else ActualizarBotonAmigos();

            ReenviarSolicitudesPendientes();
        }
    }
}
