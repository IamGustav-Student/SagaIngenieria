using System;
using System.Threading.Tasks;
using System.Windows;

namespace SagaIngenieria
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Crear y mostrar la Pantalla de Bienvenida
            var splash = new SplashScreen();
            splash.Show();

            // 2. Simular carga (Aquí podrías cargar la BD real en el futuro)
            // Esperamos 3 segundos (3000 ms) para que se vea la animación
            await Task.Delay(3000);

            // 3. Crear la Ventana Principal (pero oculta aun)
            var mainWindow = new MainWindow();

            // 4. Cerrar el Splash y mostrar la principal
            splash.Close();
            mainWindow.Show();
        }
    }
}
