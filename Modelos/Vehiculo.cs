using System;
using System.Collections.Generic;

namespace SagaIngenieria.Modelos
{
    // Esta clase define el auto de carreras
    public class Vehiculo
    {
        public int Id { get; set; }

        public int ClienteId { get; set; } // Para saber de quién es
        public Cliente Cliente { get; set; } // Conexión con el dueño

        public string Marca { get; set; } = string.Empty; // Ej: Porsche
        public string Modelo { get; set; } = string.Empty; // Ej: 911 GT3
        public string Categoria { get; set; } = string.Empty; // Ej: Turismo Nacional

        // Un vehículo tiene muchos ensayos históricos
        public List<Ensayo> Ensayos { get; set; } = new List<Ensayo>();
    }
}
