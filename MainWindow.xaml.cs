using SagaIngenieria.Modelos;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Media = System.Windows.Media;

namespace SagaIngenieria
{
    public class PuntoDeEnsayo
    {
        public double Tiempo { get; set; }
        public double Posicion { get; set; }
        public double Fuerza { get; set; }
        public double Velocidad { get; set; }
    }

    public partial class MainWindow : Window
    {
        private Simulador _miSimulador = new Simulador();

        List<double> bufferTiempo = new List<double>();
        List<double> bufferPos = new List<double>();
        List<double> bufferFuerza = new List<double>();
        List<double> bufferVel = new List<double>();

        Queue<double> filtroVelocidad = new Queue<double>();
        Queue<double> filtroFuerza = new Queue<double>();
        const int TAMANO_FILTRO = 8;

        List<PuntoDeEnsayo> _ensayoGrabado = new List<PuntoDeEnsayo>();
        List<PuntoDeEnsayo> _ensayoCargado = new List<PuntoDeEnsayo>();
        List<PuntoDeEnsayo> _ensayoReferencia = new List<PuntoDeEnsayo>();

        double tiempoActual = 0;
        double lastPos = 0;
        double lastTime = 0;

        double offsetPosicion = 0;
        double offsetFuerza = 0;

        double maxCompresion = 0;
        double maxExpansion = 0;

        // CORRECCIÓN 1: Variable para recordar la velocidad elegida
        double _frecuenciaObjetivo = 1.0;

        DateTime _inicioGrabacion;
        int _contadorCiclos = 0;
        bool _cicloPositivo = false;

        bool _motorEncendido = false;
        bool _grabando = false;
        bool _viendoHistorial = false;
        string _modoGrafico = "FvsD";

        public MainWindow()
        {
            InitializeComponent();
            InicializarBaseDeDatos();
            ConfigurarGraficoInicial();
            _miSimulador.NuevosDatosRecibidos += ProcesarDatosFisicos;
        }

        // --- CONTROLES VENTANA ---
        private void btnMinimizar_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void btnMaximizar_Click(object sender, RoutedEventArgs e) => this.WindowState = (this.WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        private void btnSalir_Click(object sender, RoutedEventArgs e)
        {
            if (_motorEncendido || _grabando)
            {
                if (MessageBox.Show("Motor activo. ¿Salir?", "Precaución", MessageBoxButton.YesNo) == MessageBoxResult.No) return;
            }
            Application.Current.Shutdown();
        }

        // --- CONFIGURACIÓN ---
        private void btnConfig_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new VentanaConfiguracion();
            ventana.ConfiguracionAceptada += AplicarConfiguracion;
            ventana.ShowDialog();
        }

        private void AplicarConfiguracion(List<double> listaVelocidades)
        {
            double hzInicial = listaVelocidades[0];

            // Guardamos la velocidad real
            _frecuenciaObjetivo = hzInicial;

            _miSimulador.SetFrecuencia(hzInicial);

            MessageBox.Show($"Máquina configurada a {hzInicial} Hz.\n(Barrido cargado: {listaVelocidades.Count} pasos)");

            // Actualizamos el texto inmediatamente (incluso si el motor está apagado)
            txtFreq.Text = _frecuenciaObjetivo.ToString("F1");
        }

        private void InicializarBaseDeDatos()
        {
            try
            {
                using (var db = new Modelos.SagaContext())
                {
                    db.Database.EnsureCreated();
                    if (!db.Vehiculos.Any())
                    {
                        var cliente = new Modelos.Cliente { Nombre = "Taller SAGA", Email = "info@saga.com" };
                        db.Clientes.Add(cliente);
                        db.Vehiculos.Add(new Modelos.Vehiculo { Marca = "Genérico", Modelo = "Banco", Cliente = cliente });
                        db.SaveChanges();
                    }
                }
            }
            catch { }
        }

        private void ConfigurarGraficoInicial()
        {
            GraficoPrincipal.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#000000");
            GraficoPrincipal.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#111111");
            GraficoPrincipal.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#333333");
            GraficoPrincipal.Plot.Axes.Color(ScottPlot.Color.FromHex("#AAAAAA"));
            GraficoPrincipal.Plot.Axes.Title.Label.Text = "SAGA - MONITOR V2";
            GraficoPrincipal.Plot.Axes.Title.Label.ForeColor = ScottPlot.Color.FromHex("#FFFFFF");

            GraficoPrincipal.Plot.ShowLegend();
            GraficoPrincipal.Plot.Legend.FontColor = ScottPlot.Color.FromHex("#FFFFFF");
            GraficoPrincipal.Plot.Legend.FontSize = 12;
            GraficoPrincipal.Plot.Legend.BackgroundColor = ScottPlot.Color.FromHex("#222222");
            GraficoPrincipal.Plot.Legend.OutlineColor = ScottPlot.Color.FromHex("#444444");
            GraficoPrincipal.Plot.Legend.Location = Alignment.UpperRight;
        }

        private void ProcesarDatosFisicos(double rawPos, double rawFuerza)
        {
            Dispatcher.Invoke(() =>
            {
                double posReal = rawPos - offsetPosicion;
                double fuerzaCruda = rawFuerza - offsetFuerza;
                double dt = (tiempoActual - lastTime);
                double velCruda = 0;
                if (dt > 0) velCruda = (posReal - lastPos) / dt;

                lastPos = posReal;
                lastTime = tiempoActual;

                // Filtros
                filtroVelocidad.Enqueue(velCruda);
                filtroFuerza.Enqueue(fuerzaCruda);
                if (filtroVelocidad.Count > TAMANO_FILTRO) filtroVelocidad.Dequeue();
                if (filtroFuerza.Count > TAMANO_FILTRO) filtroFuerza.Dequeue();

                double velocidadLimpia = filtroVelocidad.Count > 0 ? filtroVelocidad.Average() : velCruda;
                double fuerzaLimpia = filtroFuerza.Count > 0 ? filtroFuerza.Average() : fuerzaCruda;

                if (!_viendoHistorial)
                {
                    txtFuerza.Text = fuerzaLimpia.ToString("F1");
                    txtPosicion.Text = posReal.ToString("F1");
                    txtVelocidad.Text = velocidadLimpia.ToString("F1");

                    barFuerza.Value = Math.Abs(fuerzaLimpia);
                    barPosicion.Value = Math.Abs(posReal) + 50;
                    barVelocidad.Value = Math.Abs(velocidadLimpia);

                    if (fuerzaLimpia > maxCompresion) { maxCompresion = fuerzaLimpia; txtMaxComp.Text = maxCompresion.ToString("F1"); }
                    if (fuerzaLimpia < maxExpansion) { maxExpansion = fuerzaLimpia; txtMaxExpa.Text = maxExpansion.ToString("F1"); }

                    if (posReal > 0 && !_cicloPositivo) { _cicloPositivo = true; _contadorCiclos++; txtCiclos.Text = _contadorCiclos.ToString(); }
                    else if (posReal < 0) { _cicloPositivo = false; }

                    // CORRECCIÓN 2: Usamos la variable real en lugar del texto fijo "1.5"
                    txtFreq.Text = _motorEncendido ? _frecuenciaObjetivo.ToString("F1") : "0.0";

                    txtAmp.Text = _motorEncendido ? "100" : "0";

                    if (_motorEncendido && _grabando) txtCrono.Text = (DateTime.Now - _inicioGrabacion).ToString(@"hh\:mm\:ss");

                    if (_motorEncendido) { double temp = 45 + (_contadorCiclos * 0.1); if (temp > 80) temp = 80; txtTemp.Text = $"{temp:F1}°C"; }

                    bufferTiempo.Add(tiempoActual);
                    bufferPos.Add(posReal);
                    bufferFuerza.Add(fuerzaLimpia);
                    bufferVel.Add(velocidadLimpia);
                    tiempoActual += 10;

                    if (bufferTiempo.Count > 1000)
                    {
                        bufferTiempo.RemoveAt(0); bufferPos.RemoveAt(0); bufferFuerza.RemoveAt(0); bufferVel.RemoveAt(0);
                    }
                }

                if (_grabando)
                {
                    _ensayoGrabado.Add(new PuntoDeEnsayo { Tiempo = tiempoActual, Posicion = posReal, Fuerza = fuerzaLimpia, Velocidad = velocidadLimpia });
                }

                if (!_viendoHistorial) ActualizarGrafico();
            });
        }

        // ... (El resto del archivo sigue igual: CalcularCurvaPromedio, ActualizarGrafico, Botones, etc.)
        // Copia el resto de las funciones del archivo anterior para completarlo.

        private (double[] vel, double[] fza) CalcularCurvaPromedio(List<PuntoDeEnsayo> datos) { if (datos == null || datos.Count < 10) return (new double[0], new double[0]); var puntos = datos.Select(p => new { V = p.Velocidad, F = p.Fuerza }).ToList(); var puntosOrdenados = puntos.OrderBy(p => p.V).ToList(); int ventana = 20; var velSuave = new List<double>(); var fzaSuave = new List<double>(); for (int i = 0; i < puntosOrdenados.Count; i++) { int start = Math.Max(0, i - ventana / 2); int end = Math.Min(puntosOrdenados.Count, i + ventana / 2); int count = end - start; double sumV = 0; double sumF = 0; for (int j = start; j < end; j++) { sumV += puntosOrdenados[j].V; sumF += puntosOrdenados[j].F; } velSuave.Add(sumV / count); fzaSuave.Add(sumF / count); } return (velSuave.ToArray(), fzaSuave.ToArray()); }
        private void ActualizarGrafico() { GraficoPrincipal.Plot.Clear(); if (_ensayoReferencia != null && _ensayoReferencia.Count > 0) { double[] refX = null, refY = null; switch (_modoGrafico) { case "FvsD": refX = _ensayoReferencia.Select(p => p.Posicion).ToArray(); refY = _ensayoReferencia.Select(p => p.Fuerza).ToArray(); break; case "FvsV": refX = _ensayoReferencia.Select(p => p.Velocidad).ToArray(); refY = _ensayoReferencia.Select(p => p.Fuerza).ToArray(); break; case "FPromVsV": var res = CalcularCurvaPromedio(_ensayoReferencia); refX = res.vel; refY = res.fza; break; } if (refX != null && refY != null && refX.Length > 0) { var spRef = GraficoPrincipal.Plot.Add.Scatter(refX, refY); spRef.Color = ScottPlot.Color.FromHex("#444444"); spRef.LineWidth = 2; spRef.LinePattern = LinePattern.Dotted; spRef.Label = "Referencia (Fondo)"; } } var colorFuerza = ScottPlot.Color.FromHex("#FF4081"); var colorPos = ScottPlot.Color.FromHex("#00E5FF"); var colorVel = ScottPlot.Color.FromHex("#B2FF59"); var colorProm = ScottPlot.Color.FromHex("#FFA500"); var colorHistorial = ScottPlot.Color.FromHex("#FFD700"); double[] datosX = null; double[] datosY = null; double[] datosY2 = null; var fuenteDatos = _viendoHistorial ? _ensayoCargado : null; switch (_modoGrafico) { case "FvsD": datosX = _viendoHistorial ? fuenteDatos.Select(p => p.Posicion).ToArray() : bufferPos.ToArray(); datosY = _viendoHistorial ? fuenteDatos.Select(p => p.Fuerza).ToArray() : bufferFuerza.ToArray(); break; case "FvsV": datosX = _viendoHistorial ? fuenteDatos.Select(p => p.Velocidad).ToArray() : bufferVel.ToArray(); datosY = _viendoHistorial ? fuenteDatos.Select(p => p.Fuerza).ToArray() : bufferFuerza.ToArray(); break; case "FPromVsV": if (_viendoHistorial) { var res = CalcularCurvaPromedio(fuenteDatos); datosX = res.vel; datosY = res.fza; } else { var pts = new List<PuntoDeEnsayo>(); for (int i = 0; i < bufferVel.Count; i++) pts.Add(new PuntoDeEnsayo { Velocidad = bufferVel[i], Fuerza = bufferFuerza[i] }); var res = CalcularCurvaPromedio(pts); datosX = res.vel; datosY = res.fza; } break; case "FDvsT": datosX = _viendoHistorial ? fuenteDatos.Select(p => p.Tiempo).ToArray() : null; datosY = _viendoHistorial ? fuenteDatos.Select(p => p.Fuerza).ToArray() : bufferFuerza.ToArray(); datosY2 = _viendoHistorial ? fuenteDatos.Select(p => p.Posicion).ToArray() : bufferPos.ToArray(); break; case "FVvsT": datosX = _viendoHistorial ? fuenteDatos.Select(p => p.Tiempo).ToArray() : null; datosY = _viendoHistorial ? fuenteDatos.Select(p => p.Fuerza).ToArray() : bufferFuerza.ToArray(); datosY2 = _viendoHistorial ? fuenteDatos.Select(p => p.Velocidad).ToArray() : bufferVel.ToArray(); break; case "PicoVsV": datosX = _viendoHistorial ? fuenteDatos.Select(p => p.Velocidad).ToArray() : bufferVel.ToArray(); datosY = _viendoHistorial ? fuenteDatos.Select(p => p.Fuerza).ToArray() : bufferFuerza.ToArray(); break; } if (datosY == null && datosX == null) return; switch (_modoGrafico) { case "FvsD": var sp1 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY); sp1.Color = _viendoHistorial ? colorHistorial : colorFuerza; sp1.LineWidth = 2; sp1.Label = "Ciclo"; GraficoPrincipal.Plot.Title("Ciclo de Histéresis"); GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Posición (mm)"; GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)"; break; case "FvsV": var sp2 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY); sp2.Color = _viendoHistorial ? colorHistorial : colorVel; sp2.LineWidth = 2; sp2.Label = "Amortiguamiento"; GraficoPrincipal.Plot.Title("Fuerza vs Velocidad"); GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Velocidad (mm/s)"; GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)"; break; case "FPromVsV": var spProm = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY); spProm.Color = colorProm; spProm.LineWidth = 3; spProm.MarkerSize = 0; spProm.Label = "Promedio"; GraficoPrincipal.Plot.Title("Curva Característica (Promedio)"); GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Velocidad (mm/s)"; GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)"; break; case "FDvsT": case "FVvsT": if (_viendoHistorial) { var s1 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY); s1.Color = colorFuerza; s1.Axes.YAxis = GraficoPrincipal.Plot.Axes.Left; s1.Label = "Fuerza"; var s2 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY2); s2.Color = (_modoGrafico == "FDvsT" ? colorPos : colorVel); s2.Axes.YAxis = GraficoPrincipal.Plot.Axes.Right; s2.Label = (_modoGrafico == "FDvsT" ? "Posición" : "Velocidad"); } else { var s1 = GraficoPrincipal.Plot.Add.Signal(datosY); s1.Color = colorFuerza; s1.Axes.YAxis = GraficoPrincipal.Plot.Axes.Left; s1.Label = "Fuerza"; var s2 = GraficoPrincipal.Plot.Add.Signal(datosY2); s2.Color = (_modoGrafico == "FDvsT" ? colorPos : colorVel); s2.Axes.YAxis = GraficoPrincipal.Plot.Axes.Right; s2.Label = (_modoGrafico == "FDvsT" ? "Posición" : "Velocidad"); } GraficoPrincipal.Plot.Title("Dominio del Tiempo"); GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Tiempo (ms)"; GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)"; GraficoPrincipal.Plot.Axes.Right.Label.Text = (_modoGrafico == "FDvsT" ? "Posición (mm)" : "Velocidad (mm/s)"); break; case "PicoVsV": var sp3 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY); sp3.Color = ScottPlot.Color.FromHex("#FFFFFF"); sp3.MarkerSize = 2; sp3.LineWidth = 0; sp3.Label = "Picos"; GraficoPrincipal.Plot.Title("Puntos Pico"); GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Velocidad (mm/s)"; GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)"; break; } GraficoPrincipal.Plot.Axes.AutoScale(); GraficoPrincipal.Refresh(); }
        private void CambiarGrafico_Checked(object sender, RoutedEventArgs e) { if (sender is RadioButton rb && rb.Tag != null) { _modoGrafico = rb.Tag.ToString(); if (_viendoHistorial || _ensayoReferencia.Count > 0) ActualizarGrafico(); } }
        private void btnMotor_Click(object sender, RoutedEventArgs e) { if (!_motorEncendido) { _viendoHistorial = false; InfoBox.Visibility = Visibility.Collapsed; _miSimulador.Iniciar(); _motorEncendido = true; if (txtBtnMotor != null) txtBtnMotor.Text = "APAGAR"; txtEstado.Text = "MOTOR ENCENDIDO - MODO VIVO"; txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Yellow); btnGrabar.IsEnabled = true; PanelGuardado.Visibility = Visibility.Collapsed; _inicioGrabacion = DateTime.Now; _contadorCiclos = 0; } else { _miSimulador.Detener(); _motorEncendido = false; if (_grabando) btnGuardar_Click(null, null); if (txtBtnMotor != null) txtBtnMotor.Text = "ENCENDER"; txtEstado.Text = "DETENIDO"; txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Gray); btnGrabar.IsEnabled = false; btnGuardar.IsEnabled = false; } }
        private void btnGrabar_Click(object sender, RoutedEventArgs e) { _ensayoGrabado.Clear(); _grabando = true; _viendoHistorial = false; InfoBox.Visibility = Visibility.Collapsed; PanelGuardado.Visibility = Visibility.Collapsed; btnGrabar.IsEnabled = false; btnGrabar.Opacity = 0.5; btnGuardar.IsEnabled = true; btnGuardar.Opacity = 1; txtEstado.Text = "● GRABANDO DATOS..."; txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Red); _inicioGrabacion = DateTime.Now; maxCompresion = 0; maxExpansion = 0; }
        private void btnGuardar_Click(object sender, RoutedEventArgs e) { _grabando = false; btnGrabar.IsEnabled = false; btnGrabar.Opacity = 0.5; btnGuardar.IsEnabled = false; btnGuardar.Opacity = 0.5; PanelGuardado.Visibility = Visibility.Visible; txtEstado.Text = "ESPERANDO DATOS..."; txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Cyan); }
        private void btnConfirmarGuardado_Click(object sender, RoutedEventArgs e) { PanelGuardado.Visibility = Visibility.Collapsed; string nombreCliente = txtInputCliente.Text; string modeloVehiculo = txtInputVehiculo.Text; string notasEnsayo = txtInputNotas.Text; txtEstado.Text = "GUARDANDO..."; var datosParaGuardar = new List<PuntoDeEnsayo>(_ensayoGrabado); Task.Run(() => { try { string json = System.Text.Json.JsonSerializer.Serialize(datosParaGuardar); byte[] datosBytes = System.Text.Encoding.UTF8.GetBytes(json); using (var db = new Modelos.SagaContext()) { var cliente = db.Clientes.FirstOrDefault(c => c.Nombre == nombreCliente); if (cliente == null) { cliente = new Modelos.Cliente { Nombre = nombreCliente, Email = "N/A" }; db.Clientes.Add(cliente); db.SaveChanges(); } var vehiculo = db.Vehiculos.FirstOrDefault(v => v.Modelo == modeloVehiculo && v.ClienteId == cliente.Id); if (vehiculo == null) { vehiculo = new Modelos.Vehiculo { Marca = "SAGA", Modelo = modeloVehiculo, ClienteId = cliente.Id }; db.Vehiculos.Add(vehiculo); db.SaveChanges(); } var ensayo = new Modelos.Ensayo { Fecha = DateTime.Now, Notas = notasEnsayo, MaxCompresion = maxCompresion, MaxExpansion = maxExpansion, VehiculoId = vehiculo.Id, DatosCrudos = datosBytes }; db.Ensayos.Add(ensayo); db.SaveChanges(); } Dispatcher.Invoke(() => { btnGrabar.IsEnabled = true; btnGrabar.Opacity = 1; MessageBox.Show($"¡Ensayo Guardado!"); txtEstado.Text = "GUARDADO OK"; InfoBox.Visibility = Visibility.Visible; lblCliente.Text = $"Cliente: {nombreCliente}"; lblVehiculo.Text = $"Vehículo: {modeloVehiculo}"; lblFecha.Text = $"Fecha: {DateTime.Now}"; lblNotas.Text = notasEnsayo; _ensayoCargado = new List<PuntoDeEnsayo>(datosParaGuardar); _viendoHistorial = true; ActualizarGrafico(); }); } catch (Exception ex) { Dispatcher.Invoke(() => { MessageBox.Show("Error: " + ex.Message); btnGrabar.IsEnabled = true; }); } }); }
        private void btnCancelarGuardado_Click(object sender, RoutedEventArgs e) { PanelGuardado.Visibility = Visibility.Collapsed; btnGrabar.IsEnabled = true; btnGrabar.Opacity = 1; txtEstado.Text = "GUARDADO CANCELADO"; txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Orange); }
        private void btnCalibrarCero_Click(object sender, RoutedEventArgs e) { if (bufferPos.Count > 0) { offsetPosicion += bufferPos.Last(); offsetFuerza += bufferFuerza.Last(); MessageBox.Show("Cero Establecido"); } }
        private void btnQuitarRef_Click(object sender, RoutedEventArgs e) { _ensayoReferencia.Clear(); RefBox.Visibility = Visibility.Collapsed; ActualizarGrafico(); }
        private void btnNuevo_Click(object sender, RoutedEventArgs e) { _viendoHistorial = false; _ensayoCargado.Clear(); InfoBox.Visibility = Visibility.Collapsed; PanelGuardado.Visibility = Visibility.Collapsed; bufferTiempo.Clear(); bufferPos.Clear(); bufferFuerza.Clear(); bufferVel.Clear(); maxCompresion = 0; maxExpansion = 0; GraficoPrincipal.Plot.Clear(); GraficoPrincipal.Plot.Title("MONITOR EN VIVO"); GraficoPrincipal.Refresh(); txtEstado.Text = "LISTO"; }
        private void btnCargar_Click(object sender, RoutedEventArgs e) { var ventana = new VentanaHistorial(); ventana.EnsayoSeleccionado += CargarEnsayoEnPantalla; ventana.EnsayoParaReferencia += CargarReferenciaEnPantalla; ventana.ShowDialog(); }
        private void CargarEnsayoEnPantalla(Modelos.Ensayo ensayo) { var puntos = DeserializarEnsayo(ensayo); if (puntos != null) { _ensayoCargado = puntos; _viendoHistorial = true; PanelGuardado.Visibility = Visibility.Collapsed; txtMaxComp.Text = ensayo.MaxCompresion.ToString("F1"); txtMaxExpa.Text = ensayo.MaxExpansion.ToString("F1"); InfoBox.Visibility = Visibility.Visible; lblCliente.Text = $"Cliente: {ensayo.Vehiculo?.Cliente?.Nombre ?? "N/A"}"; lblVehiculo.Text = $"Vehículo: {ensayo.Vehiculo?.Modelo ?? "N/A"}"; lblFecha.Text = $"Fecha: {ensayo.Fecha.ToShortDateString()}"; lblNotas.Text = string.IsNullOrEmpty(ensayo.Notas) ? "Sin notas" : ensayo.Notas; ActualizarGrafico(); txtEstado.Text = "VISUALIZANDO HISTORIAL"; txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Cyan); } }
        private void CargarReferenciaEnPantalla(Modelos.Ensayo ensayo) { var puntos = DeserializarEnsayo(ensayo); if (puntos != null) { _ensayoReferencia = puntos; RefBox.Visibility = Visibility.Visible; lblRefCliente.Text = $"Cliente: {ensayo.Vehiculo?.Cliente?.Nombre ?? "N/A"}"; lblRefVehiculo.Text = $"Vehículo: {ensayo.Vehiculo?.Modelo ?? "N/A"}"; lblRefFecha.Text = $"Fecha: {ensayo.Fecha.ToShortDateString()}"; ActualizarGrafico(); } }
        private List<PuntoDeEnsayo> DeserializarEnsayo(Modelos.Ensayo ensayo) { try { if (ensayo.DatosCrudos == null || ensayo.DatosCrudos.Length == 0) return null; string json = System.Text.Encoding.UTF8.GetString(ensayo.DatosCrudos); if (string.IsNullOrWhiteSpace(json)) return null; return System.Text.Json.JsonSerializer.Deserialize<List<PuntoDeEnsayo>>(json); } catch { MessageBox.Show("Error al leer archivo"); return null; } }
        private void btnImprimir_Click(object sender, RoutedEventArgs e) { Modelos.Ensayo ensayoAImprimir = null; if (_viendoHistorial && _ensayoCargado.Count > 0) { ensayoAImprimir = new Modelos.Ensayo { Id = 0, Fecha = DateTime.Parse(lblFecha.Text.Replace("Fecha: ", "")), Notas = lblNotas.Text, MaxCompresion = double.Parse(txtMaxComp.Text), MaxExpansion = double.Parse(txtMaxExpa.Text), Vehiculo = new Modelos.Vehiculo { Modelo = lblVehiculo.Text.Replace("Vehículo: ", ""), Cliente = new Modelos.Cliente { Nombre = lblCliente.Text.Replace("Cliente: ", "") } }, DatosCrudos = System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(_ensayoCargado)) }; } else if (!_motorEncendido && _ensayoGrabado.Count > 0) { if (MessageBox.Show("Imprimir ensayo no guardado?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.No) return; ensayoAImprimir = new Modelos.Ensayo { Fecha = DateTime.Now, Notas = "Ensayo temporal", MaxCompresion = maxCompresion, MaxExpansion = maxExpansion, Vehiculo = new Modelos.Vehiculo { Modelo = "En Vivo", Cliente = new Modelos.Cliente { Nombre = "Taller" } }, DatosCrudos = System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(_ensayoGrabado)) }; } else { MessageBox.Show("Carga un ensayo primero."); return; } var saveDialog = new Microsoft.Win32.SaveFileDialog { FileName = $"Reporte_SAGA_{DateTime.Now:yyyyMMdd_HHmm}.pdf", Filter = "Documento PDF|*.pdf" }; if (saveDialog.ShowDialog() == true) { try { GeneradorReporte.Generar(ensayoAImprimir, saveDialog.FileName); if (MessageBox.Show("Reporte listo. ¿Abrir?", "PDF", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true } }.Start(); } } catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); } } }
        private void btnExportar_Click(object sender, RoutedEventArgs e) { List<PuntoDeEnsayo> datos = null; if (_viendoHistorial) datos = _ensayoCargado; else if (_ensayoGrabado.Count > 0) datos = _ensayoGrabado; if (datos == null || datos.Count == 0) { MessageBox.Show("No hay datos para exportar."); return; } var saveDialog = new Microsoft.Win32.SaveFileDialog { FileName = $"Datos_SAGA_{DateTime.Now:yyyyMMdd_HHmm}.csv", Filter = "Archivo CSV (Excel)|*.csv" }; if (saveDialog.ShowDialog() == true) { try { var sb = new StringBuilder(); sb.AppendLine("Tiempo(s);Posicion(mm);Fuerza(kg);Velocidad(mm/s)"); foreach (var p in datos) sb.AppendLine($"{p.Tiempo:F3};{p.Posicion:F2};{p.Fuerza:F2};{p.Velocidad:F2}"); System.IO.File.WriteAllText(saveDialog.FileName, sb.ToString()); MessageBox.Show("Exportado a Excel correctamente."); } catch (Exception ex) { MessageBox.Show("Error: " + ex.Message); } } }
    }
}

