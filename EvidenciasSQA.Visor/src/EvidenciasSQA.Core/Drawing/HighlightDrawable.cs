using System.Drawing;
using System.Windows.Media;
using System.Xml.Serialization;
using DrawingColor = System.Drawing.Color;

namespace EvidenciasSQA.Core.Drawing;

/// <summary>
/// Resaltado semitransparente, equivalente al Rect con fill=color y
/// opacity=0.35 del módulo web (Fabric.js). Cubre una región con el color
/// activo al 35% de opacidad para subrayar información relevante.
/// </summary>
public sealed class HighlightDrawable : DrawableObject
{
    /// <summary>Opacidad del relleno (0.35 replicado del módulo web).</summary>
    public const double Opacity = 0.35;

    public HighlightDrawable()
    {
        FillColor = DrawingColor.Yellow;
    }

    [XmlIgnore]
    public DrawingColor FillColor { get; set; }

    [XmlElement("FillColor")]
    public int FillColorArgb
    {
        get => FillColor.ToArgb();
        set => FillColor = DrawingColor.FromArgb(value);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext dc, RenderMode mode)
    {
        var fill = new SolidColorBrush(
            System.Windows.Media.Color.FromArgb(
                (byte)(FillColor.A * Opacity),
                FillColor.R, FillColor.G, FillColor.B));

        dc.DrawRectangle(fill, null, RenderHelpers.ToWpfRect(NormalizedBounds));

        if (Selected)
        {
            RenderHelpers.DrawSelectionAdornment(dc, RenderHelpers.ToWpfRect(NormalizedBounds));
        }
    }

    /// <inheritdoc/>
    public override void RenderForExport(Graphics g, RenderMode mode)
    {
        using SolidBrush brush = new(
            DrawingColor.FromArgb((byte)(FillColor.A * Opacity), FillColor.R, FillColor.G, FillColor.B));
        g.FillRectangle(brush, NormalizedBounds);
    }
}