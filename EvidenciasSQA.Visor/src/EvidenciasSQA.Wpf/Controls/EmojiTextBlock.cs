using System.Collections.Concurrent;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EvidenciasSQA.Core.TextRendering;

namespace EvidenciasSQA.Wpf.Controls;

/// <summary>
/// TextBlock enriquecido que renderiza emojis A COLOR dentro de texto normal.
///
/// Por qué existe: WPF (FormattedText/GlyphRun) NO renderiza glifos emoji a color
/// (los degrada a monocromo o tofu). Este control divide el texto en runs y compone
/// cada emoji como una imagen en línea (InlineUIContainer) cuyo bitmap se renderiza
/// con GDI+ vía ColorEmojiTextRenderer (glifos COLR/CPAL de "Segoe UI Emoji").
/// El texto normal se mantiene en la tipografía principal (Segoe UI Variable, se
/// hereda del elemento) sin degradar la calidad ClearType.
///
/// Uso (DataBinding): vincular la propiedad <see cref="EmojiText"/> a una cadena del
/// ViewModel construida con ColorEmojiTextRenderer.BuildMetaLine(...). Ejemplo:
///   &lt;controls:EmojiTextBlock EmojiText="{Binding MetadataLine}" /&gt;
/// </summary>
public class EmojiTextBlock : TextBlock
{
    /// <summary>Cache de emojis renderizados por (codepoint + tamaño en píxeles).</summary>
    private static readonly ConcurrentDictionary<(string Emoji, int PixelSize), ImageSource> EmojiCache = new();

    /// <summary>DP bindable con el texto mixto (emojis incluidos).</summary>
    public static readonly DependencyProperty EmojiTextProperty =
        DependencyProperty.Register(
            nameof(EmojiText),
            typeof(string),
            typeof(EmojiTextBlock),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender, OnEmojiTextChanged));

    public string EmojiText
    {
        get => (string)GetValue(EmojiTextProperty);
        set => SetValue(EmojiTextProperty, value);
    }

    private static void OnEmojiTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EmojiTextBlock block)
        {
            block.RebuildInlines();
        }
    }

    /// <summary>Reconstruye los inlines: runs de texto + imágenes de emoji en línea.</summary>
    private void RebuildInlines()
    {
        Inlines.Clear();
        string text = EmojiText ?? string.Empty;
        if (text.Length == 0)
        {
            return;
        }

        int pixelSize = Math.Max(8, (int)Math.Round(FontSize * 1.15));
        foreach ((string runText, bool isEmoji) in ColorEmojiTextRenderer.SplitRuns(text))
        {
            if (isEmoji)
            {
                ImageSource emoji = GetEmojiImage(runText, pixelSize);
                if (emoji != null)
                {
                    var image = new System.Windows.Controls.Image
                    {
                        Source = emoji,
                        Width = pixelSize,
                        Height = pixelSize,
                        Stretch = Stretch.Uniform,
                        SnapsToDevicePixels = true,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var container = new InlineUIContainer(image)
                    {
                        BaselineAlignment = BaselineAlignment.Center
                    };
                    Inlines.Add(container);
                }
                else
                {
                    Inlines.Add(new Run(runText));
                }
            }
            else
            {
                Inlines.Add(new Run(runText));
            }
        }
    }

    /// <summary>
    /// Obtiene (y cachea) el emoji renderizado a color. Se usa "Segoe UI Emoji" para
    /// los glifos COLR/CPAL; el fondo es transparente y el bitmap se congela.
    /// </summary>
    private static ImageSource GetEmojiImage(string emoji, int pixelSize)
    {
        var key = (emoji, pixelSize);
        if (EmojiCache.TryGetValue(key, out ImageSource? cached))
        {
            return cached;
        }

        try
        {
            using var textFont = new System.Drawing.Font("Segoe UI", pixelSize, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
            using var emojiFont = ColorEmojiTextRenderer.CreateEmojiFont(pixelSize);
            BitmapSource source = ColorEmojiTextRenderer.RenderToBitmapSource(
                emoji, textFont, emojiFont, System.Drawing.Color.White, pixelSize);
            EmojiCache[key] = source;
            return source;
        }
        catch
        {
            return null;
        }
    }
}