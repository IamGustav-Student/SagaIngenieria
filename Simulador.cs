using System;
using System.Threading;
using System.Threading.Tasks;

namespace SagaIngenieria
{
    public class Simulador
    {
        private bool _activo = false;
        public event Action<double, double> NuevosDatosRecibidos;

        // NUEVO: Variable para controlar la velocidad real en Hz
        private double _frecuenciaActual = 1.0; // 1 Hz por defecto
        private double _amplitudMm = 50.0;      // 50mm de recorrido (Estándar)

        // Función para cambiar la velocidad en vivo
        public void SetFrecuencia(double hz)
        {
            // Limitamos por seguridad (como decía el cartel del software viejo: 0.1 a 10 Hz)
            if (hz < 0.1) hz = 0.1;
            if (hz > 10.0) hz = 10.0;
            _frecuenciaActual = hz;
        }

        public void Iniciar()
        {
            if (_activo) return;
            _activo = true;
            Task.Run(GenerarDatos);
        }

        public void Detener()
        {
            _activo = false;
        }

        private async Task GenerarDatos()
        {
            double tiempoAcumulado = 0;
            Random ruido = new Random();

            // Simulación de física a 10ms (100 muestras por segundo)
            double dt = 0.01;

            while (_activo)
            {
                // FÍSICA REALISTA BASADA EN HERTZ
                // Omega (w) = 2 * PI * Frecuencia
                double w = 2 * Math.PI * _frecuenciaActual;

                // 1. Posición Senoidal: x = A * sin(w * t)
                double posicion = _amplitudMm * Math.Sin(w * tiempoAcumulado);

                // 2. Velocidad (Derivada): v = A * w * cos(w * t)
                // (Nota: La velocidad crece si aumentamos la frecuencia)
                double velocidadTeorica = _amplitudMm * w * Math.Cos(w * tiempoAcumulado);

                // 3. Fuerza (Amortiguador): F = C * v
                // Simulamos que el amortiguador es más duro en expansión (velocidad negativa)
                double dureza = (velocidadTeorica > 0) ? 0.15 : 0.25;
                double fuerza = velocidadTeorica * dureza;

                // Agregamos ruido realista
                fuerza += (ruido.NextDouble() - 0.5) * 2.0;

                NuevosDatosRecibidos?.Invoke(posicion, fuerza);

                tiempoAcumulado += dt;
                await Task.Delay((int)(dt * 1000));
            }
        }
    }
}