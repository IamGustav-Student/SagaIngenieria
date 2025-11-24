using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
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

        // BUFFER VIVO (Cola circular para animación suave)
        List<double> bufferTiempo = new List<double>();
        List<double> bufferPos = new List<double>();
        List<double> bufferFuerza = new List<double>();
        List<double> bufferVel = new List<double>();

        // MEMORIA DE GRABACIÓN (Datos crudos completos)
        List<PuntoDeEnsayo> _ensayoGrabado = new List<PuntoDeEnsayo>();

        // MEMORIA DE REPLAY (Datos cargados del historial)
        List<PuntoDeEnsayo> _ensayoCargado = new List<PuntoDeEnsayo>();

        // Variables Físicas
        double tiempoActual = 0;
        double lastPos = 0;
        double lastTime = 0;

        // Calibración
        double offsetPosicion = 0;
        double offsetFuerza = 0;

        // Picos
        double maxCompresion = 0;
        double maxExpansion = 0;

        // Estados
        bool _motorEncendido = false;
        bool _grabando = false;
        bool _viendoHistorial = false; // NUEVO: ¿Estamos viendo un archivo viejo?
        string _modoGrafico = "FvsD";

        public MainWindow()
        {
            InitializeComponent();
            InicializarBaseDeDatos();
            ConfigurarGraficoInicial();

            _miSimulador.NuevosDatosRecibidos += ProcesarDatosFisicos;
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

        // --- BUCLE DE PROCESAMIENTO (100Hz) ---
        private void ProcesarDatosFisicos(double rawPos, double rawFuerza)
        {
            Dispatcher.Invoke(() =>
            {
                // Si estamos viendo un historial, ignoramos los datos en vivo para el gráfico
                // (pero quizás quieras seguir calculando picos si el motor gira... por ahora priorizamos el historial)

                // 1. Física en Vivo
                double posReal = rawPos - offsetPosicion;
                double fuerzaReal = rawFuerza - offsetFuerza;
                double dt = (tiempoActual - lastTime);
                double velocidad = 0;
                if (dt > 0) velocidad = (posReal - lastPos) / dt;

                lastPos = posReal;
                lastTime = tiempoActual;

                // Actualizar números siempre (Telemetría viva)
                if (!_viendoHistorial)
                {
                    txtFuerza.Text = fuerzaReal.ToString("F1");
                    txtPosicion.Text = posReal.ToString("F1");
                    txtVelocidad.Text = velocidad.ToString("F1");

                    // Picos en vivo
                    if (fuerzaReal > maxCompresion) { maxCompresion = fuerzaReal; txtMaxComp.Text = maxCompresion.ToString("F1"); }
                    if (fuerzaReal < maxExpansion) { maxExpansion = fuerzaReal; txtMaxExpa.Text = maxExpansion.ToString("F1"); }

                    // Buffer Circular
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

                // GRABACIÓN
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

                // Refrescar Gráfico
                if (!_viendoHistorial) ActualizarGrafico();
            });
        }

        // --- CEREBRO GRÁFICO MEJORADO ---
        private void ActualizarGrafico()
        {
            GraficoPrincipal.Plot.Clear();

            // Colores Neon distintivos
            var colorFuerza = ScottPlot.Color.FromHex("#FF4081"); // Rosa
            var colorPos = ScottPlot.Color.FromHex("#00E5FF");    // Cyan
            var colorVel = ScottPlot.Color.FromHex("#B2FF59");    // Verde
            var colorHistorial = ScottPlot.Color.FromHex("#FFD700"); // Oro

            // 1. SELECCIÓN DE DATOS (¿Vivo o Memoria?)
            double[] datosX = null;
            double[] datosY = null;
            double[] datosY2 = null; // Para gráficos dobles

            if (_viendoHistorial)
            {
                // Usamos los datos cargados del archivo
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
                        datosX = _ensayoCargado.Select(p => p.Tiempo).ToArray(); // Eje X es tiempo
                        datosY = _ensayoCargado.Select(p => p.Fuerza).ToArray();
                        datosY2 = _ensayoCargado.Select(p => p.Posicion).ToArray();
                        break;
                    case "FVvsT":
                        datosX = _ensayoCargado.Select(p => p.Tiempo).ToArray();
                        datosY = _ensayoCargado.Select(p => p.Fuerza).ToArray();
                        datosY2 = _ensayoCargado.Select(p => p.Velocidad).ToArray();
                        break;
                    case "PicoVsV":
                        // Para picos usamos scatter simple por ahora
                        datosX = _ensayoCargado.Select(p => p.Velocidad).ToArray();
                        datosY = _ensayoCargado.Select(p => p.Fuerza).ToArray();
                        break;
                }
            }
            else
            {
                // Usamos el buffer en vivo
                switch (_modoGrafico)
                {
                    case "FvsD": datosX = bufferPos.ToArray(); datosY = bufferFuerza.ToArray(); break;
                    case "FvsV": datosX = bufferVel.ToArray(); datosY = bufferFuerza.ToArray(); break;
                    case "FDvsT": datosY = bufferFuerza.ToArray(); datosY2 = bufferPos.ToArray(); break; // Signal plot no usa X
                    case "FVvsT": datosY = bufferFuerza.ToArray(); datosY2 = bufferVel.ToArray(); break;
                    case "PicoVsV": datosX = bufferVel.ToArray(); datosY = bufferFuerza.ToArray(); break;
                }
            }

            // 2. DIBUJADO SEGÚN TIPO
            // Aquí unificamos la lógica: no importa si es vivo o grabado, se dibuja igual.

            if (datosY == null && datosX == null) return; // Seguridad

            switch (_modoGrafico)
            {
                case "FvsD": // Scatter X-Y
                    var sp1 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY);
                    sp1.Color = _viendoHistorial ? colorHistorial : colorFuerza;
                    sp1.LineWidth = 2;
                    GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Posición (mm)";
                    GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)";
                    GraficoPrincipal.Plot.Title(_viendoHistorial ? "HISTORIAL: Ciclo de Histéresis" : "VIVO: Ciclo de Histéresis");
                    break;

                case "FvsV": // Scatter X-Y
                    var sp2 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY);
                    sp2.Color = _viendoHistorial ? colorHistorial : colorVel;
                    sp2.LineWidth = 2;
                    GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Velocidad (mm/s)";
                    GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)";
                    GraficoPrincipal.Plot.Title("Análisis de Amortiguamiento");
                    break;

                case "FDvsT": // Doble eje Y
                    // Signal Plot es más rápido para series de tiempo
                    if (_viendoHistorial)
                    {
                        // En historial usamos Scatter porque el tiempo puede no ser uniforme si hay pausas
                        var s1 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY); s1.Color = colorFuerza; s1.Axes.YAxis = GraficoPrincipal.Plot.Axes.Left;
                        var s2 = GraficoPrincipal.Plot.Add.Scatter(datosX, datosY2); s2.Color = colorPos; s2.Axes.YAxis = GraficoPrincipal.Plot.Axes.Right;
                    }
                    else
                    {
                        // En vivo usamos Signal
                        var s1 = GraficoPrincipal.Plot.Add.Signal(datosY); s1.Color = colorFuerza; s1.Axes.YAxis = GraficoPrincipal.Plot.Axes.Left;
                        var s2 = GraficoPrincipal.Plot.Add.Signal(datosY2); s2.Color = colorPos; s2.Axes.YAxis = GraficoPrincipal.Plot.Axes.Right;
                    }
                    GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)";
                    GraficoPrincipal.Plot.Axes.Right.Label.Text = "Posición (mm)";
                    GraficoPrincipal.Plot.Title("Dominio del Tiempo");
                    break;

                case "FVvsT": // Doble eje Y
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

        // --- BOTONES ---

        private void CambiarGrafico_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag != null)
            {
                _modoGrafico = rb.Tag.ToString();
                // Si estamos viendo historial, forzamos el redibujado al cambiar el botón
                if (_viendoHistorial) ActualizarGrafico();
            }
        }

        private void btnMotor_Click(object sender, RoutedEventArgs e)
        {
            if (!_motorEncendido)
            {
                // Si estaba viendo historial, vuelvo al modo vivo al prender motor
                _viendoHistorial = false;
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
            _viendoHistorial = false; // Asegurar que vemos lo que grabamos

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
                        MessageBox.Show($"¡Ensayo Guardado!\nPodrás ver todos los gráficos (FvsD, FvsV, etc) desde el Historial.");
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
            // Limpia todo y vuelve al modo VIVO
            _viendoHistorial = false;
            _ensayoCargado.Clear();

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
                if (ensayo.DatosCrudos == null || ensayo.DatosCrudos.Length == 0)
                {
                    MessageBox.Show("Ensayo vacío.");
                    return;
                }

                string json = System.Text.Encoding.UTF8.GetString(ensayo.DatosCrudos);
                if (string.IsNullOrWhiteSpace(json)) return;

                var puntosRecuperados = System.Text.Json.JsonSerializer.Deserialize<List<PuntoDeEnsayo>>(json);

                if (puntosRecuperados != null && puntosRecuperados.Count > 0)
                {
                    // 1. CARGAMOS LA MEMORIA DE REPLAY
                    _ensayoCargado = puntosRecuperados;
                    _viendoHistorial = true; // ¡ACTIVAMOS EL MODO HISTORIAL!

                    // 2. Restauramos valores pico del ensayo para mostrarlos en el panel
                    txtMaxComp.Text = ensayo.MaxCompresion.ToString("F1");
                    txtMaxExpa.Text = ensayo.MaxExpansion.ToString("F1");

                    // 3. Dibujamos (Usando el gráfico que esté seleccionado actualmente en los botones)
                    ActualizarGrafico();

                    txtEstado.Text = "VISUALIZANDO HISTORIAL (Use los botones de arriba para cambiar de gráfico)";
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