using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SagaIngenieria.Modelos
{
    public class Ensayo
    {
        [Key]
        public int Id { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;

        // Metadatos del Ensayo
        public string Tipo { get; set; } = "Estándar"; // Ej: "Fuerza vs Vel", "Destructivo"
        public string Operador { get; set; } = "Admin";
        public string Comentarios { get; set; } = string.Empty;

        // Propiedad Notas (Alias de Comentarios o campo adicional para reportes)
        // Usamos [NotMapped] si solo queremos un alias, o lo agregamos a la BD.
        // Aquí lo agregamos como propiedad real para persistencia.
        public string Notas { get; set; } = string.Empty;

        // Resultados Resumidos (Para búsquedas rápidas)
        public double VelocidadMax { get; set; }

        // Propiedades requeridas por el Generador de Reportes
        // Alias para FuerzaMaxCompresion y FuerzaMaxExpansion si queremos mantener nombres consistentes
        public double FuerzaMaxCompresion { get; set; }
        public double FuerzaMaxExpansion { get; set; }

        // Propiedades de acceso rápido (Alias) para compatibilidad con GeneradorReporte.cs
        [NotMapped]
        public double MaxCompresion
        {
            get => FuerzaMaxCompresion;
            set => FuerzaMaxCompresion = value;
        }

        [NotMapped]
        public double MaxExpansion
        {
            get => FuerzaMaxExpansion;
            set => FuerzaMaxExpansion = value;
        }

        public double Temperatura { get; set; }

        // EL SECRETO: Los datos crudos (Arrays de Tiempo, Pos, Fuerza) guardados como BLOB
        // Esto permite guardar gráficas enteras en un solo campo.
        public byte[] DatosCrudos { get; set; }

        [ForeignKey("Vehiculo")]
        public int VehiculoId { get; set; }
        public virtual Vehiculo Vehiculo { get; set; }
    }
}