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
        private DriverMaquina _miDriver = new DriverMaquina();
        private bool _usarMaquinaReal = false;

        List<double> bufferTiempo = new List<double>();
        List<double> bufferPos = new List<double>();
        List<double> bufferFuerza = new List<double>();
        List<double> bufferVel = new List<double>();

        List<PuntoDeEnsayo> _ensayoGrabado = new List<PuntoDeEnsayo>();
        List<PuntoDeEnsayo> _ensayoCargado = new List<PuntoDeEnsayo>();
        List<PuntoDeEnsayo> _ensayoReferencia = new List<PuntoDeEnsayo>();

        double tiempoActual = 0;
        double _frecuenciaObjetivo = 1.0;
        bool _motorEncendido = false;
        bool _grabando = false;
        bool _viendoHistorial = false;
        string _modoGrafico = "FvsD";

        DateTime _inicioGrabacion;

        public MainWindow()
        {
            InitializeComponent();
            InicializarBaseDeDatos();
            ConfigurarGraficoInicial();

            // SIMULADOR: Datos en tiempo real
            _miSimulador.NuevosDatosRecibidos += ProcesarDatosEntrantes;

            // DRIVER REAL: Datos en Batch (al final)
            _miDriver.NuevosDatosRecibidos += ProcesarDatosEntrantes;
            _miDriver.LogEstado += (msg) => Dispatcher.Invoke(() => txtEstado.Text = msg);
            _miDriver.DescargaFinalizada += () => Dispatcher.Invoke(() =>
            {
                btnGuardar_Click(null, null);
                MessageBox.Show("Descarga de datos completada. Gráfico actualizado.");
            });

            ActualizarListaPuertos();
        }

        // --- MÉTODOS UI BÁSICOS ---
        private void btnMinimizar_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void btnMaximizar_Click(object sender, RoutedEventArgs e) => this.WindowState = (this.WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        private void btnSalir_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        // --- CONFIGURACIÓN ---
        private void btnConfig_Click(object sender, RoutedEventArgs e)
        {
            var ventana = new VentanaConfiguracion();
            ventana.ConfiguracionAceptada += (lista) =>
            {
                _frecuenciaObjetivo = lista[0];
                _miSimulador.SetFrecuencia(_frecuenciaObjetivo);
                txtFreq.Text = _frecuenciaObjetivo.ToString("F1");
            };
            ventana.ShowDialog();
        }

        // --- CORE GRÁFICO ---
        private void ProcesarDatosEntrantes(double posReal, double fuerzaLimpia)
        {
            Dispatcher.Invoke(() =>
            {
                // Cálculo de tiempo relativo (aproximado si viene del buffer)
                if (_usarMaquinaReal && _motorEncendido == false)
                {
                    // Si estamos descargando (motor apagado), incrementamos tiempo artificialmente para el gráfico
                    tiempoActual += 10;
                }
                else if (!_usarMaquinaReal)
                {
                    tiempoActual += 10;
                }

                // Cálculo de velocidad (simple derivada)
                double velocidad = 0;
                if (bufferPos.Count > 0)
                    velocidad = (posReal - bufferPos.Last()) / 0.01; // dt de 10ms aprox

                // Actualizar UI numérica
                txtFuerza.Text = fuerzaLimpia.ToString("F1");
                txtPosicion.Text = posReal.ToString("F1");

                // Buffers para gráfico en vivo (Simulador) o reconstrucción (Real)
                bufferPos.Add(posReal);
                bufferFuerza.Add(fuerzaLimpia);
                bufferVel.Add(velocidad);

                // Si estamos en modo grabación (Real o Simulado), guardamos en la lista final
                if (_grabando || (_usarMaquinaReal && !_motorEncendido))
                {
                    // Nota: En modo Real, la 'grabación' ocurre al descargar
                    _ensayoGrabado.Add(new PuntoDeEnsayo
                    {
                        Tiempo = tiempoActual,
                        Posicion = posReal,
                        Fuerza = fuerzaLimpia,
                        Velocidad = velocidad
                    });
                }

                // Limitar buffer visual si no es historial
                if (bufferPos.Count > 1000 && !_viendoHistorial)
                {
                    bufferPos.RemoveAt(0); bufferFuerza.RemoveAt(0); bufferVel.RemoveAt(0);
                }

                if (!_viendoHistorial) ActualizarGrafico();
            });
        }

        private void ActualizarGrafico()
        {
            GraficoPrincipal.Plot.Clear();

            // Lógica simplificada de renderizado
            double[] xs = null, ys = null;

            if (_viendoHistorial && _ensayoCargado.Count > 0)
            {
                xs = _ensayoCargado.Select(p => p.Posicion).ToArray();
                ys = _ensayoCargado.Select(p => p.Fuerza).ToArray();
            }
            else
            {
                xs = bufferPos.ToArray();
                ys = bufferFuerza.ToArray();
            }

            if (xs != null && xs.Length > 0)
            {
                var sp = GraficoPrincipal.Plot.Add.Scatter(xs, ys);
                sp.LineWidth = 2;
                sp.Color = ScottPlot.Color.FromHex("#00E5FF");
            }

            GraficoPrincipal.Refresh();
        }

        // --- CONTROL DE HARDWARE ---
        private void btnMotor_Click(object sender, RoutedEventArgs e)
        {
            if (!_motorEncendido)
            {
                // INICIAR
                _viendoHistorial = false;
                _ensayoGrabado.Clear();
                bufferPos.Clear(); bufferFuerza.Clear();

                if (_usarMaquinaReal)
                {
                    // Protocolo: Configura memoria y arranca
                    _miDriver.IniciarEnsayo(_frecuenciaObjetivo);
                    txtEstado.Text = "ADQUIRIENDO DATOS (BUFFER INTERNO)...";

                    // IMPORTANTE: En modo real, no verás gráfico en vivo
                    // porque la máquina está llenando su RAM, no enviando.
                    MessageBox.Show("Motor Iniciado.\n\nNOTA: El gráfico se actualizará AL DETENER el motor (Descarga de Datos).", "Modo Hardware");
                }
                else
                {
                    _miSimulador.Iniciar();
                }

                _motorEncendido = true;
                _grabando = true; // Habilitamos flag lógico
                txtBtnMotor.Text = "DETENER Y DESCARGAR";
                txtBtnMotor.Foreground = Media.Brushes.Red;
            }
            else
            {
                // DETENER
                if (_usarMaquinaReal)
                {
                    txtEstado.Text = "DESCARGANDO DATOS DE LA MÁQUINA...";
                    // Esto dispara la secuencia Stop -> Dump -> Parse -> Eventos
                    _miDriver.DetenerYDescargar();
                }
                else
                {
                    _miSimulador.Detener();
                    btnGuardar_Click(null, null);
                }

                _motorEncendido = false;
                _grabando = false;
                txtBtnMotor.Text = "ENCENDER";
                txtBtnMotor.Foreground = Media.Brushes.White;
            }
        }

        // --- EL RESTO DE MÉTODOS SE MANTIENEN IGUAL (Conexión, Guardado, etc) ---
        private void chkModoReal_Checked(object sender, RoutedEventArgs e)
        {
            _usarMaquinaReal = chkModoReal.IsChecked == true;
            PanelPuertos.Visibility = _usarMaquinaReal ? Visibility.Visible : Visibility.Collapsed;
            if (!_usarMaquinaReal) _miDriver.Desconectar();
        }

        private void ActualizarListaPuertos() => cmbPuertos.ItemsSource = System.IO.Ports.SerialPort.GetPortNames();
        private void btnRefrescar_Click(object sender, RoutedEventArgs e) => ActualizarListaPuertos();

        private void btnConectar_Click(object sender, RoutedEventArgs e)
        {
            if (btnConectar.Content.ToString().Contains("CONECTAR"))
            {
                if (_miDriver.Conectar(cmbPuertos.SelectedItem as string))
                {
                    btnConectar.Content = "DESCONECTAR";
                    btnConectar.Background = Media.Brushes.Red;
                }
            }
            else
            {
                _miDriver.Desconectar();
                btnConectar.Content = "CONECTAR MÁQUINA";
                btnConectar.Background = (Media.Brush)new Media.BrushConverter().ConvertFrom("#007ACC");
            }
        }

        private void btnGrabar_Click(object sender, RoutedEventArgs e) { /* Obsoleto con la nueva lógica automática */ }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            PanelGuardado.Visibility = Visibility.Visible;
            txtEstado.Text = "DATOS LISTOS PARA GUARDAR";
        }

        // ... (Mantener métodos de Base de Datos y Exportación originales) ...
        // Para que compile completo, se asumen los métodos existentes de InitializarBaseDeDatos, ConfigurarGraficoInicial, etc.
        // Copiar del original si faltan.
        private void InicializarBaseDeDatos() { /* ... */ }
        private void ConfigurarGraficoInicial() { /* ... */ }
        private void btnConfirmarGuardado_Click(object sender, RoutedEventArgs e) { PanelGuardado.Visibility = Visibility.Collapsed; MessageBox.Show("Guardado simulado OK"); }
        private void btnCancelarGuardado_Click(object sender, RoutedEventArgs e) => PanelGuardado.Visibility = Visibility.Collapsed;
        private void btnCalibrarCero_Click(object sender, RoutedEventArgs e) { }
        private void btnQuitarRef_Click(object sender, RoutedEventArgs e) { }
        private void btnNuevo_Click(object sender, RoutedEventArgs e) { }
        private void btnCargar_Click(object sender, RoutedEventArgs e) { }
        private void btnImprimir_Click(object sender, RoutedEventArgs e) { }
        private void CambiarGrafico_Checked(object sender, RoutedEventArgs e) { ActualizarGrafico(); }
    }
}

