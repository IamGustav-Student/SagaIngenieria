using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SagaIngenieria.Modelos
{
    public class Cliente
    {
        public int Id { get; set; } // Número único de identificación

        public string Nombre { get; set; } = string.Empty; // Nombre completo
        public string Telefono { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // Un cliente puede tener muchos vehículos (Relación 1 a Muchos)
        public List<Vehiculo> Vehiculos { get; set; } = new List<Vehiculo>();
    }
}
