/*
 * EvidenciasSQA - a free and open source screenshot tool
 * Copyright (C) 2004-2026 Thomas Braun, Jens Klingen, Robin Krom
 *
 * For more information see: https://evidenciassqa.com/
 * The EvidenciasSQA project is hosted on GitHub https://github.com/evidenciassqa/evidenciassqa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 1 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Text;

namespace EvidenciasSQA.Helpers
{
    /// <summary>
    /// Hornea el header corporativo en las capturas (GDI+).
    /// Replica 1:1 el diseño de la especificación del Electron
    /// (documentation: "Integración del Header Corporativo", image-worker.js:drawHeader):
    /// - Gradiente HORIZONTAL #002b55 → #004080.
    /// - Borde inferior de acento naranja #FF6B00 (4px) — SOLO abajo.
    /// - Logo 65×45 centrado verticalmente en el header (placeholder naranja "SQA"
    ///   si el logo no está disponible).
    /// - Título Bold 700 18px blanco en (100, 14).
    /// - "Origen: <URL>" Semibold 600 16px con wrap en cualquier carácter (wrapTextAnywhere),
    ///   líneas en y = 41 + i*22, ancho máximo = canvasWidth - 130.
    /// - Meta Regular 400 17px al 85% de opacidad: "ID: EV-XX | 📅 fecha | 🌐 navegador | 💻 SO".
    ///   Los glifos emoji (📅 🌐 💻) se dibujan con la familia nativa "Segoe UI Emoji"
    ///   (fallback "Segoe UI Symbol") para garantizar glifos a color reales.
    /// - Altura dinámica: 100px + (líneas de origen - 1) * 22.
    /// - Recorte del header PREVIO (detección por línea naranja rgb(255,107,0) escaneada
    ///   de abajo hacia arriba, máx. 400px, en x=20) y fondo blanco bajo el contenido
    ///   (idéntico al worker process-image / stamp-evidence-id).
    /// - Guard anti-OOM: dimensiones > 16384px → devuelve null (sin header).
    /// </summary>
    public static class CorporateHeaderBaker
    {
        private const int StripeBottom = 4;
        private const int LogoX = 20;
        private const int LogoWidth = 65;
        private const int LogoHeight = 45;
        private const int TextX = 100;
        private const int TitleY = 14;
        private const int UrlFirstY = 41;
        private const int UrlLineHeight = 22;
        private const int MetaBaseY = 68;
        private const int BaseHeaderHeight = 100;
        private const int MaxHeaderScan = 400;
        private const int MaxDimension = 16384;

        public static readonly Color GradientStart = Color.FromArgb(0x00, 0x2B, 0x55);
        public static readonly Color GradientEnd = Color.FromArgb(0x00, 0x40, 0x80);
        public static readonly Color StripeColor = Color.FromArgb(0xFF, 0x6B, 0x00);

        private static readonly object LogoLock = new object();
        private static Bitmap _cachedLogo;

        // Famillas de fuentes exactas para cada peso (Electron usa pesos numéricos explícitos)
        private static readonly string[] TitleFontCandidates = { "Segoe UI", "Segoe UI Variable", "Segoe UI" };
        private static readonly string[] OriginFontCandidates = { "Segoe UI Semibold", "Segoe UI" };
        private static readonly string[] MetaFontCandidates = { "Segoe UI", "Segoe UI Variable", "Segoe UI" };
        private static readonly string[] EmojiFontCandidates = { "Segoe UI Emoji", "Segoe UI Symbol", "Segoe UI" };

        /// <summary>
        /// Crea una fuente con el peso exacto requerido por la especificación.
        /// Título: 700 (Bold) - usa "Segoe UI" con FontStyle.Bold (sintético 700 sobre 400).
        /// Origen: 600 (Semibold) - usa "Segoe UI Semibold" con Regular.
        /// Meta: 400 (Regular) - usa "Segoe UI" con Regular.
        /// </summary>
        private static Font CreateFontForWeight(string[] familyCandidates, float sizeInPixels, FontStyle style)
        {
            foreach (string familyName in familyCandidates)
            {
                try
                {
                    using (var family = new FontFamily(familyName))
                    {
                        if (family.IsStyleAvailable(FontStyle.Regular))
                        {
                            return new Font(family, sizeInPixels, style, GraphicsUnit.Pixel);
                        }
                    }
                }
                catch (Exception)
                {
                    // Try next candidate
                }
            }

            // Fallback genérico
            return new Font("Segoe UI", sizeInPixels, style, GraphicsUnit.Pixel);
        }

        // Métodos de conveniencia para cada elemento con peso exacto
        private static Font CreateTitleFont(float sizeInPixels) => CreateFontForWeight(TitleFontCandidates, sizeInPixels, FontStyle.Bold);
        private static Font CreateOriginFont(float sizeInPixels) => CreateFontForWeight(OriginFontCandidates, sizeInPixels, FontStyle.Regular);
        private static Font CreateMetaFont(float sizeInPixels) => CreateFontForWeight(MetaFontCandidates, sizeInPixels, FontStyle.Regular);

        /// <summary>
        /// Crea la fuente nativa de emojis ("Segoe UI Emoji" con fallbacks). Los glifos
        /// a color (COLR/CPAL) solo existen en estas familias, nunca en "Segoe UI" ni en
        /// las genéricas tipo "sans-serif" (renderizaban tofu/cuadros vacíos).
        /// </summary>
        private static Font CreateEmojiFont(float sizeInPixels)
        {
            foreach (string familyName in EmojiFontCandidates)
            {
                try
                {
                    using (var family = new FontFamily(familyName))
                    {
                        if (family.IsStyleAvailable(FontStyle.Regular))
                        {
                            return new Font(family, sizeInPixels, FontStyle.Regular, GraphicsUnit.Pixel);
                        }
                    }
                }
                catch (Exception)
                {
                    // Try the next candidate
                }
            }

            return new Font("Segoe UI", sizeInPixels, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        /// <summary>
        /// Hornea el header corporativo sobre la imagen y devuelve la nueva imagen
        /// (header + contenido sin el header previo). Devuelve null si la imagen es
        /// nula o supera 16384px en alguna dimensión (guard anti-OOM: sin header).
        /// El llamador libera la imagen devuelta.
        /// </summary>
        public static Bitmap Bake(Image source, CorporateHeaderMeta meta)
        {
            if (source == null)
            {
                return null;
            }

            int srcWidth = source.Width;
            int srcHeight = source.Height;
            if (srcWidth <= 0 || srcHeight <= 0)
            {
                return null;
            }

            // Guard anti-OOM: imágenes extremas se devuelven sin header (worker MAX_DIMENSION).
            if (srcWidth > MaxDimension || srcHeight > MaxDimension)
            {
                return null;
            }

            // Detección del header previo: línea naranja #FF6B00 de abajo hacia arriba
            // (máx. 400px), muestreando x = min(20, w-1). headerEnd = y + 4 incluye la línea.
            int headerEnd = DetectPreviousHeaderEnd(source, srcWidth, srcHeight);

            // Altura del contenido real (sin header viejo).
            int contentHeight = srcHeight - headerEnd;

            // Medir el origen con la fuente del header para calcular la altura dinámica.
            string displayUrl = string.IsNullOrWhiteSpace(meta.Origin) ? "Adjunto Local" : meta.Origin;
            List<string> urlLines = WrapTextAnywhere($"Origen: {displayUrl}", srcWidth - 130);
            int dynHeaderHeight = BaseHeaderHeight + (urlLines.Count - 1) * UrlLineHeight;

            var result = new Bitmap(srcWidth, dynHeaderHeight + contentHeight, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                // Fondo con degradado corporativo HORIZONTAL (#002b55 → #004080).
                using (var gradient = new LinearGradientBrush(
                    new Rectangle(0, 0, srcWidth, dynHeaderHeight), GradientStart, GradientEnd, 0f))
                {
                    g.FillRectangle(gradient, 0, 0, srcWidth, dynHeaderHeight);
                }

                // Borde inferior de acento naranja (4px, SOLO abajo).
                using (var stripe = new SolidBrush(StripeColor))
                {
                    g.FillRectangle(stripe, 0, dynHeaderHeight - StripeBottom, srcWidth, StripeBottom);
                }

                // Logo 65×45 centrado verticalmente (o placeholder naranja "SQA").
                int logoY = (dynHeaderHeight - LogoHeight) / 2;
                using (Image logo = GetLogo())
                {
                    if (logo != null)
                    {
                        g.DrawImage(logo, LogoX, logoY, LogoWidth, LogoHeight);
                    }
                    else
                    {
                        using (var placeholder = new SolidBrush(StripeColor))
                        {
                            g.FillRectangle(placeholder, LogoX, logoY, LogoWidth, LogoHeight);
                        }

                        using (var white = new SolidBrush(Color.White))
                        using (var sqaFont = new Font("Segoe UI", 18f, FontStyle.Bold, GraphicsUnit.Pixel))
                        {
                            StringFormat center = new StringFormat
                            {
                                Alignment = StringAlignment.Center,
                                LineAlignment = StringAlignment.Center
                            };
                            g.DrawString("SQA", sqaFont, white,
                                new RectangleF(LogoX, logoY, LogoWidth, LogoHeight), center);
                        }
                    }
                }

                // Título Bold 700 18px blanco.
                using (var titleFont = CreateTitleFont(18f))
                using (var white = new SolidBrush(Color.White))
                {
                    g.DrawString(string.IsNullOrWhiteSpace(meta.Title) ? "Evidencia de prueba QA" : meta.Title,
                        titleFont, white, TextX, TitleY);
                }

                // Origen Semibold 600 16px con wrap en cualquier carácter.
                using (var urlFont = CreateOriginFont(16f))
                using (var white = new SolidBrush(Color.White))
                {
                    for (int i = 0; i < urlLines.Count; i++)
                    {
                        g.DrawString(urlLines[i], urlFont, white, TextX, UrlFirstY + i * UrlLineHeight);
                    }
                }

                // Meta Regular 400 17px al 85% de opacidad: "ID: EV-XX | 📅 fecha | 🌐 navegador | 💻 SO".
                // Los emojis se dibujan como IMÁGENES PNG pre-renderizadas (GDI+ no produce color).
                int metaY = MetaBaseY + (urlLines.Count - 1) * UrlLineHeight;
                string idLabel = meta.BuildEvidenceIdString();
                string dateStr = meta.CaptureTimestamp.ToString("dd/MM/yyyy, hh:mm:ss tt");
                string browserLabel = string.IsNullOrWhiteSpace(meta.Browser) ? "N/A" : meta.Browser;
                string osLabel = string.IsNullOrWhiteSpace(meta.Os) ? "N/A" : meta.Os;
                using (var metaFont = CreateMetaFont(17f))
                using (var emojiFont = CreateEmojiFont(17f))
                using (var metaBrush = new SolidBrush(Color.FromArgb(217, Color.White))) // 0.85 * 255 ≈ 217
                {
                    string metaText = $"{idLabel} | 📅 {dateStr} | 🌐 {browserLabel} | 💻 {osLabel}";
                    EvidenciasSQA.Core.TextRendering.ColorEmojiTextRenderer.DrawWithEmojis(g, metaText, metaFont, emojiFont, metaBrush, TextX, metaY);
                }

                // Fondo blanco bajo el contenido (idéntico al worker).
                g.FillRectangle(Brushes.White, 0, dynHeaderHeight, srcWidth, contentHeight);

                // Copiar el contenido real (debajo del header viejo si existía).
                // src = (0, headerEnd) → dest = (0, dynHeaderHeight), como el worker.
                if (contentHeight > 0)
                {
                    g.DrawImage(source,
                        new Rectangle(0, dynHeaderHeight, srcWidth, contentHeight),
                        new Rectangle(0, headerEnd, srcWidth, contentHeight),
                        GraphicsUnit.Pixel);
                }
            }

            return result;
        }

        /// <summary>
        /// Escanea la línea naranja #FF6B00 (rgb 255,107,0) de abajo hacia arriba
        /// (máx. 400px) en x = min(20, w-1). Devuelve headerEnd = y + 4, o 0 si no hay.
        /// </summary>
        private static int DetectPreviousHeaderEnd(Image source, int srcWidth, int srcHeight)
        {
            try
            {
                int sampleX = Math.Min(20, srcWidth - 1);
                int startY = Math.Min(srcHeight - 1, MaxHeaderScan);
                for (int y = startY; y >= 0; y--)
                {
                    using (Bitmap onePx = new Bitmap(1, 1, PixelFormat.Format32bppArgb))
                    {
                        using (Graphics g = Graphics.FromImage(onePx))
                        {
                            g.DrawImage(source, new Rectangle(0, 0, 1, 1),
                                new Rectangle(sampleX, y, 1, 1), GraphicsUnit.Pixel);
                        }

                        Color c = onePx.GetPixel(0, 0);
                        if (c.R == 255 && c.G == 107 && c.B == 0)
                        {
                            return y + 4;
                        }
                    }
                }
            }
            catch
            {
                // Conservador: sin detección → no recortar.
            }

            return 0;
        }

        /// <summary>
        /// Carga y cachea el logo corporativo desde Media\SQA1.png (junto al ejecutable).
        /// Devuelve null si no está disponible (el baker dibuja el placeholder naranja "SQA").
        /// IMPORTANTE: devuelve una COPIA fresca del logo cacheado. El llamador la envuelve
        /// en 'using' y la dispone sin dañar el caché (bug 2026-08-18: el primer bake
        /// disponía la instancia cacheada compartida → ArgumentException en el segundo
        /// bake → headers omitidos silenciosamente en todas las capturas posteriores).
        /// </summary>
        private static Image GetLogo()
        {
            if (_cachedLogo != null)
            {
                return new Bitmap(_cachedLogo);
            }

            lock (LogoLock)
            {
                if (_cachedLogo != null)
                {
                    return new Bitmap(_cachedLogo);
                }

                try
                {
                    string[] candidates =
                    {
                        Path.Combine(AppContext.BaseDirectory, "Media", "SQA1.png"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Media", "SQA1.png")
                    };

                    foreach (string path in candidates)
                    {
                        if (File.Exists(path))
                        {
                            using (var original = Image.FromFile(path))
                            {
                                _cachedLogo = new Bitmap(original);
                            }

                            return new Bitmap(_cachedLogo);
                        }
                    }
                }
                catch
                {
                    _cachedLogo = null;
                }

                return null;
            }
        }

        /// <summary>
        /// Replica de wrapTextAnywhere del worker: rompe el texto en CUALQUIER carácter
        /// (no solo espacios) cuando la línea excede maxWidth.
        /// </summary>
        private static List<string> WrapTextAnywhere(string text, int maxWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                return lines;
            }

            using (var measure = Graphics.FromImage(new Bitmap(1, 1)))
            using (var font = CreateOriginFont(16f))
            {
                string currentLine = string.Empty;
                foreach (char ch in text)
                {
                    string testLine = currentLine + ch;
                    if (measure.MeasureString(testLine, font).Width > maxWidth && currentLine.Length > 0)
                    {
                        lines.Add(currentLine);
                        currentLine = ch.ToString();
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
            }

            return lines;
        }

        /// <summary>
        /// Dibuja un texto que puede contener emojis mezclados con texto normal:
        /// cada run se dibuja con su fuente correcta (base vs emoji) y se avanza el
        /// cursor midiendo el ancho real del run. Los emojis se renderizan con
        /// TextRenderingHint.AntiAlias (ClearType degrada los glifos a color).
        /// </summary>
        private static void DrawTextWithEmojis(Graphics g, string text, Font textFont, Font emojiFont,
            Brush brush, float x, float y)
        {
            foreach ((string runText, bool isEmoji) in SplitEmojiRuns(text))
            {
                Font runFont = isEmoji ? emojiFont : textFont;
                TextRenderingHint previousHint = g.TextRenderingHint;
                if (isEmoji)
                {
                    g.TextRenderingHint = TextRenderingHint.AntiAlias;
                }

                g.DrawString(runText, runFont, brush, x, y);
                x += g.MeasureString(runText, runFont).Width;
                g.TextRenderingHint = previousHint;
            }
        }

        /// <summary>
        /// Divide el texto en runs de emoji / no-emoji preservando los pares suplentes
        /// y los selectores de variación (VS16 / ZWJ) como parte del glifo emoji.
        /// </summary>
        private static List<(string Text, bool IsEmoji)> SplitEmojiRuns(string text)
        {
            var runs = new List<(string, bool)>();
            if (string.IsNullOrEmpty(text))
            {
                return runs;
            }

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
            {
                runs.Add((buffer.ToString(), currentIsEmoji.Value));
            }

            return runs;
        }

        /// <summary>
        /// True si el Rune pertenece a los rangos de glifos emoji (símbolos y pictogramas,
        /// misceláneos, dingbats, flechas, selectores de variación y ZWJ).
        /// </summary>
        private static bool IsEmojiRune(Rune rune)
        {
            int v = rune.Value;
            return (v >= 0x1F000 && v <= 0x1FAFF) || // Symbols & Pictographs / Extended
                   (v >= 0x2600 && v <= 0x27BF) ||   // Misc symbols & Dingbats
                   (v >= 0x2190 && v <= 0x21FF) ||   // Arrows
                   (v >= 0x2B00 && v <= 0x2BFF) ||   // Misc symbols and arrows
                   (v >= 0xFE00 && v <= 0xFE0F) ||   // Variation selectors (VS16)
                   v == 0x200D;                      // Zero Width Joiner
        }
    }
}