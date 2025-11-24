using System;
using System.Collections.Generic;
using System.Windows;
// CORRECCIÓN 1: Usamos un "Apodo" para los colores de Windows y evitamos la confusión
using Media = System.Windows.Media;
using ScottPlot;

namespace SagaIngenieria
{
    // Clase auxiliar para guardar cada punto
    public class PuntoDeEnsayo
    {
        public double Tiempo { get; set; }
        public double Posicion { get; set; }
        public double Fuerza { get; set; }
    }

    public partial class MainWindow : Window
    {
        // --- 1. CEREBRO Y MEMORIA ---
        private Simulador _miSimulador = new Simulador();

        // Memoria para el gráfico (solo visual, últimos segundos)
        List<double> historialTiempo = new List<double>();
        List<double> historialFuerza = new List<double>();

        // Memoria para GRABAR (toda la prueba)
        List<PuntoDeEnsayo> _bufferEnsayo = new List<PuntoDeEnsayo>();

        // Variables de Estado
        double tiempoActual = 0;
        double maxCompresion = 0;
        double maxExpansion = 0;

        // Banderas de Control (Semáforos lógicos)
        bool _motorEncendido = false;
        bool _grabando = false;

        public MainWindow()
        {
            InitializeComponent();

            // --- 2. INICIALIZAR BASE DE DATOS ---
            try
            {
                using (var db = new Modelos.SagaContext())
                {
                    db.Database.EnsureCreated();

                    // Verificamos si existe algún vehículo. Si no, creamos uno.
                    bool hayVehiculos = false;
                    foreach (var v in db.Vehiculos) { hayVehiculos = true; break; }

                    if (!hayVehiculos)
                    {
                        var clienteDefault = new Modelos.Cliente { Nombre = "Taller SAGA", Email = "admin@saga.com" };
                        db.Clientes.Add(clienteDefault);
                        var autoDefault = new Modelos.Vehiculo { Marca = "SAGA", Modelo = "Banco de Pruebas", Cliente = clienteDefault };
                        db.Vehiculos.Add(autoDefault);
                        db.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error iniciando base de datos: " + ex.Message);
            }

            // --- 3. CONFIGURACIÓN DEL GRÁFICO ---
            // Aquí usamos ScottPlot.Color explícitamente para evitar dudas
            GraficoPrincipal.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("#000000");
            GraficoPrincipal.Plot.DataBackground.Color = ScottPlot.Color.FromHex("#000000");
            GraficoPrincipal.Plot.Grid.MajorLineColor = ScottPlot.Color.FromHex("#222222");

            GraficoPrincipal.Plot.Axes.Title.Label.Text = "Análisis en Tiempo Real";
            GraficoPrincipal.Plot.Axes.Title.Label.ForeColor = ScottPlot.Color.FromHex("#FFFFFF");

            GraficoPrincipal.Plot.Axes.Left.Label.Text = "Fuerza (kg)";
            GraficoPrincipal.Plot.Axes.Left.Label.ForeColor = ScottPlot.Color.FromHex("#FFFFFF");

            GraficoPrincipal.Plot.Axes.Bottom.Label.Text = "Tiempo (ms)";
            GraficoPrincipal.Plot.Axes.Bottom.Label.ForeColor = ScottPlot.Color.FromHex("#FFFFFF");

            // --- 4. CONEXIÓN ---
            _miSimulador.NuevosDatosRecibidos += AlRecibirDatos;
            // El simulador empieza apagado. Esperamos al botón.
        }

        // --- LÓGICA DE BOTONES ---

        // 1. ENCENDER / APAGAR MOTOR
        private void btnMotor_Click(object sender, RoutedEventArgs e)
        {
            if (!_motorEncendido)
            {
                // ENCENDER
                _miSimulador.Iniciar();
                _motorEncendido = true;

                // Actualizar visual (Usando el apodo 'Media' para los colores de Windows)
                txtMotor.Text = "APAGAR MOTOR";
                btnMotor.Background = new Media.SolidColorBrush(Media.Color.FromRgb(200, 0, 0)); // Rojo

                btnGrabar.IsEnabled = true;
                btnGrabar.Opacity = 1.0;

                txtEstado.Text = "MOTOR GIRANDO - LISTO PARA ENSAYAR";
                txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Yellow);
            }
            else
            {
                // APAGAR
                _miSimulador.Detener();
                _motorEncendido = false;

                if (_grabando) btnGuardar_Click(null, null);

                txtMotor.Text = "ENCENDER MOTOR";
                btnMotor.Background = new Media.SolidColorBrush(Media.Color.FromRgb(68, 68, 68)); // Gris

                btnGrabar.IsEnabled = false;
                btnGrabar.Opacity = 0.5;
                btnGuardar.IsEnabled = false;
                btnGuardar.Opacity = 0.5;

                txtEstado.Text = "SISTEMA DETENIDO";
                txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Gray);
            }
        }

        // 2. EMPEZAR A GRABAR
        private void btnGrabar_Click(object sender, RoutedEventArgs e)
        {
            _bufferEnsayo.Clear();
            maxCompresion = 0;
            maxExpansion = 0;
            txtMaxComp.Text = "0.0";
            txtMaxExpa.Text = "0.0";

            _grabando = true;

            btnGrabar.IsEnabled = false;
            btnGrabar.Opacity = 0.5;

            btnGuardar.IsEnabled = true;
            btnGuardar.Background = new Media.SolidColorBrush(Media.Colors.DodgerBlue);
            btnGuardar.Opacity = 1.0;

            txtEstado.Text = "● GRABANDO DATOS...";
            txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.Red);
        }

        // 3. FINALIZAR Y GUARDAR
        private void btnGuardar_Click(object sender, RoutedEventArgs e)
        {
            if (!_grabando) return;

            _grabando = false;

            btnGrabar.IsEnabled = true;
            btnGrabar.Opacity = 1.0;

            btnGuardar.IsEnabled = false;
            btnGuardar.Background = new Media.SolidColorBrush(Media.Color.FromRgb(68, 68, 68));
            btnGuardar.Opacity = 0.5;

            txtEstado.Text = "GUARDANDO EN BASE DE DATOS...";

            // Usamos un hilo de fondo para no congelar la pantalla mientras guarda
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    string datosJson = System.Text.Json.JsonSerializer.Serialize(_bufferEnsayo);
                    byte[] datosBytes = System.Text.Encoding.UTF8.GetBytes(datosJson);

                    using (var db = new Modelos.SagaContext())
                    {
                        var nuevoEnsayo = new Modelos.Ensayo
                        {
                            Fecha = DateTime.Now,
                            Notas = "Ensayo Manual #" + DateTime.Now.ToString("HH:mm:ss"),
                            MaxCompresion = maxCompresion,
                            MaxExpansion = maxExpansion,
                            DatosCrudos = datosBytes,
                            VehiculoId = 1
                        };

                        db.Ensayos.Add(nuevoEnsayo);
                        db.SaveChanges();
                    }

                    // Volver al hilo principal para mostrar el mensaje
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"¡Ensayo Guardado!\nSe capturaron {_bufferEnsayo.Count} puntos.");
                        txtEstado.Text = "ENSAYO GUARDADO - MOTOR ENCENDIDO";
                        txtEstado.Foreground = new Media.SolidColorBrush(Media.Colors.LightGreen);
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Error al guardar: " + ex.Message);
                        txtEstado.Text = "ERROR DE GUARDADO";
                    });
                }
            });
        }

        // --- BUCLE PRINCIPAL ---
        private void AlRecibirDatos(double posicion, double fuerza)
        {
            Dispatcher.Invoke(() =>
            {
                // UI Updates
                txtFuerza.Text = fuerza.ToString("F1");
                txtPosicion.Text = posicion.ToString("F1");

                // Picos
                if (fuerza > maxCompresion)
                {
                    maxCompresion = fuerza;
                    txtMaxComp.Text = maxCompresion.ToString("F1");
                }
                if (fuerza < maxExpansion)
                {
                    maxExpansion = fuerza;
                    txtMaxExpa.Text = maxExpansion.ToString("F1");
                }

                // Grabar
                if (_grabando)
                {
                    _bufferEnsayo.Add(new PuntoDeEnsayo
                    {
                        Tiempo = tiempoActual,
                        Posicion = posicion,
                        Fuerza = fuerza
                    });
                }

                // Gráfico
                historialTiempo.Add(tiempoActual);
                historialFuerza.Add(fuerza);
                tiempoActual += 10;

                if (historialTiempo.Count > 500)
                {
                    historialTiempo.RemoveAt(0);
                    historialFuerza.RemoveAt(0);
                }

                GraficoPrincipal.Plot.Clear();
                var linea = GraficoPrincipal.Plot.Add.Scatter(historialTiempo.ToArray(), historialFuerza.ToArray());
                linea.Color = ScottPlot.Color.FromHex("#00E5FF");
                linea.LineWidth = 2;
                GraficoPrincipal.Plot.Axes.AutoScale();
                GraficoPrincipal.Refresh();
            });
        }
    }
}