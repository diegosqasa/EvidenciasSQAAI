using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EvidenciasSQA.Wpf.Controls;

/// <summary>
/// Visor GPU ligero: Image estándar de WPF con ScaleTransform + TranslateTransform
/// para pan/zoom. WPF acelera la transformación por GPU (DirectX).
/// Comportamiento tipo Fotos de Windows: fit-to-window centrado al cargar y al
/// redimensionar (si la vista está en modo fit), rueda = zoom suave (150 ms) anclado
/// al cursor, arrastre = pan por eje con límites estrictos (cursor de mano), doble
/// clic / Escape / botón "Ajustar" = fit animado, botones +/- y 1:1, % en la barra
/// de estado. Padding interno: la imagen nunca toca los bordes del control.
///
/// MODELO DE LÍMITES:
/// El Image interno usa Stretch=None: dibuja la imagen a su tamaño natural en DIP
/// (píxeles / DPI * 96). El tamaño renderizado es NaturalSize * Scale. Todos los
/// límites (centrado y clamp de pan) se calculan con ese tamaño natural en relación
/// al ÁREA EFECTIVA (ActualWidth/Height - Padding), NUNCA con RenderSize.
///
/// MODELO DE ANIMACIÓN (zoom suave):
/// Motor manual con DispatcherTimer (~15 ms/tick, EaseOut cuadrático): SOLO se anima
/// la escala (SetValue directo) y el translate se recalcula en el mismo tick con la
/// fórmula de anclaje  t = anchor + (s/s0)·(t0 - anchor), que mantiene el punto de la
/// imagen bajo el cursor EXACTO durante todo el escalado. NO se usan animaciones WPF
/// (DoubleAnimation/BeginAnimation): sus relojes internos disparan eventos de Freezable
/// cuya desuscripción lanzaba "Handler has not been registered with this event" en
/// caminos de cancelación. Con SetValue el valor base ES siempre el actual (no hay
/// valores fantasma que consolidar) y no hay eventos que limpiar: la cancelación es
/// solo parar el timer. Los límites (UpdatePosition) se aplican al terminar.
/// </summary>
public sealed class FastImageViewer : FrameworkElement
{
    private readonly Image _image = new() { Stretch = Stretch.None };

    /// <summary>
    /// Stretch property - controls how the image is stretched within the viewer.
    /// Default es None: el ajuste al área lo hace exclusivamente el ScaleTransform
    /// interno (FitToWindow). Uniform + autofit manual provocaban doble escala
    /// (imagen cortada y descentrada); corregido en 1.0.3.
    /// </summary>
    public static readonly DependencyProperty StretchProperty =
        DependencyProperty.Register(nameof(Stretch), typeof(Stretch), typeof(FastImageViewer), new PropertyMetadata(Stretch.None, OnStretchChanged));

    private static void OnStretchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FastImageViewer viewer && e.NewValue is Stretch stretch)
        {
            viewer._image.Stretch = stretch;
        }
    }

    public Stretch Stretch
    {
        get => (Stretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>
    /// Margen interno del visor: el área efectiva de cálculo es
    /// ActualWidth/Height menos este Padding. En modo fit la imagen se ajusta al
    /// área efectiva y en pan/zoom nunca toca los bordes del control.
    /// </summary>
    public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(
        nameof(Padding),
        typeof(Thickness),
        typeof(FastImageViewer),
        new PropertyMetadata(new Thickness(0), OnViewportMetricChanged));

    private static void OnViewportMetricChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FastImageViewer viewer)
        {
            return;
        }

        viewer.CancelZoomAnimation();
        if (viewer._inFitMode)
        {
            viewer.FitToWindow();
        }
        else
        {
            viewer.UpdatePosition();
        }
    }

    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>
    /// Notifica el porcentaje de zoom actual (100 = tamaño natural) tras cualquier
    /// cambio de escala. Lo consume la barra de estado del MainWindow.
    /// </summary>
    public event EventHandler<double>? ZoomChanged;

    private const double MinScale = 0.05;
    private const double MaxScale = 32.0;
    private const double WheelFactor = 1.15;
    private const double ZoomAnimationMs = 150.0;

    private readonly ScaleTransform _scale = new();
    private readonly TranslateTransform _translate = new();
    private Point? _dragStart;
    private double _dragStartPanX;
    private double _dragStartPanY;

    /// <summary>
    /// true = la vista sigue el ajuste a la ventana (se re-fitea al redimensionar);
    /// false = zoom/pan manual del usuario (el resize conserva el zoom y solo re-ancla).
    /// </summary>
    private bool _inFitMode = true;

    // Estado de la animación de zoom (motor manual con DispatcherTimer: se anima SOLO
    // la escala con SetValue directo y el translate se recalcula en cada tick con la
    // fórmula de anclaje. NO se usan animaciones WPF (DoubleAnimation): sus relojes
    // internos disparan eventos de Freezable (Changed/Completed) que en caminos de
    // cancelación/reemplazo lanzan "Handler has not been registered with this event"
    // y matan el proceso. El timer no tiene handlers de Freezable que limpiar.)
    private readonly DispatcherTimer _zoomTimer;
    private bool _animating;
    private double _animStartScale;
    private double _animStartX;
    private double _animStartY;
    private Point _animAnchor;
    private bool _centerExactOnComplete;
    private double _animFrom;
    private double _animTo;
    private DateTime _animStartTime;

    public FastImageViewer()
    {
        var group = new TransformGroup();
        group.Children.Add(_scale);
        group.Children.Add(_translate);
        _image.RenderTransform = group;

        AddVisualChild(_image);

        _zoomTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(15)
        };
        _zoomTimer.Tick += OnZoomTick;

        // SizeChanged: si la ventana cambia de tamaño, la vista NUNCA debe quedar
        // desplazada hacia esquinas invisibles. En modo fit se re-fitea; con zoom
        // manual se conserva el zoom y se re-ancla la imagen dentro del área.
        SizeChanged += (_, _) =>
        {
            if (ActualWidth <= 0 || ActualHeight <= 0)
            {
                return;
            }

            CancelZoomAnimation();
            if (_inFitMode)
            {
                FitToWindow();
            }
            else
            {
                UpdatePosition();
            }
        };
    }

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(ImageSource),
        typeof(FastImageViewer),
        new PropertyMetadata(null, OnSourceChanged));

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _image;

    protected override Size MeasureOverride(Size availableSize)
    {
        _image.Measure(availableSize);
        return _image.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _image.Arrange(new Rect(finalSize));
        UpdatePosition();
        return finalSize;
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FastImageViewer viewer && e.NewValue is ImageSource src)
        {
            // Imagen nueva: reset DETERMINISTA de transformaciones ANTES del fit.
            // Si el control aún no está medido (visión Collapsed → ActualWidth=0),
            // FitToWindow difiere (scale <= 0 → early return), pero _inFitMode=true
            // garantiza que el SizeChanged posterior re-aplique el fit. Antes, si el
            // usuario había hecho zoom/pan manual (_inFitMode=false), la imagen nueva
            // quedaba a la escala anterior, cortada y anclada a la esquina sup-izq
            // (UpdatePosition con clamp a [Pad + Area - img, Pad]).
            viewer.CancelZoomAnimation();
            viewer.ResetTransforms();
            viewer._image.Source = src;
            viewer.Cursor = Cursors.Hand;
            viewer.FitToWindow();
        }
        else if (d is FastImageViewer empty)
        {
            // Estado vacío (spec especificacion-visor-estado-vacio.md §1.1/§2.1):
            // al pasar Source a null (borrado total, carga fallida) se libera el
            // bitmap del Image interno y se resetea TODO el estado de transformación.
            // Sin esto, la imagen anterior quedaba pintada aunque el archivo ya no
            // existiera (historial vacío + visor mostrando la última captura).
            empty.CancelZoomAnimation();
            empty.ResetTransforms();
            empty._image.Source = null;
            empty.Cursor = Cursors.Arrow;
            empty.NotifyZoom();
        }
    }

    /// <summary>
    /// Reset determinista de la transformación para una imagen nueva o el estado
    /// vacío: escala 1:1, sin desplazamiento y modo fit FORZADO (true). El fit real
    /// se re-evalúa en FitToWindow (o en SizeChanged si el control aún no está
    /// medido). Con _inFitMode=false el SizeChanged posterior haría UpdatePosition
    /// y la imagen quedaría cortada en la esquina sup-izq.
    /// </summary>
    private void ResetTransforms()
    {
        _scale.ScaleX = _scale.ScaleY = 1.0;
        _translate.X = _translate.Y = 0.0;
        _inFitMode = true;
    }

    // ============================================================
    // Tamaño natural de la imagen (lo que el Image con Stretch=None
    // dibuja realmente): píxeles convertidos a DIP con el DPI de la
    // fuente. Estricto en píxeles (PixelWidth/PixelHeight) y robusto
    // ante imágenes con DPI != 96 (p. ej. capturas a 144/192 DPI).
    // ============================================================

    private static Size GetNaturalRenderSize(ImageSource? source)
    {
        if (source is BitmapSource bmp)
        {
            double dpiX = bmp.DpiX > 0 ? bmp.DpiX : 96.0;
            double dpiY = bmp.DpiY > 0 ? bmp.DpiY : 96.0;
            return new Size(bmp.PixelWidth * 96.0 / dpiX, bmp.PixelHeight * 96.0 / dpiY);
        }

        if (source is { Width: > 0, Height: > 0 } src)
        {
            return new Size(src.Width, src.Height);
        }

        return Size.Empty;
    }

    private Size NaturalSize => GetNaturalRenderSize(_image.Source);

    /// <summary>Área efectiva del visor (excluye el Padding interno).</summary>
    private double AreaWidth => Math.Max(0, ActualWidth - Padding.Left - Padding.Right);

    private double AreaHeight => Math.Max(0, ActualHeight - Padding.Top - Padding.Bottom);

    private Point CenterPoint => new(ActualWidth / 2.0, ActualHeight / 2.0);

    private double ComputeFitScale()
    {
        Size natural = NaturalSize;
        if (natural.IsEmpty)
        {
            return 0;
        }

        double aw = AreaWidth;
        double ah = AreaHeight;
        if (aw <= 0 || ah <= 0)
        {
            return 0;
        }

        double scale = Math.Min(aw / natural.Width, ah / natural.Height);
        return double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0 ? 0 : scale;
    }

    // ============================================================
    // Posicionamiento (único punto de cálculo del TranslateTransform)
    // ============================================================

    /// <summary>
    /// ÚNICO punto de cálculo de posición (TranslateTransform). Invariante:
    /// - Eje en el que la imagen escalada NO llena el área efectiva: centrado EXACTO
    ///   dentro del área (desplazado por Padding), calculado desde cero — independiente
    ///   del valor previo o del target de arrastre. Es lo que evita que la imagen se
    ///   "escape" a la esquina superior izquierda con zoom menor al ajuste.
    /// - Eje en el que la imagen SÍ llena el área efectiva: clamp estricto del valor
    ///   (target explícito del arrastre o el valor actual) a [Pad + Area - img, Pad],
    ///   de modo que la imagen nunca toque los bordes del control (Padding como
    ///   margen, como Fotos de Windows).
    /// Se invoca tras el fit, el zoom, el pan y el redimensionado.
    /// </summary>
    private void UpdatePosition(double? targetX = null, double? targetY = null)
    {
        Size natural = NaturalSize;
        if (natural.IsEmpty || natural.Width <= 0 || natural.Height <= 0)
        {
            return;
        }

        double scaledW = natural.Width * _scale.ScaleX;
        double scaledH = natural.Height * _scale.ScaleY;

        double offsetX, offsetY;

        if (scaledW <= ActualWidth)
        {
            offsetX = (ActualWidth - scaledW) / 2.0;
        }
        else
        {
            offsetX = Math.Clamp(targetX ?? _translate.X,
                                ActualWidth - scaledW + Padding.Left,
                                Padding.Left);
        }

        if (scaledH <= ActualHeight)
        {
            offsetY = (ActualHeight - scaledH) / 2.0;
        }
        else
        {
            offsetY = Math.Clamp(targetY ?? _translate.Y,
                                ActualHeight - scaledH + Padding.Bottom,
                                Padding.Top);
        }

        _translate.X = offsetX;
        _translate.Y = offsetY;
    }

    /// <summary>
    /// Centra la imagen de forma EXACTA también cuando excede el área efectiva
    /// (parte media visible). Se usa en "1:1", donde la posición es determinista.
    /// </summary>
    private void CenterExact()
    {
        Size natural = NaturalSize;
        if (natural.IsEmpty)
        {
            return;
        }

        double aw = AreaWidth;
        double ah = AreaHeight;
        if (aw <= 0 || ah <= 0)
        {
            return;
        }

        double imgW = natural.Width * _scale.ScaleX;
        double imgH = natural.Height * _scale.ScaleY;
        _translate.X = Padding.Left + (aw - imgW) / 2.0;
        _translate.Y = Padding.Top + (ah - imgH) / 2.0;
    }

    private void NotifyZoom()
    {
        ZoomChanged?.Invoke(this, Math.Round(_scale.ScaleX * 100.0));
    }

    // ============================================================
    // Fit-to-window
    // ============================================================

    /// <summary>
    /// Ajuste inmediato (sin animación): usado al cargar una imagen nueva, al
    /// redimensionar la ventana o al cambiar el Padding. Centrado exacto en el
    /// área efectiva.
    /// </summary>
    public void FitToWindow()
    {
        CancelZoomAnimation();

        double scale = ComputeFitScale();
        if (scale <= 0)
        {
            return;
        }

        _scale.ScaleX = _scale.ScaleY = scale;
        _inFitMode = true;
        UpdatePosition();
        NotifyZoom();
    }

    /// <summary>
    /// Ajuste a la ventana ANIMADO (150 ms hacia el centro). Se invoca con doble
    /// clic, Escape o el botón "Ajustar" — mismo comportamiento que Fotos.
    /// </summary>
    public void ResetViewState()
    {
        double scale = ComputeFitScale();
        if (scale <= 0)
        {
            return;
        }

        AnimateScaleTo(scale, CenterPoint, setFitMode: true);
    }

    // ============================================================
    // Zoom (rueda y botones), con animación suave anclada
    // ============================================================

    /// <summary>Zoom hacia dentro (botón +), anclado al centro del visor.</summary>
    public void ZoomIn()
    {
        AnimateScaleTo(_scale.ScaleX * WheelFactor, CenterPoint, setFitMode: false);
    }

    /// <summary>Zoom hacia fuera (botón -), anclado al centro del visor.</summary>
    public void ZoomOut()
    {
        AnimateScaleTo(_scale.ScaleX / WheelFactor, CenterPoint, setFitMode: false);
    }

    /// <summary>Escala real 1:1 (100%), centrada en el área efectiva (parte media si excede).</summary>
    public void ActualSize()
    {
        AnimateScaleTo(1.0, CenterPoint, setFitMode: false, centerExactOnComplete: true);
    }

    /// <summary>
    /// Motor de zoom suave manual (DispatcherTimer, ~15 ms por tick): interpola la
    /// escala con easing cuadrático (EaseOut) y recalcula el translate en CADA tick
    /// con la fórmula de anclaje
    ///   t = anchor + (s/s0) · (t0 - anchor)
    /// que mantiene el punto de la imagen bajo <paramref name="anchor"/> fijo durante
    /// todo el escalado (smooth zoom al cursor). Los límites se aplican al terminar.
    /// Re-entrante: un nuevo zoom cancela el anterior sin salto (el valor base del
    /// transform es SIEMPRE el valor actual, no hay valores "animados" fantasma).
    /// </summary>
    private void AnimateScaleTo(double targetScale, Point anchor, bool setFitMode, bool centerExactOnComplete = false)
    {
        targetScale = Math.Clamp(targetScale, MinScale, MaxScale);
        CancelZoomAnimation();

        double s0 = _scale.ScaleX;
        _inFitMode = setFitMode;

        if (Math.Abs(targetScale - s0) < 0.001)
        {
            // Sin cambio real de escala: aplicar posición final directamente.
            if (centerExactOnComplete)
            {
                CenterExact();
            }
            else
            {
                UpdatePosition();
            }

            NotifyZoom();
            return;
        }

        _animStartScale = s0;
        _animStartX = _translate.X;
        _animStartY = _translate.Y;
        _animAnchor = anchor;
        _animFrom = s0;
        _animTo = targetScale;
        _centerExactOnComplete = centerExactOnComplete;
        _animStartTime = DateTime.UtcNow;
        _animating = true;

        _zoomTimer.Start();
    }

    private void OnZoomTick(object? sender, EventArgs e)
    {
        if (!_animating)
        {
            _zoomTimer.Stop();
            return;
        }

        double progress = (DateTime.UtcNow - _animStartTime).TotalMilliseconds / ZoomAnimationMs;
        if (progress >= 1.0)
        {
            progress = 1.0;
        }

        // EaseOut cuadrático: 1 - (1 - p)^2
        double eased = 1.0 - (1.0 - progress) * (1.0 - progress);
        double s = _animFrom + (_animTo - _animFrom) * eased;
        double k = s / _animStartScale;

        _scale.ScaleX = s;
        _scale.ScaleY = s;
        _translate.X = _animAnchor.X + k * (_animStartX - _animAnchor.X);
        _translate.Y = _animAnchor.Y + k * (_animStartY - _animAnchor.Y);

        if (progress >= 1.0)
        {
            OnZoomAnimationCompleted();
        }
    }

    private void OnZoomAnimationCompleted()
    {
        _zoomTimer.Stop();
        _animating = false;

        // Límites al finalizar: la imagen nunca queda fuera del área efectiva.
        if (_centerExactOnComplete)
        {
            CenterExact();
        }
        else
        {
            UpdatePosition();
        }

        NotifyZoom();
    }

    private void CancelZoomAnimation()
    {
        _zoomTimer.Stop();
        _animating = false;
    }

    // ============================================================
    // Input: rueda (zoom al cursor), arrastre (pan), doble clic (fit)
    // ============================================================

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        Point cursor = e.GetPosition(this);
        double factor = e.Delta > 0 ? WheelFactor : 1.0 / WheelFactor;
        AnimateScaleTo(_scale.ScaleX * factor, cursor, setFitMode: false);
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_image.Source == null) return;

        // Doble clic = ajustar a la ventana (comportamiento de Fotos de Windows)
        if (e.ClickCount == 2)
        {
            ResetViewState();
            e.Handled = true;
            return;
        }

        CaptureMouse();
        _dragStart = e.GetPosition(this);
        _dragStartPanX = _translate.X;
        _dragStartPanY = _translate.Y;
        Cursor = Cursors.Hand;
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_dragStart is Point start)
        {
            Point current = e.GetPosition(this);
            UpdatePosition(_dragStartPanX + (current.X - start.X), _dragStartPanY + (current.Y - start.Y));
        }
        else if (_image.Source != null)
        {
            Cursor = Cursors.Hand;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _dragStart = null;
        Cursor = _image.Source != null ? Cursors.Hand : Cursors.Arrow;
        ReleaseMouseCapture();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_dragStart is null)
        {
            Cursor = Cursors.Arrow;
        }
    }
}