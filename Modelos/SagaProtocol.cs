namespace SagaIngenieria.Modelos
{
    /// <summary>
    /// Definición estricta del protocolo según "Protocolo de comunicación.doc".
    /// REVISIÓN INGENIERÍA: Se eliminaron comandos inventados.
    /// </summary>
    public static class SagaProtocol
    {
        // --- 1. ACCESO Y CONTROL ---
        // Doc Sección 1: Clave de acceso.
        // PC envía :C00Z -> Equipo responde :C99Z o :C88Z
        public const string HabilitarEquipo = ":C00Z";

        // Doc: "En caso de que se cuelgue... se puede deshabilitar" (No especifica comando explícito de deshabilitar, 
        // pero asumimos reset o re-envío de C00Z. Mantenemos el estándar de cierre si existiera en versiones nuevas, 
        // pero por defecto usamos el handshake básico).

        // --- 2. MOTOR (Secciones 22 y 23) ---
        // :C15DXXZ -> XX es frecuencia * 10 en Hexa.
        public const string EncenderMotorHeader = ":C15D";
        public const string DetenerMotor = ":C16Z";

        // --- 3. ADQUISICIÓN (MODO BATCH - Secciones 24 y 25) ---

        // Paso 1: Configurar cantidad de datos a adquirir.
        // :C17DXXXXZ (XXXX = muestras en Hexa, Max 3FFF = 16383)
        public const string ConfigurarAdquisicionHeader = ":C17D";

        // Paso 2: Pedir envío de datos almacenados (Volcado).
        public const string IniciarDescargaDatos = ":C18Z";

        // Paso 3: Handshake de paquete. 
        // La PC debe enviar 'Q' para pedir el siguiente paquete de 16 datos.
        public const string AcknowledgePaquete = "Q";

        // --- 4. RESPUESTAS ESPERADAS ---
        public const string RespuestaOK_99 = ":C99Z"; // Operación completada / Habilitado
        public const string RespuestaOK_88 = ":C88Z"; // Alternativa de habilitado

        public const string HeaderPaqueteDatos = ":C18D"; // Cabecera de trama de datos
        public const string FinDeTransmision = ":C19Z";   // Fin de volcado
        public const string Terminador = "Z";
    }
}