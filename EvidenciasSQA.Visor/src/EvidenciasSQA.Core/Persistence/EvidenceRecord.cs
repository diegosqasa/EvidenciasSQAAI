namespace EvidenciasSQA.Core.Persistence;

/// <summary>
/// Registro de evidencia para la galería. Modelo plano (sin base MVVM):
/// la fuente de verdad es el sistema de archivos, no una base de datos.
/// </summary>
public sealed class EvidenceRecord
{
    public int Id { get; init; }

    /// <summary>Ej: "Evidencia_14" (nombre del archivo sin extensión).</summary>
    public string EvidenceCode { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    /// <summary>Timestamp de creación del archivo (aproximación del momento de captura).</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Origen de la captura. El escaneo de carpeta no lo conoce: vacío → "Captura Local".</summary>
    public string OriginUrl { get; init; } = string.Empty;
}
