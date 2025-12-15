using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SagaIngenieria.Modelos;

namespace SagaIngenieria
{
    public class DriverMaquina
    {
        private SerialPort _puertoSerie;
        private bool _conectado = false;

        // Eventos
        public event Action<double, double> NuevosDatosRecibidos; // Para gráfico
        public event Action<string> LogEstado; // Para debug en UI
        public event Action DescargaFinalizada; // Nuevo: Avisa cuando termina de bajar todo

        public DriverMaquina()
        {
            _puertoSerie = new SerialPort();
            // Suscribimos al evento de recepción de datos para el modo asíncrono
            _puertoSerie.DataReceived += ProcesarEntradaPuerto;
        }

        // --- CONEXIÓN ---
        public bool Conectar(string nombrePuerto)
        {
            if (_puertoSerie.IsOpen) _puertoSerie.Close();

            // Configuración según estándar industrial RS232 para microcontroladores viejos
            _puertoSerie.PortName = nombrePuerto;
            _puertoSerie.BaudRate = 57600; // Según tu código legacy
            _puertoSerie.DataBits = 8;
            _puertoSerie.Parity = Parity.None;
            _puertoSerie.StopBits = StopBits.One;
            _puertoSerie.Handshake = Handshake.None;
            _puertoSerie.ReadTimeout = 1000;
            _puertoSerie.WriteTimeout = 1000;

            try
            {
                _puertoSerie.Open();
                _puertoSerie.DiscardInBuffer();
                _puertoSerie.DiscardOutBuffer();

                // 1. HANDSHAKE (Sección 1 del Doc)
                LogEstado?.Invoke($"Enviando Handshake ({SagaProtocol.HabilitarEquipo})...");
                _puertoSerie.Write(SagaProtocol.HabilitarEquipo);

                // Esperamos respuesta brevemente (Bloqueante solo al inicio)
                Thread.Sleep(200);
                string respuesta = _puertoSerie.ReadExisting();

                if (respuesta.Contains(":C99Z") || respuesta.Contains(":C88Z"))
                {
                    _conectado = true;
                    LogEstado?.Invoke("CONEXIÓN EXITOSA: Equipo Habilitado.");
                    return true;
                }
                else
                {
                    // Intento fallback: A veces ya estaba conectado
                    LogEstado?.Invoke($"Respuesta inesperada: {respuesta}. Asumiendo conexión forzada.");
                    _conectado = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogEstado?.Invoke($"ERROR COM: {ex.Message}");
                return false;
            }
        }

        public void Desconectar()
        {
            try
            {
                if (_puertoSerie.IsOpen) _puertoSerie.Close();
            }
            catch { }
            _conectado = false;
            LogEstado?.Invoke("Puerto cerrado.");
        }

        // --- CONTROL DE MOTOR Y ADQUISICIÓN ---

        public void IniciarEnsayo(double frecuenciaHz)
        {
            if (!_conectado) return;

            try
            {
                _puertoSerie.DiscardInBuffer();

                // PASO 1: Configurar Adquisición (Sección 24)
                // Pedimos el máximo de muestras (3FFF = 16383 puntos).
                // Esto asegura que la máquina grabe internamente.
                string cmdConfig = $"{SagaProtocol.ConfigurarAdquisicionHeader}3FFF{SagaProtocol.Terminador}";
                _puertoSerie.Write(cmdConfig);
                LogEstado?.Invoke("Memoria Configurada (3FFF).");
                Thread.Sleep(100); // Tiempo para que el micro procese

                // PASO 2: Encender Motor (Sección 22)
                int valorEntero = (int)(frecuenciaHz * 10);
                string hexFreq = valorEntero.ToString("X2"); // 1.5Hz -> 15 -> 0F
                string cmdMotor = $"{SagaProtocol.EncenderMotorHeader}{hexFreq}{SagaProtocol.Terminador}";

                _puertoSerie.Write(cmdMotor);
                LogEstado?.Invoke($"MOTOR ON: {frecuenciaHz} Hz. Grabando en equipo...");
            }
            catch (Exception ex)
            {
                LogEstado?.Invoke("Error al Iniciar: " + ex.Message);
            }
        }

        public void DetenerYDescargar()
        {
            if (!_conectado) return;

            Task.Run(async () =>
            {
                try
                {
                    // PASO 1: Parar Motor
                    _puertoSerie.Write(SagaProtocol.DetenerMotor);
                    LogEstado?.Invoke("MOTOR STOP. Iniciando descarga de datos...");

                    await Task.Delay(500); // Esperar que el motor frene y el micro se estabilice

                    // PASO 2: Pedir Volcado de Datos (Sección 25)
                    _bufferRecepcion.Clear();
                    _descargandoDatos = true;
                    _puertoSerie.Write(SagaProtocol.IniciarDescargaDatos);
                }
                catch (Exception ex)
                {
                    LogEstado?.Invoke("Error en Secuencia de Parada: " + ex.Message);
                }
            });
        }

        // --- PROCESAMIENTO DE DATOS (COMPLEJO) ---

        private StringBuilder _bufferRecepcion = new StringBuilder();
        private bool _descargandoDatos = false;

        private void ProcesarEntradaPuerto(object sender, SerialDataReceivedEventArgs e)
        {
            if (!_descargandoDatos) return;

            try
            {
                string data = _puertoSerie.ReadExisting();
                _bufferRecepcion.Append(data);

                string bufferStr = _bufferRecepcion.ToString();

                // Analizamos si llegó el fin de transmisión
                if (bufferStr.Contains(SagaProtocol.FinDeTransmision))
                {
                    _descargandoDatos = false;
                    LogEstado?.Invoke("Descarga COMPLETADA.");
                    DescargaFinalizada?.Invoke();
                    return;
                }

                // Analizamos paquetes completos :C18D...
                // Formato: :C18D [CCCC PPP] [CCCC PPP] ... Z
                // CCCC: 4 hex Fuerza
                // PPP: 3 hex Posición

                while (true)
                {
                    int indexHeader = bufferStr.IndexOf(SagaProtocol.HeaderPaqueteDatos);
                    int indexTerminador = bufferStr.IndexOf(SagaProtocol.Terminador, indexHeader + 1);

                    if (indexHeader != -1 && indexTerminador != -1)
                    {
                        // Tenemos un paquete completo
                        string paquete = bufferStr.Substring(indexHeader, (indexTerminador - indexHeader) + 1);

                        // Parsear el contenido del paquete
                        ParsearPaquete(paquete);

                        // Eliminamos lo procesado del buffer
                        bufferStr = bufferStr.Substring(indexTerminador + 1);
                        _bufferRecepcion.Clear();
                        _bufferRecepcion.Append(bufferStr);

                        // CRÍTICO: El protocolo dice "la PC entonces debe enviar una Q"
                        // Enviamos el ACK para pedir el siguiente paquete
                        _puertoSerie.Write(SagaProtocol.AcknowledgePaquete);
                    }
                    else
                    {
                        break; // Esperar más datos
                    }
                }
            }
            catch (Exception ex)
            {
                LogEstado?.Invoke("Error RX: " + ex.Message);
            }
        }

        private void ParsearPaquete(string paquete)
        {
            // Ejemplo paquete: :C18D 01FF00A 020000B Z
            // Quitamos Header (:C18D) y Terminador (Z)
            string payload = paquete.Replace(SagaProtocol.HeaderPaqueteDatos, "").Replace(SagaProtocol.Terminador, "");

            // El payload contiene bloques de 7 caracteres (CCCCPPP)
            // CCCC = 4 chars fuerza
            // PPP = 3 chars posición

            int tamanoBloque = 7;
            for (int i = 0; i <= payload.Length - tamanoBloque; i += tamanoBloque)
            {
                try
                {
                    string bloque = payload.Substring(i, tamanoBloque);
                    string hexFuerza = bloque.Substring(0, 4);
                    string hexPos = bloque.Substring(4, 3);

                    // Conversión Hex a Int
                    int valFuerza = Convert.ToInt32(hexFuerza, 16);
                    int valPos = Convert.ToInt32(hexPos, 16);

                    // --- CALIBRACIÓN FÍSICA ---
                    // Ajustar estos factores según tu hardware real
                    double fuerzaKg = valFuerza * 0.1; // Suposición estándar

                    // Posición: El AD7730 suele ser 24 bits, pero aquí envían 12 bits (3 hex).
                    // Si el valor es Signed (Complemento a 2) hay que tratarlo, 
                    // pero asumiremos Unsigned con offset por ahora.
                    double posMm = valPos * 0.1;

                    NuevosDatosRecibidos?.Invoke(posMm, fuerzaKg);
                }
                catch
                {
                    // Ignorar punto corrupto
                }
            }
        }
    }
}