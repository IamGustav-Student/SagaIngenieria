using System;
using System.Threading;
using System.Threading.Tasks;

namespace SagaIngenieria
{
    // Esta clase simula ser la máquina real
    public class Simulador
    {
        private bool _activo = false;

        // Este "evento" es como un grito: avisa cuando tiene datos nuevos
        public event Action<double, double> NuevosDatosRecibidos;

        public void Iniciar()
        {
            _activo = true;
            // Arrancamos una tarea en segundo plano (para no congelar la pantalla)
            Task.Run(GenerarDatos);
        }

        public void Detener()
        {
            _activo = false;
        }

        private async Task GenerarDatos()
        {
            double angulo = 0;
            Random ruido = new Random();

            while (_activo)
            {
                // 1. MATEMÁTICA: Creamos una onda senoidal (sube y baja)
                // Simula el amortiguador moviéndose 50mm
                double posicion = 50 * Math.Sin(angulo);

                // 2. FÍSICA: La fuerza depende de la velocidad (aquí simplificado)
                // Simula 100kg de fuerza
                double fuerza = 100 * Math.Cos(angulo);

                // 3. REALISMO: Le agregamos un poquito de "ruido" aleatorio
                fuerza += (ruido.NextDouble() - 0.5) * 5;

                // 4. AVISAR: Enviamos los datos (Posición, Fuerza) a quien esté escuchando
                NuevosDatosRecibidos?.Invoke(posicion, fuerza);

                // Avanzamos el ángulo y esperamos 10 milisegundos
                angulo += 0.1;
                await Task.Delay(10);
            }
        }
    }
}