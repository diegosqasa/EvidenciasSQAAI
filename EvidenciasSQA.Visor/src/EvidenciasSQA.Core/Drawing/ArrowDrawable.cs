using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using DrawingColor = System.Drawing.Color;
using DrawingPoint = System.Drawing.Point;
using MediaBrush = System.Windows.Media.Brush;
using MediaPen = System.Windows.Media.Pen;
using MediaColor = System.Windows.Media.Color;
using MediaPoint = System.Windows.Point;

namespace EvidenciasSQA.Core.Drawing;

/// <summary>
/// Flecha de anotación (línea + punta triangular al final), equivalente a la
/// combinación Line + Triangle (group) del módulo web (Fabric.js).
///
/// Geometría: el origen (Left, Top) es el inicio de la línea; (Width, Height)
/// es el delta hasta el final, donde se dibuja la punta apuntando en la
/// dirección de la línea (strokeLineCap='round').
/// </summary>
public sealed class ArrowDrawable : DrawableObject
{
    public ArrowDrawable()
    {
        LineColor = DrawingColor.Red;
        LineThickness = 2;
    }

    [XmlIgnore]
    public DrawingColor LineColor { get; set; }

    [XmlElement("LineColor")]
    public int LineColorArgb
    {
        get => LineColor.ToArgb();
        set => LineColor = DrawingColor.FromArgb(value);
    }

    public int LineThickness { get; set; }

    /// <summary>Tamaño de la punta en píxeles (proporcional al grosor).</summary>
    [XmlIgnore]
    public double HeadLength => Math.Max(10, LineThickness * 4);

    [XmlIgnore]
    public DrawingPoint Start => new(Left, Top);

    [XmlIgnore]
    public DrawingPoint End => new(Left + Width, Top + Height);

    /// <inheritdoc/>
    public override bool ClickableAt(DrawingPoint point)
    {
        // Distancia punto-segmento < umbral (6px), o cerca de la punta.
        double threshold = Math.Max(6, LineThickness + 4);
        return DistanceToSegment(point) <= threshold;
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext dc, RenderMode mode)
    {
        MediaPoint start = new(Left, Top);
        MediaPoint end = new(Left + Width, Top + Height);

        MediaPen pen = RenderHelpers.ToMediaPen(LineColor, Math.Max(1, LineThickness));
        dc.DrawLine(pen, start, end);

        // Punta: triángulo apuntando en la dirección de la línea.
        var headBrush = new SolidColorBrush(RenderHelpers.ToMediaColor(LineColor));
        dc.DrawGeometry(headBrush, null, BuildHeadGeometry(start, end));

        if (Selected)
        {
            RenderHelpers.DrawSelectionAdornment(dc, RenderHelpers.ToWpfRect(Bounds));
        }
    }

    /// <inheritdoc/>
    public override void RenderForExport(Graphics g, RenderMode mode)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.CompositingQuality = CompositingQuality.HighQuality;

        using System.Drawing.Pen pen = new(LineColor, LineThickness)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        g.DrawLine(pen, Start, End);

        using SolidBrush headBrush = new(LineColor);
        g.FillPolygon(headBrush, BuildHeadPoints());
    }

    /// <summary>
    /// Geometría WPF de la punta: triángulo isósceles con la base a
    /// <see cref="HeadLength"/> del final y ancho proporcional a la longitud.
    /// </summary>
    private StreamGeometry BuildHeadGeometry(MediaPoint start, MediaPoint end)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1)
        {
            return null;
        }

        double ux = dx / length;
        double uy = dy / length;
        double halfWidth = HeadLength * 0.45;
        MediaPoint apex = end;
        MediaPoint baseCenter = new(end.X - ux * HeadLength, end.Y - uy * HeadLength);
        MediaPoint baseA = new(baseCenter.X - uy * halfWidth, baseCenter.Y + ux * halfWidth);
        MediaPoint baseB = new(baseCenter.X + uy * halfWidth, baseCenter.Y - ux * halfWidth);

        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(apex, true, true);
            ctx.LineTo(baseA, true, false);
            ctx.LineTo(baseB, true, false);
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>Puntos GDI+ de la punta (exportación).</summary>
    private System.Drawing.PointF[] BuildHeadPoints()
    {
        float dx = End.X - Start.X;
        float dy = End.Y - Start.Y;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        if (length < 1)
        {
            return [End];
        }

        float ux = dx / length;
        float uy = dy / length;
        float halfWidth = (float)(HeadLength * 0.45);
        float baseX = End.X - (float)HeadLength * ux;
        float baseY = End.Y - (float)HeadLength * uy;

        return
        [
            End,
            new System.Drawing.PointF(baseX - uy * halfWidth, baseY + ux * halfWidth),
            new System.Drawing.PointF(baseX + uy * halfWidth, baseY - ux * halfWidth)
        ];
    }

    /// <summary>Distancia mínima del punto al segmento (start → end).</summary>
    private double DistanceToSegment(DrawingPoint p)
    {
        double px = p.X, py = p.Y;
        double sx = Left, sy = Top;
        double ex = Left + Width, ey = Top + Height;

        double dx = ex - sx, dy = ey - sy;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1)
        {
            return Math.Sqrt((px - sx) * (px - sx) + (py - sy) * (py - sy));
        }

        double t = Math.Clamp(((px - sx) * dx + (py - sy) * dy) / lenSq, 0, 1);
        double projX = sx + t * dx;
        double projY = sy + t * dy;
        return Math.Sqrt((px - projX) * (px - projX) + (py - projY) * (py - projY));
    }
}