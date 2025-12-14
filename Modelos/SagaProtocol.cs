namespace SagaIngenieria.Modelos
{
    /// <summary>
    /// Definición estática del protocolo de comunicación SAGA.
    /// Fuente: Código Legacy (SerialDynoDriver.cs)
    /// </summary>
    public static class SagaProtocol
    {
        // COMANDOS DE CONTROL
        // La "DA" es crítica: Significa "Device Active". Sin esto, la máquina ignora al PC.
        public const string HabilitarEquipo = ":C00DAZ";
        public const string DeshabilitarEquipo = ":C00DHZ";

        // COMANDOS DE MOTOR
        public const string EncenderMotorHeader = ":C15D"; // Se le concatena la frecuencia en HEX
        public const string DetenerMotor = ":C16Z";

        // ADQUISICIÓN DE DATOS
        // :C1AZ Pide una lectura instantánea (Polling)
        public const string LeerSensoresInstantaneo = ":C1AZ";

        // RESPUESTAS ESPERADAS
        public const string HeaderRespuestaDatos = ":C1BD"; // La máquina responde con esto antes de los datos
        public const string Terminador = "Z";
    }
}