using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SagaIngenieria.Modelos
{
    public class Cliente
    {
        [Key]
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // Relación: Un cliente tiene muchos vehículos
        public virtual ICollection<Vehiculo> Vehiculos { get; set; } = new List<Vehiculo>();
    }
}
