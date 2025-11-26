using System;
using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;

namespace SagaIngenieria
{
    public partial class VentanaHistorial : Window
    {
        // Evento 1: Abrir para ver (Modo Replay)
        public event Action<Modelos.Ensayo> EnsayoSeleccionado;

        // Evento 2: Usar de fondo (Modo Comparación) - NUEVO
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
            if (GridEnsayos.SelectedItem is Modelos.Ensayo ensayoElegido)
            {
                EnsayoSeleccionado?.Invoke(ensayoElegido);
                this.Close();
            }
            else MessageBox.Show("Seleccione una fila.");
        }

        // NUEVA FUNCIÓN: COMPARAR
        private void btnComparar_Click(object sender, RoutedEventArgs e)
        {
            if (GridEnsayos.SelectedItem is Modelos.Ensayo ensayoElegido)
            {
                // Disparamos el evento de REFERENCIA
                EnsayoParaReferencia?.Invoke(ensayoElegido);
                this.Close();
            }
            else MessageBox.Show("Seleccione una fila para usar de fondo.");
        }

        private void btnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (GridEnsayos.SelectedItem is Modelos.Ensayo ensayoElegido)
            {
                if (MessageBox.Show("¿Borrar ensayo?", "Confirmar", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    using (var db = new Modelos.SagaContext())
                    {
                        db.Ensayos.Remove(ensayoElegido);
                        db.SaveChanges();
                    }
                    CargarDatos();
                }
            }
        }
    }
}
