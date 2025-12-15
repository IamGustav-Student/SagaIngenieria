using System;
using System.Windows;
using SagaIngenieria.Modelos;

namespace SagaIngenieria
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // 1. Inicializar Base de Datos (Crea el archivo .db si no existe)
                SagaContext.Inicializar();

                // 2. Crear e iniciar la ventana principal MANUALMENTE
                MainWindow window = new MainWindow();
                this.MainWindow = window; // Asignar como ventana principal de la app
                window.Show(); // ¡IMPORTANTE! Forzar visualización
                window.Activate(); // Traer al frente
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fatal al iniciar la aplicación:\n{ex.Message}",
                                "Error Crítico",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                Shutdown(); // Cerrar si falla la carga crítica
            }
        }
    }
}