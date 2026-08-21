using EvidenciasSQA.Core.Model;

namespace EvidenciasSQA.Core.Interfaces;

/// <summary>
/// Contrato de un destino de exportación, calco fiel de EvidenciasSQA.Base.Interfaces.IDestination.
///
/// Esta interfaz es el punto de extensión de la "capa de plugins": cualquier consumidor de
/// una captura (archivo, portapapeles, impresora, subida web, ...) implementa IDestination
/// y queda disponible para la UI sin modificar la capa del editor.
/// </summary>
public interface IDestination : IDisposable, IComparable<IDestination>
{
    /// <summary>
    /// Designación corta y estable, usada para persistir la configuración
    /// ("File", "Clipboard", ...). En EvidenciasSQA: "FileNoDialog", "Editor", ...
    /// </summary>
    string Designation { get; }

    /// <summary>Descripción mostrada en la UI.</summary>
    string Description { get; }

    /// <summary>Prioridad para ordenar destinos (menor = primero).</summary>
    int Priority { get; }

    /// <summary>Indica si el destino está habilitado (EvidenciasSQA: checkboxes de quick settings).</summary>
    bool IsActive { get; }

    /// <summary>
    /// Punto de entrada de la exportación. Firmado igual que EvidenciasSQA
    /// (manuallyInitiated + surface + captureDetails) pero con nuestros tipos.
    /// </summary>
    /// <param name="manuallyInitiated">true si el usuario eligió este destino desde la UI.</param>
    /// <param name="surface">Documento/superficie a exportar.</param>
    /// <param name="captureDetails">Metadatos de la captura (fecha, nombre previo, ...).</param>
    /// <returns>ExportInformation con el estado y la ubicación de la exportación.</returns>
    ExportInformation ExportCapture(bool manuallyInitiated, SurfaceDocument surface, CaptureDetails? captureDetails);
}
