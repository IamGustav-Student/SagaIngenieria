using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls; // Para manejar los RadioButtons
using Media = System.Windows.Media;
using ScottPlot;
using System.Linq; // Necesario para funciones de listas como Last()

namespace SagaIngenieria
{
    public class PuntoDeEnsayo
    {
        public double Tiempo { get; set; }
        public double Posicion { get; set; }
        public double Fuerza { get; set; }
        public double Velocidad { get; set; } // Nueva variable calculada
    }

    public partial class MainWindow : Window
    {
        private Simulador _miSimulador = new Simulador();

        // --- MEMORIA DE DATOS ---
        // Usamos listas para guardar la historia reciente (buffer visual)
        // Capacidad 1000 puntos para que se vea fluido
        List<double> bufferTiempo = new List<double>();
        List<double> bufferPos = new List<double>();
        List<double> bufferFuerza = new List<double>();
        List<double> bufferVel = new List<double>();

        // Memoria completa para guardar en disco
        List<PuntoDeEnsayo> _ensayoCompleto = new List<PuntoDeEnsayo>();

        // Variables Físicas
        double tiempoActual = 0;
        double lastPos = 0;     // Para calcular velocidad
        double lastTime = 0;    // Para calcular velocidad

        // Variables de Calibración (Tara)
        double offsetPosicion = 0;
        double offsetFuerza = 0;

        // Picos
        double maxCompresion = 0;
        double maxExpansion = 0;

        // Estados
        bool _motorEncendido = false;
        bool _grabando = false;
        string _modoGrafico = "FvsD"; // Modo por defecto

        public MainWindow()
        {
            InitializeComponent();
            InicializarBaseDeDatos();
            ConfigurarGraficoInicial();

            // Conectar simulador
            _miSimulador.NuevosDatosRecibidos += ProcesarDatosFisicos;
        }

        private void InicializarBaseDeDatos()
        {
            try
            {
                using (var db = new Modelos.SagaContext())
                {
                    db.Database.EnsureCreated();
                    // Data seeding simplificado
                    if (!db.Vehiculos.Any())
                    {
                        var cliente = new Modelos.Cliente { Nombre = "SAGA Default", Email = "info@saga.com" };
                        db.Clientes.Add(cliente);
                        db.Vehiculos.Add(new Modelos.Vehiculo { Marca = "Genérico", Modelo = "Banco", Cliente = cliente });
                        db.SaveChanges();
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("Error BD: " + ex.Message); }
        }

        private void ConfigurarGraficoInicial()
        {
            // Colores base del tema oscuro
            GraficoPrincipal.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#000000");
            GraficoPrincipal.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#111111");
            GraficoPrincipal.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#333333");
            GraficoPrincipal.Plot.Axes.Color(ScottPlot.Color.FromHex("#AAAAAA"));
        }

        // --- NÚCLEO MATEMÁTICO (Se ejecuta 100 veces/seg) ---
        private void ProcesarDatosFisicos(double rawPos, double rawFuerza)
        {
            Dispatcher.Invoke(() =>
            {
                // 1. APLICAR CALIBRACIÓN (TARA)
                double posReal = rawPos - offsetPosicion;
                double fuerzaReal = rawFuerza - offsetFuerza;

                // 2. CALCULAR VELOCIDAD (Derivada: v = dx / dt)
                // dt es el tiempo entre muestras (aprox 0.01s en el simulador)
                double dt = (tiempoActual - lastTime);
                double velocidad = 0;

                if (dt > 0)
                    velocidad = (posReal - lastPos) / dt; // mm/ms -> m/s * factor

                // Filtro simple de ruido para velocidad (opcional)
                // velocidad = velocidad * 0.8 + lastVel * 0.2; 

                lastPos = posReal;
                lastTime = tiempoActual;

                // 3. ACTUALIZAR TELEMETRÍA (Textos)
                txtFuerza.Text = fuerzaReal.ToString("F1");
                txtPosicion.Text = posReal.ToString("F1");
                txtVelocidad.Text = velocidad.ToString("F1");

                // 4. DETECCIÓN DE PICOS
                if (fuerzaReal > maxCompresion)
                {
                    maxCompresion = fuerzaReal;
                    txtMaxComp.Text = maxCompresion.ToString("F1");
                }
                if (fuerzaReal < maxExpansion)
                {
                    maxExpansion = fuerzaReal;
                    txtMaxExpa.Text = maxExpansion.ToString("F1");
                }

                // 5. GUARDAR EN BUFFER CIRCULAR (Para visualización)
                bufferTiempo.Add(tiempoActual);
                bufferPos.Add(posReal);
                bufferFuerza.Add(fuerzaReal);
                bufferVel.Add(velocidad);
                tiempoActual += 10; // +10ms

                // Mantener buffer limpio (últimos 500 puntos = 5 segundos)
                if (bufferTiempo.Count > 500)
                {
                    bufferTiempo.RemoveAt(0);
                    bufferPos.RemoveAt(0);
                    bufferFuerza.RemoveAt(0);
                    bufferVel.RemoveAt(0);
                }

                // 6. GRABACIÓN (Si está activo)
                if (_grabando)
                {
                    _ensayoCompleto.Add(new PuntoDeEnsayo
                    {
                        Tiempo = tiempoActual,
                        Posicion = posReal,
                        Fuerza = fuerzaReal,
                        Velocidad = velocidad
                    });
                }

                // 7. DIBUJAR GRÁFICO SEGÚN MODO
                ActualizarGrafico();
            });
        }

        // --- LÓGICA DE GRAFICADO ---
        private void ActualizarGrafico()
        {
            GraficoPrincipal.Plot.Clear();

            // Colores "Neon Racing"
            var colorFuerza = ScottPlot.Color.FromHex("#FF4081"); // Rosa
            var colorPos = ScottPlot.Color.FromHex("#00E5FF");    // Cyan
            var colorVel = ScottPlot.Color.FromHex("#B2FF59");    // Verde Lima

            switch (_modoGrafico)
            {
                case "FvsD": // Fuerza vs Desplazamiento (Histéresis)
                    var sp1 = GraficoPrincipal.Plot.Add.Scatter(bufferPos.ToArray(), bufferFuerza.ToArray());
                    sp1.Color = colorFuerza;
                    sp1.LineWidth = 2;
                    GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Posición (mm)";
                    GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)";
                    break;

                case "FvsV": // Fuerza vs Velocidad
                    var sp2 = GraficoPrincipal.Plot.Add.Scatter(bufferVel.ToArray(), bufferFuerza.ToArray());
                    sp2.Color = colorVel;
                    sp2.LineWidth = 2;
                    GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Velocidad (mm/s)";
                    GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)";
                    break;

                case "FDvsT": // Fuerza y Posición vs Tiempo (Dos ejes Y)
                    var sigF = GraficoPrincipal.Plot.Add.Signal(bufferFuerza.ToArray());
                    sigF.Color = colorFuerza;
                    sigF.Axes.YAxis = GraficoPrincipal.Plot.Axes.Left;

                    var sigP = GraficoPrincipal.Plot.Add.Signal(bufferPos.ToArray());
                    sigP.Color = colorPos;
                    sigP.Axes.YAxis = GraficoPrincipal.Plot.Axes.Right; // Eje derecho

                    GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)";
                    GraficoPrincipal.Plot.Axes.Right.Label.Text = "Posición (mm)";
                    GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Tiempo (samples)";
                    break;

                case "FVvsT": // Fuerza y Velocidad vs Tiempo
                    var sigF2 = GraficoPrincipal.Plot.Add.Signal(bufferFuerza.ToArray());
                    sigF2.Color = colorFuerza;

                    var sigV = GraficoPrincipal.Plot.Add.Signal(bufferVel.ToArray());
                    sigV.Color = colorVel;
                    sigV.Axes.YAxis = GraficoPrincipal.Plot.Axes.Right;

                    GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)";
                    GraficoPrincipal.Plot.Axes.Right.Label.Text = "Velocidad (mm/s)";
                    break;

                case "PicoVsV": // Simulación simple de picos
                                // En un ensayo real esto acumularía puntos de varios ciclos.
                                // Aquí mostramos el scatter actual como referencia.
                    var sp3 = GraficoPrincipal.Plot.Add.Scatter(bufferVel.ToArray(), bufferFuerza.ToArray());
                    sp3.Color = ScottPlot.Color.FromHex("#FFFFFF");
                    sp3.MarkerSize = 2;
                    sp3.LineWidth = 0; // Solo puntos
                    GraficoPrincipal.Plot.Title("Análisis de Picos (En Desarrollo)");
                    break;
            }

            GraficoPrincipal.Plot.Axes.AutoScale();
            GraficoPrincipal.Refresh();
        }

        // --- EVENTOS DE LA UI ---

        // Al cambiar un RadioButton de gráfico
        private void CambiarGrafico_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag != null)
            {
                _modoGrafico = rb.Tag.ToString();
            }
        }

        private void btnMotor_Click(object sender, RoutedEventArgs e)
        {
            if (!_motorEncendido)
            {
                _miSimulador.Iniciar();
                _motorEncendido = true;

                // CAMBIO ROBUSTO: Cambiamos el texto directamente si el nombre falla
                if (txtBtnMotor != null) txtBtnMotor.Text = "APAGAR";

                txtEstado.Text = "MOTOR ENCENDIDO - ESPERANDO ORDEN DE GRABACIÓN";
                txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Yellow);
                btnGrabar.IsEnabled = true;
            }
            else
            {
                _miSimulador.Detener();
                _motorEncendido = false;
                if (_grabando) btnGuardar_Click(null, null);

                if (txtBtnMotor != null) txtBtnMotor.Text = "ENCENDER";

                txtEstado.Text = "SISTEMA DETENIDO";
                txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Gray);
                btnGrabar.IsEnabled = false;
                btnGuardar.IsEnabled = false;
            }
        }

        private void btnGrabar_Click(object sender, RoutedEventArgs e)
        {
            _ensayoCompleto.Clear();
            _grabando = true;

            btnGrabar.IsEnabled = false;
            btnGuardar.IsEnabled = true;

            txtEstado.Text = "● GRABANDO DATOS...";
            txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Red);

            // Resetear picos para el nuevo ensayo
            maxCompresion = 0;
            maxExpansion = 0;
        }

        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            _grabando = false;
            btnGrabar.IsEnabled = true;
            btnGuardar.IsEnabled = false;

            txtEstado.Text = "GUARDANDO...";

            // Guardar en BD (Mock)
            try
            {
                using (var db = new Modelos.SagaContext())
                {
                    var ensayo = new Modelos.Ensayo
                    {
                        Fecha = DateTime.Now,
                        Notas = "Ensayo Multigráfico",
                        MaxCompresion = maxCompresion,
                        MaxExpansion = maxExpansion,
                        VehiculoId = 1,
                        DatosCrudos = new byte[0] // Placeholder
                    };
                    db.Ensayos.Add(ensayo);
                    db.SaveChanges();
                }
                MessageBox.Show($"Ensayo guardado con {_ensayoCompleto.Count} puntos.");
                txtEstado.Text = "GUARDADO EXITOSO";
                txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.LightGreen);
            }
            catch (Exception ex) { MessageBox.Show("Error DB: " + ex.Message); }
        }

        // --- NUEVAS FUNCIONES SOLICITADAS ---

        private void btnCalibrarCero_Click(object sender, RoutedEventArgs e)
        {
            // TARE: El valor actual se convierte en el nuevo 0
            if (bufferPos.Count > 0 && bufferFuerza.Count > 0)
            {
                // Tomamos el último valor recibido crudo (aprox)
                // Como ya aplicamos offset en el loop, sumamos el offset viejo para obtener el raw
                // y ese será el nuevo offset.
                // Simplificación: offset += valor_actual_mostrado

                offsetPosicion += bufferPos.Last();
                offsetFuerza += bufferFuerza.Last();

                MessageBox.Show("Sensores Tarados a Cero.");
            }
        }

        private void btnNuevo_Click(object sender, RoutedEventArgs e)
        {
            // Limpia todo para empezar de cero visualmente
            bufferTiempo.Clear();
            bufferPos.Clear();
            bufferFuerza.Clear();
            bufferVel.Clear();

            maxCompresion = 0;
            maxExpansion = 0;

            GraficoPrincipal.Plot.Clear();
            GraficoPrincipal.Refresh();

            txtEstado.Text = "LISTO PARA NUEVA PRUEBA";
        }

        private void btnCargar_Click(object sender, RoutedEventArgs e)
        {
            // Aquí abriremos la ventana de historial en el futuro.
            // Por ahora, mostramos cuántos ensayos hay en la BD.
            using (var db = new Modelos.SagaContext())
            {
                int count = db.Ensayos.Count();
                MessageBox.Show($"Hay {count} ensayos guardados en la base de datos.\n(El visor de historial estará disponible en la próxima actualización).");
            }
        }
    }
}