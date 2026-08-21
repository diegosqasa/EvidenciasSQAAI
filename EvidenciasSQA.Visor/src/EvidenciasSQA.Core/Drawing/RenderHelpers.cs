using System.Drawing;
using System.Windows;
using System.Windows.Media;
using DrawingColor = System.Drawing.Color;
using DrawingPoint = System.Drawing.Point;
using DrawingBrush = System.Drawing.Brush;
using DrawingPen = System.Drawing.Pen;
using MediaBrush = System.Windows.Media.Brush;
using MediaPen = System.Windows.Media.Pen;
using MediaColor = System.Windows.Media.Color;

namespace EvidenciasSQA.Core.Drawing;

/// <summary>
/// Puente de conversión entre los dos mundos gráficos que conviven en el prototipo:
/// GDI+ (System.Drawing, fuente de verdad de la imagen) y WPF (System.Windows.Media).
/// </summary>
public static class RenderHelpers
{
    public static Rect ToWpfRect(Rectangle rect) => new(rect.X, rect.Y, Math.Max(0, rect.Width), Math.Max(0, rect.Height));

    public static MediaColor ToMediaColor(DrawingColor color) =>
        MediaColor.FromArgb(color.A, color.R, color.G, color.B);

    public static MediaBrush ToMediaBrush(DrawingColor color) => new SolidColorBrush(ToMediaColor(color));

    public static MediaPen ToMediaPen(DrawingColor color, double thickness) =>
        new(ToMediaBrush(color), thickness);

    public static Rectangle ToDrawingRect(Rect rect) =>
        new((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);

    public static DrawingPoint ToDrawingPoint(System.Windows.Point point) =>
        new((int)point.X, (int)point.Y);

    /// <summary>
    /// Borde punteado + esquinas de selección (equivalente visual de los Adorners
    /// de EvidenciasSQA: TargetAdorner / ResizeAdorner).
    /// Estilo SQA_OBJ_CONFIG replicado del módulo web (Fabric.js): esquinas
    /// naranjas #FF6B00 con borde blanco, tamaño 8px.
    /// </summary>
    public static void DrawSelectionAdornment(DrawingContext dc, Rect rect)
    {
        var dashPen = new MediaPen(new SolidColorBrush(MediaColor.FromArgb(220, 0x00, 0x2B, 0x55)), 1)
        {
            DashStyle = DashStyles.Dash
        };
        dc.DrawRectangle(null, dashPen, rect);

        const double size = 8;
        var cornerBrush = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0x6B, 0x00));
        var cornerStroke = new MediaPen(new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF)), 1);
        foreach (System.Windows.Point corner in new[]
                 {
                     new System.Windows.Point(rect.Left, rect.Top),
                     new System.Windows.Point(rect.Right, rect.Top),
                     new System.Windows.Point(rect.Left, rect.Bottom),
                     new System.Windows.Point(rect.Right, rect.Bottom)
                 })
        {
            dc.DrawRectangle(cornerBrush, cornerStroke,
                new Rect(corner.X - size / 2, corner.Y - size / 2, size, size));
        }
    }

    /// <summary>
    /// Sombreado del overlay de recorte (rgba(0,0,0,0.7)) y borde naranja #fca311,
    /// replicando el sistema de recorte del módulo web (Fabric.js).
    /// </summary>
    public static void DrawCropOverlay(DrawingContext dc, Rect cropRect, double imageWidth, double imageHeight)
    {
        var shade = new SolidColorBrush(MediaColor.FromArgb(179, 0, 0, 0)); // 0.7 * 255 ≈ 179
        var borderPen = new MediaPen(new SolidColorBrush(MediaColor.FromRgb(0xfc, 0xa3, 0x11)), 1.5);

        // 4 shades alrededor del rectángulo de recorte
        dc.DrawRectangle(shade, null, new Rect(0, 0, imageWidth, cropRect.Top));
        dc.DrawRectangle(shade, null, new Rect(0, cropRect.Bottom, imageWidth, imageHeight - cropRect.Bottom));
        dc.DrawRectangle(shade, null, new Rect(0, cropRect.Top, cropRect.Left, cropRect.Height));
        dc.DrawRectangle(shade, null, new Rect(cropRect.Right, cropRect.Top, imageWidth - cropRect.Right, cropRect.Height));

        dc.DrawRectangle(null, borderPen, cropRect);

        // 4 handles de esquina: naranja #FF6B00 con borde blanco
        const double handleSize = 8;
        var handleBrush = new SolidColorBrush(MediaColor.FromRgb(0xFF, 0x6B, 0x00));
        var handleStroke = new MediaPen(new SolidColorBrush(MediaColor.FromRgb(0xFF, 0xFF, 0xFF)), 1);
        foreach (System.Windows.Point corner in new[]
                 {
                     new System.Windows.Point(cropRect.Left, cropRect.Top),
                     new System.Windows.Point(cropRect.Right, cropRect.Top),
                     new System.Windows.Point(cropRect.Left, cropRect.Bottom),
                     new System.Windows.Point(cropRect.Right, cropRect.Bottom)
                 })
        {
            dc.DrawRectangle(handleBrush, handleStroke,
                new Rect(corner.X - handleSize / 2, corner.Y - handleSize / 2, handleSize, handleSize));
        }
    }
}
