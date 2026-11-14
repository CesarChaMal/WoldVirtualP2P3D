using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Visor3D.Chat;
using Visor3D.RedP2P;
using Visor3D.Seguridad;
using Visor3D.Services;
using Visor3D.Update;

namespace Visor3D
{
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Lee la versión local del propio ensamblado (definida en el .csproj).
        /// Se usa <c>AssemblyInformationalVersion</c> porque respeta el formato
        /// del tag de GitHub (p. ej. "0.0.013").
        /// </summary>
        private static string ObtenerVersionLocal()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrWhiteSpace(info))
                {
                    int plus = info.IndexOf('+');       // descarta metadatos "+<sha>"
                    return plus >= 0 ? info.Substring(0, plus) : info;
                }

                var v = asm.GetName().Version;
                if (v != null) return $"{v.Major}.{v.Minor}.{v.Build}";
            }
            catch { }
            return "0.0.0";
        }

        private async Task CheckUpdatesAsync()
        {
            Log("Iniciando CheckUpdatesAsync...");
            Dispatcher.Invoke(MostrarActualizadorBuscando);

            try
            {
                Log($"Versión local: {CurrentVersion}");
                var checker = new UpdateChecker();
                _updateInfo = await checker.CheckForUpdateAsync(CurrentVersion);

                var info = _updateInfo;
                if (info != null)
                {
                    Log($"¡Actualización encontrada!: {info.TagName} ({info.Version})");
                    Dispatcher.Invoke(() => MostrarActualizadorDisponible(info));
                }
                else
                {
                    Log("Sistema al día.");
                    Dispatcher.Invoke(MostrarActualizadorAlDia);
                }
            }
            catch (Exception ex)
            {
                Log($"Error crítico: {ex.Message}");
                Dispatcher.Invoke(() => MostrarActualizadorError("No se pudo comprobar si hay actualizaciones."));
            }
        }

        // ── Estados del botón de actualización ────────────────────────────
        // El botón vive SIEMPRE dentro de la barra de sesión: es zona WPF pura,
        // fuera del HwndHost de Godot, así que se ve y recibe clics siempre.

        private static SolidColorBrush Pincel(byte r, byte g, byte b)
        {
            var br = new SolidColorBrush(Color.FromRgb(r, g, b));
            br.Freeze();
            return br;
        }

        private void PararPulso()
        {
            GlowActualizar.BeginAnimation(DropShadowEffect.OpacityProperty, null);
            GlowActualizar.Opacity = 0;
        }

        private void IniciarPulso()
        {
            var pulso = new DoubleAnimation(0.15, 0.85, TimeSpan.FromSeconds(1.6))
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };
            GlowActualizar.BeginAnimation(DropShadowEffect.OpacityProperty, pulso);
        }

        private void MostrarActualizadorBuscando()
        {
            PararPulso();
            BtnActualizar.IsEnabled = false;
            BtnActualizar.Cursor = System.Windows.Input.Cursors.Wait;
            BtnActualizar.Background = Pincel(0x0F, 0x27, 0x44);
            BtnActualizar.BorderBrush = Pincel(0x1D, 0x3A, 0x5C);
            BtnActualizar.Foreground = Pincel(0x7E, 0xCF, 0xFF);
            IconoDescarga.Visibility = Visibility.Visible;
            IconoAlDia.Visibility = Visibility.Collapsed;
            TxtEtiquetaActualizar.Text = "Buscando…";
            TxtProgresoPorcentaje.Visibility = Visibility.Collapsed;
            BarProgreso.Visibility = Visibility.Collapsed;
            BtnActualizar.ToolTip = "Buscando actualizaciones…";
        }

        private void MostrarActualizadorDisponible(ReleaseInfo info)
        {
            BtnActualizar.IsEnabled = true;
            BtnActualizar.Cursor = System.Windows.Input.Cursors.Hand;
            BtnActualizar.Background = Pincel(0x0B, 0x2A, 0x46);
            BtnActualizar.BorderBrush = Pincel(0x38, 0xBD, 0xF8);
            BtnActualizar.Foreground = Pincel(0xE2, 0xF6, 0xFF);
            IconoDescarga.Visibility = Visibility.Visible;
            IconoAlDia.Visibility = Visibility.Collapsed;
            TxtEtiquetaActualizar.Text = $"Actualizar v{info.Version}";
            TxtProgresoPorcentaje.Visibility = Visibility.Collapsed;
            BarProgreso.Visibility = Visibility.Collapsed;
            BtnActualizar.ToolTip =
                $"Nueva versión {info.Version} disponible.\n" +
                $"Tamaño: {info.AssetSize / 1048576.0:0} MB\n" +
                "Haz clic para descargar e instalar.";
            IniciarPulso();
        }

        private void MostrarActualizadorAlDia()
        {
            PararPulso();
            _updateInfo = null;
            BtnActualizar.IsEnabled = true;
            BtnActualizar.Cursor = System.Windows.Input.Cursors.Hand;
            BtnActualizar.Background = Pincel(0x0F, 0x1F, 0x1A);
            BtnActualizar.BorderBrush = Pincel(0x22, 0xC5, 0x5E);
            BtnActualizar.Foreground = Pincel(0x94, 0xA3, 0xB8);
            IconoDescarga.Visibility = Visibility.Collapsed;
            IconoAlDia.Visibility = Visibility.Visible;
            TxtEtiquetaActualizar.Text = "Al día";
            TxtProgresoPorcentaje.Visibility = Visibility.Collapsed;
            BarProgreso.Visibility = Visibility.Collapsed;
            BtnActualizar.ToolTip = $"Sistema al día (v{CurrentVersion}).\nHaz clic para volver a comprobar.";
        }

        private void MostrarActualizadorDescargando()
        {
            PararPulso();
            BtnActualizar.IsEnabled = true;              // clic = cancelar
            BtnActualizar.Cursor = System.Windows.Input.Cursors.Hand;
            BtnActualizar.Background = Pincel(0x0B, 0x2A, 0x46);
            BtnActualizar.BorderBrush = Pincel(0x38, 0xBD, 0xF8);
            BtnActualizar.Foreground = Pincel(0xE2, 0xF6, 0xFF);
            IconoDescarga.Visibility = Visibility.Visible;
            IconoAlDia.Visibility = Visibility.Collapsed;
            TxtEtiquetaActualizar.Text = "Descargando…";
            TxtProgresoPorcentaje.Text = "0 %";
            TxtProgresoPorcentaje.Visibility = Visibility.Visible;
            FijarProgreso(0);
            BarProgreso.Visibility = Visibility.Visible;
            BtnActualizar.ToolTip = "Descargando la actualización…\nHaz clic para cancelar.";
        }

        private void FijarProgreso(double porcentaje)
        {
            double pct = Math.Clamp(porcentaje, 0, 100);
            ColProgreso.Width = new GridLength(pct, GridUnitType.Star);
            ColResto.Width = new GridLength(Math.Max(0.001, 100 - pct), GridUnitType.Star);
        }

        private void MostrarActualizadorError(string mensaje)
        {
            PararPulso();
            _descargandoActualizacion = false;
            BtnActualizar.IsEnabled = true;
            BtnActualizar.Cursor = System.Windows.Input.Cursors.Hand;
            BtnActualizar.Background = Pincel(0x2A, 0x12, 0x15);
            BtnActualizar.BorderBrush = Pincel(0xF8, 0x71, 0x71);
            BtnActualizar.Foreground = Pincel(0xFC, 0xA5, 0xA5);
            IconoDescarga.Visibility = Visibility.Visible;
            IconoAlDia.Visibility = Visibility.Collapsed;
            TxtEtiquetaActualizar.Text = "Reintentar";
            TxtProgresoPorcentaje.Visibility = Visibility.Collapsed;
            BarProgreso.Visibility = Visibility.Collapsed;
            BtnActualizar.ToolTip = mensaje;
            Log($"[Error updater] {mensaje}");
        }

        private async void OnActualizarClicked(object sender, RoutedEventArgs e)
        {
            // Durante la descarga, el clic cancela (el .part se conserva para reanudar).
            if (_descargandoActualizacion)
            {
                _ctsDescarga?.Cancel();
                return;
            }

            // Sin actualización pendiente, el clic sirve para volver a comprobar.
            if (_updateInfo == null)
            {
                _ = CheckUpdatesAsync();
                return;
            }

            var info = _updateInfo;

            try
            {
                _descargandoActualizacion = true;
                MostrarActualizadorDescargando();

                // Progreso: barra de 2 px en el borde inferior de la barra + % en el pill.
                var progreso = new Progress<DownloadProgress>(p =>
                {
                    FijarProgreso(p.Percentage);
                    TxtProgresoPorcentaje.Text = $"{p.Percentage:0} %";
                });

                _ctsDescarga?.Dispose();
                _ctsDescarga = new CancellationTokenSource();

                var downloader = new UpdateDownloader();
                bool success = await downloader.DownloadAsync(info, progreso, _ctsDescarga.Token);

                if (_ctsDescarga.IsCancellationRequested)
                {
                    _descargandoActualizacion = false;
                    MostrarActualizadorDisponible(info);
                    return;
                }

                if (!success)
                {
                    MostrarActualizadorError(downloader.LastError ??
                        "No se pudo descargar la actualización. Pulsa «Reintentar».");
                    return;
                }

                FijarProgreso(100);
                TxtProgresoPorcentaje.Text = "100 %";
                TxtEtiquetaActualizar.Text = "Reiniciando…";
                BtnActualizar.IsEnabled = false;
                _descargandoActualizacion = false;

                // Delegar el reemplazo del .exe en el script externo y cerrar limpiamente.
                string exeActual = Environment.ProcessPath
                                   ?? Process.GetCurrentProcess().MainModule?.FileName
                                   ?? "";
                bool lanzado = UpdateApplier.ApplyPendingUpdate(exeActual);
                if (!lanzado)
                {
                    MostrarActualizadorError("La actualización se descargó pero no pudo aplicarse automáticamente.");
                    return;
                }

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MostrarActualizadorError($"Error durante la actualización: {ex.Message}");
            }
        }

        public void MostrarPantallaBienvenida()
        {
            DetenerChatSesion();

            _godotHostMain?.StopGodot();
            ContenedorGodotMain.Children.Clear();
            _godotHostMain = null;

            _p2pServicio?.Dispose();
            _p2pServicio = null;
            DetenerTunelWebNodo();
            BannerP2P.Visibility = Visibility.Collapsed;
            TxtUrlP2P.Text = "";
            _urlPublica = "";
            _enlaceCompartible = "";
            _hostSesionUrl = "";

            // Limpiar URL del tunel en el WebNodo al cerrar sesión
            App.WebNodo.PublicTunnelUrl = null;
            BtnWebNodo.Content = "🌐 WebNodo";
            BtnWebNodo.ClearValue(System.Windows.Controls.Button.BackgroundProperty);
            BtnWebNodo.ClearValue(System.Windows.Controls.Button.BorderBrushProperty);
            BtnWebNodo.ToolTip = "Abre el portal de descarga del visor (WebNodo) para compartirlo con tus amigos";

            PanelBienvenida.Visibility = Visibility.Visible;
            PanelMundo3D.Visibility = Visibility.Collapsed;
            BarraInferiorLogin.Visibility = Visibility.Collapsed;

            // En el sistema oficial con Lucía 3D, el botón principal siempre está visible
            // para entrar al metaverso o registrarse con la anfitriona.
            BtnCrearCuenta.Visibility = Visibility.Visible;

            ActualizarEstadoCuentaUI();

            // Antes de registrarse o entrar: ¿hay alguien en la red P2P?
            _ = ActualizarEstadoRedAsync();
        }

        public void ActivarModoLogin()
        {
            _godotHostMain?.StopGodot();
            ContenedorGodotMain.Children.Clear();
            _godotHostMain = null;

            PanelBienvenida.Visibility = Visibility.Collapsed;
            PanelMundo3D.Visibility = Visibility.Collapsed;
            BarraInferiorLogin.Visibility = Visibility.Visible;
        }

        private void OnCrearCuentaClicked(object sender, RoutedEventArgs e)
        {
            EntrarAlMetaverso();
        }

        #region Actualización Oficial: Lucía 3D y Servidor Blockchain

        private async Task IniciarServidorBlockchainAsync()
        {
            try
            {
                TxtEstadoServidorBlockchain.Text = "⚡ Servidor Blockchain Oficial: Comprobando puerto 8545...";
                IndicadorServidorLed.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11));

                bool ok = await _servidorBlockchain.IniciarAsync();
                if (ok)
                {
                    TxtEstadoServidorBlockchain.Text = $"⚡ Servidor Blockchain Oficial: Puerto 8545 ACTIVO ({_servidorBlockchain.TotalBloques} bloques)";
                    IndicadorServidorLed.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    TxtEstadoBlockchainTop.Text = $"⚡ Blockchain: 8545 ({_servidorBlockchain.TotalBloques} blk)";
                    BordeEstadoBlockchainTop.Background = new SolidColorBrush(Color.FromRgb(6, 78, 59));
                }
                else
                {
                    string err = _servidorBlockchain.UltimoError ?? "Desconectado";
                    TxtEstadoServidorBlockchain.Text = $"⚠️ Servidor Blockchain Oficial: {err}";
                    IndicadorServidorLed.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    TxtEstadoBlockchainTop.Text = "⚠️ Blockchain: Offline";
                    BordeEstadoBlockchainTop.Background = new SolidColorBrush(Color.FromRgb(127, 29, 29));
                }
            }
            catch (Exception ex)
            {
                Log($"[Blockchain] Error en IniciarServidorBlockchainAsync: {ex.Message}");
            }
        }

        private async Task IniciarServidorLuciaAsync()
        {
            try
            {
                bool ok = await _servidorLucia.IniciarAsync();
                TxtEstadoLuciaTop.Text = ok ? "🧠 LucIA: 8765" : "🔴 LucIA: parada";
                TxtEstadoLuciaTop.ToolTip = ok
                    ? "Cerebro de LucIA escuchando en 127.0.0.1:8765"
                    : (_servidorLucia.UltimoError ?? "No se pudo arrancar LucIA");
                BordeEstadoLuciaTop.Background = new SolidColorBrush(
                    ok ? Color.FromRgb(59, 7, 100) : Color.FromRgb(127, 29, 29));
                if (!ok)
                    Log($"[LucIA] El cerebro no arrancó: {_servidorLucia.UltimoError ?? "desconocido"}");
            }
            catch (Exception ex)
            {
                Log($"[LucIA] Error al iniciar el cerebro: {ex.Message}");
            }
        }

        private void ActualizarEstadoCuentaUI()
        {
            bool tieneCuentaOficial = PerfilUsuario.TieneCuentaOficial;
            if (tieneCuentaOficial)
            {
                var cuenta = PerfilUsuario.ObtenerCuentaOficial();
                string usuario = !string.IsNullOrWhiteSpace(cuenta.Nick) ? cuenta.Nick : "Oficial";
                string hash = cuenta.CuentaHash ?? "";
                string hashCorto = hash.Length > 12 ? hash.Substring(0, 12) + "…" : hash;

                TxtEstadoCuentaOficial.Text = $"✓ Cuenta Oficial Activa: {usuario}";
                TxtEstadoCuentaOficial.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                TxtDetalleCuentaOficial.Text = $"Bloque: {hashCorto} | Registro inmutable en blockchain local/P2P.";

                BtnCrearCuenta.ToolTip = $"Entrar al metaverso con la cuenta oficial '{usuario}'";
                BtnCrearCuenta.Content = "✦ Entrar al Metaverso con Lucía 3D ✦";
            }
            else if (PerfilUsuario.TieneIdentidad)
            {
                string usuario = PerfilUsuario.NombreGuardado();
                TxtEstadoCuentaOficial.Text = $"✓ Perfil Local: {usuario}";
                TxtEstadoCuentaOficial.Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250));
                TxtDetalleCuentaOficial.Text = "Puedes entrar directamente o sincronizar tu cuenta con Lucía 3D en la blockchain.";
                BtnCrearCuenta.Content = "✦ Entrar al Metaverso con Lucía 3D ✦";
            }
            else
            {
                TxtEstadoCuentaOficial.Text = "✦ Sin Cuenta Registrada en este PC";
                TxtEstadoCuentaOficial.Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253));
                TxtDetalleCuentaOficial.Text = "Lucía 3D te recibirá en el metaverso para crear tu cuenta en un bloque inmutable (sin contraseñas).";
                BtnCrearCuenta.Content = "✦ Registrarse y Entrar con Lucía 3D ✦";
            }
        }

        private async void EntrarAlMetaverso()
        {
            try
            {
                if (!_servidorBlockchain.EstaEnLinea)
                {
                    TxtEstadoP2P.Text = "⚡ Encendiendo servidor blockchain oficial...";
                    await IniciarServidorBlockchainAsync();
                }

                BarraInferiorLogin.Visibility = Visibility.Collapsed;
                PanelBienvenida.Visibility = Visibility.Collapsed;
                PanelMundo3D.Visibility = Visibility.Visible;

                string usuario = "Visitante";
                if (PerfilUsuario.TieneCuentaOficial)
                {
                    var cta = PerfilUsuario.ObtenerCuentaOficial();
                    if (!string.IsNullOrWhiteSpace(cta.Nick))
                    {
                        usuario = cta.Nick;
                        string hashCorto = cta.CuentaHash.Length > 10 ? cta.CuentaHash.Substring(0, 10) : cta.CuentaHash;
                        TxtSesionUsuario.Text = $"Cuenta: {usuario} ({hashCorto})";
                    }
                }
                else if (PerfilUsuario.TieneIdentidad)
                {
                    usuario = PerfilUsuario.NombreGuardado();
                    TxtSesionUsuario.Text = $"Usuario: {usuario}";
                }
                else
                {
                    TxtSesionUsuario.Text = "Anfitriona: Lucía 3D (Recepción)";
                }

                AjustarBarraResponsive();

                IniciarGodotMain();
                IniciarP2P(usuario);
                string uuid = ObtenerUuidDelPerfil(usuario);
                IniciarChatSesion(usuario, uuid);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al entrar al Metaverso:\n{ex.Message}",
                    "Error al Iniciar Metaverso", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AbrirOficinaLucia()
        {
            try
            {
                string ruta = !string.IsNullOrEmpty(_rutaDatosGodot) ? _rutaDatosGodot : PerfilUsuario.GodotUserDataDir;
                if (!Directory.Exists(ruta)) Directory.CreateDirectory(ruta);
                File.WriteAllText(Path.Combine(ruta, "abrir_recepcion.flag"), "1");
                Log("[Lucía 3D] Flag abrir_recepcion.flag activado para recepción de Lucía 3D.");
            }
            catch (Exception ex)
            {
                Log($"[Lucía 3D] Error al escribir flag de recepción: {ex.Message}");
            }

            if (_godotHostMain != null)
            {
                ReiniciarMotor3D();
            }
            else
            {
                EntrarAlMetaverso();
            }
        }

        private void VolverALuciaRecepcion()
        {
            AbrirOficinaLucia();
        }

        /// <summary>
        /// Abre Godot como ventana de construcción. El plugin inferior guarda la
        /// escena y el visor la coloca en la isla del jugador.
        /// </summary>
        private void AbrirConstructorGodot()
        {
            try
            {
                if (_constructorHost != null)
                {
                    PanelConstructorEditor.Visibility = Visibility.Visible;
                    ContenedorGodotMain.Visibility = Visibility.Collapsed;
                    return;
                }

                string godot = GodotConfig.GetGodotExePath();
                string plantilla = AppExtractor.ConstructorTemplateDir;
                if (!Directory.Exists(plantilla))
                    plantilla = Path.Combine(AppContext.BaseDirectory, "ConstructorGodot");
                if (!Directory.Exists(plantilla))
                    plantilla = @"E:\WoldVirtualP2P3D\VersionDe_sesion\CONTRRF\TLLDCD\src\VSC#\Visor3Ddesarrollo\ConstructorGodot";
                if (!File.Exists(godot) || !File.Exists(Path.Combine(plantilla, "project.godot")))
                {
                    MessageBox.Show("No se encontró Godot o el proyecto vacío de construcción.",
                        "Construcción", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string proyecto = RutaIsla3DEscribible();
                if (string.IsNullOrEmpty(proyecto))
                {
                    MessageBox.Show("No se encontró una carpeta escribible para ISLA3D.",
                        "Construcción", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Directory.CreateDirectory(proyecto);
                if (!File.Exists(Path.Combine(proyecto, "project.godot")))
                    CopiarPlantillaConstructor(plantilla, proyecto);
                Directory.CreateDirectory(Path.Combine(proyecto, "ISLA3D"));
                _rutaProyectoIsla3D = proyecto;

                ContenedorGodotMain.Visibility = Visibility.Collapsed;
                PanelConstructorEditor.Visibility = Visibility.Visible;
                _constructorHost = new GodotHwndHost(godot, proyecto, "", "", null, editorMode: true);
                _constructorHost.GodotExited += (_, _) => Dispatcher.BeginInvoke(new Action(CerrarConstructorGodot));
                ContenedorEditor.Child = _constructorHost;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo abrir el constructor dentro del visor:\n{ex.Message}",
                    "Construcción", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private string? PedirUbicacionIsla3D()
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string inicial = !string.IsNullOrEmpty(_rutaProyectoIsla3D)
                ? Path.GetDirectoryName(_rutaProyectoIsla3D)!
                : Path.Combine(docs, "WoldVirtualP2P3D");
            if (!Directory.Exists(inicial))
                inicial = docs;

            var dlg = new Window
            {
                Title = "Guardar carpeta ISLA3D",
                Width = 680,
                Height = 245,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(10, 15, 26)),
            };

            var grid = new Grid { Margin = new Thickness(18) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var texto = new TextBlock
            {
                Text = "Se creará una carpeta llamada ISLA3D.\nElige dónde guardarla y pulsa Guardar.",
                Foreground = new SolidColorBrush(Color.FromRgb(220, 235, 255)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12),
            };
            Grid.SetRow(texto, 0);
            grid.Children.Add(texto);

            var fila = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var ruta = new TextBox
            {
                Text = inicial,
                Height = 32,
                FontSize = 12,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromRgb(19, 28, 44)),
                Foreground = new SolidColorBrush(Color.FromRgb(220, 235, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 90, 140)),
                Padding = new Thickness(8, 4, 8, 4),
            };
            Grid.SetColumn(ruta, 0);
            fila.Children.Add(ruta);

            var examinar = new Button
            {
                Content = "Examinar…",
                Width = 110,
                Height = 32,
                Margin = new Thickness(8, 0, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(180, 230, 255)),
                Background = new SolidColorBrush(Color.FromRgb(15, 35, 55)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(45, 120, 170)),
            };
            examinar.Click += (_, _) =>
            {
                var dialogo = new OpenFolderDialog
                {
                    Title = "Elige dónde guardar la carpeta ISLA3D",
                    InitialDirectory = Directory.Exists(ruta.Text) ? ruta.Text : inicial,
                };
                if (dialogo.ShowDialog() == true)
                    ruta.Text = dialogo.FolderName;
            };
            Grid.SetColumn(examinar, 1);
            fila.Children.Add(examinar);
            Grid.SetRow(fila, 1);
            grid.Children.Add(fila);

            var aviso = new TextBlock
            {
                Text = "La carpeta se llamará ISLA3D dentro de la ubicación elegida.",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 12),
            };
            Grid.SetRow(aviso, 2);
            grid.Children.Add(aviso);

            var botones = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            var guardar = new Button
            {
                Content = "Guardar",
                Width = 110,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(5, 46, 22)),
                Background = new SolidColorBrush(Color.FromRgb(52, 211, 153)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                FontWeight = FontWeights.Bold,
            };
            var cancelar = new Button
            {
                Content = "Cancelar",
                Width = 100,
                Height = 32,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            };
            string? resultado = null;
            guardar.Click += (_, _) =>
            {
                string baseElegida = ruta.Text.Trim();
                if (string.IsNullOrWhiteSpace(baseElegida))
                    return;
                try
                {
                    // Comprobar permiso real antes de cerrar el diálogo.
                    Directory.CreateDirectory(baseElegida);
                    string prueba = Path.Combine(baseElegida, $".wvp_write_{Guid.NewGuid():N}.tmp");
                    File.WriteAllText(prueba, "ok");
                    File.Delete(prueba);
                }
                catch (Exception ex)
                {
                    string alternativa = RutaIsla3DEscribible();
                    if (!string.IsNullOrEmpty(alternativa))
                    {
                        resultado = alternativa;
                        MessageBox.Show(
                            $"Windows no permite escribir en:\n{baseElegida}\n\n" +
                            $"Se guardará ISLA3D en una carpeta permitida:\n{alternativa}",
                            "Construcción", MessageBoxButton.OK, MessageBoxImage.Information);
                        dlg.Close();
                        return;
                    }
                    MessageBox.Show(
                        $"Windows no permite escribir en:\n{baseElegida}\n\n{ex.Message}\n\n" +
                        "Elige otra ubicación, por ejemplo Documentos o una carpeta de E:\\.",
                        "Construcción", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                resultado = string.Equals(Path.GetFileName(baseElegida), "ISLA3D", StringComparison.OrdinalIgnoreCase)
                    ? baseElegida
                    : Path.Combine(baseElegida, "ISLA3D");
                dlg.Close();
            };
            cancelar.Click += (_, _) => dlg.Close();
            botones.Children.Add(guardar);
            botones.Children.Add(cancelar);
            Grid.SetRow(botones, 3);
            grid.Children.Add(botones);

            dlg.Content = grid;
            dlg.ShowDialog();
            return resultado;
        }

        private static bool PuedeEscribir(string ruta)
        {
            try
            {
                Directory.CreateDirectory(ruta);
                string prueba = Path.Combine(ruta, $".wvp_write_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(prueba, "ok");
                File.Delete(prueba);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string RutaIsla3DEscribible()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string[] candidatas =
            {
                Path.Combine(AppContext.BaseDirectory, "ISLA3D"),
                Path.Combine(local, "WoldVirtualP2P3D", "ISLA3D"),
                Path.Combine(docs, "WoldVirtualP2P3D", "ISLA3D"),
            };
            foreach (string candidata in candidatas)
            {
                if (PuedeEscribir(candidata))
                    return candidata;
            }
            return "";
        }

        private static void CopiarPlantillaConstructor(string origen, string destino)
        {
            Directory.CreateDirectory(destino);
            foreach (string archivo in Directory.GetFiles(origen))
            {
                string nombre = Path.GetFileName(archivo);
                if (nombre.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    continue;
                string destinoArchivo = Path.Combine(destino, nombre);
                if (!File.Exists(destinoArchivo))
                    File.Copy(archivo, destinoArchivo);
            }
            foreach (string carpeta in Directory.GetDirectories(origen))
            {
                string nombre = Path.GetFileName(carpeta);
                if (nombre.Equals(".godot", StringComparison.OrdinalIgnoreCase) ||
                    nombre.Equals("ISLA3D", StringComparison.OrdinalIgnoreCase) ||
                    nombre.Equals("importado", StringComparison.OrdinalIgnoreCase))
                    continue;
                CopiarPlantillaConstructor(carpeta, Path.Combine(destino, nombre));
            }
        }

        private async void UsarConstruccionEnIsla()
        {
            try
            {
                string ruta = !string.IsNullOrEmpty(_rutaDatosGodot)
                    ? _rutaDatosGodot
                    : PerfilUsuario.GodotUserDataDir;
                Directory.CreateDirectory(ruta);
                string escena = Path.Combine(ruta, "isla_construida.tscn");
                string flag = Path.Combine(ruta, "construccion_lista.flag");
                if (!File.Exists(escena))
                {
                    MessageBox.Show("Todavía no hay una escena guardada. Construye algo y espera unos segundos.",
                        "Construcción", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Pedir al plugin que guarde la escena actual con las últimas
                // transformaciones antes de aplicarla.
                try { File.Delete(flag); } catch { }
                File.WriteAllText(Path.Combine(ruta, "construccion_guardar.solicitud"), "1");

                bool listo = false;
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(200);
                    if (File.Exists(flag)) { listo = true; break; }
                }
                if (!listo)
                {
                    MessageBox.Show("Godot no confirmó el guardado. Espera un momento y vuelve a pulsar.",
                        "Construcción", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                MessageBox.Show("La construcción se añadirá a tu isla. Si estás en la oficina, entra al mundo.",
                    "Construcción", MessageBoxButton.OK, MessageBoxImage.Information);
                // Al cerrar el constructor se vuelve al mundo y se ve la pieza.
                CerrarConstructorGodot();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo aplicar la construcción:\n{ex.Message}",
                    "Construcción", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CerrarConstructorGodot()
        {
            try { _constructorHost?.StopGodot(); } catch { }
            _constructorHost = null;
            ContenedorEditor.Child = null;
            PanelConstructorEditor.Visibility = Visibility.Collapsed;
            ContenedorGodotMain.Visibility = Visibility.Visible;
            try { _godotHostMain?.ActivateGodotFocus(); } catch { }
        }

        /// <summary>
        /// Abre la oficina de LucIA mostrando también el avatar del jugador.
        /// </summary>
        private void AbrirOficinaConAvatar()
        {
            try
            {
                string ruta = !string.IsNullOrEmpty(_rutaDatosGodot) ? _rutaDatosGodot : PerfilUsuario.GodotUserDataDir;
                Directory.CreateDirectory(ruta);
                File.WriteAllText(Path.Combine(ruta, "abrir_oficina_avatar.flag"), "1");
                Log("[Lucía 3D] Oficina con avatar solicitada.");
            }
            catch (Exception ex)
            {
                Log($"[Lucía 3D] No se pudo marcar la oficina con avatar: {ex.Message}");
            }
            AbrirOficinaLucia();
        }

        /// <summary>
        /// Lleva a LucIA a la isla del jugador. Godot lee el flag al entrar.
        /// </summary>
        private void InvitarLuciaIsla()
        {
            try
            {
                string ruta = !string.IsNullOrEmpty(_rutaDatosGodot) ? _rutaDatosGodot : PerfilUsuario.GodotUserDataDir;
                Directory.CreateDirectory(ruta);
                File.WriteAllText(Path.Combine(ruta, "invitar_lucia.flag"), "1");
                Log("[Lucía 3D] Invitación a la isla solicitada.");
            }
            catch (Exception ex)
            {
                Log($"[Lucía 3D] No se pudo marcar la invitación: {ex.Message}");
            }
            if (_godotHostMain != null) ReiniciarMotor3D();
            else EntrarAlMetaverso();
        }

        private void ImportarCuentaDialogo()
        {
            try
            {
                var ofd = new OpenFileDialog
                {
                    Title = "Importar Cuenta Oficial del Metaverso (Lucía 3D)",
                    Filter = "Archivos de Cuenta (*.json;*.zip)|*.json;*.zip|Archivo JSON (*.json)|*.json|Archivo ZIP (*.zip)|*.zip|Todos los archivos (*.*)|*.*",
                    Multiselect = false
                };

                if (ofd.ShowDialog() != true) return;

                string archivoSeleccionado = ofd.FileName;
                string ext = Path.GetExtension(archivoSeleccionado).ToLowerInvariant();

                string destinoDir = !string.IsNullOrEmpty(_rutaDatosGodot) ? _rutaDatosGodot : PerfilUsuario.GodotUserDataDir;
                if (!Directory.Exists(destinoDir)) Directory.CreateDirectory(destinoDir);

                if (ext == ".zip")
                {
                    using (var zip = ZipFile.OpenRead(archivoSeleccionado))
                    {
                        var entradaJson = zip.GetEntry("cuenta.json");
                        if (entradaJson != null)
                        {
                            string destJson = Path.Combine(destinoDir, "cuenta.json");
                            entradaJson.ExtractToFile(destJson, overwrite: true);
                        }
                    }

                    string destZip = Path.Combine(destinoDir, Path.GetFileName(archivoSeleccionado));
                    File.Copy(archivoSeleccionado, destZip, overwrite: true);
                }
                else if (ext == ".json")
                {
                    string contenido = File.ReadAllText(archivoSeleccionado);
                    if (!contenido.Contains("bloque_hash") && !contenido.Contains("usuario"))
                    {
                        MessageBox.Show("El archivo JSON seleccionado no parece ser una cuenta oficial válida (falta bloque_hash o usuario).",
                            "Archivo no válido", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    string destJson = Path.Combine(destinoDir, "cuenta.json");
                    File.Copy(archivoSeleccionado, destJson, overwrite: true);
                }

                ActualizarEstadoCuentaUI();

                var cta = PerfilUsuario.ObtenerCuentaOficial();
                string usr = !string.IsNullOrWhiteSpace(cta.Nick) ? cta.Nick : "desconocido";
                string hash = cta.CuentaHash ?? "---";
                string hashCorto = hash.Length > 12 ? hash.Substring(0, 12) + "…" : hash;

                MessageBox.Show(
                    $"Cuenta oficial importada con éxito.\n\n" +
                    $"• Usuario: {usr}\n" +
                    $"• Bloque: {hashCorto}\n\n" +
                    $"Ya puedes entrar al metaverso con tu identidad oficial.",
                    "Cuenta Importada", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al importar la cuenta:\n{ex.Message}",
                    "Error al importar", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        private void OnAccederClicked(object sender, RoutedEventArgs e)
        {
            ActivarModoLogin();
        }

        private void OnMigrarCuentaClicked(object sender, RoutedEventArgs e)
        {
            // Actualizacion de cuenta: vuelve a la recepcion de LucIA, donde
            // esta el formulario nuevo (.zip + llave + frase semilla) que
            // sustituye las credenciales antiguas por el hash unico.
            try
            {
                if (!string.IsNullOrEmpty(_rutaDatosGodot) && Directory.Exists(_rutaDatosGodot))
                {
                    File.WriteAllText(Path.Combine(_rutaDatosGodot, "abrir_recepcion.flag"), "1");
                }
            }
            catch (Exception error)
            {
                Console.WriteLine($"[cuenta] no se pudo marcar la recepcion: {error.Message}");
            }
            ReiniciarMotor3D();
        }

        private void OnVolverBienvenidaClicked(object sender, RoutedEventArgs e)
        {
            // La antigua pantalla de bienvenida ya no existe en el flujo:
            // volver atrás lleva otra vez a la oficina de LucIA.
            AbrirOficinaLucia();
        }

        /// <summary>Copia al portapapeles versión, estado, rutas y las últimas líneas del log.</summary>
        private async void OnDiagnosticoClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Wold Virtual P2P 3D v{CurrentVersion}");
                sb.AppendLine($"Windows: {Environment.OSVersion}");
                sb.AppendLine($"Runtime: {AppExtractor.RuntimeDir}");
                sb.AppendLine($"PCK: {File.Exists(AppExtractor.PckPath)} ({TamanoMb(AppExtractor.PckPath)} MB)");
                sb.AppendLine($"Godot: {File.Exists(AppExtractor.GodotExePath)}");
                sb.AppendLine($"Estado: {TxtEstadoP2P.Text}");
                sb.AppendLine($"Logs: {VisorLog.Carpeta}");
                sb.AppendLine("---- log (últimas 40 líneas) ----");
                sb.AppendLine(VisorLog.Cola(40));

                if (!CopiarAlPortapapeles(sb.ToString())) return;

                string previo = TxtEstadoP2P.Text;
                TxtEstadoP2P.Text = "🩺 Diagnóstico copiado";
                await Task.Delay(3000);
                if (TxtEstadoP2P.Text.StartsWith("🩺")) TxtEstadoP2P.Text = previo;
            }
            catch (Exception ex)
            {
                VisorLog.Error("No se pudo copiar el diagnóstico", ex);
            }
        }

        private static string TamanoMb(string path)
        {
            try
            {
                return File.Exists(path)
                    ? (new FileInfo(path).Length / 1048576.0).ToString("0.0")
                    : "0";
            }
            catch { return "?"; }
        }

        /// <summary>
        /// Godot escribe user://session_host.txt cuando migra el anfitrión.
        /// Se relee para actualizar el enlace de invitación sin reiniciar.
        /// </summary>
        private void ComprobarHostMigrado()
        {
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Godot", "app_userdata", "WoldVirtualP2P3D", "session_host.txt");
                if (!File.Exists(path)) return;

                // session_host.txt sólo lo escribe el anfitrión LOCAL, y su túnel
                // efímero muere al cerrar el proceso. Un archivo anterior a este
                // arranque es de una sesión pasada: no debe convertirnos en
                // clientes de un destino que ya no existe.
                if (File.GetLastWriteTimeUtc(path) < _arranqueUtc) return;

                string nuevo = File.ReadAllText(path).Trim();
                if (string.IsNullOrWhiteSpace(nuevo)) return;
                if (nuevo.Equals(_hostSesionUrl, StringComparison.OrdinalIgnoreCase)) return;
                // Si el archivo apunta a nuestro propio portal, somos el anfitrión.
                if (!string.IsNullOrEmpty(_urlPublica) &&
                    nuevo.Equals(_urlPublica, StringComparison.OrdinalIgnoreCase)) return;

                _hostSesionUrl = nuevo;
                // Sólo se actualiza el enlace de invitación. _urlAmigoPendiente no se
                // toca: eso decide el rol con el que arranca Godot y un anfitrión no
                // debe arrancar como cliente de su propio túnel.
                if (!string.IsNullOrEmpty(_urlPublica))
                    _enlaceCompartible = EnlaceInvitacion(_urlPublica);
                AjustarBarraResponsive();
                TxtUrlP2P.ToolTip = $"Anfitrión actual de la sesión:\n{nuevo}\n\nTu portal: {_urlPublica}";
                Log($"[Migracion] Nuevo anfitrión de sesión: {nuevo}");
            }
            catch { }
        }

        /// <summary>
        /// Normaliza un enlace de invitación: devuelve la URL base y, si trae
        /// <c>?host=</c>, el anfitrión raíz; con <c>?invitacion=</c>, la credencial
        /// de la sesión que exige el proxy WebSocket del anfitrión.
        /// </summary>
        public static string NormalizarUrlSesion(
            string url, out string? hostRaiz, out string? tokenInvitacion)
        {
            hostRaiz = null;
            tokenInvitacion = null;
            string limpia = (url ?? "").Trim().Trim('"', '\'');

            if (!Uri.TryCreate(limpia, UriKind.Absolute, out Uri? uri))
                return limpia;

            string query = uri.Query.TrimStart('?');
            foreach (string parte in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int igual = parte.IndexOf('=');
                if (igual <= 0) continue;

                string clave = Uri.UnescapeDataString(parte[..igual]);
                string valor = Uri.UnescapeDataString(parte[(igual + 1)..]).Trim();

                if (clave.Equals("host", StringComparison.OrdinalIgnoreCase))
                {
                    if (Uri.TryCreate(valor, UriKind.Absolute, out _))
                        hostRaiz = valor.TrimEnd('/');
                }
                else if (clave.Equals("invitacion", StringComparison.OrdinalIgnoreCase) &&
                         EsTokenInvitacion(valor))
                {
                    tokenInvitacion = valor;
                }
            }

            return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        }

        /// <summary>Token de sesión P2P: 32 caracteres hexadecimales.</summary>
        public static bool EsTokenInvitacion(string? valor)
        {
            if (valor == null || valor.Length != 32) return false;
            foreach (char c in valor)
            {
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!hex) return false;
            }
            return true;
        }

        /// <summary>
        /// Un invitado adopta la invitación del anfitrión raíz: así su propio
        /// WebNodo la exige si le toca re-alojar la sesión tras una migración.
        /// </summary>
        private void AdoptarTokenInvitacion(string? token)
        {
            if (!EsTokenInvitacion(token)) return;
            _tokenInvitacion = token!;
            App.WebNodo.TokenInvitacion = token!;
        }

        /// <summary>
        /// Invitación vigente: la adoptada del anfitrión raíz o, si este visor
        /// abre la sesión, la que generó su propio WebNodo.
        /// </summary>
        private string TokenSesionP2P => !string.IsNullOrWhiteSpace(_tokenInvitacion)
            ? _tokenInvitacion
            : App.WebNodo.TokenInvitacion;

        /// <summary>Portal compartible con anfitrión raíz e invitación de la sesión.</summary>
        private string EnlaceInvitacion(string baseUrl)
        {
            string token = TokenSesionP2P;
            var sb = new StringBuilder(baseUrl);
            char sep = baseUrl.Contains('?') ? '&' : '?';
            if (!string.IsNullOrWhiteSpace(_hostSesionUrl) &&
                !_hostSesionUrl.Equals(baseUrl, StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(sep).Append("host=").Append(Uri.EscapeDataString(_hostSesionUrl));
                sep = '&';
            }
            if (!string.IsNullOrWhiteSpace(token))
                sb.Append(sep).Append("invitacion=").Append(Uri.EscapeDataString(token));
            return sb.ToString();
        }

        /// <summary>
        /// Destino de túnel para Godot con la invitación. Las IP de LAN van
        /// directas al 8911 y no pasan por el proxy, así que se dejan intactas.
        /// </summary>
        private string UrlTunelConInvitacion(string url)
        {
            string token = TokenSesionP2P;
            if (string.IsNullOrWhiteSpace(token)) return url;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return url;
            }

            string baseUrl = url.TrimEnd('/');
            string sep = baseUrl.Contains('?') ? "&" : "/?";
            return $"{baseUrl}{sep}invitacion={Uri.EscapeDataString(token)}";
        }

        public static bool TryParseConnectionTarget(string input, out string host, out int port)
        {
            host = "";
            port = 8911; // WebSocket del motor Godot (TCP)
            if (string.IsNullOrWhiteSpace(input)) return false;

            string s = input.Trim().Trim('"', '\'');
            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) s = s.Substring(7);
            else if (s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) s = s.Substring(8);
            else if (s.StartsWith("woldvirtual://", StringComparison.OrdinalIgnoreCase)) s = s.Substring(14);

            int slashIdx = s.IndexOf('/');
            if (slashIdx >= 0) s = s.Substring(0, slashIdx);

            int colonIdx = s.LastIndexOf(':');
            if (colonIdx >= 0 && colonIdx < s.Length - 1)
            {
                string portPart = s.Substring(colonIdx + 1);
                if (int.TryParse(portPart, out int parsedPort))
                {
                    // Si el puerto recibido es el del WebNodo HTTP (3030-3040) o de la API P2P (7890),
                    // el tráfico del motor Godot viaja por WebSocket (TCP 8911) del anfitrión.
                    if ((parsedPort >= 3030 && parsedPort <= 3040) || parsedPort == 7890 || parsedPort == 80 || parsedPort == 443)
                    {
                        port = 8911;
                    }
                    else
                    {
                        port = parsedPort;
                    }
                }
                s = s.Substring(0, colonIdx);
            }

            host = s.Trim();
            return !string.IsNullOrEmpty(host);
        }

        /// <summary>
        /// True si el texto es una dirección IPv4 literal (p. ej. 192.168.1.10).
        /// Permite distinguir una IP directa de un nombre de host de túnel HTTP.
        /// </summary>
        public static bool EsIpLiteral(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return false;
            string[] partes = texto.Trim().Split('.');
            if (partes.Length != 4) return false;
            foreach (string p in partes)
            {
                if (!byte.TryParse(p, out _)) return false;
            }
            return true;
        }

        /// <summary>
        /// Al entrar como anfitrión, el banner queda a la espera del endpoint público del
        /// túnel efímero (SSH/Cloudflare), que se genera de forma asíncrona tras el login.
        /// La IP LAN privada (192.168.x.x) NO se muestra como enlace principal porque no es
        /// accesible desde fuera de la red local: queda solo como alternativa en el tooltip.
        /// </summary>
        private void MostrarDireccionAnfitrion()
        {
            var ips = IpsLanCompartibles();
            _ipsLanFallback = string.Join(", ", ips.Select(ip => ip + ":8911"));

            TxtUrlP2P.Text = "⏳ Generando enlace público…";
            TxtUrlP2P.ToolTip = string.IsNullOrEmpty(_ipsLanFallback)
                ? "El enlace público efímero se está generando…"
                : $"Enlace público en preparación…\n(Alternativa en la misma red local: {_ipsLanFallback})";
            BtnCopiarUrlP2P.Content = "📋 Copiar enlace";
            BannerP2P.Visibility = Visibility.Collapsed;

            Console.WriteLine(string.IsNullOrEmpty(_ipsLanFallback)
                ? "[Visor3D] ANFITRIÓN: generando túnel público (sin IPs LAN alternativas)."
                : $"[Visor3D] ANFITRIÓN: túnel público en preparación. Fallback LAN: {_ipsLanFallback}");
        }

        /// <summary>
        /// Refleja en el banner el endpoint público/efímero del túnel activo (en lugar de la
        /// IP privada LAN). El botón «Copiar enlace» pasa a copiar la URL completa del túnel.
        /// </summary>
        private void MostrarEnlacePublico(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            _urlPublica = url;
            if (string.IsNullOrEmpty(_hostSesionUrl)) _hostSesionUrl = url;   // soy el anfitrión raíz

            // El enlace que se comparte lleva el anfitrión raíz y la invitación de
            // la sesión: sin ese token el proxy WebSocket del anfitrión no admite.
            _enlaceCompartible = EnlaceInvitacion(url);

            AjustarBarraResponsive();
            TxtUrlP2P.ToolTip =
                $"Enlace de la sesión:\n{_enlaceCompartible}\n\n" +
                $"Tu portal: {url}\nAnfitrión raíz: {_hostSesionUrl}";
            BtnCopiarUrlP2P.Content = "📋 Copiar enlace";
            BannerP2P.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// La barra debe caber en la ventana por defecto (1120 px). En anchos ajustados
        /// la llave se acorta y el enlace pasa a modo compacto; la URL completa sigue en
        /// el tooltip y se copia igual (se guarda en <see cref="_urlPublica"/>).
        /// </summary>
        private void AjustarBarraResponsive()
        {
            bool amplio = ActualWidth >= 1360;

            TxtEtiquetaEnlace.Visibility = amplio ? Visibility.Visible : Visibility.Collapsed;
            string enlace = !string.IsNullOrEmpty(_enlaceCompartible) ? _enlaceCompartible : _urlPublica;
            if (!string.IsNullOrEmpty(enlace))
                TxtUrlP2P.Text = amplio ? enlace : "enlace listo";
        }

        /// <summary>IPv4 de la máquina excluyendo loopback (127.) y APIPA (169.254.).</summary>
        private static System.Collections.Generic.List<string> IpsLanCompartibles()
        {
            var resultado = new System.Collections.Generic.List<string>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string ip = ua.Address.ToString();
                        if (ip.StartsWith("127.") || ip.StartsWith("169.254.")) continue;
                        if (!resultado.Contains(ip)) resultado.Add(ip);
                    }
                }
            }
            catch { }
            return resultado;
        }

        private void GuardarP2PSessionConfig()
        {
            try
            {
                bool hasTarget = TryParseConnectionTarget(_urlAmigoPendiente, out string targetHost, out int targetPort);
                bool esClienteRemoto = hasTarget && targetHost != "127.0.0.1" && targetHost != "localhost" && !string.IsNullOrEmpty(targetHost);
                // misma raíz que la línea de órdenes del motor, para que el fichero de
                // sesión no vuelva a apuntar al portal del invitado que compartió el enlace
                string peerUrl = !string.IsNullOrWhiteSpace(_hostSesionUrl) ? _hostSesionUrl : (_urlAmigoPendiente ?? "");
                var sessionData = new
                {
                    peerUrl,
                    invitacion = TokenSesionP2P,
                    remoteIp = hasTarget ? targetHost : "",
                    remotePort = hasTarget ? targetPort : 8911,
                    esCliente = esClienteRemoto,
                    rolEfectivo = esClienteRemoto ? "cliente_remoto" : "servidor_local",
                    hostResuelto = esClienteRemoto ? targetHost : "127.0.0.1",
                    myTunnelUrl = App.WebNodo.PublicTunnelUrl ?? "",
                    puertoLocal = 7890,
                    puertoGodot = 8911,
                    esPrimerUsuario = !hasTarget,
                    timestamp = DateTime.UtcNow.ToString("o")
                };

                string json = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions { WriteIndented = true });
                string dir = Path.Combine(TestUserDataDir, "user");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "p2p_session.json"), json, Encoding.UTF8);

                // También en la carpeta del proyecto Godot user:// si aplica
                string godotUserDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Godot", "app_userdata", "WoldVirtualP2P3D");
                if (!Directory.Exists(godotUserDir)) Directory.CreateDirectory(godotUserDir);
                File.WriteAllText(Path.Combine(godotUserDir, "p2p_session.json"), json, Encoding.UTF8);

                // También en la raíz del proyecto para redundancia
                try
                {
                    string pDir = GodotConfig.GetProjectPath();
                    if (Directory.Exists(pDir))
                    {
                        File.WriteAllText(Path.Combine(pDir, "p2p_session.json"), json, Encoding.UTF8);
                    }
                }
                catch { }

                Console.WriteLine($"[Visor3D] p2p_session.json generado: target={targetHost}:{targetPort}, esCliente={sessionData.esCliente}");
                System.Diagnostics.Debug.WriteLine($"[Visor3D] p2p_session.json generado: target={targetHost}:{targetPort}, esCliente={sessionData.esCliente}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Visor3D] Error guardando p2p_session.json: {ex.Message}");
            }
        }

        /// <summary>
        /// Cierra el circuito de la cuota por ubicación (F9): captura la firma física
        /// SMTPLC (gateway ARP + Wi-Fi ambiental + subred WAN) y deja su hash en
        /// user://ubicacion_smtplc.json para que la recepción de LucIA (Godot) lo envíe
        /// en el POST /alta-cuenta. Se escribe antes de lanzar el motor. Nunca viaja la
        /// ubicación cruda: solo el LocationClusterHash ya hasheado (64 hex).
        /// </summary>
        private async Task GuardarUbicacionSmtplcAsync()
        {
            try
            {
                // La IP pública es lo único remoto; acotamos para no frenar el arranque
                // sin conexión (CaptureAsync cae a 127.0.0.1 / SUBNET_LOCAL si falla).
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var firma = await Task.Run(() => Visor3D.Seguridad.SMTPLC.LocationFingerprinter.CaptureAsync(cts.Token));
                string hash = firma.LocationClusterHash;
                if (string.IsNullOrWhiteSpace(hash)) return;

                string json = JsonSerializer.Serialize(new
                {
                    ubicacion_hash = hash,
                    capturado_utc = DateTime.UtcNow.ToString("o")
                }, new JsonSerializerOptions { WriteIndented = true });

                string dirUsuario = Path.Combine(TestUserDataDir, "user");
                Directory.CreateDirectory(dirUsuario);
                File.WriteAllText(Path.Combine(dirUsuario, "ubicacion_smtplc.json"), json, Encoding.UTF8);

                string dirGodot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Godot", "app_userdata", "WoldVirtualP2P3D");
                Directory.CreateDirectory(dirGodot);
                File.WriteAllText(Path.Combine(dirGodot, "ubicacion_smtplc.json"), json, Encoding.UTF8);

                System.Diagnostics.Debug.WriteLine($"[Visor3D] ubicacion_smtplc.json generado: {hash[..Math.Min(16, hash.Length)]}...");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Visor3D] Error guardando ubicacion_smtplc.json: {ex.Message}");
            }
        }

        private async void IniciarP2P(string user)
        {
            try
            {
                _p2pServicio = new RedP2PServicio(user);

                _p2pServicio.UrlTunnelLista += url =>
                {
                    // Este es el túnel de la API P2P local (puerto 7890). El enlace público que
                    // se comparte con los amigos es el del túnel WebNodo (ver EstablecerTunelEfimeroAsync),
                    // que es el que transporta el tráfico del metaverso por WebSocket.
                    Dispatcher.Invoke(() =>
                    {
                        GuardarP2PSessionConfig();
                        TxtEstadoP2P.Text = "🟢 Red P2P Online";
                        BordeEstadoP2P.Background = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(6, 78, 59));
                    });
                };

                // Reconexión automática del túnel: informar en la barra de estado.
                _p2pServicio.TunelCaido += host =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtEstadoP2P.Text = $"🔄 Túnel SSH ({host}) caído, reconectando…";
                        BordeEstadoP2P.Background = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(120, 53, 15));
                    });
                };

                _p2pServicio.TunelReintentando += (intento, espera) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtEstadoP2P.Text = $"🔄 Reconectando túnel… (intento {intento}, en {espera}s)";
                    });
                };

                _p2pServicio.NuevoPar += peer =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtEstadoP2P.Text = $"🟢 Amigo Conectado: {peer}";
                    });
                };

                await _p2pServicio.IniciarAsync();

                // Levantar túnel público efímero para WebNodo (cloudflared / SSH)
                _ = EstablecerTunelEfimeroAsync();

                // Si había URL de amigo ingresada en el login, conectarse automáticamente
                if (!string.IsNullOrWhiteSpace(_urlAmigoPendiente))
                {
                    bool ok = await _p2pServicio.ConectarConParAsync(_urlAmigoPendiente);
                    Dispatcher.Invoke(() =>
                    {
                        if (ok)
                        {
                            TxtEstadoP2P.Text = $"🟢 Enlazado con amigo";
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[P2P] Error al iniciar: {ex.Message}");
            }
        }


        private void ActualizarEstadoVentanasPrivadas()
        {
            foreach (var par in _ventanasPrivadas)
                par.Value.SetEnLinea(_uidsEnLinea.Contains(par.Key));
        }

        private List<(string Uid, string Nombre)> PersonasEnSesion()
        {
            var vistos = new Dictionary<string, string>();
            foreach (var par in _uidPorId)
            {
                if (string.IsNullOrEmpty(par.Value) || par.Value == _chatUid) continue;
                if (vistos.ContainsKey(par.Value)) continue;
                string nombre = _nombresChat.TryGetValue(par.Key, out string? n) ? n : "Jugador";
                vistos[par.Value] = ChatProtocol.CleanName(nombre);
            }
            return vistos.Select(x => (x.Key, x.Value)).ToList();
        }

        private void AlternarPanelAmigos()
        {
            bool visible = PanelAmigos.Visibility == Visibility.Visible;
            PanelAmigos.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
            if (!visible)
            {
                RefrescarAmigos();
                TxtCodigoAmigo.Focus();
            }
        }

        private void CopiarCodigoAmigo()
        {
            string codigo = ChatProtocol.BuildFriendCode(_chatUid);
            if (codigo.Length == 0) return;

            try
            {
                Clipboard.SetText(codigo);
                TxtEstadoAmigos.Text = "Código copiado al portapapeles.";
            }
            catch
            {
                TxtEstadoAmigos.Text = "No se pudo copiar. Código: " + codigo;
            }
        }

        private void OnCodigoAmigoKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter) return;
            EnviarSolicitudPorCodigo();
            e.Handled = true;
        }

        private void EnviarSolicitudPorCodigo()
        {
            if (_amigos == null) return;

            string codigo = ChatProtocol.CleanUid(TxtCodigoAmigo.Text).ToUpperInvariant();
            if (codigo.Length == 0)
            {
                TxtEstadoAmigos.Text = "Introduce el código de la otra persona.";
                return;
            }

            if (codigo == ChatProtocol.BuildFriendCode(_chatUid))
            {
                TxtEstadoAmigos.Text = "Ese es tu propio código.";
                return;
            }

            var cliente = _chatClient;
            if (cliente?.IsConnected != true)
            {
                TxtEstadoAmigos.Text = "Sin conexión con el chat. Reintentando…";
                return;
            }

            if (_amigos.TieneSaliente(codigo))
            {
                TxtEstadoAmigos.Text = "Ya hay una solicitud pendiente con ese código.";
                return;
            }

            _amigos.RegistrarSaliente("", codigo, "");
            _amigos.Guardar();
            TxtCodigoAmigo.Clear();
            TxtEstadoAmigos.Text = "Solicitud enviada. Esperando respuesta…";
            RefrescarAmigos();
            _ = cliente.SendFriendRequestAsync(codigo);
        }

        private void SolicitarAmistad(string uid, string nombre)
        {
            if (_amigos == null) return;

            string id = ChatProtocol.CleanUid(uid);
            if (id.Length == 0 || id == _chatUid) return;

            string limpio = ChatProtocol.CleanName(nombre);
            if (_amigos.EsAmigo(id))
            {
                TxtEstadoAmigos.Text = $"{limpio} ya es tu amigo.";
                return;
            }
            if (_amigos.TieneEntrante(id))
            {
                TxtEstadoAmigos.Text = $"{limpio} ya te envió una solicitud; acéptala en la lista.";
                return;
            }
            if (_amigos.TieneSaliente(id))
            {
                TxtEstadoAmigos.Text = $"Ya enviaste una solicitud a {limpio}.";
                return;
            }

            var cliente = _chatClient;
            if (cliente?.IsConnected != true)
            {
                TxtEstadoAmigos.Text = "Sin conexión con el chat. Reintentando…";
                return;
            }

            _amigos.RegistrarSaliente(id, "", limpio);
            _amigos.Guardar();
            TxtEstadoAmigos.Text = $"Solicitud enviada a {limpio}.";
            RefrescarAmigos();
            _ = cliente.SendFriendRequestAsync(id);
        }

        private bool TieneSolicitudEnviadaA(string uid)
        {
            if (_amigos == null) return false;
            if (_amigos.TieneSaliente(uid)) return true;

            string codigo = ChatProtocol.BuildFriendCode(uid);
            return codigo.Length > 0 && _amigos.Outgoing.Any(r =>
                r.Uid.Length == 0 &&
                string.Equals(r.Code, codigo, StringComparison.OrdinalIgnoreCase));
        }

        private void ResponderSolicitud(string uid, string nombre, bool aceptar)
        {
            if (_amigos == null) return;

            string id = ChatProtocol.CleanUid(uid);
            if (id.Length == 0) return;

            string limpio = ChatProtocol.CleanName(nombre);
            var cliente = _chatClient;
            if (cliente?.IsConnected != true)
            {
                TxtEstadoAmigos.Text = "Sin conexión con el chat. No se puede responder ahora mismo.";
                return;
            }

            _amigos.QuitarEntrante(id);
            if (aceptar) _amigos.AgregarAmigo(id, limpio);
            _amigos.Guardar();

            TxtEstadoAmigos.Text = aceptar
                ? $"{limpio} ya es tu amigo."
                : $"Solicitud de {limpio} rechazada.";
            AgregarMensajeSistemaChat(aceptar
                ? $"Has aceptado a {limpio}."
                : $"Has rechazado la solicitud de {limpio}.");

            RefrescarAmigos();
            _ = cliente.SendFriendResponseAsync(id, aceptar);
        }

        private void CancelarSolicitudEnviada(FriendRequestRecord solicitud)
        {
            if (_amigos == null) return;

            string clave = solicitud.Uid.Length > 0 ? solicitud.Uid : solicitud.Code;
            _amigos.QuitarSaliente(clave);
            _amigos.Guardar();
            TxtEstadoAmigos.Text = "Solicitud cancelada.";
            RefrescarAmigos();
        }

        private void QuitarAmigo(string uid)
        {
            if (_amigos == null) return;

            string id = ChatProtocol.CleanUid(uid);
            var amigo = _amigos.BuscarAmigo(id);
            if (amigo == null) return;

            string nombre = string.IsNullOrWhiteSpace(amigo.Name) ? "este amigo" : amigo.Name;
            if (MessageBox.Show(
                    $"¿Quitar a «{nombre}» de tu lista de amigos?\n\n" +
                    "Solo se borra en este equipo; la otra persona no recibe ningún aviso.",
                    "Amigos", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            _amigos.QuitarAmigo(id);
            _amigos.Guardar();

            if (_ventanasPrivadas.TryGetValue(id, out var ventana))
            {
                _ventanasPrivadas.Remove(id);
                try { ventana.CerrarPorSesion(); } catch { }
            }
            _privadosNoLeidos.Remove(id);
            _historialPrivado.Remove(id);

            TxtEstadoAmigos.Text = $"{nombre} ya no está en tu lista.";
            RefrescarAmigos();
        }

        private void AbrirChatPrivado(string uid, string nombre)
        {
            if (_amigos == null) return;

            string id = ChatProtocol.CleanUid(uid);
            if (id.Length == 0 || !_amigos.EsAmigo(id)) return;

            string limpio = _amigos.BuscarAmigo(id)?.Name ?? ChatProtocol.CleanName(nombre);
            if (_ventanasPrivadas.TryGetValue(id, out var existente))
            {
                if (existente.IsVisible)
                {
                    existente.Activate();
                    existente.Focus();
                    _privadosNoLeidos.Remove(id);
                    RefrescarAmigos();
                    return;
                }
                _ventanasPrivadas.Remove(id);
            }

            var ventana = new PrivateChatWindow(
                id, limpio,
                texto => EnviarMensajePrivado(id, texto),
                v => _ventanasPrivadas.Remove(id));
            ventana.Owner = this;
            ventana.Leido += v =>
            {
                if (_privadosNoLeidos.Remove(id)) RefrescarAmigos();
            };
            ventana.SetEnLinea(_uidsEnLinea.Contains(id));

            if (_historialPrivado.TryGetValue(id, out var historial) && historial.Count > 0)
                ventana.CargarHistorial(historial);

            _ventanasPrivadas[id] = ventana;
            _privadosNoLeidos.Remove(id);
            RefrescarAmigos();
            ventana.Show();
            ventana.Activate();
        }

        private void EnviarMensajePrivado(string uid, string texto)
        {
            string id = ChatProtocol.CleanUid(uid);
            if (id.Length == 0) return;

            string autor = _amigos?.BuscarAmigo(id)?.Name ?? "Tú";
            RegistrarHistorialPrivado(id, autor, texto, propio: true);

            var cliente = _chatClient;
            if (cliente?.IsConnected != true)
            {
                if (_ventanasPrivadas.TryGetValue(id, out var ventana))
                    ventana.AgregarSistema("Sin conexión con el chat. El mensaje no se envió.");
                return;
            }

            _ = cliente.SendDirectAsync(id, texto);
        }

        private void RegistrarHistorialPrivado(string uid, string autor, string texto, bool propio)
        {
            if (!_historialPrivado.TryGetValue(uid, out var lista))
            {
                lista = new List<MensajePrivado>();
                _historialPrivado[uid] = lista;
            }

            lista.Add(new MensajePrivado(propio, propio ? "Tú" : autor, texto));
            if (lista.Count > 200) lista.RemoveRange(0, lista.Count - 200);
        }

        private void ReenviarSolicitudesPendientes()
        {
            var cliente = _chatClient;
            if (cliente?.IsConnected != true || _amigos == null) return;

            foreach (var pendiente in _amigos.Outgoing.ToList())
            {
                if (pendiente.Uid.Length > 0 && _amigos.EsAmigo(pendiente.Uid)) continue;

                string objetivo = pendiente.Uid;
                bool resuelto = false;

                if (objetivo.Length == 0 && pendiente.Code.Length > 0)
                {
                    foreach (var persona in PersonasEnSesion())
                    {
                        if (!string.Equals(
                                ChatProtocol.BuildFriendCode(persona.Uid),
                                pendiente.Code,
                                StringComparison.OrdinalIgnoreCase))
                            continue;

                        objetivo = persona.Uid;
                        _amigos.ResolverSaliente(pendiente.Code, objetivo, persona.Nombre);
                        _amigos.Guardar();
                        resuelto = true;
                        break;
                    }
                }

                if (objetivo.Length == 0 || !_uidsEnLinea.Contains(objetivo)) continue;

                string clave = cliente.MyId + ":" + objetivo;
                if (!_reenviosAmistad.Add(clave)) continue;

                _ = cliente.SendFriendRequestAsync(objetivo);
                if (resuelto) RefrescarAmigos();
            }
        }

        private void RefrescarAmigos()
        {
            if (ListaAmigos == null) return;

            TxtMiCodigo.Text = _chatUid.Length > 0 ? ChatProtocol.BuildFriendCode(_chatUid) : "—";
            ListaAmigos.Children.Clear();
            ListaSolicitudes.Children.Clear();
            ListaEnSesion.Children.Clear();

            var amigos = _amigos?.Friends.ToList() ?? new List<FriendRecord>();
            var entrantes = _amigos?.Incoming.ToList() ?? new List<FriendRequestRecord>();
            var salientes = _amigos?.Outgoing.ToList() ?? new List<FriendRequestRecord>();

            if (amigos.Count == 0)
                ListaAmigos.Children.Add(TextoAmigos("Aún no tienes amigos."));
            else
                foreach (var amigo in amigos
                             .OrderByDescending(a => _uidsEnLinea.Contains(a.Uid))
                             .ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase))
                    ListaAmigos.Children.Add(CrearFilaAmigo(amigo));

            LblSolicitudes.Visibility = entrantes.Count + salientes.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            foreach (var solicitud in entrantes.OrderByDescending(s => s.At))
                ListaSolicitudes.Children.Add(CrearFilaSolicitud(solicitud));

            foreach (var pendiente in salientes.OrderByDescending(s => s.At))
                ListaSolicitudes.Children.Add(CrearFilaPendiente(pendiente));

            var enSesion = PersonasEnSesion()
                .Where(p => _amigos?.EsAmigo(p.Uid) != true &&
                            _amigos?.TieneSaliente(p.Uid) != true &&
                            _amigos?.TieneEntrante(p.Uid) != true)
                .OrderBy(p => p.Nombre, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            LblEnSesion.Visibility = enSesion.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            foreach (var persona in enSesion)
                ListaEnSesion.Children.Add(CrearFilaSesion(persona.Uid, persona.Nombre));

            ActualizarBotonAmigos();
        }

        private void ActualizarBotonAmigos()
        {
            if (BtnAmigos == null) return;

            int amigos = _amigos?.Friends.Count ?? 0;
            int enLinea = _amigos?.Friends.Count(f => _uidsEnLinea.Contains(f.Uid)) ?? 0;
            int pendientes = (_amigos?.Incoming.Count ?? 0) + _privadosNoLeidos.Values.Sum();

            BtnAmigos.Content = pendientes > 0 ? $"👥 Amigos ({pendientes})" : "👥 Amigos";
            BtnAmigos.ToolTip = amigos > 0
                ? $"{enLinea}/{amigos} amigos en línea"
                : "Abrir tu lista de amigos";
        }

        private Border CrearFilaAmigo(FriendRecord amigo)
        {
            bool enLinea = _uidsEnLinea.Contains(amigo.Uid);
            var grid = NuevaFilaAmigo();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var punto = new System.Windows.Shapes.Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = new SolidColorBrush(enLinea
                    ? Color.FromRgb(45, 212, 191)
                    : Color.FromRgb(71, 85, 105)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(punto, 0);
            grid.Children.Add(punto);

            string etiqueta = string.IsNullOrWhiteSpace(amigo.Name) ? "Jugador" : amigo.Name;
            if (_privadosNoLeidos.TryGetValue(amigo.Uid, out int noLeidos) && noLeidos > 0)
                etiqueta += $" ({noLeidos})";

            var nombre = new TextBlock
            {
                Text = etiqueta,
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                FontSize = 10.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 4, 0),
                ToolTip = enLinea ? "En línea" : "Sin conexión"
            };
            Grid.SetColumn(nombre, 1);
            grid.Children.Add(nombre);

            var chat = BotonAmigos("Chat");
            chat.Click += (s, e) => AbrirChatPrivado(amigo.Uid, amigo.Name);
            Grid.SetColumn(chat, 2);
            grid.Children.Add(chat);

            var quitar = BotonAmigos("✕");
            quitar.ToolTip = "Quitar de tu lista de amigos";
            quitar.Click += (s, e) => QuitarAmigo(amigo.Uid);
            Grid.SetColumn(quitar, 3);
            grid.Children.Add(quitar);

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 16, 29)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 34, 56)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 2, 3, 2),
                Margin = new Thickness(0, 1, 0, 1),
                Child = grid
            };
        }

        private Border CrearFilaSolicitud(FriendRequestRecord solicitud)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            string nombre = string.IsNullOrWhiteSpace(solicitud.Name) ? "Jugador" : solicitud.Name;
            var texto = new TextBlock
            {
                Text = $"👋 {nombre}",
                Foreground = new SolidColorBrush(Color.FromRgb(251, 191, 36)),
                FontSize = 10.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(texto, 0);
            grid.Children.Add(texto);

            var aceptar = BotonAmigos("✓");
            aceptar.ToolTip = $"Aceptar a {nombre}";
            aceptar.Click += (s, e) => ResponderSolicitud(solicitud.Uid, nombre, true);
            Grid.SetColumn(aceptar, 1);
            grid.Children.Add(aceptar);

            var rechazar = BotonAmigos("✕");
            rechazar.ToolTip = "Rechazar la solicitud";
            rechazar.Click += (s, e) => ResponderSolicitud(solicitud.Uid, nombre, false);
            Grid.SetColumn(rechazar, 2);
            grid.Children.Add(rechazar);

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 22, 10)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(80, 62, 20)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 2, 3, 2),
                Margin = new Thickness(0, 1, 0, 1),
                Child = grid
            };
        }

        private Border CrearFilaPendiente(FriendRequestRecord solicitud)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            string nombre = !string.IsNullOrWhiteSpace(solicitud.Name)
                ? solicitud.Name
                : (!string.IsNullOrWhiteSpace(solicitud.Code) ? "Código " + solicitud.Code : "Solicitud");
            var texto = new TextBlock
            {
                Text = $"⏳ {nombre}",
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 10.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Esperando respuesta"
            };
            Grid.SetColumn(texto, 0);
            grid.Children.Add(texto);

            var cancelar = BotonAmigos("✕");
            cancelar.ToolTip = "Cancelar la solicitud enviada";
            cancelar.Click += (s, e) => CancelarSolicitudEnviada(solicitud);
            Grid.SetColumn(cancelar, 1);
            grid.Children.Add(cancelar);

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(13, 20, 36)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(29, 58, 92)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 2, 3, 2),
                Margin = new Thickness(0, 1, 0, 1),
                Child = grid
            };
        }

        private Border CrearFilaSesion(string uid, string nombre)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var texto = new TextBlock
            {
                Text = nombre,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 10.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(texto, 0);
            grid.Children.Add(texto);

            var agregar = BotonAmigos("Añadir");
            agregar.ToolTip = $"Enviar solicitud de amistad a {nombre}";
            agregar.Click += (s, e) => SolicitarAmistad(uid, nombre);
            Grid.SetColumn(agregar, 1);
            grid.Children.Add(agregar);

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(10, 16, 29)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(22, 34, 56)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 2, 3, 2),
                Margin = new Thickness(0, 1, 0, 1),
                Child = grid
            };
        }

        private static Grid NuevaFilaAmigo()
            => new() { Margin = new Thickness(0) };

        private static Button BotonAmigos(string texto)
        {
            var boton = new Button
            {
                Content = texto,
                Height = 20,
                MinWidth = 22,
                Padding = new Thickness(5, 0, 5, 0),
                Margin = new Thickness(3, 0, 0, 0),
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                Background = new SolidColorBrush(Color.FromRgb(22, 34, 56)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(42, 67, 101)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            boton.Resources.Add(typeof(Border), new Style(typeof(Border))
            {
                Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(4)) }
            });
            return boton;
        }

        private static TextBlock TextoAmigos(string texto) => new()
        {
            Text = texto,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            FontSize = 10,
            FontStyle = FontStyles.Italic,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 1, 2, 1)
        };

        private static void MarcarBotonChat(Button boton, bool activo)
        {
            boton.Background = new SolidColorBrush(activo
                ? Color.FromRgb(6, 78, 59)
                : Color.FromRgb(22, 34, 56));
            boton.BorderBrush = new SolidColorBrush(activo
                ? Color.FromRgb(34, 197, 94)
                : Color.FromRgb(42, 67, 101));
            boton.Foreground = new SolidColorBrush(activo
                ? Colors.White
                : Color.FromRgb(226, 232, 240));
        }

        private void OnChatTextBoxKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                EnviarMensajeChat();
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                CerrarPanelChat();
                e.Handled = true;
            }
        }

        private void EnviarMensajeChat()
        {
            string texto = TxtChat.Text.Trim();
            if (texto.Length == 0) return;

            var cliente = _chatClient;
            if (cliente == null || !cliente.IsConnected)
            {
                AgregarMensajeSistemaChat("Sin conexión con el anfitrión del chat. Reintentando…");
                return;
            }

            _ = cliente.SendTextAsync(texto);
            TxtChat.Clear();
            TxtChat.Focus();
        }

        private void AlternarMicrofonoChat()
        {
            if (!_chatActivo || _chatVoice == null) return;

            if (_chatVoice.IsMicOn)
            {
                _chatVoice.StopMic();
                MarcarBotonChat(BtnChatMic, false);
                BtnChatMic.Content = "🎙 Voz";
            }
            else
            {
                if (ChatVoice.MicrophoneCount == 0)
                {
                    MessageBox.Show("No se detectó ningún micrófono en este equipo.",
                        "Chat de voz", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!_chatVoice.StartMic())
                {
                    MessageBox.Show(_chatVoice.LastError ?? "No se pudo abrir el micrófono.",
                        "Chat de voz", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                MarcarBotonChat(BtnChatMic, true);
                BtnChatMic.Content = "🎙 Voz ON";
            }

            _ = _chatClient?.SendStateAsync(_chatVoice.IsMicOn, _chatWebcam?.IsRunning == true);
            ActualizarEstadoChat();
        }

        private void AlternarCamaraChat()
        {
            if (!_chatActivo) return;

            if (_chatWebcam?.IsRunning == true)
            {
                _chatWebcam.Dispose();
                _chatWebcam = null;
                ImgCamLocal.Source = null;
                MarcoCamLocal.Visibility = Visibility.Collapsed;
                MarcarBotonChat(BtnChatCam, false);
                BtnChatCam.Content = "📷 Cámara";
            }
            else
            {
                if (ChatWebcam.ListDevices().Count == 0)
                {
                    MessageBox.Show("No se detectó ninguna webcam conectada.",
                        "Chat con webcam", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var camara = new ChatWebcam();
                camara.FrameJpeg += OnCamaraFrameLocal;
                camara.Error += mensaje => Dispatcher.BeginInvoke(new Action(() => Log($"[Chat] {mensaje}")));

                if (!camara.Start(0))
                {
                    MessageBox.Show(camara.LastError ?? "No se pudo abrir la webcam.",
                        "Chat con webcam", MessageBoxButton.OK, MessageBoxImage.Warning);
                    camara.Dispose();
                    return;
                }

                _chatWebcam = camara;
                if (!PanelVideos.Children.Contains(MarcoCamLocal))
                    PanelVideos.Children.Insert(0, MarcoCamLocal);
                MarcoCamLocal.Visibility = Visibility.Visible;
                MarcarBotonChat(BtnChatCam, true);
                BtnChatCam.Content = "📷 Cámara ON";
            }

            ActualizarFranjaVideos();
            _ = _chatClient?.SendStateAsync(_chatVoice?.IsMicOn == true, _chatWebcam?.IsRunning == true);
            ActualizarEstadoChat();
        }

        /// <summary>Se ejecuta en el hilo de captura de NAudio: solo encola el envío.</summary>
        private void OnMicrofonoChat(byte[] pcm)
        {
            var cliente = _chatClient;
            if (cliente == null || !cliente.IsConnected) return;
            _ = cliente.SendMediaAsync(ChatProtocol.KindAudio, pcm);
        }

        /// <summary>Se ejecuta en el hilo de DirectShow: preview local y envío al relay.</summary>
        private void OnCamaraFrameLocal(byte[] jpeg)
        {
            BitmapImage? imagen = DecodificarJpeg(jpeg);
            if (imagen != null)
                Dispatcher.BeginInvoke(new Action(() => ImgCamLocal.Source = imagen));

            var cliente = _chatClient;
            if (cliente == null || !cliente.IsConnected) return;
            _ = cliente.SendMediaAsync(ChatProtocol.KindVideo, jpeg);
        }

        private static BitmapImage? DecodificarJpeg(byte[] datos)
        {
            try
            {
                using var ms = new MemoryStream(datos);
                var imagen = new BitmapImage();
                imagen.BeginInit();
                imagen.CacheOption = BitmapCacheOption.OnLoad;
                imagen.StreamSource = ms;
                imagen.EndInit();
                imagen.Freeze();
                return imagen;
            }
            catch
            {
                return null;
            }
        }

        private void MostrarTileVideo(int id, BitmapImage imagen)
        {
            if (!_tilesChat.TryGetValue(id, out var tile))
            {
                var nombreControl = new TextBlock
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(0, 217, 255)),
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(5, 3, 5, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 152
                };
                var imagenControl = new Image
                {
                    Width = 152,
                    Height = 86,
                    Stretch = Stretch.UniformToFill,
                    Margin = new Thickness(4)
                };
                var pila = new StackPanel();
                pila.Children.Add(nombreControl);
                pila.Children.Add(imagenControl);

                var marco = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(5, 8, 17)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(45, 212, 191)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(0, 0, 6, 0),
                    Child = pila
                };

                tile = (marco, imagenControl, nombreControl);
                _tilesChat[id] = tile;
                PanelVideos.Children.Add(marco);
            }

            tile.Imagen.Source = imagen;
            tile.Nombre.Text = _nombresChat.TryGetValue(id, out string? nombre) ? nombre : $"Peer {id}";
            tile.Marco.Visibility = Visibility.Visible;
            _ultimoVideoChat[id] = DateTime.UtcNow;
            ActualizarFranjaVideos();
        }

        /// <summary>La franja de webcams solo ocupa espacio si hay alguna activa.</summary>
        private void ActualizarFranjaVideos()
        {
            bool hayVideo = MarcoCamLocal.Visibility == Visibility.Visible;
            if (!hayVideo)
                foreach (var tile in _tilesChat.Values)
                    if (tile.Marco.Visibility == Visibility.Visible) { hayVideo = true; break; }
            MarcoVideosChat.Visibility = hayVideo ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ActualizarNombreTile(int id)
        {
            if (!_tilesChat.TryGetValue(id, out var tile)) return;
            if (_nombresChat.TryGetValue(id, out string? nombre))
                tile.Nombre.Text = nombre;
        }

        private void OcultarTileVideo(int id)
        {
            _ultimoVideoChat.Remove(id);
            if (_tilesChat.TryGetValue(id, out var tile))
                tile.Marco.Visibility = Visibility.Collapsed;
            ActualizarFranjaVideos();
        }

        /// <summary>Vacía los tiles remotos sin expulsar del árbol la vista local del XAML.</summary>
        internal static void LimpiarTilesVideo(Panel panel, UIElement preservar)
        {
            for (int i = panel.Children.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(panel.Children[i], preservar))
                    panel.Children.RemoveAt(i);
            }
        }

        private void QuitarTileVideo(int id)
        {
            _ultimoVideoChat.Remove(id);
            if (!_tilesChat.TryGetValue(id, out var tile)) return;

            PanelVideos.Children.Remove(tile.Marco);
            _tilesChat.Remove(id);
            ActualizarFranjaVideos();
        }

        private void AgregarMensajeChat(string autor, string texto, Color color)
        {
            var bloque = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11.5,
                Margin = new Thickness(2, 1, 2, 1)
            };
            bloque.Inlines.Add(new Run
            {
                Text = autor + ": ",
                Foreground = new SolidColorBrush(color),
                FontWeight = FontWeights.Bold
            });
            bloque.Inlines.Add(new Run
            {
                Text = texto,
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240))
            });
            AnadirLineaChat(bloque);
        }

        private void AgregarMensajeSistemaChat(string texto)
        {
            var bloque = new TextBlock
            {
                Text = "· " + texto,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 10.5,
                FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(2, 1, 2, 1)
            };
            AnadirLineaChat(bloque);
        }

        private void AnadirLineaChat(TextBlock bloque)
        {
            ListaChat.Items.Add(bloque);
            while (ListaChat.Items.Count > 400)
                ListaChat.Items.RemoveAt(0);
            ListaChat.ScrollIntoView(bloque);
        }

        private void OnCopiarUrlP2PClicked(object sender, RoutedEventArgs e)
        {
            string enlace = !string.IsNullOrWhiteSpace(_enlaceCompartible)
                ? _enlaceCompartible
                : (!string.IsNullOrWhiteSpace(_urlPublica) ? _urlPublica : TxtUrlP2P.Text.Trim());
            if (string.IsNullOrWhiteSpace(enlace)) return;

            // Evitar copiar el texto de estado mientras el túnel aún no está listo.
            if (!enlace.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "El enlace público aún se está generando. Espera unos segundos e inténtalo de nuevo.",
                    "Enlace en preparación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (!CopiarAlPortapapeles(enlace))
            {
                MessageBox.Show(
                    $"El portapapeles está ocupado por otra aplicación. Copia el enlace manualmente:\n\n{enlace}",
                    "Portapapeles ocupado",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show(
                $"¡Enlace público copiado al portapapeles!\n\n{enlace}\n\n" +
                "Compártelo con tu amigo (por WhatsApp u otro medio) para que pueda unirse a tu mundo desde cualquier red.",
                "Enlace P2P Copiado",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>
        /// Copia texto al portapapeles con reintentos. En Windows el portapapeles puede estar
        /// bloqueado momentáneamente por otra aplicación (COMException / ExternalException),
        /// por lo que un único intento puede fallar de forma espuria.
        /// </summary>
        private static bool CopiarAlPortapapeles(string texto)
        {
            for (int intento = 0; intento < 5; intento++)
            {
                try
                {
                    Clipboard.SetText(texto);
                    return true;
                }
                catch
                {
                    Thread.Sleep(60);
                }
            }
            return false;
        }

        private async void OnConectarAmigoClicked(object sender, RoutedEventArgs e)
        {
            // LucIA es la anfitriona y resuelve el destino de la sesión: ya no se pega
            // un enlace de invitación. Conectar valida que ese destino es alcanzable.
            string destino = ResolverDestinoConexion();
            if (string.IsNullOrWhiteSpace(destino))
            {
                MessageBox.Show(
                    "LucIA aún no ha resuelto el destino de la sesión.\nInicia sesión para que el motor arranque la red P2P y vuelve a validar.",
                    "Sin destino de sesión", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TxtUrlAmigo.Text = destino;
            BtnConectarAmigo.Content = "⏳ Validando...";
            BtnConectarAmigo.IsEnabled = false;

            bool responde = await DescubrimientoP2P.SondearAsync(destino);
            BtnConectarAmigo.Content = "🔗 Validar conexión";
            BtnConectarAmigo.IsEnabled = true;

            if (responde)
            {
                MessageBox.Show(
                    "✔ Conexión validada: la sesión de LucIA responde.\nYa puedes iniciar sesión para entrar al mundo.",
                    "Conexión validada", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    "No se pudo validar la conexión con el destino de la sesión.\nAsegúrate de que LucIA esté en línea y el túnel esté activo.",
                    "Sin conexión", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Resuelve el destino de la sesión sin pedir al usuario que pegue un enlace:
        /// lo fija LucIA (el motor escribe p2p_session.json con peerUrl/hostResuelto)
        /// o, en su defecto, el túnel efímero propio (soy el anfitrión raíz).
        /// </summary>
        private string ResolverDestinoConexion()
        {
            if (!string.IsNullOrWhiteSpace(_hostSesionUrl))
                return _hostSesionUrl;
            if (!string.IsNullOrWhiteSpace(_urlAmigoPendiente))
                return _urlAmigoPendiente;

            foreach (var ruta in RutasP2PSession())
            {
                try
                {
                    if (!File.Exists(ruta)) continue;
                    using var doc = JsonDocument.Parse(File.ReadAllText(ruta));
                    var root = doc.RootElement;
                    string? candidato = null;
                    if (root.TryGetProperty("peerUrl", out var p) && !string.IsNullOrWhiteSpace(p.GetString()))
                        candidato = p.GetString();
                    else if (root.TryGetProperty("hostResuelto", out var h) && !string.IsNullOrWhiteSpace(h.GetString()))
                        candidato = h.GetString();
                    if (!string.IsNullOrWhiteSpace(candidato))
                    {
                        _hostSesionUrl = candidato!;
                        return candidato!;
                    }
                }
                catch { /* archivo de sesión ilegible o aún no escrito */ }
            }

            // Última opción: mi propio túnel efímero (soy el anfitrión raíz).
            return _urlPublica;
        }

        private IEnumerable<string> RutasP2PSession()
        {
            yield return Path.Combine(TestUserDataDir, "user", "p2p_session.json");
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Godot", "app_userdata", "WoldVirtualP2P3D", "p2p_session.json");
        }

        /// <summary>
        /// Lee el UUID del usuario desde su perfil local (user_profile.json o credentials.json).
        /// Devuelve cadena vacía si no se puede obtener (no bloquea el flujo).
        /// </summary>
        private string ObtenerUuidDelPerfil(string user)
        {
            foreach (var (path, campoUsuario) in new[] {
                (UserProfilePath, "nombre"),
                (CredentialsPath, "username") })
            {
                if (!File.Exists(path)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
                    var root = doc.RootElement;
                    string guardado = root.TryGetProperty(campoUsuario, out var u) ? u.GetString() ?? "" : "";
                    if (!guardado.Equals(user, StringComparison.OrdinalIgnoreCase)) continue;
                    // uuid puede estar como "uuid", "id", "userId"
                    foreach (string campo in new[] { "uuid", "id", "userId" })
                        if (root.TryGetProperty(campo, out var v) && !string.IsNullOrEmpty(v.GetString()))
                            return v.GetString()!;
                }
                catch { }
            }
            return string.Empty;
        }


        private async Task<bool> IniciarWebNodoExternoAsync()
        {
            try
            {
                if (_webNodoHelper != null && !_webNodoHelper.HasExited)
                    return App.WebNodo.IsRunning;

                string exe = Environment.ProcessPath ?? "";
                if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
                    return false;

                string script = Path.Combine(AppContext.BaseDirectory, "webnodo", "fallback.py");
                if (!File.Exists(script) && RutasVisor.SrcDir is string src)
                    script = Path.Combine(src, "VSC#", "Visor3Ddesarrollo", "webnodo", "fallback.py");

                string? python = RutasVisor.PythonWExe;
                if (string.IsNullOrEmpty(python) || !File.Exists(python) || !File.Exists(script))
                    return false;

                _webNodoHelper = Process.Start(new ProcessStartInfo
                {
                    FileName = python,
                    Arguments = $"\"{script}\" --puerto 3030 --exe \"{exe}\"",
                    WorkingDirectory = RutasVisor.DirTrabajo,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });

                for (int i = 0; i < 20; i++)
                {
                    await Task.Delay(300);
                    try
                    {
                        using var cliente = new TcpClient();
                        var tarea = cliente.ConnectAsync("127.0.0.1", 3030);
                        if (tarea.Wait(300) && cliente.Connected)
                        {
                            App.WebNodo.MarcarExterno(3030);
                            VisorLog.Info("[WebNodo] Portal auxiliar listo en http://localhost:3030/");
                            return true;
                        }
                    }
                    catch { }
                    if (_webNodoHelper != null && _webNodoHelper.HasExited) break;
                }
            }
            catch (Exception ex)
            {
                VisorLog.Info($"[WebNodo] No se pudo abrir el portal auxiliar: {ex.Message}");
            }
            return false;
        }

        private async void OnWebNodoClicked(object sender, RoutedEventArgs e)
        {            try
            {
                // Arrancar el servidor WebNodo embebido si no está corriendo
                if (!App.WebNodo.IsRunning)
                {
                    await Task.Run(() => App.WebNodo.Start());
                }
                if (!App.WebNodo.IsRunning)
                {
                    await IniciarWebNodoExternoAsync();
                }

                // Si el servidor no pudo iniciar, avisar al usuario
                if (!App.WebNodo.IsRunning)
                {
                    string err = App.WebNodo.StartError ?? "Error desconocido al enlazar el servidor local.";
                    MessageBox.Show($"WebNodo no pudo iniciarse en el puerto local ({App.WebNodo.ActivePort}):\n{err}",
                                    "WebNodo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Si ya hay túnel público efímero listo (ej. https://xxx.trycloudflare.com/)
                if (!string.IsNullOrWhiteSpace(App.WebNodo.PublicTunnelUrl))
                {
                    string publicUrl = App.WebNodo.PublicTunnelUrl;
                    try { Clipboard.SetText(publicUrl); } catch { }

                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(publicUrl)
                    {
                        UseShellExecute = true
                    });

                    MessageBox.Show(
                        $"🌍 ¡Portal WebNodo Efímero de Sesión en línea!\n\n" +
                        $"URL Pública:\n{publicUrl}\n\n" +
                        $"✅ Enlace copiado al portapapeles.\n" +
                        $"Compártelo con cualquier amigo para que descargue el visor y se una a tu sesión en el metaverso 3D.",
                        "WebNodo Público Efímero",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Si aún no ha terminado de conectar el túnel público, abrir local y avisar
                string localUrl = $"http://localhost:{App.WebNodo.ActivePort}/";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(localUrl)
                {
                    UseShellExecute = true
                });

                // Asegurar que el túnel efímero se esté generando
                _ = EstablecerTunelEfimeroAsync();

                MessageBox.Show(
                    $"🌐 Portal WebNodo abierto localmente en:\n{localUrl}\n\n" +
                    $"⏳ El túnel público efímero Cloudflare se está conectando en segundo plano...\n" +
                    $"En unos segundos el botón cambiará a '🌐 WebNodo (Online)' y la URL pública quedará lista y copiada al portapapeles.",
                    "WebNodo — Conectando túnel efímero",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No se pudo abrir el portal WebNodo:\n{ex.Message}",
                                "WebNodo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ═══════════════════════════════════════════════════════════
        // TÚNEL PÚBLICO EFÍMERO para WebNodo (cloudflared → SSH reverso)
        // ═══════════════════════════════════════════════════════════
        private async Task EstablecerTunelEfimeroAsync()
        {
            // Idempotente: si ya hay un túnel activo con URL, no volver a levantarlo
            if (_tunnelProcess != null && !_tunnelProcess.HasExited && !string.IsNullOrWhiteSpace(App.WebNodo.PublicTunnelUrl))
            {
                return;
            }

            try
            {
                // Barrido defensivo: si una ejecución anterior murió sin limpiar (p. ej. el
                // usuario "finalizó la tarea" desde el Administrador de tareas), aquí matamos
                // el cloudflared/ssh huérfano antes de crear uno nuevo.
                TunnelProcessRegistry.BarrerHuerfano(TunelWebNodoSlot);

                // Asegurar que WebNodo está activo antes de levantar el túnel
                if (!App.WebNodo.IsRunning)
                {
                    App.WebNodo.Start();
                }

                int webPort = App.WebNodo.ActivePort;

                // Opción 1: cloudflared (HTTPS sin advertencia de seguridad, ej. trycloudflare.com)
                string? cfExe = await BuscarODescargarCloudflaredAsync(CancellationToken.None);
                if (cfExe != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[Tunel] Probando cloudflared en puerto {webPort}...");
                    var res = await TryRunProcessTunnelAsync(
                        cfExe,
                        $"tunnel --url http://127.0.0.1:{webPort} --http-host-header 127.0.0.1:{webPort} --no-autoupdate",
                        @"https://[\w\-]+\.trycloudflare\.com",
                        35000,
                        CancellationToken.None);
                    if (res != null && !string.IsNullOrEmpty(res.Url))
                    {
                        _tunnelProcess = res.Process;
                        TunnelProcessRegistry.Persistir(TunelWebNodoSlot, res.Process.Id);
                        string pubUrl = res.Url;
                        Dispatcher.Invoke(() =>
                        {
                            TxtEstadoP2P.Text = "🌍 Túnel público activo";
                            App.WebNodo.PublicTunnelUrl = pubUrl;
                            GuardarP2PSessionConfig(); // Refrescar config con la URL del túnel
                            MostrarEnlacePublico(pubUrl);
                            BtnWebNodo.Content = "🌐 WebNodo (Online)";
                            BtnWebNodo.ToolTip = $"WebNodo público activo:\n{pubUrl}\nHaz clic para abrir en el navegador y copiar.";
                            BtnWebNodo.Background = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(6, 78, 59));
                            BtnWebNodo.BorderBrush = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(34, 197, 94));
                            try { Clipboard.SetText(pubUrl); } catch { }
                        });
                        return;
                    }
                }

                // Opción 2: SSH reverso (serveo.net / localhost.run)
                string sshExe = @"C:\Windows\System32\OpenSSH\ssh.exe";
                if (!File.Exists(sshExe)) sshExe = "ssh";

                var candidates = new[]
                {
                    new { Name = "serveo.net", Args = $"-o StrictHostKeyChecking=no -o UserKnownHostsFile=NUL -o ServerAliveInterval=30 -o ConnectTimeout=15 -R 80:127.0.0.1:{webPort} serveo.net", Pat = @"https://[\w\-]+\.serveo\.net" },
                    new { Name = "localhost.run", Args = $"-o StrictHostKeyChecking=no -o UserKnownHostsFile=NUL -o ServerAliveInterval=30 -o ConnectTimeout=15 -R 80:127.0.0.1:{webPort} nokey@localhost.run", Pat = @"https://[\w\-]+\.lhr\.life" }
                };

                foreach (var c in candidates)
                {
                    System.Diagnostics.Debug.WriteLine($"[Tunel] Probando {c.Name} en puerto {webPort}...");
                    var res = await TryRunProcessTunnelAsync(sshExe, c.Args, c.Pat, 23000, CancellationToken.None);
                    if (res != null && !string.IsNullOrEmpty(res.Url))
                    {
                        _tunnelProcess = res.Process;
                        TunnelProcessRegistry.Persistir(TunelWebNodoSlot, res.Process.Id);
                        string pubUrl = res.Url;
                        Dispatcher.Invoke(() =>
                        {
                            TxtEstadoP2P.Text = $"🌍 Túnel público ({c.Name})";
                            App.WebNodo.PublicTunnelUrl = pubUrl;
                            GuardarP2PSessionConfig(); // Refrescar config con la URL del túnel
                            MostrarEnlacePublico(pubUrl);
                            BtnWebNodo.Content = "🌐 WebNodo (Online)";
                            BtnWebNodo.ToolTip = $"WebNodo público ({c.Name}):\n{pubUrl}\nHaz clic para abrir en el navegador y copiar.";
                            BtnWebNodo.Background = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(6, 78, 59));
                            BtnWebNodo.BorderBrush = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Color.FromRgb(34, 197, 94));
                            try { Clipboard.SetText(pubUrl); } catch { }
                        });
                        return;
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    TxtEstadoP2P.Text = "🟢 WebNodo local (sin túnel público)";
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Tunel] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Detiene de forma forzosa y limpia el proceso del túnel del portal (cloudflared/ssh):
        /// mata todo su árbol de procesos y olvida su PID persistido. Idempotente.
        /// </summary>
        private void DetenerTunelWebNodo()
        {
            TunnelProcessRegistry.MatarYOlvidar(TunelWebNodoSlot, _tunnelProcess);
            _tunnelProcess = null;
        }

        private async Task<Visor3D.Services.P2PIPFS.EphemeralTunnelResult?> TryRunProcessTunnelAsync(string exe, string args, string urlPattern, int timeoutMs, CancellationToken token)
        {
            return await Visor3D.Services.P2PIPFS.EphemeralTunnelRunner.StartAsync(exe, args, urlPattern, timeoutMs, token);
        }

        private async Task<string?> BuscarODescargarCloudflaredAsync(CancellationToken token)
        {
            string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string appBase = AppDomain.CurrentDomain.BaseDirectory;
            string localAppDataWold = Path.Combine(localApp, "WoldVirtualP2P3D");
            string tempDir = Path.GetTempPath();

            string[] candidateLocations = {
                "cloudflared",
                Path.Combine(appBase, "cloudflared.exe"),
                Path.Combine(localAppDataWold, "cloudflared.exe"),
                Path.Combine(tempDir, "cloudflared.exe"),
                Path.Combine(localApp, "Programs", "cloudflared", "cloudflared.exe"),
                Path.Combine(progFiles, "Cloudflare", "cloudflared.exe"),
                @"C:\ProgramData\chocolatey\bin\cloudflared.exe",
                Path.Combine(localApp, "Microsoft", "WinGet", "Links", "cloudflared.exe")
            };

            foreach (var loc in candidateLocations)
            {
                try
                {
                    if (loc == "cloudflared" || File.Exists(loc))
                    {
                        using var test = Process.Start(new ProcessStartInfo
                        {
                            FileName = loc,
                            Arguments = "--version",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        });
                        if (test != null && test.WaitForExit(3000) && test.ExitCode == 0)
                            return loc;
                    }
                }
                catch { }
            }

            // Descargar cloudflared si no está instalado (a LocalAppData para asegurar permisos de escritura)
            try
            {
                Directory.CreateDirectory(localAppDataWold);
                string downloadTarget = Path.Combine(localAppDataWold, "cloudflared.exe");

                const string cfUrl = "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe";
                using var dl = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
                dl.DefaultRequestHeaders.UserAgent.ParseAdd("WoldVirtualP2P/1.0");
                using var resp = await dl.GetAsync(cfUrl, HttpCompletionOption.ResponseHeadersRead, token);
                if (resp.IsSuccessStatusCode)
                {
                    await using var fs = File.Create(downloadTarget);
                    await resp.Content.CopyToAsync(fs, token);
                    return downloadTarget;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Cloudflare-Download] {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Polling para detectar solicitudes de validación de túnel SSH desde LucIA (Godot).
        /// Cuando Godot crea tunel_solicitud.json, se muestra diálogo de confirmación al usuario.
        /// </summary>
        private void PollSolicitudValidacionTunel()
        {
            if (_validacionTunelEnCurso || string.IsNullOrEmpty(_rutaDatosGodot))
                return;

            var rutaSolicitud = Path.Combine(_rutaDatosGodot, "tunel_solicitud.json");
            if (!File.Exists(rutaSolicitud))
                return;

            try
            {
                var json = File.ReadAllText(rutaSolicitud);
                var datos = System.Text.Json.JsonDocument.Parse(json);
                var accion = datos.RootElement.GetProperty("accion").GetString();

                if (accion == "iniciar_tunel_ssh")
                {
                    _validacionTunelEnCurso = true;
                    File.Delete(rutaSolicitud);  // Eliminar solicitud procesada

                    // Mostrar diálogo de confirmación manual
                    Dispatcher.Invoke(() => MostrarDialogoConfirmacionTunel());
                }
                else if (accion == "conectar")
                {
                    // Botón «CONECTAR» de la oficina de LucIA: valida la conexión
                    // y levanta el túnel sin diálogo adicional.
                    _validacionTunelEnCurso = true;
                    File.Delete(rutaSolicitud);
                    Dispatcher.Invoke(() => TxtEstadoP2P.Text = "🔗 Validando conexión con LucIA…");
                    Dispatcher.BeginInvoke(new Action(() => { _ = ConectarOficinaAsync(); }));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PollValidacion] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Muestra diálogo de confirmación al usuario antes de iniciar el túnel SSH.
        /// </summary>
        private void MostrarDialogoConfirmacionTunel()
        {
            var resultado = MessageBox.Show(
                "LucIA solicita validar tu conexión mediante un túnel SSH efímero.\n\n" +
                "Esto permitirá que otros usuarios se conecten a tu sesión de forma segura.\n\n" +
                "¿Deseas iniciar el túnel SSH?",
                "Validación de conexión - LucIA",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (resultado == MessageBoxResult.Yes)
            {
                Task.Run(async () => await IniciarTunelSSHManual());
            }
            else
            {
                // Usuario rechazó: escribir resultado de error
                EscribirResultadoValidacion(false, "Usuario rechazó el túnel SSH");
                _validacionTunelEnCurso = false;
            }
        }

        /// <summary>
        /// Conexión solicitada desde el botón CONECTAR de la oficina de LucIA.
        /// Levanta el túnel efímero de WebNodo y devuelve el resultado a Godot.
        /// </summary>
        private async Task ConectarOficinaAsync()
        {
            try
            {
                await EstablecerTunelEfimeroAsync();
                bool ok = !string.IsNullOrWhiteSpace(_urlPublica);
                EscribirResultadoValidacion(ok,
                    ok ? $"Túnel WebNodo activo: {_urlPublica}" : "No se pudo activar el túnel WebNodo");
                Dispatcher.Invoke(() =>
                {
                    TxtEstadoP2P.Text = ok ? $"🟢 Conectado · {_urlPublica}" : "🔴 Sin túnel WebNodo";
                });
            }
            catch (Exception ex)
            {
                EscribirResultadoValidacion(false, $"Error al conectar: {ex.Message}");
            }
            finally
            {
                _validacionTunelEnCurso = false;
            }
        }

        /// <summary>
        /// Inicia el túnel SSH efímero usando P2PTunnelManager.
        /// </summary>
        private async Task IniciarTunelSSHManual()
        {
            try
            {
                // Iniciar el servicio P2P si no está activo
                if (_p2pServicio == null)
                {
                    _p2pServicio = new RedP2P.RedP2PServicio("LucIA-Host", 7890);
                    await _p2pServicio.IniciarAsync();
                }

                // Esperar a que el túnel esté activo
                var timeout = TimeSpan.FromSeconds(30);
                var inicio = DateTime.UtcNow;

                while (_p2pServicio.TunnelActivo == false && DateTime.UtcNow - inicio < timeout)
                {
                    await Task.Delay(500);
                }

                if (_p2pServicio.TunnelActivo)
                {
                    // Escribir resultado exitoso
                    EscribirResultadoValidacion(true, "Túnel SSH iniciado correctamente");
                    Dispatcher.Invoke(() =>
                    {
                        TxtEstadoP2P.Text = $"🟢 Túnel SSH activo: {_p2pServicio.UrlPublicaPropia}";
                    });
                }
                else
                {
                    EscribirResultadoValidacion(false, "Timeout: no se pudo establecer el túnel SSH");
                }
            }
            catch (Exception ex)
            {
                EscribirResultadoValidacion(false, $"Error al iniciar túnel: {ex.Message}");
            }
            finally
            {
                _validacionTunelEnCurso = false;
            }
        }

        /// <summary>
        /// Escribe el resultado de la validación en validacion_resultado.json para que Godot lo lea.
        /// </summary>
        private void EscribirResultadoValidacion(bool exito, string mensaje)
        {
            try
            {
                if (string.IsNullOrEmpty(_rutaDatosGodot))
                    return;

                var rutaResultado = Path.Combine(_rutaDatosGodot, "validacion_resultado.json");
                var datos = new
                {
                    exito = exito,
                    mensaje = mensaje,
                    timestamp = DateTime.UtcNow.ToString("o")
                };

                var json = System.Text.Json.JsonSerializer.Serialize(datos, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(rutaResultado, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EscribirResultado] Error: {ex.Message}");
            }
        }
    }
}
