using System;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;

namespace SagaIngenieria
{
    public partial class VentanaHistorial : Window
    {
        // --- EVENTOS PÚBLICOS (Los enchufes que faltaban) ---

        // Evento 1: Cuando el usuario quiere ABRIR un ensayo (Replay)
        public event Action<Modelos.Ensayo> EnsayoSeleccionado;

        // Evento 2: Cuando el usuario quiere usarlo de FONDO (Comparar)
        public event Action<Modelos.Ensayo> EnsayoParaReferencia;

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
                    // Traemos los datos incluyendo las relaciones (Join)
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

        // Botón "ABRIR GRÁFICO"
        private void btnAbrir_Click(object sender, RoutedEventArgs e)
        {
            if (GridEnsayos.SelectedItem is Modelos.Ensayo ensayoElegido)
            {
                // Avisamos a MainWindow que cargue este ensayo
                EnsayoSeleccionado?.Invoke(ensayoElegido);
                this.Close();
            }
            else
            {
                MessageBox.Show("Por favor, seleccione una fila primero.");
            }
        }

        // Botón "USAR DE FONDO"
        private void btnComparar_Click(object sender, RoutedEventArgs e)
        {
            if (GridEnsayos.SelectedItem is Modelos.Ensayo ensayoElegido)
            {
                // Avisamos a MainWindow que use este ensayo como referencia gris
                EnsayoParaReferencia?.Invoke(ensayoElegido);
                this.Close();
            }
            else
            {
                MessageBox.Show("Por favor, seleccione una fila primero.");
            }
        }

        // Botón "ELIMINAR"
        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (GridEnsayos.SelectedItem is Modelos.Ensayo ensayoElegido)
            {
                if (MessageBox.Show("¿Seguro que deseas borrar este ensayo para siempre?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    using (var db = new Modelos.SagaContext())
                    {
                        db.Ensayos.Remove(ensayoElegido);
                        db.SaveChanges();
                    }
                    CargarDatos(); // Recargamos la tabla
                }
            }
        }
    }
}
