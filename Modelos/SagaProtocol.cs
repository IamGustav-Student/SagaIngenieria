namespace SagaIngenieria.Modelos
{
    public static class SagaProtocol
    {
        // Comandos de Control (Handshake)
        public const string HabilitarEquipo = ":C00DAZ"; // EL CORRECTO
        public const string DeshabilitarEquipo = ":C00DHZ";
        public const string DetenerMotor = ":C16Z";
        public const string ResetPantalla = ":C14D0Z";

        // Comandos de Configuración (Hardware)
        // Descubiertos en frmConfCelda.frm
        public const string ConfModeRegister = ":C05D2080Z";
        public const string ConfGainRegister = ":C0DD480000Z";
        public const string ConfOffsetRegister = ":C0BD800000Z";
        public const string ConfFilterRegister = ":C07D266200Z"; // Crítico para estabilidad
        public const string EjecutarConfig = ":C12Z";

        // Comandos de Operación
        public const string LeerSensores = ":C1AZ"; // Polling (Calibración)
        public const string DescargaMasiva = ":C18Z"; // Batch Dump
        public const string Acknowledge = "Q"; // Para pedir siguiente paquete

        // Comandos dinámicos (requieren parámetros)
        public static string EncenderMotor(double hz)
        {
            // Formato VB6: ":C15D" & tHex(velocidades(j) * 10, "00") & "Z"
            int val = (int)(hz * 10);
            return $":C15D{val:X2}Z";
        }

        public static string ConfigurarAdquisicion(long cantidadDatos)
        {
            // Formato VB6: ":C17D" & tHex(cantDatosA, "0000") & "Z"
            return $":C17D{cantidadDatos:X4}Z";
        }
    }
}