using System.Windows.Threading;

namespace EvidenciasSQA.Core.Services;

/// <summary>
/// Tipo de toast in-app (paridad con showToast de la web: success/error/warning/info,
/// con los colores de borde e icono de main.css #toast y ui-utils.js).
/// </summary>
public enum ToastType
{
    Success,
    Error,
    Warning,
    Info
}

/// <summary>
/// Mensaje a mostrar en el toast in-app.
/// </summary>
public sealed class ToastMessage
{
    public string Text { get; }

    public ToastType Type { get; }

    /// <summary>Duración en pantalla. null = valor por defecto (3 s, como la web).</summary>
    public TimeSpan? Duration { get; }

    public ToastMessage(string text, ToastType type = ToastType.Success, TimeSpan? duration = null)
    {
        Text = text;
        Type = type;
        Duration = duration;
    }
}

/// <summary>
/// Servicio de toasts in-app. Los view models publican mensajes transitorios de un solo
/// uso (copiar, descargar, eliminar, exportar, captura recibida) y la ventana los muestra
/// mediante un ToastHost. Reemplazo único: un nuevo toast sustituye al anterior (spec web).
/// </summary>
public interface IToastService
{
    /// <summary>Se dispara cuando hay un mensaje que mostrar. Se invoca en el hilo del Dispatcher de UI.</summary>
    event Action<ToastMessage>? ToastRequested;

    /// <summary>Publica un mensaje de toast.</summary>
    void Show(string text, ToastType type = ToastType.Success, TimeSpan? duration = null);
}

/// <summary>
/// Implementación concreta del servicio de toasts in-app. Mantiene un único evento que la
/// ventana suscribe; los mensajes se encolan en el Dispatcher de la UI para que cualquier hilo
/// pueda publicarlos de forma segura. Reemplazo único: cada nuevo mensaje sustituye al anterior.
/// El FrameworkReference Microsoft.WindowsDesktop.App de Core expone Dispatcher/Application.
/// </summary>
public sealed class ToastService : IToastService
{
    private readonly Dispatcher _dispatcher;

    public ToastService(Dispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher ?? System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    /// <summary>Instancia única compartida por el visor y el editor.</summary>
    public static IToastService Instance { get; } = new ToastService();

    public event Action<ToastMessage>? ToastRequested;

    public void Show(string text, ToastType type = ToastType.Success, TimeSpan? duration = null)
    {
        var message = new ToastMessage(text, type, duration);
        if (_dispatcher.CheckAccess())
        {
            ToastRequested?.Invoke(message);
        }
        else
        {
            _dispatcher.BeginInvoke(() => ToastRequested?.Invoke(message));
        }
    }
}