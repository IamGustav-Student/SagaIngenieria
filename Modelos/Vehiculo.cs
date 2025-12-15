using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SagaIngenieria.Modelos
{
    public class Vehiculo
    {
        [Key]
        public int Id { get; set; }
        public string Marca { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public string Patente { get; set; } = string.Empty; // Placa/Matrícula

        [ForeignKey("Cliente")]
        public int ClienteId { get; set; }
        public virtual Cliente Cliente { get; set; }

        // Relación: Un vehículo tiene muchos ensayos históricos
        public virtual ICollection<Ensayo> Ensayos { get; set; } = new List<Ensayo>();
    }
}
