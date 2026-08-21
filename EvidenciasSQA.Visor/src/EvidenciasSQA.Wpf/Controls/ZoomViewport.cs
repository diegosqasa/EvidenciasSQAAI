using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace EvidenciasSQA.Wpf.Controls
{
    /// <summary>
    /// Estado global del visor (máquina de estados EXPLÍCITA, nunca un caso "por
    /// defecto" no modelado). Transiciones: Empty → HasCapture (ShowImage) y
    /// HasCapture → Empty (ReleaseImage). Ver especificacion-visor-estado-vacio.md.
    /// </summary>
    public enum ViewerState
    {
        /// <summary>Sin imagen cargada: placeholder centrado por layout, todo zoom inactivo.</summary>
        Empty,
        /// <summary>Con captura: el zoom binario (Fit ↔ Natural) está disponible.</summary>
        HasCapture
    }

    /// <summary>
    /// Modo de zoom binario del visor (equivalente de la clase .zoom-active del DOM).
    /// NO es zoom continuo: es un toggle entre "ajustar a pantalla" y "tamaño natural
    /// 1:1" (ver viewer-container-zoom-greenshot.md §1.2).
    /// </summary>
    public enum ZoomMode
    {
        /// <summary>Imagen completa visible, centrada por el layout (object-fit: contain).</summary>
        Fit,
        /// <summary>Tamaño natural (1 px imagen = 1 px físico), desplazable por pan/scroll.</summary>
        Natural
    }

    /// <summary>
    /// Contenedor de imagen del visor: zoom binario (clic = Fit ↔ Natural), pan por
    /// arrastre con umbral de 5 px y estado vacío idempotente.
    ///
    /// ARQUITECTURA (adaptación WPF del contrato Greenshot §2.2):
    ///   ScrollViewer (equivale al Panel.AutoScroll de WinForms) → Grid stretch
    ///   → Image. El CENTRADO es responsabilidad del layout (Grid + Alignment=Center),
    ///   NUNCA de cálculos manuales: en Empty y en Fit no existe transformación que
    ///   re-calcular al redimensionar la ventana (la "escala fantasma" del doc §4.1
    ///   es imposible por diseño: no hay coordenadas de pan persistidas contra un
    ///   viewport nuevo).
    ///
    /// DPI (§6.2): WPF es DPI-aware por proceso; con Stretch=None la imagen se dibuja
    /// a su tamaño natural en DIP (px / (Dpi/96)), es decir 1 px de imagen = 1 px
    /// físico en pantalla a cualquier DPI (96/120/144). El padding usa DIPs.
    ///
    /// MEMORIA (§6.1/§6.3): en WPF el ImageSource no es IDisposable (WIC se libera
    /// por GC); el equivalente anti-fuga es SOLTAR la referencia (Source = null) en
    /// ReleaseImage y NO retener el FileStream: ShowImage(string) decodifica con
    /// BitmapCacheOption.OnLoad + Freeze dentro de un using, materializando los píxeles
    /// y cerrando el stream (sin file-lock, el bug clásico de Image.FromFile). Para
    /// capturas extremas (&gt;5000×30000 px) el host debe cargar un thumbnail 2× de
    /// pantalla en Fit y el original al entrar en Natural (estrategia documentada en
    /// viewer-container-zoom-greenshot.md §6.3).
    /// </summary>
    public sealed class ZoomViewport : UserControl
    {
        /// <summary>Umbral de arrastre que distingue clic de pan (doc §1.2).</summary>
        private const double PanDragThreshold = 5.0;

        /// <summary>Margen del modo Fit (doc §2.1: padding 30px; responsive menor es del host).</summary>
        private const double FitPadding = 30.0;

        /// <summary>Margen del modo Natural (doc §2.1: padding 40px).</summary>
        private const double NaturalPadding = 40.0;

        /// <summary>
        /// Radio de las esquinas redondeadas de la captura (réplica del
        /// img#screenshot del Electron: border-radius 8px + sombra suave).
        /// </summary>
        private const double ImageCornerRadius = 8.0;

        private readonly ScrollViewer _scrollHost;
        private readonly Grid _layoutGrid;
        private readonly Image _imageHost;
        private readonly Border _imageBorder;
        private readonly RectangleGeometry _imageClip;
        private readonly Grid _emptyState;
        private readonly DropShadowEffect _shadow;

        private ViewerState _state = ViewerState.Empty;
        private ZoomMode _mode = ZoomMode.Fit;
        private bool _isBusy;

        // --- Estado del pan (solo relevante en Natural) ---
        private bool _mouseDown;
        private bool _wasDragging;
        private Point _dragStart;

        // --- PreserveZoomState / RestoreZoomState (navegación ◀▶, doc §3.2) ---
        private ZoomMode _preservedMode = ZoomMode.Fit;
        private Vector _preservedScrollOffset;

        /// <summary>Sustituye updateZoomInfo → StatusStrip/InfoBar del host.</summary>
        public event Action<ZoomMode>? ZoomModeChanged;

        /// <summary>Se dispara al liberar la imagen (ventana oculta al tray, doc §3.3).</summary>
        public event Action? ImageReleased;

        /// <summary>
        /// Notifica el porcentaje de zoom (100 = tamaño natural). En Fit reporta el
        /// porcentaje de ajuste calculado (min(w/image, h/image)); en Natural 100.
        /// </summary>
        public event EventHandler<double>? ZoomChanged;

        /// <summary>Imagen del visor (compatibilidad de binding con el host: null → Empty).</summary>
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(ImageSource),
            typeof(ZoomViewport),
            new PropertyMetadata(null, OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ZoomViewport)d).ShowImage(e.NewValue as ImageSource);
        }

        public ImageSource? Source
        {
            get => (ImageSource?)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public ZoomMode Mode => _mode;

        /// <summary>Guarda crítica nº 1 del estado vacío: todo zoom/pan hace early-return si es false.</summary>
        public bool HasImage => _state == ViewerState.HasCapture;

        public ViewerState State => _state;

        /// <summary>Carga en curso: el host lo usa para ignorar capturas entrantes (doc §3.2).</summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                _isBusy = value;
                Cursor = value ? Cursors.Wait : (HasImage ? ModeCursor : Cursors.Arrow);
            }
        }

        private Cursor ModeCursor => _mode == ZoomMode.Natural ? Cursors.SizeAll : Cursors.Hand;

        public ZoomViewport()
        {
            _shadow = new DropShadowEffect { BlurRadius = 15, ShadowDepth = 2, Opacity = 0.08, Color = Colors.Black };

            _imageHost = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // Contenedor con esquinas redondeadas (réplica del img#screenshot del
            // Electron: border-radius 8px + sombra). El clip se recalcula al cambiar
            // el tamaño del borde (el radio solo debe afectar al rectángulo de la
            // imagen renderizada, nunca al viewport completo).
            _imageClip = new RectangleGeometry { RadiusX = ImageCornerRadius, RadiusY = ImageCornerRadius };
            _imageBorder = new Border
            {
                CornerRadius = new CornerRadius(ImageCornerRadius),
                Clip = _imageClip,
                Background = Brushes.Transparent,
                Effect = _shadow,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = _imageHost
            };
            _imageBorder.SizeChanged += (_, _) => UpdateImageClip();

            // Grid stretch dentro del ScrollViewer: es el patrón estándar WPF de
            // "contenido centrado con scroll opcional". Si la imagen no llena, el Grid
            // ocupa el viewport y el Border queda centrado por Alignment (layout puro).
            // Si la excede (Natural), el Grid crece y aparecen las scrollbars.
            _layoutGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            _layoutGrid.Children.Add(_imageBorder);

            _scrollHost = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _layoutGrid,
                Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF6, 0xF9))
            };

            // Estado vacío: centrado EXCLUSIVAMENTE por el layout (Alignment=Center),
            // equivalente al #empty-state flex de Electron; el resize es gratis (O(1)).
            _emptyState = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Visible
            };
            _emptyState.Children.Add(new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Sin evidencias",
                        FontSize = 22,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37)),
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = "Captura una pantalla para comenzar",
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x63)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 6, 0, 0)
                    }
                }
            });

            var root = new Grid();
            root.Children.Add(_scrollHost);
            root.Children.Add(_emptyState);
            Content = root;

            // Estados de ratón para pan + toggle (los modales del host capturan su
            // propio input; este control solo recibe eventos con la ventana activa).
            // OJO (fix Fase 18): el ScrollViewer interno marca MouseDown y MouseMove
            // como Handled (clase ScrollContentPresenter/ScrollViewer), y los eventos
            // específicos MouseLeftButtonDown/MouseMove son DIRECT (no burbujean al
            // ancestro) → los handlers con += NUNCA se disparaban (pan muerto, clic
            // actuaba como toggle). Fix: AddHandler sobre los eventos GENERICOS
            // burbujeantes con handledEventsToo=true (documentado en Fase 18 del README).
            AddHandler(Mouse.MouseDownEvent, (MouseButtonEventHandler)OnViewportMouseLeftButtonDown, true);
            AddHandler(Mouse.MouseMoveEvent, (MouseEventHandler)OnViewportMouseMove, true);
            MouseLeftButtonUp += OnViewportMouseLeftButtonUp;

            // En Fit el % de ajuste y el tamaño del borde redondeado dependen del
            // tamaño del viewport (recalcular al redimensionar).
            SizeChanged += (_, _) =>
            {
                if (HasImage && _mode == ZoomMode.Fit)
                {
                    UpdateImageSize();
                    NotifyZoom();
                }
            };

            Cursor = Cursors.Arrow;
        }

        // ============================================================
        // API pública (contrato §2.2)
        // ============================================================

        /// <summary>
        /// Muestra una imagen ya decodificada por el host, siempre en Fit y con el
        /// zoom/scroll reseteados (doc §3.2). Idempotente: si se llama dos veces con
        /// la misma imagen, el segundo call solo re-aplica Fit (sin errores ni estado
        /// inconsistente). null → ReleaseImage (transición a Empty).
        /// </summary>
        public void ShowImage(ImageSource? source)
        {
            if (source == null)
            {
                ReleaseImage();
                return;
            }

            _imageHost.Source = source;
            _state = ViewerState.HasCapture;
            _mode = ZoomMode.Fit;          // carga de captura nueva SIEMPRE resetea a Fit
            ApplyMode();                    // también centra por layout y resetea scroll
            _emptyState.Visibility = Visibility.Collapsed;
            ZoomModeChanged?.Invoke(_mode);
            NotifyZoom();
        }

        /// <summary>
        /// Carga desde disco sin bloquear el archivo: FileStream con FileShare.ReadWrite
        /// + BitmapCacheOption.OnLoad + Freeze dentro de un using (los píxeles quedan
        /// materializados y el stream se cierra → sin fuga de handles ni file-lock,
        /// el equivalente seguro de Image.FromFile del doc §6.1).
        /// </summary>
        public void ShowImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                ReleaseImage();
                return;
            }

            IsBusy = true;
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var decoder = BitmapDecoder.Create(
                    fs,
                    BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile,
                    BitmapCacheOption.OnLoad);
                ImageSource frame = decoder.Frames[0];
                frame.Freeze();
                ShowImage(frame);
            }
            catch (Exception)
            {
                // Decodificación fallida (archivo corrupto/bloqueado): ir a Empty de
                // forma transaccional, sin dejar la imagen anterior a medias.
                ReleaseImage();
                throw;
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Clic: toggle Fit ↔ Natural (doc §3.1). El clic tras un arrastre NO alterna
        /// (lo decide el flag _wasDragging, ver OnMouseLeftButtonUp). Early-return en
        /// Empty: sin imagen no existe toggle (guarda nº 2 del estado vacío).
        /// </summary>
        public void ToggleZoom()
        {
            if (!HasImage)
            {
                return;
            }

            _mode = _mode == ZoomMode.Fit ? ZoomMode.Natural : ZoomMode.Fit;
            ApplyMode();
            ZoomModeChanged?.Invoke(_mode);
        }

        /// <summary>Vuelve a Fit (Escape, carga de captura nueva). Idempotente.</summary>
        public void ResetZoom()
        {
            if (!HasImage || _mode == ZoomMode.Fit)
            {
                return;
            }

            _mode = ZoomMode.Fit;
            ApplyMode();
            ZoomModeChanged?.Invoke(_mode);
        }

        /// <summary>Guarda modo + offset de scroll para la navegación ◀▶ (doc §3.2). No-op en Empty.</summary>
        public void PreserveZoomState()
        {
            if (!HasImage)
            {
                return;
            }

            _preservedMode = _mode;
            _preservedScrollOffset = new Vector(_scrollHost.HorizontalOffset, _scrollHost.VerticalOffset);
        }

        /// <summary>
        /// Re-aplica el estado preservado sobre la captura siguiente. Solo restaura si
        /// estaba en Natural (en Fit la carga normal ya hace Fit). WPF clamp automático:
        /// si la nueva imagen es más pequeña, el offset fuera de rango se recorta sin
        /// excepción (a diferencia de AutoScrollPosition de WinForms, aquí nunca hay
        /// offsets negativos — no requiere normalización).
        /// </summary>
        public void RestoreZoomState()
        {
            if (!HasImage || _preservedMode != ZoomMode.Natural)
            {
                return;
            }

            _mode = ZoomMode.Natural;
            ApplyMode();
            _scrollHost.ScrollToHorizontalOffset(_preservedScrollOffset.X);
            _scrollHost.ScrollToVerticalOffset(_preservedScrollOffset.Y);
            ZoomModeChanged?.Invoke(_mode);
        }

        /// <summary>
        /// Libera la imagen mostrada y transiciona a Empty (ventana oculta al tray,
        /// doc §3.3). Idempotente: llamarlo dos veces seguidas (o tras ShowImage(null))
        /// no arroja error ni cambia nada (guarda nº 3 del estado vacío). En WPF
        /// soltar Source es suficiente: WIC libera el bitmap por GC.
        /// </summary>
        public void ReleaseImage()
        {
            if (_state == ViewerState.Empty && _imageHost.Source == null)
            {
                return; // idempotencia: doble llamada no-op
            }

            _mouseDown = false;
            _wasDragging = false;
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }

            _imageHost.Source = null;       // libera el bitmap decodificado
            _mode = ZoomMode.Fit;
            _state = ViewerState.Empty;
            _emptyState.Visibility = Visibility.Visible;
            Cursor = Cursors.Arrow;
            ImageReleased?.Invoke();
        }

        // ============================================================
        // Pan (solo en Natural) + clic toggle — umbral de 5 px
        // ============================================================

        private void OnViewportMouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        {
            if (!HasImage)
            {
                return;
            }

            // El umbral se evalúa en MouseMove (no aquí): el doc §6.5 advierte que
            // decidir en MouseDown rompe el toggle para arrastres cortos.
            _mouseDown = true;
            _wasDragging = false;
            _dragStart = e.GetPosition(this);
            CaptureMouse();
            e.Handled = true;
        }

        private void OnViewportMouseMove(object? sender, MouseEventArgs e)
        {
            if (!_mouseDown || !HasImage || _mode != ZoomMode.Natural)
            {
                return;
            }

            Point pos = e.GetPosition(this);
            double dx = pos.X - _dragStart.X;
            double dy = pos.Y - _dragStart.Y;

            if (!_wasDragging && (Math.Abs(dx) > PanDragThreshold || Math.Abs(dy) > PanDragThreshold))
            {
                _wasDragging = true;   // un arrastre legítimo comenzó
                Cursor = Cursors.SizeAll;
            }

            if (_wasDragging)
            {
                // Pan: el scroll sigue al ratón (dx/dy invertidos respecto al drag).
                _scrollHost.ScrollToHorizontalOffset(_scrollHost.HorizontalOffset - dx);
                _scrollHost.ScrollToVerticalOffset(_scrollHost.VerticalOffset - dy);
                _dragStart = pos;      // delta incremental, sin acumular error
            }

            e.Handled = true;
        }

        private void OnViewportMouseLeftButtonUp(object? sender, MouseButtonEventArgs e)
        {
            bool wasDragging = _wasDragging;
            _mouseDown = false;
            _wasDragging = false;

            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }

            // Clic limpio (sin arrastre previo) = toggle. El clic tras arrastre se
            // ignora (doc §1.2: isPanningWasActive).
            if (!wasDragging && HasImage)
            {
                ToggleZoom();
            }

            e.Handled = true;
        }

        // ============================================================
        // Internos
        // ============================================================

        private void ApplyMode()
        {
            if (!HasImage)
            {
                return; // guarda: ningún cálculo de transformación en Empty (doc §4.1)
            }

            bool natural = _mode == ZoomMode.Natural;

            _imageHost.Stretch = natural ? Stretch.None : Stretch.Uniform;
            _imageBorder.Margin = new Thickness(natural ? NaturalPadding : FitPadding);

            _scrollHost.HorizontalScrollBarVisibility = natural ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
            _scrollHost.VerticalScrollBarVisibility = natural ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;

            // El borde (con esquinas redondeadas) debe abrazar el rectángulo RENDERIZADO
            // de la imagen: en Natural el tamaño natural en DIP (px / DPI * 96), en Fit
            // el ajuste a pantalla (misma escala que NotifyZoom). El clip se actualiza
            // vía SizeChanged del borde.
            UpdateImageSize();

            if (natural)
            {
                _scrollHost.ScrollToLeftEnd();
                _scrollHost.ScrollToTop();   // empezar arriba-izquierda (doc §1.2)
            }

            Cursor = ModeCursor;
            NotifyZoom();
        }

        /// <summary>
        /// Tamaño natural de la imagen en DIP (1 px de imagen = 96/Dpi DIP; a DPI
        /// estándar, píxel a píxel). Estricto en píxeles (PixelWidth/Height) y robusto
        /// ante capturas con DPI != 96 (p. ej. 144/192).
        /// </summary>
        private Size GetNaturalImageSize()
        {
            if (_imageHost.Source is BitmapSource bmp)
            {
                double dpiX = bmp.DpiX > 0 ? bmp.DpiX : 96.0;
                double dpiY = bmp.DpiY > 0 ? bmp.DpiY : 96.0;
                return new Size(bmp.PixelWidth * 96.0 / dpiX, bmp.PixelHeight * 96.0 / dpiY);
            }

            return Size.Empty;
        }

        /// <summary>
        /// Dimensiona el borde redondeado al rectángulo de la imagen renderizada y
        /// refresca su clip (las esquinas del borde siguen la imagen, no el viewport).
        /// En Fit la escala es la misma que NotifyZoom; en Natural el tamaño natural.
        /// </summary>
        private void UpdateImageSize()
        {
            if (_imageHost.Source == null)
            {
                return;
            }

            Size natural = GetNaturalImageSize();
            if (natural.IsEmpty || natural.Width <= 0 || natural.Height <= 0)
            {
                return;
            }

            double borderWidth, borderHeight;
            if (_mode == ZoomMode.Natural)
            {
                borderWidth = natural.Width;
                borderHeight = natural.Height;
            }
            else
            {
                double availW = Math.Max(1.0, ActualWidth - 2 * FitPadding);
                double availH = Math.Max(1.0, ActualHeight - 2 * FitPadding);
                double scale = Math.Min(availW / natural.Width, availH / natural.Height);
                if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
                {
                    scale = 1.0;
                }

                borderWidth = Math.Max(1.0, natural.Width * scale);
                borderHeight = Math.Max(1.0, natural.Height * scale);
            }

            _imageBorder.Width = borderWidth;
            _imageBorder.Height = borderHeight;
            UpdateImageClip();
        }

        /// <summary>
        /// Refresca el RectangleGeometry del clip del borde al tamaño actual del borde
        /// (el radio ya está fijado; solo cambia el rect de recorte). Se invoca al
        /// redimensionar el borde (SizeChanged) o al dimensionarlo (UpdateImageSize).
        /// </summary>
        private void UpdateImageClip()
        {
            double w = _imageBorder.ActualWidth;
            double h = _imageBorder.ActualHeight;
            if (w > 0 && h > 0)
            {
                _imageClip.Rect = new Rect(0, 0, w, h);
            }
        }

        /// <summary>
        /// Porcentaje de zoom para la barra de estado del host (doc §7):
        /// Natural → 100 (tamaño real); Fit → % de ajuste calculado por layout
        /// (min(ancho/activo, alto/activo) sobre el área efectiva menos padding).
        /// </summary>
        private void NotifyZoom()
        {
            double pct = 100;
            if (HasImage && _mode == ZoomMode.Fit && _imageHost.Source is BitmapSource bmp)
            {
                double w = Math.Max(1.0, ActualWidth - 2 * FitPadding);
                double h = Math.Max(1.0, ActualHeight - 2 * FitPadding);
                double imgW = Math.Max(1.0, bmp.PixelWidth);
                double imgH = Math.Max(1.0, bmp.PixelHeight);
                pct = Math.Min(w / imgW, h / imgH) * 100.0;
            }

            ZoomChanged?.Invoke(this, pct);
        }
    }
}