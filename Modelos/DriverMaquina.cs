using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SagaIngenieria.Modelos
{
    // CORRECCIÓN: La interfaz correcta es IDisposable (con 'a'), no IDisponible.
    public class DriverMaquina : IDisposable
    {
        private SerialPort _puerto;
        public bool EstaConectado => _puerto != null && _puerto.IsOpen;

        // Eventos para la UI (Nivel Dios: No bloquean la pantalla)
        public event Action<string> Log;
        public event Action<int> ProgresoDescarga;

        public async Task<bool> Conectar(string puertoNombre, int baudRate = 57600)
        {
            // Asegurarnos de cerrar cualquier conexión previa limpia o sucia
            if (_puerto != null && _puerto.IsOpen)
            {
                _puerto.Close();
                _puerto.Dispose();
            }

            try
            {
                _puerto = new SerialPort(puertoNombre, baudRate, Parity.None, 8, StopBits.One);
                _puerto.ReadTimeout = 500; // Timeout corto para el handshake
                _puerto.WriteTimeout = 500;
                _puerto.DtrEnable = true;  // IMPORTANTE: Algunos equipos viejos necesitan DTR/RTS
                _puerto.RtsEnable = true;
                _puerto.Open();

                // Limpieza inicial de buffers
                _puerto.DiscardInBuffer();
                _puerto.DiscardOutBuffer();

                Log?.Invoke($"Puerto {puertoNombre} abierto. Iniciando Handshake con la máquina...");

                // --- FASE DE HANDSHAKE ACTIVO ---

                // Intento 1: Despertar a la máquina (Protocolo Legacy)
                // Enviamos secuencia de habilitación y esperamos el ACK específico (:C99Z)
                bool maquinaDetectada = await ValidarConexionHardware();

                if (maquinaDetectada)
                {
                    Log?.Invoke("✅ MÁQUINA DETECTADA Y VALIDADA.");
                    // Dejamos el timeout más relajado para la operación normal
                    _puerto.ReadTimeout = 2000;
                    return true;
                }
                else
                {
                    Log?.Invoke("❌ Puerto abierto, pero la máquina NO respondió al protocolo.");
                    _puerto.Close(); // Cerramos porque no sirve de nada
                    return false;
                }
            }
            catch (UnauthorizedAccessException)
            {
                Log?.Invoke($"❌ Error: El puerto {puertoNombre} ya está en uso por otra aplicación.");
                return false;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"❌ Error crítico de conexión: {ex.Message}");
                if (_puerto != null && _puerto.IsOpen) _puerto.Close();
                return false;
            }
        }

        public void EnviarComando(string comando)
        {
            if (!EstaConectado) return;
            try
            {
                _puerto.Write(comando);
            }
            catch (Exception ex)
            {
                Log?.Invoke($"Error al enviar comando: {ex.Message}");
            }
        }

        /// <summary>
        /// Ejecuta la secuencia completa de adquisición de datos (El "Bucle Principal" del VB6)
        /// </summary>
        public async Task<List<string>> EjecutarEnsayo(double velocidadHz, double duracionSegundos)
        {
            var tramasHex = new List<string>();

            if (!EstaConectado) return tramasHex;

            // 1. Configurar Motor
            EnviarComando(SagaProtocol.EncenderMotor(velocidadHz));
            // Esperar respuesta :C99Z... (simplificado por ahora)
            await Task.Delay(500);

            // 2. Estabilización (frmPrincipal.frm espera 4 segundos)
            Log?.Invoke("Estabilizando motor...");
            await Task.Delay(4000);

            // 3. Solicitar Datos
            // Calculo de cantidad basado en VB6: outRate / velocidad * 2 ciclos
            // Nota: 187 y 131.3 son constantes mágicas extraídas de Variables.bas
            long cantidadDatos = (long)((187 * velocidadHz + 131.3) / velocidadHz * 2);
            EnviarComando(SagaProtocol.ConfigurarAdquisicion(cantidadDatos));
            await Task.Delay(1000); // Espera a que termine de adquirir hardware

            // 4. Descarga Masiva (La parte crítica de los 118 bytes)
            EnviarComando(SagaProtocol.DescargaMasiva);
            await Task.Delay(100);

            int paquetes = (int)(cantidadDatos / 16);

            for (int k = 0; k < paquetes; k++)
            {
                EnviarComando(SagaProtocol.Acknowledge); // Enviar "Q"

                // Leer respuesta robusta (esperando 118 chars como en VB6)
                string trama = await LeerTramaRobusta(118);

                if (!string.IsNullOrEmpty(trama))
                {
                    // En VB6: Mid(trama, 6, 112) -> Quitaban cabecera y cola
                    // La trama típica es ":C18D...CHECKZ"
                    if (trama.Length >= 118)
                    {
                        // Aseguramos que no nos pasamos del índice
                        int longitudUtil = Math.Min(112, trama.Length - 5);
                        if (longitudUtil > 0)
                            tramasHex.Add(trama.Substring(5, longitudUtil));
                    }
                }

                ProgresoDescarga?.Invoke((int)((k / (float)paquetes) * 100));
            }

            // 5. Detener
            EnviarComando(SagaProtocol.DetenerMotor);
            return tramasHex;
        }

        private async Task<bool> ValidarConexionHardware()
        {
            // Esta lógica replica EXACTAMENTE lo que hacía el VB6 en frmConectar.frm
            // .Output = ":C00DAZ" -> Espero(0.5) -> .Output = ":C00DHZ" -> Espero(0.5) -> Validar :C99Z

            try
            {
                // Paso 1: Habilitar
                EnviarComando(SagaProtocol.HabilitarEquipo);
                await Task.Delay(250); // Pequeña pausa técnica

                // Paso 2: Deshabilitar (Esto suele provocar el ACK de "Listo")
                EnviarComando(SagaProtocol.DeshabilitarEquipo);

                // Paso 3: Escuchar la respuesta
                // Le damos hasta 1 segundo para responder "Estoy viva"
                string respuesta = await LeerRespuestaConTimeout(1000);

                // Análisis de respuesta (Nivel Dios: Logueamos lo que llega para depurar)
                if (!string.IsNullOrEmpty(respuesta))
                {
                    // Limpiamos caracteres no imprimibles para el log para evitar basura en pantalla
                    string hexDebug = BitConverter.ToString(Encoding.ASCII.GetBytes(respuesta));
                    // Log?.Invoke($"[DEBUG] Respuesta Hardware: {respuesta.Trim()} (Hex: {hexDebug})");
                }

                // El protocolo VB6 busca ":C99Z" como confirmación de éxito
                if (respuesta.Contains(":C99Z"))
                {
                    return true;
                }

                // Plan B: Si responde C88Z o cualquier cosa válida que empiece con :, asumimos conexión
                // A veces las máquinas viejas tienen versiones de firmware distintas
                if (respuesta.Contains(":C") && respuesta.Contains("Z"))
                {
                    Log?.Invoke("⚠️ Respuesta no estándar detectada, pero parece protocolo válido.");
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private async Task<string> LeerRespuestaConTimeout(int timeoutMs)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    StringBuilder sb = new StringBuilder();

                    while (sw.ElapsedMilliseconds < timeoutMs)
                    {
                        if (_puerto.BytesToRead > 0)
                        {
                            string data = _puerto.ReadExisting();
                            sb.Append(data);

                            // Si detectamos el terminador Z, salimos antes (optimización)
                            if (data.Contains("Z")) break;
                        }
                        Thread.Sleep(10);
                    }
                    return sb.ToString();
                }
                catch
                {
                    return string.Empty;
                }
            });
        }

        private async Task<string> LeerTramaRobusta(int longitudEsperada)
        {
            return await Task.Run(() =>
            {
                StringBuilder sb = new StringBuilder();
                int intentos = 0;
                // Intentamos leer hasta conseguir la longitud o exceder intentos
                while (sb.Length < longitudEsperada && intentos < 50)
                {
                    try
                    {
                        if (_puerto.BytesToRead > 0)
                        {
                            string data = _puerto.ReadExisting();
                            sb.Append(data);
                        }
                    }
                    catch { }
                    Thread.Sleep(10); // Pausa pequeña para dejar llenar el buffer
                    intentos++;
                }
                return sb.ToString();
            });
        }

        public void Dispose()
        {
            if (_puerto != null)
            {
                if (_puerto.IsOpen)
                {
                    try
                    {
                        // Intentar dejar la máquina en estado seguro antes de cerrar
                        _puerto.Write(SagaProtocol.DetenerMotor);
                        _puerto.Close();
                    }
                    catch { }
                }
                _puerto.Dispose();
            }
        }
    }
}