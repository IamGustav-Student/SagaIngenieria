using SagaIngenieria.Modelos;
using ScottPlot; // Necesario para el Gráfico
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media; // Necesario para la UI de WPF

namespace SagaIngenieria
{
    public partial class MainWindow : Window
    {
        private DriverMaquina _driver;

        // Variables de Calibración (Valores por defecto del legacy)
        private double _offsetFuerza = 0;
        private double _offsetPosicion = 0;
        private double _constantePosicion = 1.0;

        // Almacenamiento de datos del último ensayo
        private double[] _tiempo;
        private double[] _fuerzaRaw;
        private double[] _posicionRaw;
        private double[] _posicionFiltrada;
        private double[] _velocidadCalculada;

        public MainWindow()
        {
            InitializeComponent();
            CargarPuertos();
            ConfigurarGrafico();

            // Inicializar Driver
            _driver = new DriverMaquina();
            _driver.Log += AgregarLog;
            // Usamos Dispatcher para volver al hilo de la UI desde el evento del driver
            _driver.ProgresoDescarga += (p) => Dispatcher.Invoke(() => PrgEnsayo.Value = p);
        }

        private void CargarPuertos()
        {
            CmbPuertos.ItemsSource = SerialPort.GetPortNames();
            if (CmbPuertos.Items.Count > 0) CmbPuertos.SelectedIndex = 0;
        }

        private void ConfigurarGrafico()
        {
            // CORRECCIÓN SCOTTPLOT 5: Configuración manual del tema oscuro

            // 1. Color de fondo del control completo y del área de datos
            WpfPlot1.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#1E1E1E");
            WpfPlot1.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#1E1E1E");

            // 2. Color de los ejes (Texto, Marcas y Líneas de borde)
            WpfPlot1.Plot.Axes.Color(ScottPlot.Color.FromHex("#FFFFFF"));

            // 3. Color de la cuadrícula (Grilla)
            WpfPlot1.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#333333");

            WpfPlot1.Refresh();
        }

        private async void BtnConectar_Click(object sender, RoutedEventArgs e)
        {
            if (CmbPuertos.SelectedItem == null) return;

            BtnConectar.IsEnabled = false;
            string puerto = CmbPuertos.SelectedItem.ToString();

            bool exito = await _driver.Conectar(puerto);

            if (exito)
            {
                TxtEstado.Text = $"CONECTADO A {puerto}";
                // CORRECCIÓN AMBIGÜEDAD: Usamos explícitamente System.Windows.Media.Colors
                TxtEstado.Background = new SolidColorBrush(System.Windows.Media.Colors.Green);
                BtnIniciar.IsEnabled = true;
                BtnDetener.IsEnabled = true;
                AgregarLog("Sistema en línea y listo.");
            }
            else
            {
                TxtEstado.Text = "ERROR DE CONEXIÓN";
                // CORRECCIÓN AMBIGÜEDAD: Usamos explícitamente System.Windows.Media.Colors
                TxtEstado.Background = new SolidColorBrush(System.Windows.Media.Colors.Red);
                BtnConectar.IsEnabled = true;
            }
        }

        private async void BtnIniciar_Click(object sender, RoutedEventArgs e)
        {
            // Validación de entrada numérica
            if (!double.TryParse(TxtVelocidad.Text, out double velocidadHz))
            {
                MessageBox.Show("Velocidad inválida");
                return;
            }

            // Estado UI: Bloquear controles y mostrar overlay
            OverlayCarga.Visibility = Visibility.Visible;
            BtnIniciar.IsEnabled = false;
            WpfPlot1.Plot.Clear();

            try
            {
                AgregarLog($"Iniciando ensayo a {velocidadHz} Hz...");

                // 1. Ejecutar Adquisición (Async - No congela UI)
                // Usamos una duración estimada fija por ahora (4s), similar a legacy
                List<string> tramasHex = await _driver.EjecutarEnsayo(velocidadHz, 4.0);

                AgregarLog($"Recibidos {tramasHex.Count} paquetes de datos.");

                // 2. Procesamiento de Datos (Parsing + Matemática)
                ProcesarDatosRecibidos(tramasHex, velocidadHz);

                // 3. Visualización
                ActualizarGrafico();
            }
            catch (Exception ex)
            {
                AgregarLog($"ERROR CRÍTICO: {ex.Message}");
                MessageBox.Show(ex.Message, "Error en Ensayo", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                OverlayCarga.Visibility = Visibility.Collapsed;
                BtnIniciar.IsEnabled = true;
                PrgEnsayo.Value = 0;
            }
        }

        private void BtnDetener_Click(object sender, RoutedEventArgs e)
        {
            _driver.EnviarComando(SagaProtocol.DetenerMotor);
            AgregarLog("Comando de parada de emergencia enviado.");
        }

        private void ProcesarDatosRecibidos(List<string> tramas, double hz)
        {
            // Parseo basado en la lógica original de VB6 (7 chars: 4 fuerza + 3 posición)

            List<double> fList = new List<double>();
            List<double> pList = new List<double>();

            foreach (var trama in tramas)
            {
                // La trama contiene múltiples muestras de 7 chars concatenadas
                for (int i = 0; i < trama.Length - 7; i += 7)
                {
                    try
                    {
                        string hexF = trama.Substring(i, 4);
                        string hexP = trama.Substring(i + 4, 3);

                        // Conversión Hex -> Entero
                        int valF = Convert.ToInt32(hexF, 16);
                        int valP = Convert.ToInt32(hexP, 16);

                        // Aplicación de constantes de calibración (Legacy logic)
                        double fuerzaKg = (valF * 0.1) - _offsetFuerza;
                        double posMm = (valP * _constantePosicion) - _offsetPosicion;

                        fList.Add(fuerzaKg);
                        pList.Add(posMm);
                    }
                    catch { /* Ignorar bytes corruptos si la trama viene sucia */ }
                }
            }

            // Generar vector de tiempo
            int n = fList.Count;
            if (n == 0) return;

            // OutRate estimado del legacy: 187 * Vel + 131.3
            double outRate = 187 * hz + 131.3;
            double dt = 1.0 / outRate;

            _tiempo = Enumerable.Range(0, n).Select(i => i * dt).ToArray();
            _fuerzaRaw = fList.ToArray();
            _posicionRaw = pList.ToArray();

            // 4. MAGIA MATEMÁTICA (SagaMath)
            // Si el checkbox está activo, aplicamos el filtro de Fourier original
            if (ChkFiltro.IsChecked == true)
            {
                AgregarLog("Aplicando filtro FFT + Derivada...");
                SagaMath.ProcesarSenal(_tiempo, _posicionRaw, 10.0, out _posicionFiltrada, out _velocidadCalculada);
            }
            else
            {
                _posicionFiltrada = _posicionRaw;
                _velocidadCalculada = new double[n]; // Ceros si no se calcula
            }
        }

        private void ActualizarGrafico()
        {
            if (_tiempo == null || _tiempo.Length == 0) return;

            WpfPlot1.Plot.Clear();
            var plt = WpfPlot1.Plot;

            // Graficar Fuerza (Eje Izquierdo)
            var sigFuerza = plt.Add.SignalXY(_tiempo, _fuerzaRaw);
            sigFuerza.Color = ScottPlot.Color.FromHex("#FF5555"); // Rojo
            sigFuerza.LegendText = "Fuerza (Kg)";

            // Graficar Posición (Eje Derecho)
            var sigPos = plt.Add.SignalXY(_tiempo, _posicionFiltrada);
            sigPos.Color = ScottPlot.Color.FromHex("#55FF55"); // Verde
            sigPos.LegendText = "Posición (mm)";
            sigPos.Axes.YAxis = plt.Axes.Right; // Asignar al eje secundario

            // Configurar Etiquetas de Ejes
            plt.Axes.Left.Label.Text = "Fuerza (Kg)";
            plt.Axes.Right.Label.Text = "Posición (mm)";
            plt.Axes.Bottom.Label.Text = "Tiempo (s)";
            plt.ShowLegend();

            WpfPlot1.Refresh();
        }

        private void AgregarLog(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                string time = DateTime.Now.ToString("HH:mm:ss");
                ListLog.Items.Insert(0, $"[{time}] {msg}");
            });
        }

        private void GraphMode_Changed(object sender, RoutedEventArgs e)
        {
            // Al cambiar el RadioButton, refrescamos el gráfico si hay datos
            if (_tiempo != null) ActualizarGrafico();
        }

        // Método para guardar el ensayo actual en la BD
        private void GuardarEnsayoActual()
        {
            if (_tiempo == null || _tiempo.Length == 0)
            {
                MessageBox.Show("No hay datos para guardar.", "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var db = new SagaContext())
                {
                    // 1. Crear estructura de puntos para serializar
                    var listaPuntos = new List<PuntoDeEnsayo>();
                    for (int i = 0; i < _tiempo.Length; i++)
                    {
                        listaPuntos.Add(new PuntoDeEnsayo(
                            _tiempo[i],
                            _posicionFiltrada[i],
                            _fuerzaRaw[i],
                            _velocidadCalculada[i]
                        ));
                    }

                    // 2. Serializar a JSON bytes (Rápido y eficiente)
                    byte[] blobDatos = JsonSerializer.SerializeToUtf8Bytes(listaPuntos);

                    // 3. Crear Objeto Ensayo (Aquí podrías abrir una ventanita para pedir Cliente/Vehículo real)
                    // Por ahora creamos un "Vehículo Default" si no existe para pruebas rápidas
                    var vehiculoDefault = db.Vehiculos.FirstOrDefault(v => v.Patente == "TEST-001");
                    if (vehiculoDefault == null)
                    {
                        var cliente = new Cliente { Nombre = "Cliente Mostrador" };
                        vehiculoDefault = new Vehiculo { Marca = "Genérico", Modelo = "Banco Prueba", Patente = "TEST-001", Cliente = cliente };
                        db.Vehiculos.Add(vehiculoDefault);
                    }

                    var nuevoEnsayo = new Ensayo
                    {
                        Fecha = DateTime.Now,
                        VelocidadMax = _velocidadCalculada.Max(),
                        FuerzaMaxCompresion = _fuerzaRaw.Min(),
                        FuerzaMaxExpansion = _fuerzaRaw.Max(),
                        DatosCrudos = blobDatos,
                        Vehiculo = vehiculoDefault,
                        Comentarios = $"Ensayo rápido a {TxtVelocidad.Text} Hz",
                        Notas = "Ensayo realizado desde el Panel de Control Rápido" // Nuevo campo
                    };

                    db.Ensayos.Add(nuevoEnsayo);
                    db.SaveChanges();

                    AgregarLog("✅ Ensayo guardado exitosamente en base de datos.");
                    MessageBox.Show("Ensayo guardado en el Historial.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar en BD: {ex.Message}");
            }
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            GuardarEnsayoActual();

        }

    }
}

