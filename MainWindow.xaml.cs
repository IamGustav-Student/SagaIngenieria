using SagaIngenieria.Modelos;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
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

            // --- LEYENDA (REFERENCIAS) ---
            GraficoPrincipal.Plot.ShowLegend();
            GraficoPrincipal.Plot.Legend.FontColor = ScottPlot.Color.FromHex("#FFFFFF");
            GraficoPrincipal.Plot.Legend.BackgroundColor = ScottPlot.Color.FromHex("#222222");
            GraficoPrincipal.Plot.Legend.OutlineColor = ScottPlot.Color.FromHex("#444444");
            GraficoPrincipal.Plot.Legend.Location = Alignment.UpperRight;
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

                    barFuerza.Value = Math.Abs(fuerzaReal);
                    barPosicion.Value = Math.Abs(posReal) + 50;
                    barVelocidad.Value = Math.Abs(velocidad);

                    if (fuerzaReal > maxCompresion) { maxCompresion = fuerzaReal; txtMaxComp.Text = maxCompresion.ToString("F1"); }
                    if (fuerzaReal < maxExpansion) { maxExpansion = fuerzaReal; txtMaxExpa.Text = maxExpansion.ToString("F1"); }

                    if (posReal > 0 && !_cicloPositivo) { _cicloPositivo = true; _contadorCiclos++; txtCiclos.Text = _contadorCiclos.ToString(); }
                    else if (posReal < 0) { _cicloPositivo = false; }

                    txtFreq.Text = _motorEncendido ? "1.5" : "0.0";
                    txtAmp.Text = _motorEncendido ? "100" : "0";

                    if (_motorEncendido && _grabando) txtCrono.Text = (DateTime.Now - _inicioGrabacion).ToString(@"hh\:mm\:ss");

                    if (_motorEncendido) { double temp = 45 + (_contadorCiclos * 0.1); if (temp > 80) temp = 80; txtTemp.Text = $"{temp:F1}°C"; }

                    bufferTiempo.Add(tiempoActual);
                    bufferPos.Add(posReal);
                    bufferFuerza.Add(fuerzaReal);
                    bufferVel.Add(velocidad);
                    tiempoActual += 10;

                    if (bufferTiempo.Count > 500)
                    {
                        bufferTiempo.RemoveAt(0); bufferPos.RemoveAt(0); bufferFuerza.RemoveAt(0); bufferVel.RemoveAt(0);
                    }
                }

                if (_grabando)
                {
                    _ensayoGrabado.Add(new PuntoDeEnsayo { Tiempo = tiempoActual, Posicion = posReal, Fuerza = fuerzaReal, Velocidad = velocidad });
                }

                if (!_viendoHistorial) ActualizarGrafico();
            });
        }

        private void ActualizarGrafico()
        {
            GraficoPrincipal.Plot.Clear();

            // --- 1. DIBUJAR REFERENCIA (FANTASMA) ---
            if (_ensayoReferencia != null && _ensayoReferencia.Count > 0)
            {
                double[] refX = null, refY = null;
                switch (_modoGrafico)
                {
                    case "FvsD": refX = _ensayoReferencia.Select(p => p.Posicion).ToArray(); refY = _ensayoReferencia.Select(p => p.Fuerza).ToArray(); break;
                    case "FvsV": refX = _ensayoReferencia.Select(p => p.Velocidad).ToArray(); refY = _ensayoReferencia.Select(p => p.Fuerza).ToArray(); break;
                    case "FPromVsV": refX = _ensayoReferencia.Select(p => p.Velocidad).ToArray(); refY = _ensayoReferencia.Select(p => p.Fuerza).ToArray(); break;
                }

                if (refX != null && refY != null && refX.Length > 0)
                {
                    var spRef = GraficoPrincipal.Plot.Add.Scatter(refX, refY);
                    spRef.Color = ScottPlot.Color.FromHex("#444444");
                    spRef.LineWidth = 2;
                    spRef.LinePattern = LinePattern.Dotted;
                    spRef.Label = "Referencia (Fondo)";
                }
            }

            // --- 2. DIBUJAR ENSAYO PRINCIPAL ---
            var colorFuerza = ScottPlot.Color.FromHex("#FF4081");
            var colorPos = ScottPlot.Color.FromHex("#00E5FF");
            var colorVel = ScottPlot.Color.FromHex("#B2FF59");
            var colorProm = ScottPlot.Color.FromHex("#FFA500");
            var colorHistorial = ScottPlot.Color.FromHex("#FFD700");

            double[] datosX = null;
            double[] datosY = null;
            double[] datosY2 = null;

            var fuenteDatos = _viendoHistorial ? _ensayoCargado : null;

            switch (_modoGrafico)
            {
                case "FvsD":
                    datosX = _viendoHistorial ? fuenteDatos.Select(p => p.Posicion).ToArray() : bufferPos.ToArray();
                    datosY = _viendoHistorial ? fuenteDatos.Select(p => p.Fuerza).ToArray() : bufferFuerza.ToArray();
                    break;
                case "FvsV":
                    datosX = _viendoHistorial ? fuenteDatos.Select(p => p.Velocidad).ToArray() : bufferVel.ToArray();
                    datosY = _viendoHistorial ? fuenteDatos.Select(p => p.Fuerza).ToArray() : bufferFuerza.ToArray();
                    break;
                case "FPromVsV":
                    datosX = _viendoHistorial ? fuenteDatos.Select(p => p.Velocidad).ToArray() : bufferVel.ToArray();
                    datosY = _viendoHistorial ? fuenteDatos.Select(p => p.Fuerza).ToArray() : bufferFuerza.ToArray();
                    break;
                case "FDvsT":
                    datosX = _viendoHistorial ? fuenteDatos.Select(p => p.Tiempo).ToArray() : null;
                    datosY = _viendoHistorial ? fuenteDatos.Select(p => p.Fuerza).ToArray() : bufferFuerza.ToArray();
                    datosY2 = _viendoHistorial ? fuenteDatos.Select(p => p.Posicion).ToArray() : bufferPos.ToArray();
                    break;
                case "FVvsT":
                    datosX = _viendoHistorial ? fuenteDatos.Select(p => p.Tiempo).ToArray() : null;
                    datosY = _viendoHistorial ? fuenteDatos.Select(p => p.Fuerza).ToArray() : bufferFuerza.ToArray();
                    datosY2 = _viendoHistorial ? fuenteDatos.Select(p => p.Velocidad).ToArray() : bufferVel.ToArray();
                    break;
                case "PicoVsV":
                    datosX = _viendoHistorial ? fuenteDatos.Select(p => p.Velocidad).ToArray() : bufferVel.ToArray();
                    datosY = _viendoHistorial ? fuenteDatos.Select(p => p.Fuerza).ToArray() : bufferFuerza.ToArray();
                    break;
            }

            if (datosY == null && datosX == null) return;

            // --- CONFIGURACIÓN DE EJES Y ETIQUETAS ---
            switch (_modoGrafico)
            {
                case "FvsD":
                    var sp1 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY);
                    sp1.Color = _viendoHistorial ? colorHistorial : colorFuerza;
                    sp1.LineWidth = 2;
                    sp1.Label = "Ciclo";

                    GraficoPrincipal.Plot.Title("Ciclo de Histéresis");
                    GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Posición (mm)"; // ETIQUETA EJE X
                    GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)";     // ETIQUETA EJE Y
                    break;

                case "FvsV":
                    var sp2 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY);
                    sp2.Color = _viendoHistorial ? colorHistorial : colorVel;
                    sp2.LineWidth = 2;
                    sp2.Label = "Amortiguamiento";

                    GraficoPrincipal.Plot.Title("Fuerza vs Velocidad");
                    GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Velocidad (mm/s)";
                    GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)";
                    break;

                case "FPromVsV":
                    var spProm = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY);
                    spProm.Color = colorProm;
                    spProm.LineWidth = 3;
                    spProm.MarkerSize = 0;
                    spProm.Label = "Promedio";

                    GraficoPrincipal.Plot.Title("Curva Característica (Promedio)");
                    GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Velocidad (mm/s)";
                    GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)";
                    break;

                case "FDvsT":
                    if (_viendoHistorial)
                    {
                        var s1 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY);
                        s1.Color = colorFuerza; s1.Axes.YAxis = GraficoPrincipal.Plot.Axes.Left; s1.Label = "Fuerza";

                        var s2 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY2);
                        s2.Color = colorPos; s2.Axes.YAxis = GraficoPrincipal.Plot.Axes.Right; s2.Label = "Posición";
                    }
                    else
                    {
                        var s1 = GraficoPrincipal.Plot.Add.Signal(datosY);
                        s1.Color = colorFuerza; s1.Axes.YAxis = GraficoPrincipal.Plot.Axes.Left; s1.Label = "Fuerza";

                        var s2 = GraficoPrincipal.Plot.Add.Signal(datosY2);
                        s2.Color = colorPos; s2.Axes.YAxis = GraficoPrincipal.Plot.Axes.Right; s2.Label = "Posición";
                    }
                    GraficoPrincipal.Plot.Title("Dominio del Tiempo (Fz + Pos)");
                    GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Tiempo (ms)";
                    GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)";
                    GraficoPrincipal.Plot.Axes.Right.Label.Text = "Posición (mm)";
                    break;

                case "FVvsT":
                    if (_viendoHistorial)
                    {
                        var s1 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY);
                        s1.Color = colorFuerza; s1.Axes.YAxis = GraficoPrincipal.Plot.Axes.Left; s1.Label = "Fuerza";

                        var s2 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY2);
                        s2.Color = colorVel; s2.Axes.YAxis = GraficoPrincipal.Plot.Axes.Right; s2.Label = "Velocidad";
                    }
                    else
                    {
                        var s1 = GraficoPrincipal.Plot.Add.Signal(datosY);
                        s1.Color = colorFuerza; s1.Axes.YAxis = GraficoPrincipal.Plot.Axes.Left; s1.Label = "Fuerza";

                        var s2 = GraficoPrincipal.Plot.Add.Signal(datosY2);
                        s2.Color = colorVel; s2.Axes.YAxis = GraficoPrincipal.Plot.Axes.Right; s2.Label = "Velocidad";
                    }
                    GraficoPrincipal.Plot.Title("Dominio del Tiempo (Fz + Vel)");
                    GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Tiempo (ms)";
                    GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)";
                    GraficoPrincipal.Plot.Axes.Right.Label.Text = "Velocidad (mm/s)";
                    break;

                case "PicoVsV":
                    var sp3 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY);
                    sp3.Color = ScottPlot.Color.FromHex("#FFFFFF");
                    sp3.MarkerSize = 2; sp3.LineWidth = 0;
                    sp3.Label = "Picos";

                    GraficoPrincipal.Plot.Title("Puntos Pico");
                    GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Velocidad (mm/s)";
                    GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)";
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
                if (_viendoHistorial || _ensayoReferencia.Count > 0) ActualizarGrafico();
            }
        }

        private void btnMotor_Click(object sender, RoutedEventArgs e)
        {
            if (!_motorEncendido)
            {
                _viendoHistorial = false;
                InfoBox.Visibility = Visibility.Collapsed;

                _miSimulador.Iniciar();
                _motorEncendido = true;
                if (txtBtnMotor != null) txtBtnMotor.Text = "APAGAR";
                txtEstado.Text = "MOTOR ENCENDIDO - MODO VIVO";
                txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Yellow);
                btnGrabar.IsEnabled = true;
                PanelGuardado.Visibility = Visibility.Collapsed;
                _inicioGrabacion = DateTime.Now;
                _contadorCiclos = 0;
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
            PanelGuardado.Visibility = Visibility.Collapsed;
            btnGrabar.IsEnabled = false; btnGrabar.Opacity = 0.5;
            btnGuardar.IsEnabled = true; btnGuardar.Opacity = 1;
            txtEstado.Text = "● GRABANDO DATOS...";
            txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Red);
            _inicioGrabacion = DateTime.Now; maxCompresion = 0; maxExpansion = 0;
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            _grabando = false;
            btnGrabar.IsEnabled = false; btnGrabar.Opacity = 0.5;
            btnGuardar.IsEnabled = false; btnGuardar.Opacity = 0.5;
            PanelGuardado.Visibility = Visibility.Visible;
            txtEstado.Text = "ESPERANDO DATOS...";
            txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Cyan);
        }

        private void btnConfirmarGuardado_Click(object sender, RoutedEventArgs e)
        {
            PanelGuardado.Visibility = Visibility.Collapsed;
            string nombreCliente = txtInputCliente.Text;
            string modeloVehiculo = txtInputVehiculo.Text;
            string notasEnsayo = txtInputNotas.Text;
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
                        var cliente = db.Clientes.FirstOrDefault(c => c.Nombre == nombreCliente);
                        if (cliente == null) { cliente = new Modelos.Cliente { Nombre = nombreCliente, Email = "N/A" }; db.Clientes.Add(cliente); db.SaveChanges(); }
                        var vehiculo = db.Vehiculos.FirstOrDefault(v => v.Modelo == modeloVehiculo && v.ClienteId == cliente.Id);
                        if (vehiculo == null) { vehiculo = new Modelos.Vehiculo { Marca = "SAGA", Modelo = modeloVehiculo, ClienteId = cliente.Id }; db.Vehiculos.Add(vehiculo); db.SaveChanges(); }
                        var ensayo = new Modelos.Ensayo { Fecha = DateTime.Now, Notas = notasEnsayo, MaxCompresion = maxCompresion, MaxExpansion = maxExpansion, VehiculoId = vehiculo.Id, DatosCrudos = datosBytes };
                        db.Ensayos.Add(ensayo);
                        db.SaveChanges();
                    }
                    Dispatcher.Invoke(() =>
                    {
                        btnGrabar.IsEnabled = true; btnGrabar.Opacity = 1;
                        MessageBox.Show($"¡Ensayo Guardado!");
                        txtEstado.Text = "GUARDADO OK";
                        InfoBox.Visibility = Visibility.Visible;
                        lblCliente.Text = $"Cliente: {nombreCliente}";
                        lblVehiculo.Text = $"Vehículo: {modeloVehiculo}";
                        lblFecha.Text = $"Fecha: {DateTime.Now}";
                        lblNotas.Text = notasEnsayo;
                        _ensayoCargado = new List<PuntoDeEnsayo>(datosParaGuardar);
                        _viendoHistorial = true;
                        ActualizarGrafico();
                    });
                }
                catch (Exception ex) { Dispatcher.Invoke(() => { MessageBox.Show("Error: " + ex.Message); btnGrabar.IsEnabled = true; }); }
            });
        }

        private void btnCancelarGuardado_Click(object sender, RoutedEventArgs e)
        {
            PanelGuardado.Visibility = Visibility.Collapsed;
            btnGrabar.IsEnabled = true; btnGrabar.Opacity = 1;
            txtEstado.Text = "GUARDADO CANCELADO";
            txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Orange);
        }

        private void btnCalibrarCero_Click(object sender, RoutedEventArgs e)
        {
            if (bufferPos.Count > 0) { offsetPosicion += bufferPos.Last(); offsetFuerza += bufferFuerza.Last(); MessageBox.Show("Cero Establecido"); }
        }

        // NUEVO BOTÓN: QUITAR REFERENCIA
        private void btnQuitarRef_Click(object sender, RoutedEventArgs e)
        {
            _ensayoReferencia.Clear();
            RefBox.Visibility = Visibility.Collapsed;
            ActualizarGrafico();
        }

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            _viendoHistorial = false; _ensayoCargado.Clear();

            // También ocultamos los paneles de info
            InfoBox.Visibility = Visibility.Collapsed;
            PanelGuardado.Visibility = Visibility.Collapsed;
            // RefBox NO se oculta automáticamente, para permitir comparar muchos ensayos contra la misma referencia.
            // Si el usuario quiere quitarla, usa el botón "Quitar" que acabamos de crear.

            bufferTiempo.Clear(); bufferPos.Clear(); bufferFuerza.Clear(); bufferVel.Clear();
            maxCompresion = 0; maxExpansion = 0;
            GraficoPrincipal.Plot.Clear(); GraficoPrincipal.Plot.Title("MONITOR EN VIVO"); GraficoPrincipal.Refresh();
            txtEstado.Text = "LISTO";
        }

        private void btnCargar_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new VentanaHistorial();
            ventana.EnsayoSeleccionado += CargarEnsayoEnPantalla;
            ventana.EnsayoParaReferencia += CargarReferenciaEnPantalla;
            ventana.ShowDialog();
        }

        private void CargarEnsayoEnPantalla(Modelos.Ensayo ensayo)
        {
            var puntos = DeserializarEnsayo(ensayo);
            if (puntos != null)
            {
                _ensayoCargado = puntos;
                _viendoHistorial = true;
                PanelGuardado.Visibility = Visibility.Collapsed;
                txtMaxComp.Text = ensayo.MaxCompresion.ToString("F1");
                txtMaxExpa.Text = ensayo.MaxExpansion.ToString("F1");

                InfoBox.Visibility = Visibility.Visible;
                lblCliente.Text = $"Cliente: {ensayo.Vehiculo?.Cliente?.Nombre ?? "N/A"}";
                lblVehiculo.Text = $"Vehículo: {ensayo.Vehiculo?.Modelo ?? "N/A"}";
                lblFecha.Text = $"Fecha: {ensayo.Fecha.ToShortDateString()}";
                lblNotas.Text = string.IsNullOrEmpty(ensayo.Notas) ? "Sin notas" : ensayo.Notas;

                ActualizarGrafico();
                txtEstado.Text = "VISUALIZANDO HISTORIAL";
                txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Cyan);
            }
        }

        private void CargarReferenciaEnPantalla(Modelos.Ensayo ensayo)
        {
            var puntos = DeserializarEnsayo(ensayo);
            if (puntos != null)
            {
                _ensayoReferencia = puntos;

                // NUEVO: MOSTRAR DETALLES EN LA BARRA SUPERIOR
                RefBox.Visibility = Visibility.Visible;
                lblRefCliente.Text = $"Cliente: {ensayo.Vehiculo?.Cliente?.Nombre ?? "N/A"}";
                lblRefVehiculo.Text = $"Vehículo: {ensayo.Vehiculo?.Modelo ?? "N/A"}";
                lblRefFecha.Text = $"Fecha: {ensayo.Fecha.ToShortDateString()}";

                ActualizarGrafico();
            }
        }

        private List<PuntoDeEnsayo> DeserializarEnsayo(Modelos.Ensayo ensayo)
        {
            try
            {
                if (ensayo.DatosCrudos == null || ensayo.DatosCrudos.Length == 0) return null;
                string json = System.Text.Encoding.UTF8.GetString(ensayo.DatosCrudos);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return System.Text.Json.JsonSerializer.Deserialize<List<PuntoDeEnsayo>>(json);
            }
            catch { MessageBox.Show("Error al leer archivo"); return null; }
        }
        private void btnImprimir_Click(object sender, RoutedEventArgs e)
        {
            // 1. Definir QUÉ vamos a imprimir
            Modelos.Ensayo ensayoAImprimir = null;

            if (_viendoHistorial && _ensayoCargado.Count > 0)
            {
                // Si estamos viendo un historial, imprimimos ese
                // Reconstruimos un objeto ensayo temporal con los datos actuales
                ensayoAImprimir = new Modelos.Ensayo
                {
                    Id = 0, // No importa para el PDF temporal
                    Fecha = DateTime.Parse(lblFecha.Text.Replace("Fecha: ", "")),
                    Notas = lblNotas.Text,
                    MaxCompresion = double.Parse(txtMaxComp.Text),
                    MaxExpansion = double.Parse(txtMaxExpa.Text),
                    Vehiculo = new Modelos.Vehiculo
                    {
                        Modelo = lblVehiculo.Text.Replace("Vehículo: ", ""),
                        Cliente = new Modelos.Cliente { Nombre = lblCliente.Text.Replace("Cliente: ", "") }
                    },
                    // Volvemos a serializar la memoria cargada para pasarla al generador
                    DatosCrudos = System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(_ensayoCargado))
                };
            }
            else if (!_motorEncendido && _ensayoGrabado.Count > 0)
            {
                // Si acabamos de grabar (pero no guardamos en historial aún), imprimimos lo de memoria
                if (MessageBox.Show("Estás imprimiendo un ensayo recién grabado.\n¿Deseas continuar?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.No) return;

                ensayoAImprimir = new Modelos.Ensayo
                {
                    Fecha = DateTime.Now,
                    Notas = "Ensayo sin guardar en BD",
                    MaxCompresion = maxCompresion,
                    MaxExpansion = maxExpansion,
                    Vehiculo = new Modelos.Vehiculo { Modelo = "En Vivo", Cliente = new Modelos.Cliente { Nombre = "Taller" } },
                    DatosCrudos = System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(_ensayoGrabado))
                };
            }
            else
            {
                MessageBox.Show("Debes CARGAR un ensayo del historial para imprimirlo.", "Nada para imprimir");
                return;
            }

            // 2. Diálogo de Guardar PDF
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"Reporte_SAGA_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
                Filter = "Documento PDF|*.pdf"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    // 3. ¡MAGIA! Llamamos a nuestro generador
                    GeneradorReporte.Generar(ensayoAImprimir, saveDialog.FileName);

                    // 4. Preguntar si quiere abrirlo
                    if (MessageBox.Show("Reporte generado con éxito.\n¿Deseas abrirlo ahora?", "Listo", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        // Truco para abrir el PDF en Windows con el visor predeterminado
                        var p = new System.Diagnostics.Process();
                        p.StartInfo = new System.Diagnostics.ProcessStartInfo(saveDialog.FileName)
                        {
                            UseShellExecute = true
                        };
                        p.Start();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al generar PDF: " + ex.Message);
                }
            }
        }
    }
}
     
