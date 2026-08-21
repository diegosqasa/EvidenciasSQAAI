using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media.Imaging;

namespace EvidenciasSQA.Core.TextRendering;

/// <summary>
/// Renderizador de texto mixto (texto + iconos a color) para el Header Corporativo SQA.
///
/// SOLUCIÓN: cada emoji se reemplaza por una imagen PNG/JPG pre-descargada que se dibuja
/// inline con el texto. Esto garantiza color en TODAS las plataformas sin depender de
/// fuentes COLR/CPAL ni GDI+ DrawString.
///
/// Emojis canónicos: 📅 → Calendario.png, 🌐 → Navegador.png, 💻 → SO.png.
    /// </summary>
    public static class ColorEmojiTextRenderer
    {
        public const string CalendarEmoji = "\U0001F4C5";
        public const string GlobeEmoji = "\U0001F310";
        public const string LaptopEmoji = "\U0001F4BB";
        public const string MetaDateFormat = "dd/MM/yyyy, hh:mm:ss tt";

        private static readonly object IconLock = new();
        private static readonly Dictionary<int, Image> IconCache = new();

        private static readonly Dictionary<string, string> EmojiToFile = new()
        {
            { CalendarEmoji, "Calendario.png" },
            { GlobeEmoji, "Navegador.png" },
            { LaptopEmoji, "SO.png" }
        };

    public static string BuildMetaLine(string idLabel, string dateStr, string browserLabel, string osLabel)
    {
        return $"{idLabel} | {CalendarEmoji} {dateStr} | {GlobeEmoji} {browserLabel} | {LaptopEmoji} {osLabel}";
    }

    public static Font CreateEmojiFont(float sizeInPixels)
    {
        return new Font("Segoe UI", sizeInPixels, FontStyle.Regular, GraphicsUnit.Pixel);
    }

    public static bool IsEmojiRune(Rune rune)
    {
        int v = rune.Value;
        return (v >= 0x1F000 && v <= 0x1FAFF) ||
               (v >= 0x2600 && v <= 0x27BF) ||
               (v >= 0x2190 && v <= 0x21FF) ||
               (v >= 0x2B00 && v <= 0x2BFF) ||
               (v >= 0xFE00 && v <= 0xFE0F) ||
               v == 0x200D;
    }

    public static List<(string Text, bool IsEmoji)> SplitRuns(string text)
    {
        var runs = new List<(string, bool)>();
        if (string.IsNullOrEmpty(text)) return runs;

        var buffer = new StringBuilder();
        bool? currentIsEmoji = null;

        foreach (Rune rune in text.EnumerateRunes())
        {
            bool isEmoji = IsEmojiRune(rune);
            if (currentIsEmoji.HasValue && isEmoji != currentIsEmoji.Value)
            {
                runs.Add((buffer.ToString(), currentIsEmoji.Value));
                buffer.Clear();
            }
            currentIsEmoji = isEmoji;
            buffer.Append(rune.ToString());
        }

        if (buffer.Length > 0 && currentIsEmoji.HasValue)
            runs.Add((buffer.ToString(), currentIsEmoji.Value));

        return runs;
    }

    /// <summary>
    /// Carga y cachea la imagen de un emoji desde Media/ (Calendario.png, Navegador.png, SO.png).
    /// </summary>
    private static Image LoadEmojiIcon(string emojiText)
    {
        foreach (var kv in EmojiToFile)
        {
            if (emojiText.Contains(kv.Key))
            {
                int key = kv.Key.EnumerateRunes().First().Value;
                lock (IconLock)
                {
                    if (IconCache.TryGetValue(key, out Image cached) && cached != null)
                        return new Bitmap(cached);
                }

                string[] searchPaths =
                {
                    Path.Combine(AppContext.BaseDirectory, "Media", kv.Value),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Media", kv.Value),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Media", kv.Value),
                    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Media", kv.Value),
                };

                foreach (string path in searchPaths)
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            using Image original = Image.FromFile(path);
                            Image copy = new Bitmap(original);
                            lock (IconLock)
                            {
                                IconCache[key] = copy;
                            }
                            return new Bitmap(copy);
                        }
                    }
                    catch { }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Dibuja texto con iconos inline: texto → DrawString, iconos → DrawImage escalado.
    /// </summary>
    public static void DrawWithEmojis(Graphics g, string text, Font textFont, Font emojiFont,
        System.Drawing.Brush brush, float x, float y)
    {
        float fontSize = textFont.GetHeight(g);
        float iconSide = fontSize;

        foreach ((string runText, bool isEmoji) in SplitRuns(text))
        {
            if (isEmoji)
            {
                using Image icon = LoadEmojiIcon(runText);
                if (icon != null)
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawImage(icon, x, y - 1, iconSide, iconSide);
                    x += iconSide;
                    continue;
                }
            }

            g.DrawString(runText, textFont, brush, x, y);
            x += g.MeasureString(runText, textFont).Width;
        }
    }

    /// <summary>Ancho total del texto mixto.</summary>
    public static float MeasureWithEmojis(Graphics g, string text, Font textFont, Font emojiFont)
    {
        float width = 0f;
        float fontSize = textFont.GetHeight(g);
        float iconSide = fontSize;

        foreach ((string runText, bool isEmoji) in SplitRuns(text))
        {
            if (isEmoji)
            {
                using Image icon = LoadEmojiIcon(runText);
                if (icon != null)
                {
                    width += iconSide;
                    continue;
                }
            }
            width += g.MeasureString(runText, isEmoji ? emojiFont : textFont).Width;
        }
        return width;
    }

    public static BitmapSource RenderToBitmapSource(string text, Font textFont, Font emojiFont, System.Drawing.Color color,
        double dpi = 96.0)
    {
        float width;
        float height;
        using (var measureBitmap = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(measureBitmap))
        {
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            width = MeasureWithEmojis(g, text, textFont, emojiFont);
            height = Math.Max(textFont.GetHeight(g), emojiFont.GetHeight(g));
        }

        int w = Math.Max(1, (int)Math.Ceiling(width));
        int h = Math.Max(1, (int)Math.Ceiling(height));
        var bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        bitmap.SetResolution((float)dpi, (float)dpi);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var brush = new SolidBrush(color);
            DrawWithEmojis(g, text, textFont, emojiFont, brush, 0f, 0f);
        }

        return BitmapToSource(bitmap, w, h, dpi);
    }

    private static BitmapSource BitmapToSource(Bitmap bitmap, int width, int height, double dpi)
    {
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new byte[data.Stride * height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            BitmapSource source = BitmapSource.Create(width, height, dpi, dpi, System.Windows.Media.PixelFormats.Bgra32, null, pixels, data.Stride);
            source.Freeze();
            return source;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}