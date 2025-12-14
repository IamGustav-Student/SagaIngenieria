using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SagaIngenieria.Modelos; // Importamos el protocolo

namespace SagaIngenieria
{
    public class DriverMaquina
    {
        private SerialPort _puertoSerie;
        private bool _conectado = false;
        private bool _motorActivo = false;
        private CancellationTokenSource _cancelToken;

        // Eventos para la UI (Mantenemos la firma original para no romper MainWindow)
        public event Action<double, double> NuevosDatosRecibidos;
        public event Action<string> LogEstado;

        public DriverMaquina()
        {
            _puertoSerie = new SerialPort();
        }

        public bool Conectar(string nombrePuerto)
        {
            
                if (_puertoSerie.IsOpen) _puertoSerie.Close();

                // 1. CONFIGURACIÓN CORRECTA (CRÍTICO)
                // El código legacy usa 57600, no 9600.
                _puertoSerie.PortName = nombrePuerto;
                _puertoSerie.BaudRate = 57600;
                _puertoSerie.DataBits = 8;
                _puertoSerie.Parity = Parity.None;
                _puertoSerie.StopBits = StopBits.One;
                _puertoSerie.Handshake = Handshake.None; // Importante: Sin control de flujo por hardware
                                                         // Tiempos de espera cortos para no congelar la UI si falla
                _puertoSerie.ReadTimeout = 500;
                _puertoSerie.WriteTimeout = 500;


            try
            {
                

                _puertoSerie.Open();

                // Limpiamos la cañería antes de arrancar
                _puertoSerie.DiscardInBuffer();
                _puertoSerie.DiscardOutBuffer();

                // 2. HANDSHAKE / HABILITACIÓN
                // Enviamos el comando de "Habilitar Equipo" (:C00DAZ)
                // Tu código enviaba C00Z, que la máquina ignoraba por seguridad.
                EnviarTramaRaw(SagaProtocol.HabilitarEquipo);

                // Esperamos un instante técnico para que el microprocesador de la máquina procese
                Thread.Sleep(150);

                _conectado = true;
                LogEstado?.Invoke($"Conectado a {nombrePuerto} (57600 baud). Equipo Habilitado.");
                return true;
            }
            catch (Exception ex)
            {
                LogEstado?.Invoke($"Error al conectar: {ex.Message}");
                return false;
            }
        }

        public void Desconectar()
        {
            try
            {
                if (_conectado && _puertoSerie.IsOpen)
                {
                    // Buenas prácticas: Avisar a la máquina que nos vamos
                    EnviarTramaRaw(SagaProtocol.DeshabilitarEquipo);
                }
            }
            catch { /* Ignorar errores al cerrar */ }
            finally
            {
                _motorActivo = false;
                if (_cancelToken != null) _cancelToken.Cancel();
                if (_puertoSerie.IsOpen) _puertoSerie.Close();
                _conectado = false;
                LogEstado?.Invoke("Desconectado.");
            }
        }

        public void EncenderMotor(double frecuenciaHz)
        {
            if (!_conectado) return;

            // Conversión a Hexadecimal según protocolo
            // Ejemplo: 1.5 Hz -> 15 decimal -> "0F" Hex
            int valorEntero = (int)(frecuenciaHz * 10);
            string hexValue = valorEntero.ToString("X2");

            // Armamos el comando: :C15D + XX + Z
            string cmd = $"{SagaProtocol.EncenderMotorHeader}{hexValue}{SagaProtocol.Terminador}";

            EnviarTramaRaw(cmd);

            _motorActivo = true;
            LogEstado?.Invoke($"Motor RUN: {frecuenciaHz} Hz (CMD: {cmd})");

            // Iniciamos el ciclo de lectura en segundo plano
            _cancelToken = new CancellationTokenSource();
            Task.Run(() => BucleLecturaDatos(_cancelToken.Token));
        }

        public void DetenerMotor()
        {
            _motorActivo = false;
            if (_cancelToken != null) _cancelToken.Cancel();

            // Comando de Parada Segura
            EnviarTramaRaw(SagaProtocol.DetenerMotor);
            LogEstado?.Invoke("Motor STOP enviado.");
        }

        // --- LÓGICA PRIVADA DE BAJO NIVEL ---

        /// <summary>
        /// Envía el string EXACTO sin agregar caracteres ocultos.
        /// CORRECCIÓN: Tu código anterior agregaba \r y dos puntos extra.
        /// </summary>
        private void EnviarTramaRaw(string trama)
        {
            if (!_puertoSerie.IsOpen) return;
            try
            {
                _puertoSerie.DiscardInBuffer(); // Limpiar ruidos viejos
                _puertoSerie.Write(trama);
            }
            catch (Exception ex)
            {
                LogEstado?.Invoke("Error TX: " + ex.Message);
            }
        }

        private async Task BucleLecturaDatos(CancellationToken token)
        {
            // Bucle de Polling (Pregunta - Respuesta)
            // Imitamos el comportamiento del Timer de VB6 del software viejo
            while (_motorActivo && _conectado && !token.IsCancellationRequested)
            {
                try
                {
                    // 1. SOLICITUD: Pedimos datos instantáneos (:C1AZ)
                    _puertoSerie.Write(SagaProtocol.LeerSensoresInstantaneo);

                    // 2. ESPERA: Damos tiempo al ADC para convertir (Crítico en RS232)
                    await Task.Delay(80, token);

                    // 3. LECTURA: Leemos todo lo que haya en el buffer
                    // Usamos ReadExisting en lugar de ReadTo para evitar bloqueos si falta la 'Z'
                    string respuesta = _puertoSerie.ReadExisting();

                    // 4. PARSEO (Decodificación)
                    // Buscamos la cabecera :C1BD
                    int indiceCabecera = respuesta.IndexOf(SagaProtocol.HeaderRespuestaDatos);

                    // Verificamos tener suficientes caracteres después de la cabecera
                    // Estructura esperada: ... :C1BD FFFF PPP ...
                    // FFFF (4 chars fuerza) + PPP (3 chars posición) = 7 chars mínimos de payload
                    if (indiceCabecera >= 0 && (respuesta.Length >= indiceCabecera + 5 + 7))
                    {
                        // Extraemos Hexadecimales
                        // Offset 5 es para saltar el ":C1BD"
                        string hexFuerza = respuesta.Substring(indiceCabecera + 5, 4);
                        string hexPos = respuesta.Substring(indiceCabecera + 9, 3);

                        // Convertimos Hex a Entero
                        int valFuerzaRaw = Convert.ToInt32(hexFuerza, 16);
                        int valPosRaw = Convert.ToInt32(hexPos, 16);

                        // ESCALADO (Según SerialDynoDriver.cs)
                        // Fuerza: Viene multiplicada por 10
                        double fuerzaKg = valFuerzaRaw * 0.1;

                        // Posición: Viene directa en pulsos/mm (Ajustar según calibración física)
                        double posicionMm = valPosRaw;

                        // Disparamos evento a la UI
                        NuevosDatosRecibidos?.Invoke(posicionMm, fuerzaKg);
                    }
                }
                catch (TaskCanceledException)
                {
                    break; // Salida limpia
                }
                catch (Exception)
                {
                    // Ignoramos errores de trama corrupta puntual para no frenar el bucle
                    // En telemetría es preferible perder un dato que frenar el proceso
                }

                // Pequeña pausa para no saturar el hilo
                await Task.Delay(20, token);
            }
        }
    }
}
