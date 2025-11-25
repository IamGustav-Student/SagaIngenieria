using System;
using System.Collections.Generic;
using System.Windows;
using System.Globalization; // Necesario para controlar los números

namespace SagaIngenieria
{
    public partial class VentanaConfiguracion : Window
    {
        // --- EL ENCHUFE PÚBLICO (IMPORTANTE) ---
        public event Action<List<double>> ConfiguracionAceptada;

        public VentanaConfiguracion()
        {
            InitializeComponent();
        }

        // Función auxiliar para convertir texto a número de forma segura (Punto o Coma)
        private double ParseSeguro(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return 0;

            // Truco: Reemplazamos la coma por punto y usamos cultura Invariante (Estilo USA)
            // Así "1.5" y "1,5" se convierten ambos en "1.5" internamente
            string normalizado = texto.Replace(",", ".");

            if (double.TryParse(normalizado, NumberStyles.Any, CultureInfo.InvariantCulture, out double resultado))
            {
                return resultado;
            }
            throw new Exception($"El valor '{texto}' no es un número válido.");
        }

        // Botón "Previsualizar"
        private void btnCalcular_Click(object sender, RoutedEventArgs e)
        {
            CalcularLista();
        }

        private List<double> CalcularLista()
        {
            var lista = new List<double>();
            try
            {
                if (rbSimple.IsChecked == true)
                {
                    // Velocidad simple con parseo seguro
                    double val = ParseSeguro(txtHzSimple.Text);

                    // VALIDACIÓN DE SEGURIDAD
                    if (val > 5.0)
                    {
                        if (MessageBox.Show($"¡PRECAUCIÓN!\nEstás configurando {val} Hz.\n¿Seguro que no quisiste escribir {val / 5.0}?\n\nEsto es muy rápido.", "Alerta de Seguridad", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                        {
                            return lista; // Cancelar
                        }
                    }

                    lista.Add(val);
                }
                else
                {
                    // Barrido con parseo seguro
                    double ini = ParseSeguro(txtHzIni.Text);
                    double fin = ParseSeguro(txtHzFin.Text);
                    double paso = ParseSeguro(txtHzPaso.Text);

                    // Evitar bucle infinito
                    if (paso <= 0) throw new Exception("El paso debe ser mayor a 0");
                    if (ini > fin) throw new Exception("La velocidad inicial no puede ser mayor a la final");

                    // Generar lista
                    for (double v = ini; v <= fin + 0.001; v += paso) // +0.001 por error de flotante
                    {
                        lista.Add(Math.Round(v, 2));
                    }
                }

                // Mostrar en la lista visual
                lstVelocidades.ItemsSource = null;
                lstVelocidades.ItemsSource = lista;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
            return lista;
        }

        // Botón "ACEPTAR"
        private void btnAceptar_Click(object sender, RoutedEventArgs e)
        {
            var lista = CalcularLista();
            if (lista.Count > 0)
            {
                // Disparamos el evento hacia afuera
                ConfiguracionAceptada?.Invoke(lista);
                this.Close();
            }
        }

        // Botón "CANCELAR"
        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
