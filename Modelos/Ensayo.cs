using System;

namespace SagaIngenieria.Modelos
{
    // Esta es la hoja de prueba de un amortiguador
    public class Ensayo
    {
        public int Id { get; set; }

        public int VehiculoId { get; set; }
        public Vehiculo Vehiculo { get; set; }

        public DateTime Fecha { get; set; } = DateTime.Now;
        public string Notas { get; set; } = string.Empty; // Ej: "Probando setup duro"

        // DATOS TÉCNICOS GUARDADOS
        public double MaxCompresion { get; set; }
        public double MaxExpansion { get; set; }

        // Aquí guardaremos toda la curva (los miles de puntos) comprimida
        // Usamos un array de bytes para que sea eficiente
        public byte[] DatosCrudos { get; set; }
    }
}
