using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using DrawingColor = System.Drawing.Color;
using MediaBrush = System.Windows.Media.Brush;
using MediaPen = System.Windows.Media.Pen;
using MediaColor = System.Windows.Media.Color;

namespace EvidenciasSQA.Core.Drawing;

/// <summary>
/// Rectángulo de anotación (borde + relleno + sombra), equivalente a
/// EvidenciasSQA.Editor.Drawing.RectangleContainer.
///
/// Los parámetros visuales se modelan como campos (estilo Fields de EvidenciasSQA)
/// pero como propiedades simples para mantener el prototipo legible.
/// </summary>
public sealed class RectangleDrawable : DrawableObject
{
    public RectangleDrawable()
    {
        LineColor = DrawingColor.Red;
        FillColor = DrawingColor.Transparent;
        LineThickness = 2;
        Shadow = true;
    }

    // XmlSerializer no puede serializar System.Drawing.Color (todas sus propiedades
    // son de solo lectura), así que el XML transporta el valor ARGB y se puentea
    // a la propiedad pública; el elemento se llama igual que la propiedad original.
    [XmlIgnore]
    public DrawingColor LineColor { get; set; }

    [XmlElement("LineColor")]
    public int LineColorArgb
    {
        get => LineColor.ToArgb();
        set => LineColor = DrawingColor.FromArgb(value);
    }

    [XmlIgnore]
    public DrawingColor FillColor { get; set; }

    [XmlElement("FillColor")]
    public int FillColorArgb
    {
        get => FillColor.ToArgb();
        set => FillColor = DrawingColor.FromArgb(value);
    }

    public int LineThickness { get; set; }
    public bool Shadow { get; set; }

    /// <inheritdoc/>
    public override void Render(DrawingContext dc, RenderMode mode)
    {
        Rect rect = RenderHelpers.ToWpfRect(NormalizedBounds);

        MediaBrush fillBrush = RenderHelpers.ToMediaBrush(FillColor);
        MediaPen linePen = RenderHelpers.ToMediaPen(LineColor, Math.Max(1, LineThickness));

        if (Shadow && mode == RenderMode.Edit)
        {
            // Sombra simple en pantalla: rectángulo desplazado semitransparente.
            dc.DrawRectangle(new SolidColorBrush(MediaColor.FromArgb(60, 0, 0, 0)), null,
                new Rect(rect.X + 3, rect.Y + 3, rect.Width, rect.Height));
        }

        dc.DrawRectangle(fillBrush, linePen, rect);

        if (Selected)
        {
            RenderHelpers.DrawSelectionAdornment(dc, rect);
        }
    }

    /// <inheritdoc/>
    public override void RenderForExport(Graphics g, RenderMode mode)
    {
        // Mismo estilo que EvidenciasSQA: calidad alta para la sombra, relleno, y
        // líneas nítidas (HighSpeed) para el contorno final.
        Rectangle rect = NormalizedBounds;

        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.CompositingQuality = CompositingQuality.HighQuality;

        bool lineVisible = LineThickness > 0 && LineColor.A > 0;

        if (Shadow && (lineVisible || FillColor.A > 0))
        {
            using System.Drawing.Pen shadowPen = new(DrawingColor.FromArgb(60, 0, 0, 0), LineThickness + 2);
            g.DrawRectangle(shadowPen, rect.X + 3, rect.Y + 3, rect.Width, rect.Height);
        }

        if (FillColor.A > 0)
        {
            using SolidBrush fill = new(FillColor);
            g.FillRectangle(fill, rect);
        }

        g.SmoothingMode = SmoothingMode.HighSpeed;
        if (lineVisible)
        {
            using System.Drawing.Pen pen = new(LineColor, LineThickness);
            g.DrawRectangle(pen, rect);
        }
    }
}
