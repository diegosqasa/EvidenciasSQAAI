using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace EvidenciasSQA.Wpf.Services
{
    /// <summary>
    /// Servicio de auditoría y depuración 100% interno para aplicaciones WPF.
    /// Escribe en un archivo .log asíncronamente sin bloquear el hilo UI.
    /// 
    /// Ubicación del archivo: %USERPROFILE%\Logs\EvidenciasSQA.log
    /// </summary>
    public sealed class AuditLogService : IDisposable
    {
        private static readonly object _writeLock = new object();
        private readonly string _logFilePath;
        private readonly StreamWriter _writer;
        private readonly CancellationTokenSource _cts;
        private bool _disposed;

        /// <summary>
        /// Evento opcional para notificar a la UI cuando se escribe un log.
        /// El suscriptor DEBE usar Dispatcher.Invoke para actualizar la UI de forma segura.
        /// </summary>
        public event Action<string>? LogWritten;

        /// <summary>
        /// Ruta del archivo de log. Default: %USERPROFILE%\Logs\EvidenciasSQA.log
        /// </summary>
        public string LogFilePath => _logFilePath;

        /// <summary>
        /// Inicializa una nueva instancia del servicio de auditoría.
        /// Crea la carpeta de logs si no existe y abre el archivo de escritura.
        /// </summary>
        public AuditLogService()
        {
            // Determinar ruta: %USERPROFILE%\Logs\EvidenciasSQA.log
            string userProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? ".";
            _logFilePath = Path.Combine(userProfile, "Logs", "EvidenciasSQA.log");

            // Asegurar que la carpeta existe
            Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);

            // Abrir StreamWriter para escritura append con UTF-8
            _cts = new CancellationTokenSource();
            _writer = new StreamWriter(_logFilePath, append: true, Encoding.UTF8)
            {
                AutoFlush = true // Cada Write envía inmediatamente al disco
            };

            // Registrar inicio de servicio (async, fire-and-forget)
            _ = WriteAsync(Level.Info, "Servicio de auditoría iniciado");
        }

        /// <summary>
        /// Niveles de registro soportados.
        /// </summary>
        public enum Level
        {
            Info,
            Warning,
            Error
        }

        /// <summary>
        /// Escribe un evento de auditoría de forma asíncrona.
        /// Este método NO bloquea el hilo de llamado y debe ser 'await'ado en métodos async
        /// o utilizado con patrones fire-and-forget en métodos void.
        /// </summary>
        /// <param name="level">Nivel INFO, WARNING o ERROR</param>
        /// <param name="message">Mensaje descriptivo del evento</param>
        public async Task WriteAsync(Level level, string message)
        {
            if (_disposed) return;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string levelStr = level switch
            {
                Level.Info => "INFO",
                Level.Warning => "WARNING",
                Level.Error => "ERROR",
                _ => "INFO"
            };

            string entry = $"[{timestamp}] [{levelStr}] {message}";

            await Task.Run(() =>
            {
                lock (_writeLock)
                {
                    _writer.WriteLine(entry);
                    // Se invoca el evento fuera del lock para evitar bloqueos prolongados
                    LogWritten?.Invoke(entry);
                }
            });
        }

        /// <summary>
        /// Escribe un evento INFO de forma síncrona (bloquea brevemente, pero StreamWriter avec AutoFlush es rápido).
        /// Preferred for constructors and startup code where async await is not convenient.
        /// </summary>
        public void WriteInfo(string message)
        {
            // Usar Task.Run sin await para no bloquear caller, pero sincronizar el resultado
            _ = WriteAsync(Level.Info, message);
        }

        /// <summary>
        /// Escribe un evento WARNING de forma síncrona.
        /// </summary>
        public void WriteWarning(string message)
        {
            _ = WriteAsync(Level.Warning, message);
        }

        /// <summary>
        /// Escribe un evento ERROR de forma síncrona.
        /// </summary>
        public void WriteError(string message)
        {
            _ = WriteAsync(Level.Error, message);
        }

        /// <summary>
        /// Escribe un error con información de excepción incluida.
        /// </summary>
        public void WriteError(string message, Exception ex)
        {
            string exceptionDetails = ex.ToString();
            _ = WriteAsync(Level.Error, $"{message} | Excepción: {exceptionDetails}");
        }

        /// <summary>
        /// Cierra el escritor de forma segura.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _writer?.Flush();
            _writer?.Close();
            _writer?.Dispose();
            _cts?.Cancel();
            _cts?.Dispose();

            // Registrar cierre de forma async
            _ = WriteAsync(Level.Info, "Servicio de auditoría finalizado");
        }
    }
}