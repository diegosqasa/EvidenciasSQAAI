namespace EvidenciasSQA.Core.Drawing;

/// <summary>
/// Modo de renderizado, análogo al RenderMode de EvidenciasSQA.
/// Edit = vista en pantalla (WPF DrawingContext); Export = horneado final (GDI+ Graphics).
/// El modo permite simplificar ciertos detalles visuales en edición
/// (p. ej. sombras/suavizado solo en exportación, como hace EvidenciasSQA).
/// </summary>
public enum RenderMode
{
    /// <summary>Renderizado interactivo en pantalla (rápido, modo retenido WPF).</summary>
    Edit,

    /// <summary>Renderizado final horneado sobre un Bitmap (exportar/guardar).</summary>
    Export
}
