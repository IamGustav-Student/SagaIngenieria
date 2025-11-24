using System;
using System.Linq; // Necesario para ordenar y filtrar
using System.Windows;
using Microsoft.EntityFrameworkCore; // Necesario para incluir datos relacionados (Cliente/Vehículo)

namespace SagaIngenieria
{
    public partial class VentanaHistorial : Window
    {
        // Evento: Avisamos a la ventana principal que el usuario eligió algo
        public event Action<Modelos.Ensayo> EnsayoSeleccionado;

        public VentanaHistorial()
        {
            InitializeComponent();
            CargarDatos();
        }

        private void CargarDatos()
        {
            try
            {
                using (var db = new Modelos.SagaContext())
                {
                    // LEER DE LA BD:
                    // Traemos los ensayos pero INCLUYENDO (.Include) los datos del vehículo y cliente
                    // para que no salga vacío en la tabla. Ordenamos por fecha descendente (lo nuevo arriba).
                    var lista = db.Ensayos
                                  .Include(e => e.Vehiculo)
                                  .ThenInclude(v => v.Cliente)
                                  .OrderByDescending(e => e.Fecha)
                                  .ToList();

                    GridEnsayos.ItemsSource = lista;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al leer historial: " + ex.Message);
            }
        }

        private void btnAbrir_Click(object sender, RoutedEventArgs e)
        {
            // Verificamos si hay algo seleccionado en la tabla
            if (GridEnsayos.SelectedItem is Modelos.Ensayo ensayoElegido)
            {
                // Disparamos el evento (avisamos a MainWindow)
                EnsayoSeleccionado?.Invoke(ensayoElegido);

                // Cerramos esta ventana
                this.Close();
            }
            else
            {
                MessageBox.Show("Por favor, seleccione una fila primero.");
            }
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (GridEnsayos.SelectedItem is Modelos.Ensayo ensayoElegido)
            {
                if (MessageBox.Show("¿Seguro que deseas borrar este ensayo para siempre?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    using (var db = new Modelos.SagaContext())
                    {
                        // Truco de EF Core para borrar por ID sin cargar todo de nuevo
                        db.Ensayos.Remove(ensayoElegido);
                        db.SaveChanges();
                    }
                    CargarDatos(); // Recargar la tabla
                }
            }
        }
    }
}
