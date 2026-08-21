namespace EvidenciasSQA.Core.Header;

/// <summary>
/// Metadatos que se estampan en el Header Corporativo SQA.
/// Espejo de los payloads de image-logic.js / image-worker.js del proyecto Evidencias SQA
/// (processImageWithHeader): contextLabel, title, fileName, dateStr, evidenceId, browser, os.
/// </summary>
public sealed class HeaderMetadata
{
    /// <summary>Contexto/origen de la captura (URL de la pestaña, ventana, ...).</summary>
    public string? ContextLabel { get; init; }

    /// <summary>Título de la evidencia.</summary>
    public string? Title { get; init; }

    /// <summary>Nombre de archivo de la evidencia (p. ej. Evidencia_01.png).</summary>
    public string? FileName { get; init; }

    /// <summary>Timestamp de la captura (si es null se usa DateTime.Now).</summary>
    public DateTime? CaptureTimestamp { get; init; }

    /// <summary>Número de evidencia para "ID: EV-XX" (si es null se intenta extraer del título).</summary>
    public int? EvidenceId { get; init; }

    /// <summary>Etiqueta del navegador (p. ej. "Chrome v126").</summary>
    public string? Browser { get; init; }

    /// <summary>Etiqueta del sistema operativo (p. ej. "Windows 11").</summary>
    public string? Os { get; init; }

    /// <summary>
    /// Genera el texto "ID: EV-XX" con el mismo padding de image-logic.js,
    /// o lo extrae del título si no hay número explícito.
    /// </summary>
    public string BuildEvidenceIdString()
    {
        if (EvidenceId is int id)
        {
            return $"ID: EV-{id:D2}";
        }

        if (Title != null && System.Text.RegularExpressions.Regex.Match(Title, @"Evidencia[_\s](\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase) is { Success: true } match)
        {
            return $"ID: EV-{int.Parse(match.Groups[1].Value):D2}";
        }

        return string.Empty;
    }
}
