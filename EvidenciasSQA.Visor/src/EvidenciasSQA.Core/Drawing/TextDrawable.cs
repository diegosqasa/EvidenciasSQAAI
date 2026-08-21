using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using DrawingColor = System.Drawing.Color;
using MediaColor = System.Windows.Media.Color;
using MediaPoint = System.Windows.Point;

namespace EvidenciasSQA.Core.Drawing;

/// <summary>
/// Texto de anotación editable, equivalente al IText del módulo web (Fabric.js):
/// fontFamily='Segoe UI', fontSize, fill=color, stroke='#fff', strokeWidth=1,
/// paintFirst='stroke' (el borde blanco se dibuja antes que el relleno) y padding.
///
/// El texto se edita en la vista (EditorWindow muestra un TextBox sobre el
/// elemento); el modelo solo lo almacena y lo renderiza en ambas vías
/// (WPF pantalla + GDI+ exportación).
/// </summary>
public sealed class TextDrawable : DrawableObject
{
    public const int Padding = 10; // padding replicado del IText web

    public TextDrawable()
    {
        FontSize = 20;
        TextColor = DrawingColor.Red;
    }

    public string Text { get; set; } = "Texto";

    public int FontSize { get; set; }

    [XmlIgnore]
    public DrawingColor TextColor { get; set; }

    [XmlElement("TextColor")]
    public int TextColorArgb
    {
        get => TextColor.ToArgb();
        set => TextColor = DrawingColor.FromArgb(value);
    }

    /// <summary>Nombre de la familia tipográfica (Segoe UI, como el IText web).</summary>
    [XmlIgnore]
    public string FontFamilyName => "Segoe UI";

    /// <inheritdoc/>
    public override void Render(DrawingContext dc, RenderMode mode)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        MediaPoint origin = new(Left + Padding, Top + Padding);
        FormattedText formatted = BuildFormattedText();

        // paintFirst='stroke': borde blanco de 1px dibujado antes que el relleno.
        var outlineGeometry = formatted.BuildGeometry(origin);
        var outlinePen = new System.Windows.Media.Pen(new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF)), 1);
        dc.DrawGeometry(null, outlinePen, outlineGeometry);

        var fillBrush = new SolidColorBrush(RenderHelpers.ToMediaColor(TextColor));
        dc.DrawGeometry(fillBrush, null, outlineGeometry);

        if (Selected)
        {
            RenderHelpers.DrawSelectionAdornment(dc, RenderHelpers.ToWpfRect(Bounds));
        }
    }

    /// <inheritdoc/>
    public override void RenderForExport(Graphics g, RenderMode mode)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        using GraphicsPath path = BuildExportPath();
        // paintFirst='stroke': contorno blanco 1px + relleno de color.
        using (System.Drawing.Pen outline = new(DrawingColor.White, 1f))
        {
            g.DrawPath(outline, path);
        }

        using SolidBrush fill = new(TextColor);
        g.FillPath(fill, path);
    }

    /// <summary>
    /// Recalcula Width/Height para que el borde contenga el texto + padding.
    /// Se invoca al crear el elemento y al confirmar la edición del texto.
    /// </summary>
    public void ResizeToText()
    {
        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        FormattedText formatted = BuildFormattedText();
        Width = (int)Math.Ceiling(formatted.Width) + Padding * 2;
        Height = (int)Math.Ceiling(formatted.Height) + Padding * 2;
    }

    /// <summary>FormattedText WPF para medida y render en pantalla.</summary>
    private FormattedText BuildFormattedText()
    {
        // PixelsPerDip fijo 1.0 (96 DPI): el modelo no vive en el árbol visual;
        // el canvas del editor usa las mismas unidades WPF a 96 DPI.
        return new FormattedText(
            Text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface(
                new System.Windows.Media.FontFamily(FontFamilyName),
                System.Windows.FontStyles.Normal,
                System.Windows.FontWeights.Normal,
                System.Windows.FontStretches.Normal),
            FontSize,
            System.Windows.Media.Brushes.Black,
            1.0);
    }

    /// <summary>GraphicsPath GDI+ para el horneado de exportación.</summary>
    private GraphicsPath BuildExportPath()
    {
        var path = new GraphicsPath();
        path.AddString(
            Text,
            new System.Drawing.FontFamily(FontFamilyName),
            (int)System.Drawing.FontStyle.Regular,
            FontSize,
            new PointF(Left + Padding, Top + Padding),
            StringFormat.GenericTypographic);
        return path;
    }
}