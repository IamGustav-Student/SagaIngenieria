using System;

namespace SagaIngenieria.Modelos
{
    public class PuntoDeEnsayo
    {
        public double Tiempo { get; set; }
        public double Posicion { get; set; }
        public double Fuerza { get; set; }
        public double Velocidad { get; set; }

        public PuntoDeEnsayo(double tiempo, double posicion, double fuerza, double velocidad)
        {
            Tiempo = tiempo;
            Posicion = posicion;
            Fuerza = fuerza;
            Velocidad = velocidad;
        }

        // Constructor vacío para serialización (si se usa JSON o XML)
        public PuntoDeEnsayo() { }
    }
}