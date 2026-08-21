namespace EvidenciasSQA.Core.Imaging;

/// <summary>
/// Formatos de salida soportados (equivalente a EvidenciasSQA OutputFormat).
/// evidenciasSqa = PNG de fondo + metadatos XML de anotaciones en la cola del archivo.
/// </summary>
public enum OutputFormat
{
    png,
    jpg,
    evidenciasSqa
}
