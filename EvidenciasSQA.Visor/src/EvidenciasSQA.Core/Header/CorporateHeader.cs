using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using EvidenciasSQA.Core.TextRendering;

namespace EvidenciasSQA.Core.Header;

/// <summary>
/// Hornea el Header Corporativo SQA sobre una captura, replicando píxel a píxel
/// la lógica de drawHeader() de image-worker.js del proyecto Evidencias SQA:
///
///  1. Detección de header previo: escanea la columna x=20 (de abajo hacia arriba,
///     hasta y=400) buscando la fila exacta (255,107,0) → el contenido real
///     comienza en y+4 (la franja mide 4px). Si no hay franja, headerEnd = 0.
///  2. Altura dinámica: 100 + (líneas de "Origen" - 1) * 22, con wrap de texto
///     carácter a carácter al ancho (w - 130).
///  3. Composición: degradado horizontal #002B55→#004080, franja naranja #FF6B00
///     de 4px abajo, logo 65×45 centrado verticalmente, título 18px bold en (100,14),
///     "Origen:" 16px en (100,41+i*22), meta 17px al 85% en (100, 68+(n-1)*22).
///  4. El contenido original se dibuja debajo del header sobre fondo blanco.
///
/// El Bitmap devuelto es SIEMPRE un nuevo propietario del llamador (gestión de
/// memoria explícita, sin mutar la captura original). Guard de dimensiones 16384.
/// </summary>
public static class CorporateHeader
{
    /// <summary>Fila naranja exacta (#FF6B00) que marca el límite del header previo.</summary>
    private const int OrangeRowR = 255, OrangeRowG = 107, OrangeRowB = 0;

    /// <summary>Detección de logo: misma lista de candidatos que preloadLogo() de image-logic.js.</summary>
    private static readonly string[] LogoSearchPaths =
    [
        "SQA1.png",
        "assets/SQA1.png",
        "Media/SQA1.png",
        "../assets/SQA1.png"
    ];

    private static Image? _logoCache;
    private static readonly object LogoLock = new();

    /// <summary>
    /// Aplica el header corporativo a la captura. Devuelve un Bitmap nuevo (dueño = llamador).
    /// </summary>
    public static Bitmap Bake(Bitmap capture, HeaderMetadata metadata, HeaderOptions? options = null)
    {
        options ??= HeaderOptions.Default;
        int srcWidth = capture.Width;
        int srcHeight = capture.Height;

        // Guard del worker: dimensiones extremas → devolver clon sin modificar.
        if (srcWidth > options.MaxDimension || srcHeight > options.MaxDimension)
        {
            return CloneImage(capture);
        }

        // 1) Detectar header previo (extensión) y recortar el contenido real.
        int headerEnd = FindExistingHeaderEnd(capture, options);
        int contentHeight = srcHeight - headerEnd;

        // 2) Medir "Origen" para la altura dinámica del header.
        string displayUrl = string.IsNullOrEmpty(metadata.ContextLabel) ? "Adjunto Local" : metadata.ContextLabel!;
        string originText = $"Origen: {displayUrl}";

        using var measure = CreateMeasureContext();
        List<string> originLines = WrapTextAnywhere(measure, originText, options, srcWidth - 130);
        int headerHeight = options.BaseHeight + (originLines.Count - 1) * options.LineStep;

        // 3) Lienzo final: nuevo header + contenido real sin el viejo.
        Bitmap result = new(srcWidth, headerHeight + contentHeight, PixelFormat.Format32bppArgb);
        result.SetResolution(96f, 96f); // pHYs 96 DPI: coincide con el chunk pHYs del worker (el visor WPF en zoom 1:1 renderiza a escala real)
        using (Graphics g = Graphics.FromImage(result))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            DrawHeader(g, srcWidth, headerHeight, metadata, originLines, options);

            // Fondo blanco bajo el contenido (worker: fillRect blanco).
            g.FillRectangle(Brushes.White, 0, headerHeight, srcWidth, contentHeight);

            // Contenido real (debajo del header viejo si existía).
            if (contentHeight > 0)
            {
                g.DrawImage(capture, new Rectangle(0, headerHeight, srcWidth, contentHeight),
                    new Rectangle(0, headerEnd, srcWidth, contentHeight), GraphicsUnit.Pixel);
            }
        }

        return result;
    }

    /// <summary>
    /// Detección de la franja naranja del header previo. Escaneo de píxeles exactos
    /// en la columna x=min(20, w-1), de abajo hacia arriba hasta y=400 (mismo
    /// recorrido que image-worker.js). Devuelve la primera fila tras la franja (y+4).
    /// </summary>
    public static int FindExistingHeaderEnd(Bitmap capture, HeaderOptions? options = null)
    {
        options ??= HeaderOptions.Default;
        int sampleX = Math.Min(20, capture.Width - 1);
        int maxY = Math.Min(capture.Height - 1, 400);

        // Lectura directa con LockBits: escanear la columna completa en una pasada.
        BitmapData data = capture.LockBits(new Rectangle(sampleX, 0, 1, maxY + 1), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            // Scan0 apunta al inicio de la región (muestra de 1px de ancho);
            // las filas están separadas por el stride de la imagen completa.
            int stride = data.Stride;
            byte[] pixel = new byte[4];
            for (int y = maxY; y >= 0; y--)
            {
                IntPtr rowPtr = data.Scan0 + y * stride;
                Marshal.Copy(rowPtr, pixel, 0, 4);
                // 32bppArgb en little-endian: B G R A
                if (pixel[2] == OrangeRowR && pixel[1] == OrangeRowG && pixel[0] == OrangeRowB)
                {
                    return y + 4;
                }
            }
        }
        finally
        {
            capture.UnlockBits(data);
        }

        return 0;
    }

    /// <summary>
    /// Dibuja el header completo sobre el Graphics de destino (usado por Bake y
    /// útil para pruebas). No toca el contenido.
    /// </summary>
    internal static void DrawHeader(Graphics g, int canvasWidth, int headerHeight, HeaderMetadata metadata, List<string> originLines, HeaderOptions options)
    {
        // Degradado horizontal corporativo.
        using (LinearGradientBrush gradient = new(new Rectangle(0, 0, canvasWidth, headerHeight), options.GradientStart, options.GradientEnd, LinearGradientMode.Horizontal))
        {
            g.FillRectangle(gradient, 0, 0, canvasWidth, headerHeight);
        }

        // Franja naranja inferior (4px).
        using (SolidBrush bandBrush = new(options.BandColor))
        {
            g.FillRectangle(bandBrush, 0, headerHeight - options.BandHeight, canvasWidth, options.BandHeight);
        }

        // Logo (o placeholder "SQA" si no existe, como el worker).
        Image? logo = LoadLogo(options);
        int logoY = (headerHeight - options.LogoHeight) / 2;
        if (logo != null)
        {
            g.DrawImage(logo, options.LogoX, logoY, options.LogoWidth, options.LogoHeight);
        }
        else
        {
            using SolidBrush orange = new(options.BandColor);
            g.FillRectangle(orange, options.LogoX, logoY, options.LogoWidth, options.LogoHeight);
            using SolidBrush white = new(Color.White);
            using Font placeholderFont = CreateFont(options.OriginFontSize + 2, FontStyle.Bold);
            g.TextRenderingHint = TextRenderingHint.AntiAlias;
            StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("SQA", placeholderFont, white, new RectangleF(options.LogoX, logoY, options.LogoWidth, options.LogoHeight), centered);
        }

        // Título (700 18px, blanco, origen top en (100,14)).
        using (Font titleFont = CreateFont(options.TitleFontSize, FontStyle.Bold))
        using (SolidBrush white = new(Color.White))
        {
            string title = string.IsNullOrEmpty(metadata.Title) ? options.TitleFallback : metadata.Title!;
            g.DrawString(title, titleFont, white, options.TitleX, options.TitleY);
        }

        // Origen Semibold (600) 16px con wrap carácter a carácter.
        using (Font originFont = CreateFont(options.OriginFontSize, FontStyle.Regular))
        using (SolidBrush white = new(Color.White))
        {
            for (int i = 0; i < originLines.Count; i++)
            {
                g.DrawString(originLines[i], originFont, white, options.TitleX, options.OriginY + i * options.LineStep);
            }
        }

        // Meta Regular (400) 17px, blanco 85%: "ID: EV-XX | 📅 fecha | 🌐 navegador | 💻 SO".
        // Emojis institucionales a color vía "Segoe UI Emoji" (ColorEmojiTextRenderer),
        // idénticos a los del worker (image-worker.js drawHeader).
        using (Font metaFont = CreateFont(options.MetaFontSize, FontStyle.Regular))
        using (Font emojiFont = ColorEmojiTextRenderer.CreateEmojiFont(options.MetaFontSize))
        using (SolidBrush metaBrush = new(Color.FromArgb(options.MetaAlpha, 255, 255, 255)))
        {
            string idLabel = metadata.BuildEvidenceIdString();
            if (string.IsNullOrEmpty(idLabel))
            {
                idLabel = "ID: ---";
            }

            string dateStr = (metadata.CaptureTimestamp ?? DateTime.Now).ToString(ColorEmojiTextRenderer.MetaDateFormat);
            string browserLabel = string.IsNullOrEmpty(metadata.Browser) ? "N/A" : metadata.Browser!;
            string osLabel = string.IsNullOrEmpty(metadata.Os) ? "N/A" : metadata.Os!;

            string meta = ColorEmojiTextRenderer.BuildMetaLine(idLabel, dateStr, browserLabel, osLabel);
            int metaY = options.MetaY + (originLines.Count - 1) * options.LineStep;
            ColorEmojiTextRenderer.DrawWithEmojis(g, meta, metaFont, emojiFont, metaBrush, options.MetaX, metaY);
        }
    }

    /// <summary>
    /// Wrap carácter a carácter con Graphics.MeasureString — equivalente a
    /// wrapTextAnywhere() del worker. Mide con la MISMA fuente del origen
    /// (Semibold) para que el salto de línea coincida con el render.
    /// </summary>
    internal static List<string> WrapTextAnywhere(Graphics g, string text, HeaderOptions options, int maxWidth)
    {
        using Font font = CreateFont(options.OriginFontSize, FontStyle.Regular);
        var lines = new List<string>();
        string currentLine = string.Empty;

        foreach (char c in text)
        {
            string testLine = currentLine + c;
            if (g.MeasureString(testLine, font).Width > maxWidth && currentLine.Length > 0)
            {
                lines.Add(currentLine);
                currentLine = c.ToString();
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine);
        }

        return lines;
    }

    /// <summary>Contexto de medición de texto 1×1 (el worker usa un canvas temporal).
    /// El bitmap base se cachea de por vida: son 4 bytes y evita fugas por Graphics.</summary>
    private static readonly Bitmap MeasureBitmap = new(1, 1, PixelFormat.Format32bppArgb);

    private static Graphics CreateMeasureContext()
    {
        Graphics g = Graphics.FromImage(MeasureBitmap);
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        return g;
    }

    /// <summary>Fuentes de UI corporativas: prioriza "Segoe UI Semibold" (600, más
    /// cuerpo y presencia) con fallback a "Segoe UI" y al sans-serif genérico.</summary>
    private static Font CreateFont(float size, FontStyle style)
    {
        foreach (string familyName in new[] { "Segoe UI Semibold", "Segoe UI" })
        {
            try
            {
                using (var family = new FontFamily(familyName))
                {
                    if (family.IsStyleAvailable(FontStyle.Regular))
                    {
                        return new Font(family, size, style, GraphicsUnit.Pixel);
                    }
                }
            }
            catch (Exception)
            {
                // Try the next candidate
            }
        }

        return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Pixel);
    }

    /// <summary>
    /// Logo cacheado (carga perezosa + cache estática, como el logoCacheCanvas del
    /// worker). Si no se encuentra el archivo devuelve null → placeholder.
    /// </summary>
    private static Image? LoadLogo(HeaderOptions options)
    {
        lock (LogoLock)
        {
            if (_logoCache != null)
            {
                return _logoCache;
            }

            string? path = ResolveLogoPath(options);
            if (path == null || !File.Exists(path))
            {
                return null;
            }

            using Image temp = Image.FromFile(path);
            _logoCache = new Bitmap(temp); // copia propia: evita el lock de archivo GDI+
            return _logoCache;
        }
    }

    private static string? ResolveLogoPath(HeaderOptions options)
    {
        if (!string.IsNullOrEmpty(options.LogoPath))
        {
            return options.LogoPath;
        }

        string baseDir = AppContext.BaseDirectory;

        foreach (string candidate in LogoSearchPaths)
        {
            // Buscar junto al exe (deploy) y contra CWD (desarrollo).
            string exePath = Path.Combine(baseDir, candidate);
            if (File.Exists(exePath))
            {
                return exePath;
            }

            string cwdPath = Path.GetFullPath(candidate);
            if (File.Exists(cwdPath))
            {
                return cwdPath;
            }
        }

        return null;
    }

    private static Bitmap CloneImage(Image source)
    {
        Bitmap clone = new(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(clone);
        g.DrawImage(source, 0, 0, source.Width, source.Height);
        return clone;
    }
}
