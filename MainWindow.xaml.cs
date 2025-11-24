using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; // Para poder arrastrar la ventana
using Media = System.Windows.Media;
using ScottPlot;
using System.Linq;
using System.Threading.Tasks;

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
        // --- 1. CEREBRO Y MEMORIA ---
        private Simulador _miSimulador = new Simulador();

        List<double> bufferTiempo = new List<double>();
        List<double> bufferPos = new List<double>();
        List<double> bufferFuerza = new List<double>();
        List<double> bufferVel = new List<double>();

        List<PuntoDeEnsayo> _ensayoGrabado = new List<PuntoDeEnsayo>();
        List<PuntoDeEnsayo> _ensayoCargado = new List<PuntoDeEnsayo>();

        double tiempoActual = 0;
        double lastPos = 0;
        double lastTime = 0;

        double offsetPosicion = 0;
        double offsetFuerza = 0;

        double maxCompresion = 0;
        double maxExpansion = 0;

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

        // --- NUEVO: Permitir mover la ventana sin barra de título ---
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        // --- NUEVO: Botón Salir ---
        private void btnSalir_Click(object sender, RoutedEventArgs e)
        {
            if (_motorEncendido || _grabando)
            {
                if (MessageBox.Show("El motor está encendido o grabando. ¿Seguro que quieres salir forzosamente?",
                    "¡Precaución!", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }
            }
            Application.Current.Shutdown();
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
                        db.Vehiculos.Add(new Modelos.Vehiculo { Marca = "Genérico", Modelo = "Banco de Pruebas", Cliente = cliente });
                        db.SaveChanges();
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Error iniciando BD: " + ex.Message); }
        }

        private void ConfigurarGraficoInicial()
        {
            GraficoPrincipal.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#000000");
            GraficoPrincipal.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#111111");
            GraficoPrincipal.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#333333");
            GraficoPrincipal.Plot.Axes.Color(ScottPlot.Color.FromHex("#AAAAAA"));
            GraficoPrincipal.Plot.Axes.Title.Label.Text = "SAGA INGENIERÍA - MONITOR";
            GraficoPrincipal.Plot.Axes.Title.Label.ForeColor = ScottPlot.Color.FromHex("#FFFFFF");
        }

        private void ProcesarDatosFisicos(double rawPos, double rawFuerza)
        {
            Dispatcher.Invoke(() =>
            {
                double posReal = rawPos - offsetPosicion;
                double fuerzaReal = rawFuerza - offsetFuerza;
                double dt = (tiempoActual - lastTime);
                double velocidad = 0;
                if (dt > 0) velocidad = (posReal - lastPos) / dt;

                lastPos = posReal;
                lastTime = tiempoActual;

                if (!_viendoHistorial)
                {
                    txtFuerza.Text = fuerzaReal.ToString("F1");
                    txtPosicion.Text = posReal.ToString("F1");
                    txtVelocidad.Text = velocidad.ToString("F1");

                    if (fuerzaReal > maxCompresion) { maxCompresion = fuerzaReal; txtMaxComp.Text = maxCompresion.ToString("F1"); }
                    if (fuerzaReal < maxExpansion) { maxExpansion = fuerzaReal; txtMaxExpa.Text = maxExpansion.ToString("F1"); }

                    bufferTiempo.Add(tiempoActual);
                    bufferPos.Add(posReal);
                    bufferFuerza.Add(fuerzaReal);
                    bufferVel.Add(velocidad);
                    tiempoActual += 10;

                    if (bufferTiempo.Count > 500)
                    {
                        bufferTiempo.RemoveAt(0);
                        bufferPos.RemoveAt(0);
                        bufferFuerza.RemoveAt(0);
                        bufferVel.RemoveAt(0);
                    }
                }

                if (_grabando)
                {
                    _ensayoGrabado.Add(new PuntoDeEnsayo
                    {
                        Tiempo = tiempoActual,
                        Posicion = posReal,
                        Fuerza = fuerzaReal,
                        Velocidad = velocidad
                    });
                }

                if (!_viendoHistorial) ActualizarGrafico();
            });
        }

        private void ActualizarGrafico()
        {
            GraficoPrincipal.Plot.Clear();

            var colorFuerza = ScottPlot.Color.FromHex("#FF4081");
            var colorPos = ScottPlot.Color.FromHex("#00E5FF");
            var colorVel = ScottPlot.Color.FromHex("#B2FF59");
            var colorHistorial = ScottPlot.Color.FromHex("#FFD700");

            double[] datosX = null;
            double[] datosY = null;
            double[] datosY2 = null;

            if (_viendoHistorial)
            {
                if (_ensayoCargado.Count == 0) return;

                switch (_modoGrafico)
                {
                    case "FvsD":
                        datosX = _ensayoCargado.Select(p => p.Posicion).ToArray();
                        datosY = _ensayoCargado.Select(p => p.Fuerza).ToArray();
                        break;
                    case "FvsV":
                        datosX = _ensayoCargado.Select(p => p.Velocidad).ToArray();
                        datosY = _ensayoCargado.Select(p => p.Fuerza).ToArray();
                        break;
                    case "FDvsT":
                        datosX = _ensayoCargado.Select(p => p.Tiempo).ToArray();
                        datosY = _ensayoCargado.Select(p => p.Fuerza).ToArray();
                        datosY2 = _ensayoCargado.Select(p => p.Posicion).ToArray();
                        break;
                    case "FVvsT":
                        datosX = _ensayoCargado.Select(p => p.Tiempo).ToArray();
                        datosY = _ensayoCargado.Select(p => p.Fuerza).ToArray();
                        datosY2 = _ensayoCargado.Select(p => p.Velocidad).ToArray();
                        break;
                    case "PicoVsV":
                        datosX = _ensayoCargado.Select(p => p.Velocidad).ToArray();
                        datosY = _ensayoCargado.Select(p => p.Fuerza).ToArray();
                        break;
                }
            }
            else
            {
                switch (_modoGrafico)
                {
                    case "FvsD": datosX = bufferPos.ToArray(); datosY = bufferFuerza.ToArray(); break;
                    case "FvsV": datosX = bufferVel.ToArray(); datosY = bufferFuerza.ToArray(); break;
                    case "FDvsT": datosY = bufferFuerza.ToArray(); datosY2 = bufferPos.ToArray(); break;
                    case "FVvsT": datosY = bufferFuerza.ToArray(); datosY2 = bufferVel.ToArray(); break;
                    case "PicoVsV": datosX = bufferVel.ToArray(); datosY = bufferFuerza.ToArray(); break;
                }
            }

            if (datosY == null && datosX == null) return;

            switch (_modoGrafico)
            {
                case "FvsD":
                    var sp1 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY);
                    sp1.Color = _viendoHistorial ? colorHistorial : colorFuerza;
                    sp1.LineWidth = 2;
                    GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Posición (mm)";
                    GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)";
                    GraficoPrincipal.Plot.Title(_viendoHistorial ? "HISTORIAL: Ciclo de Histéresis" : "VIVO: Ciclo de Histéresis");
                    break;

                case "FvsV":
                    var sp2 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY);
                    sp2.Color = _viendoHistorial ? colorHistorial : colorVel;
                    sp2.LineWidth = 2;
                    GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Velocidad (mm/s)";
                    GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)";
                    GraficoPrincipal.Plot.Title("Análisis de Amortiguamiento");
                    break;

                case "FDvsT":
                    if (_viendoHistorial)
                    {
                        var s1 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY); s1.Color = colorFuerza; s1.Axes.YAxis = GraficoPrincipal.Plot.Axes.Left;
                        var s2 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY2); s2.Color = colorPos; s2.Axes.YAxis = GraficoPrincipal.Plot.Axes.Right;
                    }
                    else
                    {
                        var s1 = GraficoPrincipal.Plot.Add.Signal(datosY); s1.Color = colorFuerza; s1.Axes.YAxis = GraficoPrincipal.Plot.Axes.Left;
                        var s2 = GraficoPrincipal.Plot.Add.Signal(datosY2); s2.Color = colorPos; s2.Axes.YAxis = GraficoPrincipal.Plot.Axes.Right;
                    }
                    GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)";
                    GraficoPrincipal.Plot.Axes.Right.Label.Text = "Posición (mm)";
                    GraficoPrincipal.Plot.Title("Dominio del Tiempo");
                    break;

                case "FVvsT":
                    if (_viendoHistorial)
                    {
                        var s1 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY); s1.Color = colorFuerza; s1.Axes.YAxis = GraficoPrincipal.Plot.Axes.Left;
                        var s2 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY2); s2.Color = colorVel; s2.Axes.YAxis = GraficoPrincipal.Plot.Axes.Right;
                    }
                    else
                    {
                        var s1 = GraficoPrincipal.Plot.Add.Signal(datosY); s1.Color = colorFuerza; s1.Axes.YAxis = GraficoPrincipal.Plot.Axes.Left;
                        var s2 = GraficoPrincipal.Plot.Add.Signal(datosY2); s2.Color = colorVel; s2.Axes.YAxis = GraficoPrincipal.Plot.Axes.Right;
                    }
                    GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)";
                    GraficoPrincipal.Plot.Axes.Right.Label.Text = "Velocidad (mm/s)";
                    GraficoPrincipal.Plot.Title("Análisis de Fase");
                    break;

                case "PicoVsV":
                    var sp3 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY);
                    sp3.Color = ScottPlot.Color.FromHex("#FFFFFF");
                    sp3.MarkerSize = 2;
                    sp3.LineWidth = 0;
                    GraficoPrincipal.Plot.Title("Puntos de Fuerza Pico");
                    break;
            }

            GraficoPrincipal.Plot.Axes.AutoScale();
            GraficoPrincipal.Refresh();
        }

        private void CambiarGrafico_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag != null)
            {
                _modoGrafico = rb.Tag.ToString();
                if (_viendoHistorial) ActualizarGrafico();
            }
        }

        private void btnMotor_Click(object sender, RoutedEventArgs e)
        {
            if (!_motorEncendido)
            {
                _viendoHistorial = false;
                InfoBox.Visibility = Visibility.Collapsed; // Ocultamos la info de historial

                _miSimulador.Iniciar();
                _motorEncendido = true;
                if (txtBtnMotor != null) txtBtnMotor.Text = "APAGAR";
                txtEstado.Text = "MOTOR ENCENDIDO";
                txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Yellow);
                btnGrabar.IsEnabled = true;
            }
            else
            {
                _miSimulador.Detener();
                _motorEncendido = false;
                if (_grabando) btnGuardar_Click(null, null);
                if (txtBtnMotor != null) txtBtnMotor.Text = "ENCENDER";
                txtEstado.Text = "DETENIDO";
                txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Gray);
                btnGrabar.IsEnabled = false;
                btnGuardar.IsEnabled = false;
            }
        }

        private void btnGrabar_Click(object sender, RoutedEventArgs e)
        {
            _ensayoGrabado.Clear();
            _grabando = true;
            _viendoHistorial = false;
            InfoBox.Visibility = Visibility.Collapsed;

            btnGrabar.IsEnabled = false;
            btnGrabar.Opacity = 0.5;
            btnGuardar.IsEnabled = true;
            btnGuardar.Opacity = 1;

            txtEstado.Text = "● GRABANDO DATOS...";
            txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Red);

            maxCompresion = 0;
            maxExpansion = 0;
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            _grabando = false;
            btnGrabar.IsEnabled = true;
            btnGrabar.Opacity = 1;
            btnGuardar.IsEnabled = false;
            btnGuardar.Opacity = 0.5;

            txtEstado.Text = "GUARDANDO...";

            var datosParaGuardar = new List<PuntoDeEnsayo>(_ensayoGrabado);

            Task.Run(() =>
            {
                try
                {
                    string json = System.Text.Json.JsonSerializer.Serialize(datosParaGuardar);
                    byte[] datosBytes = System.Text.Encoding.UTF8.GetBytes(json);

                    using (var db = new Modelos.SagaContext())
                    {
                        var ensayo = new Modelos.Ensayo
                        {
                            Fecha = DateTime.Now,
                            Notas = $"Ensayo {datosParaGuardar.Count} pts",
                            MaxCompresion = maxCompresion,
                            MaxExpansion = maxExpansion,
                            VehiculoId = 1,
                            DatosCrudos = datosBytes
                        };
                        db.Ensayos.Add(ensayo);
                        db.SaveChanges();
                    }

                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"¡Ensayo Guardado!\nPodrás ver todos los gráficos desde el Historial.");
                        txtEstado.Text = "GUARDADO OK";
                        txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.LightGreen);
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessageBox.Show("Error al guardar: " + ex.Message));
                }
            });
        }

        private void btnCalibrarCero_Click(object sender, RoutedEventArgs e)
        {
            if (bufferPos.Count > 0)
            {
                offsetPosicion += bufferPos.Last();
                offsetFuerza += bufferFuerza.Last();
                MessageBox.Show("Cero Establecido (Tare)");
            }
        }

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            _viendoHistorial = false;
            _ensayoCargado.Clear();
            InfoBox.Visibility = Visibility.Collapsed; // Ocultamos la caja

            bufferTiempo.Clear();
            bufferPos.Clear();
            bufferFuerza.Clear();
            bufferVel.Clear();
            maxCompresion = 0;
            maxExpansion = 0;

            GraficoPrincipal.Plot.Clear();
            GraficoPrincipal.Plot.Title("MONITOR EN VIVO");
            GraficoPrincipal.Refresh();
            txtEstado.Text = "LISTO";
        }

        private void btnCargar_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new VentanaHistorial();
            ventana.EnsayoSeleccionado += CargarEnsayoEnPantalla;
            ventana.ShowDialog();
        }

        private void CargarEnsayoEnPantalla(Modelos.Ensayo ensayo)
        {
            try
            {
                if (ensayo.DatosCrudos == null || ensayo.DatosCrudos.Length == 0) return;

                string json = System.Text.Encoding.UTF8.GetString(ensayo.DatosCrudos);
                if (string.IsNullOrWhiteSpace(json)) return;

                var puntosRecuperados = System.Text.Json.JsonSerializer.Deserialize<List<PuntoDeEnsayo>>(json);

                if (puntosRecuperados != null && puntosRecuperados.Count > 0)
                {
                    _ensayoCargado = puntosRecuperados;
                    _viendoHistorial = true;

                    txtMaxComp.Text = ensayo.MaxCompresion.ToString("F1");
                    txtMaxExpa.Text = ensayo.MaxExpansion.ToString("F1");

                    // --- ACTUALIZAMOS EL NUEVO INFO BOX ---
                    InfoBox.Visibility = Visibility.Visible; // ¡Lo mostramos!
                    lblCliente.Text = $"Cliente: {ensayo.Vehiculo?.Cliente?.Nombre ?? "N/A"}";
                    lblVehiculo.Text = $"Vehículo: {ensayo.Vehiculo?.Modelo ?? "N/A"}";
                    lblFecha.Text = $"Fecha: {ensayo.Fecha.ToShortDateString()} {ensayo.Fecha.ToShortTimeString()}";
                    lblNotas.Text = string.IsNullOrEmpty(ensayo.Notas) ? "Sin notas" : ensayo.Notas;

                    ActualizarGrafico();

                    txtEstado.Text = "VISUALIZANDO HISTORIAL";
                    txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Cyan);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar: " + ex.Message);
            }
        }
    }
}