using System.Windows;
using EvidenciasSQA.Forms;

namespace EvidenciasSQA.Helpers;

/// <summary>
/// Puente in-process entre el modulo Tray (WinForms) y la ventana WPF del Visor
/// (consolidacion .NET 9: ya no hay exe separado ni named pipes).
/// El MainForm del tray usa este gestor para mostrar/activar la ventana del visor
/// desde el menu contextual ("Abrir Visor").
/// </summary>
public static class VisorWindowManager
{
    private static Window _visorWindow;

    /// <summary>
    /// True cuando la app se está cerrando de verdad ("Salir" del tray): la ventana
    /// del visor DEBE permitir cerrarse (no ocultarse al tray). Sin esto, el
    /// Application.Shutdown() WPF se cancela en el handler Closing y el proceso
    /// queda vivo en segundo plano.
    /// </summary>
    public static bool IsQuitting { get; private set; }

    /// <summary>
    /// Marca la salida real de la aplicación. Se llama desde MainForm.Exit() antes
    /// del Shutdown WPF para que el Closing de la ventana del visor deje de cancelarse.
    /// </summary>
    public static void SetQuitting()
    {
        IsQuitting = true;
    }

    /// <summary>
    /// Registra la ventana principal del visor (llamado por App.OnStartup).
    /// </summary>
    public static void Register(Window visorWindow)
    {
        _visorWindow = visorWindow;
    }

    /// <summary>
    /// Muestra y activa la ventana del visor. Si estaba minimizada, la restaura.
    /// Seguro para invocar desde el hilo del MainForm (mismo Dispatcher WPF).
    /// </summary>
    public static void ShowVisor()
    {
        if (_visorWindow == null)
        {
            return;
        }

        _visorWindow.Show();
        if (_visorWindow.WindowState == WindowState.Minimized)
        {
            _visorWindow.WindowState = WindowState.Normal;
        }
        _visorWindow.Activate();
    }
}