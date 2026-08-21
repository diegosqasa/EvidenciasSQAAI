using EvidenciasSQA.Core.Imaging;

namespace EvidenciasSQA.Core.Export;

/// <summary>
/// Configuración de exportación, espejo de EvidenciasSQA SurfaceOutputSettings
/// (formato, calidad JPEG, guardar solo fondo). El contrato entre los destinos
/// (FileDestination, ClipboardDestination...) y el motor de imagen ImageIO.
/// </summary>
public sealed class SurfaceOutputSettings
{
    public OutputFormat Format { get; set; } = OutputFormat.png;

    public int JpgQuality { get; set; } = 80;

    /// <summary>
    /// true = exportar solo la imagen de fondo, sin anotaciones
    /// (Greenhot usa esto para el formato .evidenciasSqa y para "guardar original").
    /// </summary>
    public bool SaveBackgroundOnly { get; set; }

    /// <summary>
    /// Previene que una salida use .evidenciasSqa (p. ej. destinos externos que solo
    /// entienden imágenes planas). Copia el comportamiento de EvidenciasSQA
    /// (bug-2056): si el formato es EvidenciasSQA, pasa a png.
    /// </summary>
    public SurfaceOutputSettings PreventEvidenciasSqaFormat()
    {
        if (Format == OutputFormat.evidenciasSqa)
        {
            Format = OutputFormat.png;
        }

        return this;
    }
}
