using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace SagaIngenieria.Modelos
{
    /// <summary>
    /// Reimplementación de ALTO RENDIMIENTO de la lógica matemática de Variables.bas (VB6).
    /// </summary>
    public static class SagaMath
    {
        // Constantes extraídas de Variables.bas
        public const double PI = 3.14159265358979;

        /// <summary>
        /// Aplica el filtro de Fourier y calcula derivadas (Velocidad) simultáneamente.
        /// Reemplaza a las Sub Fourier e invFourier del VB6 original.
        /// </summary>
        /// <param name="tiempo">Array de tiempos</param>
        /// <param name="posicion">Array de posiciones crudas</param>
        /// <param name="frecCorte">Frecuencia de corte (ej. 10 Hz)</param>
        /// <param name="posFiltrada">Salida: Posición suavizada</param>
        /// <param name="velocidad">Salida: Velocidad calculada</param>
        public static void ProcesarSenal(double[] tiempo, double[] posicion, double frecCorte, out double[] posFiltrada, out double[] velocidad)
        {
            int nDatos = posicion.Length;

            // CORRECCIÓN: Usamos variables locales para trabajar dentro de las lambdas paralelas
            // C# no permite capturar parámetros 'out' dentro de métodos anónimos/lambdas.
            double[] localPosFiltrada = new double[nDatos];
            double[] localVelocidad = new double[nDatos];

            Complex[] spectrum = new Complex[nDatos];

            // 1. Transformada Discreta de Fourier (DFT) - Forward
            // Nota: Usamos Parallel.For para acelerar lo que en VB6 era lento.
            Parallel.For(0, nDatos, k =>
            {
                double sumReal = 0;
                double sumImag = 0;
                double omega = 2.0 * PI * k / nDatos;

                for (int i = 0; i < nDatos; i++)
                {
                    double angle = omega * i;
                    sumReal += posicion[i] * Math.Cos(angle);
                    sumImag -= posicion[i] * Math.Sin(angle);
                }
                spectrum[k] = new Complex(sumReal, sumImag);
            });

            // 2. Filtrado y Transformada Inversa (IDFT) con Derivada

            // Calculamos el paso de frecuencia base
            double deltaF = 1.0 / (tiempo[nDatos - 1] - tiempo[0]);

            Parallel.For(0, nDatos, i =>
            {
                double sumPos = 0;
                // double sumVel = 0; // Comentado si usamos derivada numérica para coincidir con VB6
                double t = i; // Índice temporal normalizado

                // Reconstrucción solo hasta la frecuencia de corte (Filtro Pasa-Bajos Ideal)
                // Esto replica la lógica "If k * deltaF < frecCorte" del VB6
                for (int k = 0; k < nDatos; k++)
                {
                    double freqActual = k * deltaF;

                    // Lógica de espejo de Fourier (Nyquist) para mantener la energía
                    if (k > nDatos / 2) freqActual = (nDatos - k) * deltaF;

                    if (freqActual <= frecCorte)
                    {
                        double angle = (2.0 * PI * k * t) / nDatos;
                        double cosA = Math.Cos(angle);
                        double sinA = Math.Sin(angle);

                        // IDFT para Posición: (Real*Cos - Imag*Sin) / N
                        double term = (spectrum[k].Real * cosA - spectrum[k].Imaginary * sinA);
                        sumPos += term;
                    }
                }
                localPosFiltrada[i] = sumPos / nDatos;
            });

            // 3. Cálculo de velocidad por diferencias finitas (Método Legacy compatible)
            // VB6: derivOut(j) = (rOut(j + 1) - rOut(j)) * oRate
            // Usamos la tasa de muestreo estimada del primer intervalo
            double rate = 0;
            if (tiempo.Length > 1 && (tiempo[1] - tiempo[0]) != 0)
            {
                rate = 1.0 / (tiempo[1] - tiempo[0]);
            }

            for (int j = 0; j < nDatos - 1; j++)
            {
                localVelocidad[j] = (localPosFiltrada[j + 1] - localPosFiltrada[j]) * rate;
            }
            // Repetir último valor para mantener longitud del array
            if (nDatos > 1)
                localVelocidad[nDatos - 1] = localVelocidad[nDatos - 2];

            // 4. Asignación final a los parámetros de salida
            posFiltrada = localPosFiltrada;
            velocidad = localVelocidad;
        }
    }
}