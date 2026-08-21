using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EvidenciasSQA.Core.TextRendering;

namespace EvidenciasSQA.Core.Header;

/// <summary>
/// Hornea el Header Corporativo SQA sobre una captura usando SOLO la pila
/// de dibujo vectorial de WPF (DrawingVisual + RenderTargetBitmap). Es la
/// variante WPF de CorporateHeader (GDI+) y replica píxel a píxel
/// drawHeader() de image-worker.js (Evidencias SQA):
///
///  1. Idempotencia: detecta la franja naranja #FF6B00 del header previo
///     (columna x=min(20, w-1), barrido de y=min(h-1,400) hacia arriba).
///     Si existe, NO reprocesa: devuelve la imagen original intacta.
///  2. Altura dinámica: 100 + (líneas de "Origen" - 1) * 22 con wrap de
///     texto carácter a carácter al ancho (w - 130).
///  3. Composición: degradado horizontal #002B55→#004080, franja naranja
///     #FF6B00 de 4px al pie, logo 65×45 centrado verticalmente (x=20),
///     título 18px bold en (100,14), "Origen:" 16px en (100,41+i*22),
///     meta 17px al 85% en (100,68+(n-1)*22). Contenido original debajo
///     del header sobre fondo blanco.
///  4. Rendimiento: el RenderTargetBitmap resultante se congela (.Freeze())
///     → se comparte sin copias entre hilos y se libera VRAM/RAM correctamente
///     en capturas concurrentes. La imagen fuente se convierte a Pbgra32
///     congelado para un muestreo determinista.
///
/// RESTRICCIÓN DE HILO: DrawingVisual/RenderTargetBitmap requieren un hilo
/// STA (UI / Dispatcher). Invocar desde el hilo de UI del tray o vía
/// Dispatcher.Invoke. La fecha estampada SIEMPRE sale de
/// HeaderMetadata.CaptureTimestamp (timestamp real de captura); DateTime.Now
/// es solo el fallback documental si el llamador no lo provee.
/// </summary>
public static class HeaderBakingService
{
    private const double Dpi = 96.0;

    /// <summary>Fila naranja exacta (#FF6B00) que marca el límite del header previo.</summary>
    private const byte OrangeR = 255, OrangeG = 107, OrangeB = 0;

    /// <summary>Detección de logo: misma lista de candidatos que preloadLogo() de image-logic.js.</summary>
    private static readonly string[] LogoSearchPaths =
    [
        "SQA1.png",
        "assets/SQA1.png",
        "Media/SQA1.png",
        "../assets/SQA1.png"
    ];

    private static ImageSource? _logoCache;
    private static readonly object LogoLock = new();

    /// <summary>
    /// Aplica el header corporativo a la captura. IDEMPOTENTE: si la imagen ya
    /// tiene un header (franja naranja detectada) devuelve la captura original
    /// sin reprocesar. El BitmapSource devuelto está congelado (thread-safe).
    /// </summary>
    /// <param name="capture">Captura original (BitmapSource WPF).</param>
    /// <param name="metadata">Metadatos estampados; CaptureTimestamp = hora real de la captura.</param>
    /// <param name="options">Parámetros de layout (default = réplica exacta del worker).</param>
    /// <param name="logo">Logo corporativo opcional (65×45). Si es null se autodetecta
    /// (SQA1.png junto al exe) y si no existe se dibuja el placeholder "SQA".</param>
    /// <returns>Nuevo BitmapSource con header horneado (congelado), o la captura original si
    /// ya tenía header o excede MaxDimension.</returns>
    public static BitmapSource Bake(BitmapSource capture, HeaderMetadata metadata, HeaderOptions? options = null, ImageSource? logo = null)
    {
        options ??= HeaderOptions.Default;

        if (capture.PixelWidth <= 0 || capture.PixelHeight <= 0)
        {
            return capture;
        }

        // Guard del worker: dimensiones extremas → devolver sin modificar (anti-OOM).
        if (capture.PixelWidth > options.MaxDimension || capture.PixelHeight > options.MaxDimension)
        {
            return capture;
        }

        // Pbgra32 congelado: muestreo determinista (BGRA little-endian) e hilos seguros.
        BitmapSource src = EnsurePbgra32(capture);
        int srcWidth = src.PixelWidth;
        int srcHeight = src.PixelHeight;

        // 1) Idempotencia: si ya hay franja naranja (header previo), no reprocesar.
        int headerEnd = FindExistingHeaderEnd(src);
        if (headerEnd > 0)
        {
            return capture;
        }

        int contentHeight = srcHeight - headerEnd;

        // 2) Medir "Origen" para la altura dinámica del header (wrap por caracteres).
        string displayUrl = string.IsNullOrEmpty(metadata.ContextLabel) ? "Adjunto Local" : metadata.ContextLabel!;
        string originText = $"Origen: {displayUrl}";

        List<string> originLines = WrapTextAnywhere(originText, srcWidth - 130, options);
        int headerHeight = options.BaseHeight + (originLines.Count - 1) * options.LineStep;

        // 3) Composición vectorial (DrawingVisual).
        DrawingVisual visual = new();
        using (DrawingContext dc = visual.RenderOpen())
        {
            DrawHeader(dc, srcWidth, headerHeight, metadata, originLines, options, logo);

            // Fondo blanco bajo el contenido (worker: fillRect blanco).
            SolidColorBrush white = Brushes.White;
            white.Freeze();
            dc.DrawRectangle(white, null, new Rect(0, headerHeight, srcWidth, contentHeight));

            // Contenido real (debajo del header viejo si existía; con idempotencia headerEnd = 0).
            if (contentHeight > 0)
            {
                CroppedBitmap content = new(src, new Int32Rect(0, headerEnd, srcWidth, contentHeight));
                content.Freeze();
                dc.DrawImage(content, new Rect(0, headerHeight, srcWidth, contentHeight));
            }
        }

        // 4) Renderizar y congelar (VRAM/RAM: compartido sin copias, sin fugas).
        RenderTargetBitmap baked = new(srcWidth, headerHeight + contentHeight, Dpi, Dpi, PixelFormats.Pbgra32);
        baked.Render(visual);
        baked.Freeze();
        return baked;
    }

    /// <summary>True si la imagen ya trae un header (franja naranja #FF6B00 detectada).</summary>
    public static bool HasExistingHeader(BitmapSource source)
    {
        if (source.PixelWidth <= 0 || source.PixelHeight <= 0)
        {
            return false;
        }

        return FindExistingHeaderEnd(EnsurePbgra32(source)) > 0;
    }

    /// <summary>
    /// Detección de la franja naranja del header previo: muestrea la columna
    /// x=min(20, w-1) desde y=min(h-1,400) hacia arriba buscando el píxel
    /// exacto (255,107,0). Devuelve y+4 (primera fila tras la franja) o 0.
    /// </summary>
    public static int FindExistingHeaderEnd(BitmapSource source)
    {
        int w = source.PixelWidth;
        int h = source.PixelHeight;
        if (w <= 0 || h <= 0)
        {
            return 0;
        }

        int sampleX = Math.Min(20, w - 1);
        int maxY = Math.Min(h - 1, 400);

        // Copia de la columna completa en una pasada (stride = 1 px * 4 bytes).
        byte[] column = new byte[4 * (maxY + 1)];
        source.CopyPixels(new Int32Rect(sampleX, 0, 1, maxY + 1), column, 4, 0);

        for (int y = maxY; y >= 0; y--)
        {
            int i = y * 4;
            // Pbgra32 little-endian: B G R A
            if (column[i] == OrangeB && column[i + 1] == OrangeG && column[i + 2] == OrangeR)
            {
                return y + 4;
            }
        }

        return 0;
    }

    /// <summary>Dibuja el header completo sobre el DrawingContext (sin tocar el contenido).</summary>
    private static void DrawHeader(DrawingContext dc, int canvasWidth, int headerHeight, HeaderMetadata metadata,
        List<string> originLines, HeaderOptions options, ImageSource? logo)
    {
        // Degradado horizontal corporativo #002B55 → #004080.
        LinearGradientBrush gradient = new(ToColor(options.GradientStart), ToColor(options.GradientEnd), 0.0);
        gradient.Freeze();
        dc.DrawRectangle(gradient, null, new Rect(0, 0, canvasWidth, headerHeight));

        // Franja naranja inferior (4px).
        SolidColorBrush band = new(ToColor(options.BandColor));
        band.Freeze();
        dc.DrawRectangle(band, null, new Rect(0, headerHeight - options.BandHeight, canvasWidth, options.BandHeight));

        // Logo (o placeholder "SQA" si no está disponible, como el worker).
        ImageSource? logoSource = logo ?? LoadLogoFromDisk(options);
        int logoY = (headerHeight - options.LogoHeight) / 2;
        if (logoSource != null)
        {
            dc.DrawImage(logoSource, new Rect(options.LogoX, logoY, options.LogoWidth, options.LogoHeight));
        }
        else
        {
            dc.DrawRectangle(band, null, new Rect(options.LogoX, logoY, options.LogoWidth, options.LogoHeight));
            FormattedText placeholder = CreateFormattedText("SQA", options.OriginFontSize + 2, FontWeights.Bold);
            placeholder.TextAlignment = TextAlignment.Center;
            double phX = options.LogoX + (options.LogoWidth - placeholder.WidthIncludingTrailingWhitespace) / 2;
            double phY = logoY + (options.LogoHeight - placeholder.Height) / 2;
            dc.DrawText(placeholder, new Point(phX, phY));
        }

        // Título (Bold 700 18px, blanco, en (100,14)): coincide con Electron 700 18px sans-serif.
        string title = string.IsNullOrEmpty(metadata.Title) ? options.TitleFallback : metadata.Title!;
        FormattedText titleText = CreateFormattedText(title, options.TitleFontSize, FontWeights.Bold);
        dc.DrawText(titleText, new Point(options.TitleX, options.TitleY));

        // Origen con wrap carácter a carácter (600 16px).
        for (int i = 0; i < originLines.Count; i++)
        {
            FormattedText originLineText = CreateFormattedText(originLines[i], options.OriginFontSize, FontWeights.SemiBold);
            dc.DrawText(originLineText, new Point(options.TitleX, options.OriginY + i * options.LineStep));
        }

        // Meta: "ID: EV-XX | 📅 fecha | 🌐 navegador | 💻 SO" (Semibold 600 17px, blanco 85%).
        // Emojis institucionales A COLOR: WPF (FormattedText) no soporta color emoji
        // (los degrada a monocromo/tofu), así que la línea se renderiza con GDI+
        // ("Segoe UI Emoji" vía ColorEmojiTextRenderer) a un BitmapSource congelado
        // y se compone como imagen — réplica visual del worker (image-worker.js).
        string idLabel = metadata.BuildEvidenceIdString();
        if (string.IsNullOrEmpty(idLabel))
        {
            idLabel = "ID: ---";
        }

        string dateStr = (metadata.CaptureTimestamp ?? DateTime.Now).ToString(ColorEmojiTextRenderer.MetaDateFormat, CultureInfo.CurrentCulture);
        string browserLabel = string.IsNullOrEmpty(metadata.Browser) ? "N/A" : metadata.Browser!;
        string osLabel = string.IsNullOrEmpty(metadata.Os) ? "N/A" : metadata.Os!;
        string meta = ColorEmojiTextRenderer.BuildMetaLine(idLabel, dateStr, browserLabel, osLabel);

        using (System.Drawing.Font metaFont = CreateGdiFont((float)options.MetaFontSize, System.Drawing.FontStyle.Regular))
        using (System.Drawing.Font emojiFont = ColorEmojiTextRenderer.CreateEmojiFont((float)options.MetaFontSize))
        {
            BitmapSource metaBitmap = ColorEmojiTextRenderer.RenderToBitmapSource(
                meta, metaFont, emojiFont, System.Drawing.Color.FromArgb(options.MetaAlpha, 255, 255, 255), Dpi);
            int metaY = options.MetaY + (originLines.Count - 1) * options.LineStep;
            dc.DrawImage(metaBitmap, new Rect(options.MetaX, metaY, metaBitmap.Width, metaBitmap.Height));
        }
    }

    /// <summary>
    /// Fuente GDI+ para Meta (400 Regular). Electron usa 400 para Meta.
    /// GDI+ no soporta pesos arbitrarios en FontStyle, usamos la familia base.
    /// </summary>
    private static System.Drawing.Font CreateGdiFont(float size, System.Drawing.FontStyle style)
    {
        // Electron usa 400 para Meta. Intentamos Segoe UI primero, luego fallback.
        foreach (string familyName in new[] { "Segoe UI", "Microsoft Sans Serif" })
        {
            try
            {
                return new System.Drawing.Font(familyName, size, style, System.Drawing.GraphicsUnit.Pixel);
            }
            catch
            {
                // Try the next candidate
            }
        }

        return new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, size, style, System.Drawing.GraphicsUnit.Pixel);
    }

    /// <summary>
    /// Wrap carácter a carácter con FormattedText — equivalente a wrapTextAnywhere()
    /// del worker. Greedy: acumula caracteres mientras el ancho medido no supere
    /// maxWidth. (FormattedText.Text es de solo lectura, por eso se mide por prefijo.)
    /// </summary>
    internal static List<string> WrapTextAnywhere(string text, int maxWidth, HeaderOptions options)
    {
        var lines = new List<string>();
        string currentLine = string.Empty;

        foreach (char c in text)
        {
            string testLine = currentLine + c;
            double width = MeasureTextWidth(testLine, options.OriginFontSize, FontWeights.SemiBold);
            if (width > maxWidth && currentLine.Length > 0)
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

    /// <summary>Ancho del texto con el tipo de letra de origen (600 16px).</summary>
    private static double MeasureTextWidth(string text, double fontSize, FontWeight weight)
    {
        FormattedText ft = CreateFormattedText(text, fontSize, weight);
        return ft.WidthIncludingTrailingWhitespace;
    }

    /// <summary>
    /// Convierte la fuente a Pbgra32 congelado para muestreo/rendering determinista.
    /// Si ya es Pbgra32, devuelve la misma instancia (sin copia).
    /// </summary>
    private static BitmapSource EnsurePbgra32(BitmapSource source)
    {
        if (source.Format == PixelFormats.Pbgra32)
        {
            return source;
        }

        FormatConvertedBitmap converted = new(source, PixelFormats.Pbgra32, null, 0);
        converted.Freeze();
        return converted;
    }

    /// <summary>
    /// Crea un FormattedText con el tipo de letra del worker (Segoe UI sans-serif,
    /// fallback automático de WPF) a 96 DPI, consistente con RenderTargetBitmap.
    /// </summary>
    private static FormattedText CreateFormattedText(string text, double fontSize, FontWeight weight)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            fontSize,
            Brushes.White,
            1.0 /* pixelsPerDip: 96 DPI, coincide con RenderTargetBitmap */);
    }

    /// <summary>Logo cacheado (carga perezosa + cache estática + freeze, como logoCacheCanvas del worker).</summary>
    private static ImageSource? LoadLogoFromDisk(HeaderOptions options)
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

            try
            {
                BitmapImage bitmap = new();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                _logoCache = bitmap;
            }
            catch
            {
                _logoCache = null;
            }

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
            // Junto al exe (deploy) y contra CWD (desarrollo).
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

    /// <summary>System.Drawing.Color → System.Windows.Media.Color (HeaderOptions usa GDI+).</summary>
    private static Color ToColor(System.Drawing.Color c) => Color.FromArgb(c.A, c.R, c.G, c.B);
}