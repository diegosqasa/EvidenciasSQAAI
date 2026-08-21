using System.Threading.Tasks;
using System.Windows.Threading;

namespace EvidenciasSQA.Core.Services;

/// <summary>
/// Solicitud de confirmación modal (paridad con showConfirmDialog de la web:
/// mensaje, título, etiqueta del botón de aceptar y variante danger).
/// </summary>
public sealed class ConfirmationRequest
{
    public string Message { get; }

    public string Title { get; }

    public string AcceptLabel { get; }

    public bool IsDanger { get; }

    public TaskCompletionSource<bool> Result { get; }

    public ConfirmationRequest(string message, string title, string acceptLabel, bool isDanger)
    {
        Message = message;
        Title = title;
        AcceptLabel = acceptLabel;
        IsDanger = isDanger;
        Result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void Resolve(bool accepted)
    {
        Result.TrySetResult(accepted);
    }

    public void TryResolve(bool accepted)
    {
        Result.TrySetResult(accepted);
    }
}

/// <summary>
/// Servicio de confirmación in-app. Regla de oro: el cuadro de diálogo se renderiza como
/// una capa (overlay con fondo oscuro translúcido) DENTRO de la misma ventana, reutilizando
/// el header personalizado — nunca como ventana nativa nueva ni BrowserWindow.
/// Los view models publican una solicitud y la ventana la muestra; el resultado llega por Task.
/// </summary>
public interface IConfirmationService
{
    /// <summary>Se dispara cuando hay una confirmación que mostrar. Se invoca en el hilo del Dispatcher de UI.</summary>
    event Action<ConfirmationRequest>? ConfirmationRequested;

    /// <summary>Solicita una confirmación modal; true si el usuario acepta, false si cancela.</summary>
    Task<bool> AskAsync(string message, string title = "Confirmar acción", string acceptLabel = "Confirmar", bool danger = false);
}

/// <summary>
/// Implementación concreta del servicio de confirmación in-app. Mantiene un único evento que
/// la ventana suscribe; las solicitudes se encolan en el Dispatcher de la UI para que cualquier
/// hilo pueda pedirlas de forma segura. El FrameworkReference Microsoft.WindowsDesktop.App de
/// Core expone Dispatcher/Application.
/// </summary>
public sealed class ConfirmationService : IConfirmationService
{
    private readonly Dispatcher _dispatcher;

    public ConfirmationService(Dispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher ?? System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    /// <summary>Instancia única compartida por el visor y el editor.</summary>
    public static IConfirmationService Instance { get; } = new ConfirmationService();

    public event Action<ConfirmationRequest>? ConfirmationRequested;

    public Task<bool> AskAsync(string message, string title = "Confirmar acción", string acceptLabel = "Confirmar", bool danger = false)
    {
        var request = new ConfirmationRequest(message, title, acceptLabel, danger);
        if (_dispatcher.CheckAccess())
        {
            ConfirmationRequested?.Invoke(request);
        }
        else
        {
            _dispatcher.BeginInvoke(() => ConfirmationRequested?.Invoke(request));
        }

        return request.Result.Task;
    }
}