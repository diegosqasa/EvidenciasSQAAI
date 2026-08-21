namespace EvidenciasSQA.Core.Model;

/// <summary>
/// Metadatos de la captura (equivalente reducido de EvidenciasSQA ICaptureDetails):
/// fecha, origen y nombre de archivo previo.
/// </summary>
public sealed class CaptureDetails
{
    public DateTime CaptureDate { get; init; } = DateTime.Now;

    /// <summary>Ruta previamente usada (para sobrescribir sin diálogo, como FileDestination).</summary>
    public string? Filename { get; set; }

    /// <summary>Título de la ventana capturada (placeholder de extensión).</summary>
    public string? Title { get; set; }
}
