using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SagaIngenieria
{
    public class DriverMaquina
    {
        private SerialPort _puertoSerie;
        private bool _conectado = false;
        private bool _motorActivo = false;

        // Evento idéntico al del simulador para no romper la UI
        public event Action<double, double> NuevosDatosRecibidos;

        // Evento para notificar errores o estados a la UI
        public event Action<string> LogEstado;

        public DriverMaquina()
        {
            _puertoSerie = new SerialPort();
            // Configuración estándar (¡Verifica estos valores con el manual del hardware!)
            _puertoSerie.BaudRate = 9600;
            _puertoSerie.DataBits = 8;
            _puertoSerie.Parity = Parity.None;
            _puertoSerie.StopBits = StopBits.One;
            _puertoSerie.ReadTimeout = 500;
            _puertoSerie.WriteTimeout = 500;
        }

        public bool Conectar(string nombrePuerto)
        {
            try
            {
                if (_puertoSerie.IsOpen) _puertoSerie.Close();

                _puertoSerie.PortName = nombrePuerto;
                _puertoSerie.Open();

                // 1. Protocolo: Clave de acceso (Según punto 1 del doc)
                // PC envía :C00Z
                string respuesta = EnviarComando("C00Z");

                // EQUIPO responde :C99Z o :C88Z
                if (respuesta.Contains("C99Z") || respuesta.Contains("C88Z"))
                {
                    _conectado = true;
                    LogEstado?.Invoke("Conexión Exitosa con Máquina");
                    return true;
                }
                else
                {
                    LogEstado?.Invoke($"Respuesta inesperada al conectar: {respuesta}");
                    _puertoSerie.Close();
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogEstado?.Invoke($"Error de puerto: {ex.Message}");
                return false;
            }
        }

        public void Desconectar()
        {
            _motorActivo = false;
            if (_puertoSerie.IsOpen) _puertoSerie.Close();
            _conectado = false;
            LogEstado?.Invoke("Desconectado.");
        }

        public void EncenderMotor(int frecuenciaHz)
        {
            if (!_conectado) return;

            // Protocolo punto 22: :C15DXXZ
            // XX es Hexa. El valor es frecuencia * 10. 
            // Ejemplo: 1.0 Hz -> 10 decimal -> 0A Hexa.

            int valorParaEnviar = frecuenciaHz * 10;
            string hexValue = valorParaEnviar.ToString("X2"); // Convierte a Hexa 2 dígitos

            string cmd = $"C15D{hexValue}Z";
            string respuesta = EnviarComando(cmd);

            // Asumimos que si no da error, arrancó. 
            // (El doc no especifica respuesta de confirmación clara aquí, a veces es C99Z)
            _motorActivo = true;
            LogEstado?.Invoke($"Motor Encendido a {frecuenciaHz} Hz ({cmd})");

            // INICIAMOS EL BUCLE DE LECTURA DE DATOS
            Task.Run(BucleLecturaDatos);
        }

        public void DetenerMotor()
        {
            _motorActivo = false;
            // Protocolo punto 23: :C16Z
            EnviarComando("C16Z");
            LogEstado?.Invoke("Motor Detenido");
        }

        // --- LÓGICA PRIVADA ---

        private string EnviarComando(string contenidoComando)
        {
            if (!_puertoSerie.IsOpen) return "";

            try
            {
                // El protocolo usa ":" al inicio y presuntamente retorno de carro
                // Según el doc: PC-> EQUIPO :C00Z
                string trama = $":{contenidoComando}\r";

                // Limpiamos buffers antes de preguntar
                _puertoSerie.DiscardInBuffer();
                _puertoSerie.Write(trama);

                // Esperamos respuesta. El protocolo dice que terminan en Z habitualmente
                // Usaremos ReadTo("Z") o ReadLine según se comporte el equipo real
                string respuesta = _puertoSerie.ReadTo("Z");
                return respuesta + "Z"; // Le agregamos la Z que se comió el ReadTo
            }
            catch (TimeoutException)
            {
                return "TIMEOUT";
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex.Message;
            }
        }

        private async Task BucleLecturaDatos()
        {
            // ESTRATEGIA: POLLING (Preguntar repetidamente)
            // Como el doc no muestra un comando de "streaming", vamos a intentar
            // leer el resultado de conversión AD7730 (Punto 5 del doc) repetidamente.
            // COMANDO: :C04Z -> Responde :C04DXXXXXXZ

            while (_motorActivo && _conectado)
            {
                try
                {
                    // 1. Pedir FUERZA (Asumiendo que C04 trae el dato del sensor de carga)
                    // Nota: Necesitamos saber qué comando trae la POSICIÓN. 
                    // El doc menciona AD7730 (conversor AD). Asumiré por ahora que es Fuerza.
                    string respuesta = EnviarComando("C04Z");

                    if (respuesta.StartsWith(":C04D"))
                    {
                        // Parsear Hexa: :C04D XXXXXX Z
                        // XXXXXX son 6 caracteres hex
                        string hexData = respuesta.Substring(5, 6);
                        int valorCrudo = Convert.ToInt32(hexData, 16);

                        // CALIBRACIÓN TEMPORAL (Esto tendrás que ajustarlo con la máquina real)
                        // Digamos que FFF is 0 y tiene signo... esto es complejo sin ver el equipo.
                        // Por ahora lo tratamos como entero simple.
                        double fuerza = (valorCrudo - 8388608) / 100.0; // Simulando un offset de 24 bits

                        // NOTA: Falta la Posición. El protocolo es vago sobre dónde leer la posición en tiempo real.
                        // Usaremos un valor simulado para la posición por ahora para que el gráfico no falle
                        double posicion = 50 * Math.Sin(DateTime.Now.Millisecond / 100.0);

                        // Disparamos evento a la UI
                        NuevosDatosRecibidos?.Invoke(posicion, fuerza);
                    }
                }
                catch
                {
                    // Ignorar errores de timeout en el bucle para no frenar
                }

                // Esperar un poco para no saturar el puerto serie
                await Task.Delay(20);
            }
        }
    }
}
