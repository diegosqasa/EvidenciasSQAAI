using EvidenciasSQA.Core.Interfaces;
using EvidenciasSQA.Core.Model;

namespace EvidenciasSQA.Core.Export;

/// <summary>
/// Base común de los destinos, análoga a EvidenciasSQA.Base.Core.AbstractDestination:
/// implementa el "boilerplate" (Designation por defecto, prioridad, activación,
/// comparación para ordenar) y deja ExportCapture como única responsabilidad real
/// del destino concreto.
/// </summary>
public abstract class AbstractDestination : IDestination
{
    public virtual string Designation => GetType().Name;

    public abstract string Description { get; }

    public virtual int Priority => 100;

    public virtual bool IsActive => true;

    public abstract ExportInformation ExportCapture(bool manuallyInitiated, SurfaceDocument surface, CaptureDetails? captureDetails);

    /// <summary>
    /// Orden natural por prioridad (menor primero) — IComparable de IDestination.
    /// </summary>
    public int CompareTo(IDestination? other) =>
        other is null ? 1 : Priority.CompareTo(other.Priority);

    /// <summary>
    /// Notificación post-exportación (estado legible para el usuario).
    /// En el prototipo no muestra UI; en EvidenciasSQA dispara la notificación del tray.
    /// </summary>
    protected static void ProcessExport(ExportInformation exportInformation, SurfaceDocument surface)
    {
        // Punto de extensión: aquí se integraría INotificationService de EvidenciasSQA.
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
