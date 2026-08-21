using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using EvidenciasSQA.Base.Core;
using EvidenciasSQA.Core.Events;
using EvidenciasSQA.Core.Helpers;
using EvidenciasSQA.Core.Services;
using EvidenciasSQA.Destinations;
using EvidenciasSQA.Forms;
using EvidenciasSQA.Helpers;
using EvidenciasSQA.HttpListeners;
using EvidenciasSQA.Wpf.ViewModels;
using EvidenciasSQA.Wpf.Views;

namespace EvidenciasSQA;

/// <summary>
/// Application WPF unificada (consolidacion .NET 9): gestiona simultaneamente los
/// dos modulos en un unico proceso y un unico Dispatcher:
///   - Modulo Tray: MainForm (WinForms oculto con NotifyIcon). El message pump es
///     este Dispatcher WPF; WinForms ya no ejecuta su propio Application.Run.
///   - Modulo Visor: MainWindow (WPF) con su ViewerViewModel. "Abrir Visor" del menu
///     del tray la muestra in-process (sin exe separado ni named pipes).
/// El flujo de captura llega al visor via el bus SqaEvents (Core), en el mismo proceso.
/// </summary>
public partial class App : Application
{
    /// <summary>Formulario oculto del tray, validado y creado por MainForm.Start.</summary>
    internal MainForm StartupForm { get; set; }

    private ViewerViewModel _visorViewModel;
    private MainWindow _visorWindow;
    private SqaHttpListener _sqaHttpListener;

    protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (StartupForm == null)
            {
                // MainForm.Start aborto el arranque (instancia unica, exit, reload, ...).
                Shutdown();
                return;
            }

            // Modulo Visor (WPF) dentro del mismo proceso que el tray.
            _visorViewModel = new ViewerViewModel();
            _visorWindow = new MainWindow(_visorViewModel);
            VisorWindowManager.Register(_visorWindow);
            // Los view models del visor usan Application.Current.MainWindow (p.ej. para
            // bloquear la ventana durante una exportacion): apuntar MainWindow al visor.
            MainWindow = _visorWindow;

            // Listener HTTP loopback de la extension web Ext_Web (contrato
            // ext-web-visor-greenshot.md): recibe capturas en :3000, las persiste en
            // ~/CapturasQA y notifica al visor via el bus SqaEvents. Se arranca ANTES
            // de que el visor cargue el historial para no perder capturas tempranas.
            // El guardado se delega a un Task.Run: la extension recibe la respuesta
            // del contrato de forma inmediata (status processing) sin bloquear el request.
            // Si el puerto esta ocupado (p.ej. la app Electron instalada corriendo),
            // NO se aborta el arranque: se registra y se continua sin listener.
            try
            {
                _sqaHttpListener = new SqaHttpListener();
                _sqaHttpListener.CaptureReceived += (_, args) => Task.Run(() => PersistIncomingCapture(args));
                _sqaHttpListener.ViewerOpenRequested += (_, _) => SqaEvents.RaiseRestoreViewerRequested();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SqaHttpListener] No se pudo iniciar en :3000 (puerto en uso). La app continua sin listener HTTP: {ex.Message}");
                _sqaHttpListener = null;
            }
            
            // Cuando el usuario cierra la ventana del visor, se oculta en lugar de
            // terminar la aplicación. La única forma de cerrar la app es desde el
            // menú tray "Salir". Durante la salida real (VisorWindowManager.IsQuitting)
            // el cierre NO se cancela: el Application.Shutdown() WPF debe poder cerrar
            // la ventana o el proceso quedaría vivo en segundo plano.
            _visorWindow.Closing += (s, e) =>
            {
                if (VisorWindowManager.IsQuitting) return;
                e.Cancel = true;
                _visorWindow.Hide();
            };

            // Regla de oro: el visor NUNCA se trae al frente por una captura; cuando
            // el usuario lo trae al frente por CUALQUIER vía (barra de tareas, tray,
            // restaurar desde minimizado) se muestra la última captura guardada en disco.
            _visorWindow.IsVisibleChanged += (_, e) =>
            {
                if (e.NewValue is true) _visorViewModel.ShowLastCapture();
            };
            _visorWindow.StateChanged += (_, e) =>
            {
                if (_visorWindow.WindowState == WindowState.Normal) _visorViewModel.ShowLastCapture();
            };

            // Modulo Tray (WinForms): formulario oculto con NotifyIcon; el message pump
            // es el Dispatcher de esta Application WPF (WinForms ya no corre su propio loop).
            StartupForm.Show();

            // Provisional durante la consolidacion: se muestra el visor al arrancar para
            // validar ambos modulos activos. El arranque silencioso en la bandeja (visor
            // abierto solo con "Abrir Visor") se restaura al cerrar esta fase.
            VisorWindowManager.ShowVisor();
        }

        /// <summary>
        /// Persiste una captura recibida de la extension web en ~/CapturasQA y notifica
        /// al visor. Secuencia identica al flujo de captura del Tray (CaptureHelper):
        /// (A) Escritura PNG → (B) BakeCorporateHeader si la extension no horneo →
        /// (C) SqaCaptureFlow.OnCaptureCompleted → SqaEvents.CaptureSaved → Visor.
        /// Best-effort: un fallo nunca rompe el request HTTP (la extension reintenta).
        /// </summary>
        private static void PersistIncomingCapture(SqaHttpListener.CaptureRequestEventArgs args)
        {
            try
            {
                byte[] bytes = args.ResolveImageBytes();
                if (bytes == null || bytes.Length == 0)
                {
                    return;
                }

                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "CapturasQA");
                Directory.CreateDirectory(folder);

                int sequenceNumber = SqaEvidenceSequence.Next(folder);
                string fullPath = Path.Combine(folder, $"Evidencias_{sequenceNumber:D2}.png");
                File.WriteAllBytes(fullPath, bytes);

                if (!args.HasHeader)
                {
                    var details = new CaptureDetails
                    {
                        Title = args.Title ?? "Extension Web",
                        DateTime = ParseTimestamp(args.Timestamp),
                        Filename = fullPath
                    };
                    FileDestination.BakeCorporateHeader(fullPath, details);
                }
                else
                {
                    // La extensión ya horneó el header completo: solo falta el pHYs
                    // 96 DPI (replica de injectPhysDpi en el camino skip del worker).
                    PngPhysChunk.Inject96Dpi(fullPath);
                }

                new SqaCaptureFlow().OnCaptureCompleted(fullPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SqaHttpListener] Error persistiendo captura: {ex.Message}");
            }
        }

        private static DateTime ParseTimestamp(string? iso)
        {
            return DateTime.TryParse(iso, out DateTime dt) ? dt : DateTime.Now;
        }
}