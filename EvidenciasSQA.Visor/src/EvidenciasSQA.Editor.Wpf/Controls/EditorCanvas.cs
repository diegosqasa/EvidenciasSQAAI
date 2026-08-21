using System.Drawing;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EvidenciasSQA.Core.Drawing;
using EvidenciasSQA.Core.Model;
using Point = System.Windows.Point;

namespace EvidenciasSQA.Editor.Wpf.Controls;

/// <summary>
/// Lienzo nativo del editor (Vista MVVM). Un FrameworkElement ligero que:
///  - Renderiza la superficie completa (fondo + elementos) con DrawingContext,
///    el modo retenido de WPF: solo se repinta cuando la superficie lo pide.
///  - Se dimensiona al tamaño de la imagen × zoom para que el ScrollViewer la
///    encuadre; el zoom se aplica como escala del contexto de dibujo
///    (equivalente a canvas.setZoom() del módulo web).
///  - Dibuja el overlay de recorte (shades + borde + handles) en coordenadas
///    de pantalla cuando CropActive.
///  - Convierte eventos de ratón en coordenadas de IMAGEN (divide por zoom)
///    y las entrega al ViewModel, que es quien decide la semántica.
///
/// Aquí NO hay lógica de imagen: todo el conocimiento vive en SurfaceDocument y
/// en los DrawableObject (capas desacopladas de la vista).
/// </summary>
public sealed class EditorCanvas : FrameworkElement
{
    /// <summary>
    /// Documento a mostrar. Al cambiar, se re-enganchan las notificaciones de
    /// RequestRender para invalidar visual sin acoplar el modelo a WPF.
    /// </summary>
    public static readonly DependencyProperty SurfaceProperty = DependencyProperty.Register(
        nameof(Surface),
        typeof(SurfaceDocument),
        typeof(EditorCanvas),
        new PropertyMetadata(null, OnSurfaceChanged));

    /// <summary>Zoom del lienzo (0.1–5.0). El ViewModel es la fuente de verdad.</summary>
    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom),
        typeof(double),
        typeof(EditorCanvas),
        new PropertyMetadata(1.0, OnVisualPropertyChanged));

    /// <summary>Rectángulo de recorte en coordenadas de IMAGEN (bind al ViewModel).</summary>
    public static readonly DependencyProperty CropRectProperty = DependencyProperty.Register(
        nameof(CropRect),
        typeof(Rectangle),
        typeof(EditorCanvas),
        new PropertyMetadata(Rectangle.Empty, OnVisualPropertyChanged));

    /// <summary>True mientras el overlay de recorte está visible.</summary>
    public static readonly DependencyProperty CropActiveProperty = DependencyProperty.Register(
        nameof(CropActive),
        typeof(bool),
        typeof(EditorCanvas),
        new PropertyMetadata(false, OnVisualPropertyChanged));

    public SurfaceDocument? Surface
    {
        get => (SurfaceDocument?)GetValue(SurfaceProperty);
        set => SetValue(SurfaceProperty, value);
    }

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public Rectangle CropRect
    {
        get => (Rectangle)GetValue(CropRectProperty);
        set => SetValue(CropRectProperty, value);
    }

    public bool CropActive
    {
        get => (bool)GetValue(CropActiveProperty);
        set => SetValue(CropActiveProperty, value);
    }

    /// <summary>Pulsa el botón izquierdo en coordenadas de imagen.</summary>
    public event Action<Point>? MouseDownOnCanvas;

    /// <summary>Movimiento con botón izquierdo presionado.</summary>
    public event Action<Point>? MouseMoveOnCanvas;

    /// <summary>Suelta el botón izquierdo.</summary>
    public event Action<Point>? MouseUpOnCanvas;

    /// <summary>Doble clic en coordenadas de imagen (aplica recorte en el VM).</summary>
    public event Action<Point>? DoubleClickOnCanvas;

    private static void OnSurfaceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (EditorCanvas)d;
        if (e.OldValue is SurfaceDocument previous)
        {
            previous.RequestRender -= canvas.OnRequestRender;
        }

        if (e.NewValue is SurfaceDocument current)
        {
            current.RequestRender += canvas.OnRequestRender;
        }

        canvas.InvalidateMeasure();
        canvas.InvalidateVisual();
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (EditorCanvas)d;
        canvas.InvalidateMeasure();
        canvas.InvalidateVisual();
    }

    private void OnRequestRender(object? sender, EventArgs e) => InvalidateVisual();

    /// <summary>
    /// El tamaño del control es el de la imagen × zoom, lo que permite al
    /// ScrollViewer desplazarse sobre el lienzo escalado (mismo contrato que el
    /// setWidth/setHeight × zoom del canvas web).
    /// </summary>
    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        if (Surface?.BackgroundBitmap != null)
        {
            double zoom = Zoom <= 0 ? 1.0 : Zoom;
            return new System.Windows.Size(Surface.ImageWidth * zoom, Surface.ImageHeight * zoom);
        }

        return new System.Windows.Size(0, 0);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (Surface?.BackgroundBitmap == null)
        {
            return;
        }

        double zoom = Zoom <= 0 ? 1.0 : Zoom;

        // Escala del lienzo: se dibuja en coordenadas de imagen y el contexto
        // las proyecta × zoom (equivalente a canvas.setZoom del módulo web).
        drawingContext.PushTransform(new ScaleTransform(zoom, zoom));
        Surface.Render(drawingContext, RenderMode.Edit);
        drawingContext.Pop();

        // Overlay de recorte en coordenadas de pantalla (handles de tamaño fijo).
        if (CropActive && CropRect.Width > 0 && CropRect.Height > 0)
        {
            Rect screenRect = new(
                CropRect.X * zoom,
                CropRect.Y * zoom,
                CropRect.Width * zoom,
                CropRect.Height * zoom);
            RenderHelpers.DrawCropOverlay(drawingContext, screenRect,
                Surface.ImageWidth * zoom, Surface.ImageHeight * zoom);
        }
    }

    private Point ToImageCoordinates(MouseEventArgs e) => new(
        e.GetPosition(this).X / (Zoom <= 0 ? 1.0 : Zoom),
        e.GetPosition(this).Y / (Zoom <= 0 ? 1.0 : Zoom));

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            CaptureMouse();

            // Doble clic → aplicar recorte (mouse:dblclick del módulo web).
            if (e.ClickCount == 2)
            {
                DoubleClickOnCanvas?.Invoke(ToImageCoordinates(e));
                e.Handled = true;
                return;
            }

            MouseDownOnCanvas?.Invoke(ToImageCoordinates(e));
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.LeftButton == MouseButtonState.Pressed && IsMouseCaptured)
        {
            MouseMoveOnCanvas?.Invoke(ToImageCoordinates(e));
        }
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.LeftButton == MouseButtonState.Released && IsMouseCaptured)
        {
            ReleaseMouseCapture();
            MouseUpOnCanvas?.Invoke(ToImageCoordinates(e));
            e.Handled = true;
        }
    }
}