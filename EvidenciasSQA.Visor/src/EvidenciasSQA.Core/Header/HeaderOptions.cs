using System.Drawing;

namespace EvidenciasSQA.Core.Header;

/// <summary>
/// Parámetros del Header Corporativo SQA. Los valores por defecto replican
/// EXACTAMENTE drawHeader() de image-worker.js (Evidencias SQA):
/// degradado #002B55→#004080 horizontal, franja #FF6B00 de 4px, logo 65×45,
/// título 18px bold en (100,14), "Origen:" 16px con wrap a w-130, meta 17px al 85%.
/// </summary>
public sealed class HeaderOptions
{
    public static HeaderOptions Default { get; } = new();

    /// <summary>Altura base del header (px).</summary>
    public int BaseHeight { get; set; } = 100;

    /// <summary>Altura extra por línea extra de "Origen" (px).</summary>
    public int LineStep { get; set; } = 22;

    /// <summary>Grosor de la franja naranja inferior (px).</summary>
    public int BandHeight { get; set; } = 4;

    public Color BandColor { get; set; } = Color.FromArgb(255, 0x6B, 0x00); // #FF6B00
    public Color GradientStart { get; set; } = Color.FromArgb(0x00, 0x2B, 0x55); // #002B55
    public Color GradientEnd { get; set; } = Color.FromArgb(0x00, 0x40, 0x80); // #004080

    /// <summary>Tamaño final del logo sobre el header (px).</summary>
    public int LogoWidth { get; set; } = 65;

    public int LogoHeight { get; set; } = 45;

    public int LogoX { get; set; } = 20;

    public int TitleX { get; set; } = 100;
    public int TitleY { get; set; } = 14;

    public int OriginY { get; set; } = 41;

    public int MetaX { get; set; } = 100;
    public int MetaY { get; set; } = 68;

    public float TitleFontSize { get; set; } = 18f;
    public float OriginFontSize { get; set; } = 16f;
    public float MetaFontSize { get; set; } = 17f;

    /// <summary>Opacidad de la línea de metadatos (worker: rgba(255,255,255,0.85)).</summary>
    public byte MetaAlpha { get; set; } = 217; // 0.85 * 255

    /// <summary>Texto por defecto del título (worker: 'Evidencia de prueba QA').</summary>
    public string TitleFallback { get; set; } = "Evidencia de prueba QA";

    /// <summary>
    /// Ruta del logo corporativo. Si no existe se usa el placeholder "SQA"
    /// (mismo fallback que el worker). null = autodetección (assets/SQA1.png).
    /// </summary>
    public string? LogoPath { get; set; }

    /// <summary>Máxima dimensión aceptada; capturas mayores se dejan sin header (guard del worker: 16384).</summary>
    public int MaxDimension { get; set; } = 16384;
}
