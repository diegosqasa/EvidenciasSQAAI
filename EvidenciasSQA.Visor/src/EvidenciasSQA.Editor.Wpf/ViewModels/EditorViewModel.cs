using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using EvidenciasSQA.Core.Drawing;
using EvidenciasSQA.Core.Export;
using EvidenciasSQA.Core.Imaging;
using EvidenciasSQA.Core.Interfaces;
using EvidenciasSQA.Core.Model;
using EvidenciasSQA.Core.Mvvm;
using EvidenciasSQA.Core.Services;
using Microsoft.Win32;
using Point = System.Windows.Point;

namespace EvidenciasSQA.Editor.Wpf.ViewModels;

/// <summary>
/// Opci�n de color del toolbar (swatch): nombre legible + color GDI + brush WPF.
/// </summary>
public sealed record ColorOption(string Name, Color Color)
{
    public System.Windows.Media.SolidColorBrush Brush =>
        new(System.Windows.Media.Color.FromArgb(Color.A, Color.R, Color.G, Color.B));
}

/// <summary>
/// Herramientas de anotaci�n activables desde la UI. Replica las herramientas
/// del m�dulo web (Fabric.js): F=Flecha, C=Cuadro, T=Texto, H=Resaltar,
/// B=Blur, R=Recortar, V/S=Seleccionar.
/// </summary>
public enum DrawMode
{
    None,
    Rectangle,
    Arrow,
    Text,
    Highlight,
    Blur,
    Crop
}

/// <summary>
/// ViewModel del M�DULO EDITOR (MVVM). El Visor delega aqu� toda edici�n:
/// este m�dulo es el �nico que conoce anotaciones, pinceles y destinos.
///
/// Replica el estado del m�dulo web (Fabric.js):
///  editor ? { zoom (0.1�5.0), tool, color, strokeWidth, fontSize,
///             history[] (10), future[], isDrawing, activeObj, startX, startY }
///  crop   ? { rect: {x,y,w,h}, isDragging, activeHandle, dragStart }
///
/// Papel en la arquitectura (3 capas desacopladas):
///  1. CAPTURA   ? CaptureSource (clipboard/archivo) produce un Bitmap.
///  2. EDITOR    ? SurfaceDocument (fondo + DrawableObject) y EditorCanvas (vista).
///  3. DESTINOS  ? IDestination (FileDestination, ClipboardDestination) exportan.
///
/// Este ViewModel solo coordina: no conoce p�xeles, ni pinceles, ni di�logos de
/// guardado; delega cada responsabilidad a su capa. La vista (EditorWindow) solo
/// le entrega coordenadas de rat�n y le pide repintar cuando SurfaceChanged.
/// </summary>
public sealed class EditorViewModel : ObservableObject, IDisposable
{
    /// <summary>M�ximo de snapshots de undo (replicado de MAX_HISTORY del m�dulo web).</summary>
    public const int MaxHistory = 10;

    /// <summary>Zoom m�nimo/m�ximo (replicado del clamp 0.1�5.0 de applyZoom).</summary>
    public const double MinZoom = 0.1;
    public const double MaxZoom = 5.0;

    private readonly List<IDestination> _destinations;
    private readonly List<string> _history = new(); // snapshots XML (max 10)
    private readonly List<string> _future = new();  // snapshots redo
    private string? _lastSnapshot;

    private SurfaceDocument? _surface;
    private CaptureDetails? _captureDetails;

    // Estado de interacci�n del rat�n (dibujo / movimiento).
    private DrawableObject? _drawingElement;
    private Point _drawingStart;
    private DrawableObject? _movingElement;
    private Point _moveOffset;

    private DrawMode _currentTool;
    private DrawableObject? _selectedElement;
    private string _statusText = "Sin imagen cargada";

    // Estilo activo (color / grosor / fuente).
    private Color _currentColor = Color.Red;
    private int _strokeWidth = 2;
    private int _fontSize = 20;

    // Zoom (0.1�5.0).
    private double _zoom = 1.0;

    // Opciones de estilo del toolbar (paleta / grosor / fuente).
    private static readonly Color[] Palette =
    [
        Color.Red, Color.FromArgb(0xFF, 0x6B, 0x00), Color.Yellow, Color.Lime,
        Color.FromArgb(0x00, 0x78, 0xD7), Color.Blue, Color.White, Color.Black,
        Color.Magenta, Color.Cyan
    ];

    private static readonly string[] PaletteNames =
    [
        "Rojo", "Naranja", "Amarillo", "Verde", "Azul claro", "Azul", "Blanco", "Negro",
        "Magenta", "Cian"
    ];

    public IReadOnlyList<ColorOption> ColorOptions { get; } =
        Palette.Select((c, i) => new ColorOption(PaletteNames[i], c)).ToList();

    public int[] StrokeWidthOptions { get; } = [1, 2, 3, 4, 5, 6, 8, 10];

    public int[] FontSizeOptions { get; } = [12, 14, 16, 18, 20, 24, 28, 32, 36, 40, 48];

    private ColorOption _selectedColorOption;

    /// <summary>Opci�n de color seleccionada en el toolbar (bind del ComboBox).</summary>
    public ColorOption SelectedColorOption
    {
        get => _selectedColorOption;
        set
        {
            if (value != null && SetProperty(ref _selectedColorOption, value))
            {
                CurrentColor = value.Color;
            }
        }
    }

    // Crop: rect en coordenadas de imagen + estado de arrastre de handles.
    private Rectangle _cropRect;
    private bool _cropActive;
    private string? _cropDragType; // "move" | "tl" | "tr" | "bl" | "br"
    private Rectangle _cropStartRect;
    private Point _cropStartMouse;

    // Edici�n de texto en l�nea (TextBox overlay en la vista).
    private TextDrawable? _editingElement;

    /// <summary>La vista lo muestra y edita: rect (imagen) del TextDrawable activo.</summary>
    public event EventHandler<Rectangle>? TextEditRequested;

    private readonly IToastService _toast;

    public EditorViewModel(IToastService? toast = null)
    {
        _toast = toast ?? ToastService.Instance;
        _selectedColorOption = ColorOptions.First(o => o.Color.ToArgb() == _currentColor.ToArgb());

        // La capa de destinos es una lista inyectable: agregar un nuevo destino
        // (impresora, subida web...) es solo registrar una clase aqu�.
        _destinations =
        [
            new FileDestination(OutputFormat.png),
            new FileDestination(OutputFormat.evidenciasSqa),
            new ClipboardDestination()
        ];

        LoadImageCommand = new RelayCommand(LoadImage);
        LoadFromClipboardCommand = new RelayCommand(LoadFromClipboard, () => Clipboard.ContainsImage());
        SavePngCommand = new RelayCommand(() => ExportTo(destinationIndex: 0), CanExport);
        SaveEvidenciasSqaCommand = new RelayCommand(() => ExportTo(destinationIndex: 1), CanExport);
        CopyCommand = new RelayCommand(() => ExportTo(destinationIndex: 2), CanExport);
        UndoCommand = new RelayCommand(Undo, () => _history.Count > 1);
        RedoCommand = new RelayCommand(Redo, () => _future.Count > 0);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected, () => _selectedElement != null);
        DuplicateSelectedCommand = new RelayCommand(DuplicateSelected, () => _selectedElement != null);
        ClearSelectionCommand = new RelayCommand(ClearSelection, () => _selectedElement != null);
        SelectToolCommand = new RelayCommand(() => CurrentTool = DrawMode.None);
        RectangleToolCommand = new RelayCommand(() => CurrentTool = DrawMode.Rectangle);
        ArrowToolCommand = new RelayCommand(() => CurrentTool = DrawMode.Arrow);
        TextToolCommand = new RelayCommand(() => CurrentTool = DrawMode.Text);
        HighlightToolCommand = new RelayCommand(() => CurrentTool = DrawMode.Highlight);
        BlurToolCommand = new RelayCommand(() => CurrentTool = DrawMode.Blur);
        CropToolCommand = new RelayCommand(() => CurrentTool = DrawMode.Crop);
        ZoomInCommand = new RelayCommand(() => ZoomBy(0.1));
        ZoomOutCommand = new RelayCommand(() => ZoomBy(-0.1));
        ZoomResetCommand = new RelayCommand(() => Zoom = 1.0);
        ZoomFitCommand = new RelayCommand(FitZoom);
    }

    /// <summary>Documento en edici�n (modelo). null hasta cargar una imagen.</summary>
    public SurfaceDocument? Surface => _surface;

    /// <summary>Ruta del archivo en edici�n (guarda in-place sobre �l). null = imagen en memoria.</summary>
    public string? FilePath => _captureDetails?.Filename;

    private bool _isDirty;

    /// <summary>True si hubo cambios (dibujar/editar/recortar/deshacer) desde la carga.</summary>
    public bool IsDirty => _isDirty;

    /// <summary>Herramienta activa (afecta al cursor y a los clics del canvas).</summary>
    public DrawMode CurrentTool
    {
        get => _currentTool;
        set
        {
            if (SetProperty(ref _currentTool, value))
            {
                Cursor = value == DrawMode.None ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.Cross;
                OnPropertyChanged(nameof(IsSelectionMode));
                OnPropertyChanged(nameof(IsDrawingMode));
            }
        }
    }

    /// <summary>Modo selección: herramienta "Seleccionar (V)" activa (DrawMode.None).</summary>
    public bool IsSelectionMode => _currentTool == DrawMode.None;

    /// <summary>Modo dibujo: cualquier herramienta de trazado activa.</summary>
    public bool IsDrawingMode => _currentTool != DrawMode.None;

    private Cursor _cursor = System.Windows.Input.Cursors.Arrow;

    public Cursor Cursor
    {
        get => _cursor;
        private set => SetProperty(ref _cursor, value);
    }

    /// <summary>Zoom actual (0.1�5.0). El canvas escala su render con este valor.</summary>
    public double Zoom
    {
        get => _zoom;
        set
        {
            double clamped = Math.Clamp(value, MinZoom, MaxZoom);
            if (SetProperty(ref _zoom, clamped))
            {
                OnPropertyChanged(nameof(ZoomPercentText));
            }
        }
    }

    /// <summary>Etiqueta del zoom: "{round(zoom*100)}%".</summary>
    public string ZoomPercentText => $"{Math.Round(_zoom * 100)}%";

    /// <summary>Rect�ngulo de recorte en coordenadas de imagen (bind del overlay del canvas).</summary>
    public Rectangle CropRect
    {
        get => _cropRect;
        private set => SetProperty(ref _cropRect, value);
    }

    /// <summary>True mientras el overlay de recorte est� visible.</summary>
    public bool CropActive
    {
        get => _cropActive;
        private set => SetProperty(ref _cropActive, value);
    }

    public Color CurrentColor
    {
        get => _currentColor;
        set
        {
            if (SetProperty(ref _currentColor, value))
            {
                // Mantiene el ComboBox del toolbar sincronizado (sin recursi�n).
                ColorOption? matching = ColorOptions.FirstOrDefault(o => o.Color.ToArgb() == value.ToArgb());
                if (matching != null && !ReferenceEquals(_selectedColorOption, matching))
                {
                    _selectedColorOption = matching;
                    OnPropertyChanged(nameof(SelectedColorOption));
                }

                ApplyColorToSelection();
            }
        }
    }

    public int StrokeWidth
    {
        get => _strokeWidth;
        set
        {
            if (SetProperty(ref _strokeWidth, value))
            {
                ApplyStrokeWidthToSelection();
            }
        }
    }

    public int FontSize
    {
        get => _fontSize;
        set
        {
            if (SetProperty(ref _fontSize, value))
            {
                ApplyFontSizeToSelection();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public ICommand LoadImageCommand { get; }
    public ICommand LoadFromClipboardCommand { get; }
    public ICommand SavePngCommand { get; }
    public ICommand SaveEvidenciasSqaCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand DuplicateSelectedCommand { get; }
    /// <summary>"Cancelar" del modo selección: deselecciona todos los objetos.</summary>
    public ICommand ClearSelectionCommand { get; }
    public ICommand SelectToolCommand { get; }
    public ICommand RectangleToolCommand { get; }
    public ICommand ArrowToolCommand { get; }
    public ICommand TextToolCommand { get; }
    public ICommand HighlightToolCommand { get; }
    public ICommand BlurToolCommand { get; }
    public ICommand CropToolCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand ZoomResetCommand { get; }
    public ICommand ZoomFitCommand { get; }

    /// <summary>La vista se suscribe para invalidar el canvas.</summary>
    public event EventHandler? SurfaceChanged;

    // ============================================================
    // Comandos
    // ============================================================

    public void LoadImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Abrir captura",
            Filter = "Im�genes (*.png;*.jpg;*.jpeg;*.bmp;*.evidenciasSqa)|*.png;*.jpg;*.jpeg;*.bmp;*.evidenciasSqa|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadFile(dialog.FileName);
        }
    }

    /// <summary>
    /// Carga un archivo para edici�n. Es el punto de entrada del Visor v�a
    /// "--file &lt;ruta&gt;": el nombre queda fijado para que "Guardar" escriba
    /// en el mismo archivo y el Visor pueda recargar el resultado.
    /// </summary>
    public void LoadFile(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            return;
        }

        _captureDetails = new CaptureDetails { Filename = fullPath };

        SurfaceDocument loaded = ImageIO.Load(fullPath);
        ReplaceSurface(loaded);
    }

    public void LoadFromClipboard()
    {
        if (!Clipboard.ContainsImage())
        {
            return;
        }

        _captureDetails = new CaptureDetails { CaptureDate = DateTime.Now };
        BitmapSource source = Clipboard.GetImage()!;
        ReplaceSurface(WicHelper.ToBitmap(source));
    }

    /// <summary>
    /// Carga una imagen YA decodificada (modo embebido: el visor entrega su
    /// BitmapSource actual sin tocar disco, preservando el contexto de imagen).
    /// Con <paramref name="filePath"/> fijado, "Guardar (in-place)" escribe sobre
    /// el archivo original del visor; el visor recarga el resultado al volver.
    /// </summary>
    public void LoadFromBitmapSource(BitmapSource source, string? filePath = null)
    {
        if (source == null)
        {
            return;
        }

        _captureDetails = filePath != null
            ? new CaptureDetails { Filename = filePath }
            : new CaptureDetails { CaptureDate = DateTime.Now };
        ReplaceSurface(WicHelper.ToBitmap(source));
    }

    // ============================================================
    // Zoom
    // ============================================================

    /// <summary>Ajusta el zoom en un delta (�0.1 con los botones).</summary>
    public void ZoomBy(double delta) => Zoom = _zoom + delta;

    /// <summary>Zoom con rueda: factor 0.999^deltaY (replicado del mouse:wheel web).</summary>
    public void ZoomByWheel(double deltaY) => Zoom = _zoom * Math.Pow(0.999, deltaY);

    /// <summary>
    /// Zoom fit-to-area: fitFactor = min(areaW/w, areaH/h, 1.0);
    /// zoom inicial = max(0.1, fitFactor) (replicado del initEditor web).
    /// </summary>
    public void FitZoom(double viewportWidth, double viewportHeight)
    {
        if (_surface?.BackgroundBitmap == null || viewportWidth <= 0 || viewportHeight <= 0)
        {
            return;
        }

        double fitFactor = Math.Min(
            viewportWidth / _surface.ImageWidth,
            Math.Min(viewportHeight / _surface.ImageHeight, 1.0));
        Zoom = Math.Max(0.1, fitFactor);
    }

    private void FitZoom()
    {
        if (_surface?.BackgroundBitmap == null)
        {
            return;
        }

        // Sin viewport conocido (comando de bot�n): ajusta a 1:1.
        Zoom = 1.0;
    }

    // ============================================================
    // Interacci�n con el canvas (la vista entrega coordenadas de IMAGEN)
    // ============================================================

    public void CanvasMouseDown(Point point)
    {
        if (_surface == null)
        {
            return;
        }

        // 1) Recorte: inicia o manipula el rect�ngulo de recorte.
        if (_currentTool == DrawMode.Crop)
        {
            BeginCrop(point);
            return;
        }

        // 2) Si hay herramienta activa ? comenzar a dibujar un elemento nuevo.
        if (_currentTool != DrawMode.None)
        {
            _drawingElement = CreateDrawableForTool(_currentTool);
            _drawingStart = point;
            _drawingElement.Left = (int)point.X;
            _drawingElement.Top = (int)point.Y;
            if (_drawingElement is TextDrawable text)
            {
                text.Width = Math.Max(120, _fontSize * 6);
                text.Height = _fontSize + Padding2();
            }

            _surface.Elements.Add(_drawingElement);

            // Texto: entra en edici�n inmediatamente (enterEditing + selectAll web).
            if (_drawingElement is TextDrawable textDrawable)
            {
                _editingElement = textDrawable;
                TextEditRequested?.Invoke(this, textDrawable.Bounds);
                CurrentTool = DrawMode.None;
            }

            return;
        }

        // 3) Selecci�n / movimiento de elementos existentes.
        var hit = _surface.Elements.TopMostAt(new System.Drawing.Point((int)point.X, (int)point.Y));
        if (hit != null)
        {
            _surface.Elements.DeselectAll();
            hit.Selected = true;
            _selectedElement = hit;
            _moveOffset = new Point(point.X - hit.Left, point.Y - hit.Top);
            _movingElement = hit;
            SyncStyleFromSelection();
        }
        else
        {
            _surface.Elements.DeselectAll();
            _selectedElement = null;
            _moveOffset = default;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        RaiseSurfaceChanged();
        RefreshStatus();
    }

    public void CanvasMouseMove(Point point)
    {
        if (_cropDragType != null)
        {
            UpdateCropDrag(point);
            return;
        }

        if (_drawingElement != null)
        {
            // Mismo c�lculo que el mouse:move web: left:min, top:min, width:abs, height:abs
            // (el ancla Left/Top queda fija para el rect�ngulo; la flecha usa el delta).
            if (_drawingElement is ArrowDrawable)
            {
                _drawingElement.Width = (int)(point.X - _drawingStart.X);
                _drawingElement.Height = (int)(point.Y - _drawingStart.Y);
            }
            else
            {
                _drawingElement.Left = (int)Math.Min(_drawingStart.X, point.X);
                _drawingElement.Top = (int)Math.Min(_drawingStart.Y, point.Y);
                _drawingElement.Width = (int)Math.Abs(point.X - _drawingStart.X);
                _drawingElement.Height = (int)Math.Abs(point.Y - _drawingStart.Y);
            }
            return;
        }

        if (_movingElement != null)
        {
            _movingElement.Left = (int)(point.X - _moveOffset.X);
            _movingElement.Top = (int)(point.Y - _moveOffset.Y);
        }
    }

    public void CanvasMouseUp(Point point)
    {
        if (_cropDragType != null)
        {
            EndCropDrag();
            return;
        }

        if (_drawingElement != null)
        {
            // Mismo descarte que el mouse:up web: si w<5 y h<5 y no es l�nea ? eliminar.
            if (_drawingElement is not ArrowDrawable &&
                Math.Abs(_drawingElement.Width) < 5 && Math.Abs(_drawingElement.Height) < 5)
            {
                _surface!.Elements.Remove(_drawingElement);
                _drawingElement = null;
                RaiseSurfaceChanged();
                return;
            }

            _drawingElement.Status = EditStatus.Drawn;
            _surface!.Elements.DeselectAll();
            _drawingElement.Selected = true;
            _selectedElement = _drawingElement;
            SyncStyleFromSelection();
            SaveSnapshot();
            _drawingElement = null;
            CurrentTool = DrawMode.None;
        }

        if (_movingElement != null)
        {
            SaveSnapshot(); // object:moving ? modified ? snapshot (web)
            _movingElement = null;
        }

        RaiseSurfaceChanged();
        RefreshStatus();
    }

    /// <summary>
    /// Doble clic: aplica el recorte si la herramienta activa es Crop
    /// (replicado del mouse:dblclick web).
    /// </summary>
    public void CanvasDoubleClick(Point point)
    {
        if (_currentTool == DrawMode.Crop && _cropActive && _cropRect.Width > 5 && _cropRect.Height > 5)
        {
            ApplyCrop();
        }
    }

    /// <summary>Confirma la edici�n de texto (Enter/blur del TextBox overlay).</summary>
    public void CommitTextEdit(string text)
    {
        if (_editingElement == null || _surface == null)
        {
            return;
        }

        TextDrawable element = _editingElement;
        _editingElement = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            _surface.Elements.Remove(element);
        }
        else
        {
            element.Text = text;
            element.ResizeToText();
            element.Status = EditStatus.Drawn;
            _surface.Elements.DeselectAll();
            element.Selected = true;
            _selectedElement = element;
            SaveSnapshot();
        }

        CurrentTool = DrawMode.None;
        RaiseSurfaceChanged();
        RefreshStatus();
    }

    /// <summary>Cancela la edici�n de texto (Escape del TextBox overlay).</summary>
    public void CancelTextEdit()
    {
        if (_editingElement == null || _surface == null)
        {
            return;
        }

        _surface.Elements.Remove(_editingElement);
        _editingElement = null;
        CurrentTool = DrawMode.None;
        RaiseSurfaceChanged();
    }

    // ============================================================
    // Crop (replicado de la secci�n 6 del m�dulo web)
    // ============================================================

    private void BeginCrop(Point point)
    {
        int x = (int)point.X;
        int y = (int)point.Y;

        // Si hay recorte activo: �pulsa un handle o dentro del rect? ? arrastre.
        if (_cropActive && _cropRect.Width > 0 && _cropRect.Height > 0)
        {
            double tolerance = 10 / _zoom; // handles de 8px en pantalla
            string? handle = HitTestCropHandle(x, y, tolerance);
            if (handle != null)
            {
                _cropDragType = handle;
                _cropStartRect = _cropRect;
                _cropStartMouse = point;
                return;
            }

            if (_cropRect.Contains(x, y))
            {
                _cropDragType = "move";
                _cropStartRect = _cropRect;
                _cropStartMouse = point;
                return;
            }
        }

        // Nuevo rect�ngulo de recorte: rect = {x:startX, y:startY, w:0, h:0}.
        _cropDragType = "new";
        _cropRect = new Rectangle(x, y, 0, 0);
        RaiseSurfaceChanged();
    }

    private void UpdateCropDrag(Point point)
    {
        if (_cropDragType == "new")
        {
            int rx = (int)Math.Min(_cropStartMouse.X, point.X);
            int ry = (int)Math.Min(_cropStartMouse.Y, point.Y);
            int rw = (int)Math.Abs(point.X - _cropStartMouse.X);
            int rh = (int)Math.Abs(point.Y - _cropStartMouse.Y);

            // Clamp a los l�mites de la imagen.
            int imgW = _surface!.ImageWidth;
            int imgH = _surface.ImageHeight;
            rx = Math.Clamp(rx, 0, imgW);
            ry = Math.Clamp(ry, 0, imgH);
            rw = Math.Clamp(rw, 0, imgW - rx);
            rh = Math.Clamp(rh, 0, imgH - ry);

            _cropRect = new Rectangle(rx, ry, rw, rh);
            if (rw > 2 && !_cropActive)
            {
                CropActive = true; // showCropOverlay (web)
            }

            RaiseSurfaceChanged();
            return;
        }

        // Arrastre de handle / movimiento (mismo c�lculo que onPointerMove web).
        double dx = (point.X - _cropStartMouse.X);
        double dy = (point.Y - _cropStartMouse.Y);

        int x = _cropStartRect.X;
        int y = _cropStartRect.Y;
        int w = _cropStartRect.Width;
        int h = _cropStartRect.Height;

        switch (_cropDragType)
        {
            case "move":
                x = (int)Math.Clamp(x + dx, 0, _surface!.ImageWidth - w);
                y = (int)Math.Clamp(y + dy, 0, _surface.ImageHeight - h);
                break;
            case "tl":
                x = (int)Math.Clamp(x + dx, 0, _cropStartRect.Right - 5);
                y = (int)Math.Clamp(y + dy, 0, _cropStartRect.Bottom - 5);
                w = _cropStartRect.Right - x;
                h = _cropStartRect.Bottom - y;
                break;
            case "tr":
                y = (int)Math.Clamp(y + dy, 0, _cropStartRect.Bottom - 5);
                w = (int)Math.Clamp(w + dx, 5, _surface!.ImageWidth - x);
                h = _cropStartRect.Bottom - y;
                break;
            case "bl":
                x = (int)Math.Clamp(x + dx, 0, _cropStartRect.Right - 5);
                w = _cropStartRect.Right - x;
                h = (int)Math.Clamp(h + dy, 5, _surface!.ImageHeight - y);
                break;
            case "br":
                w = (int)Math.Clamp(w + dx, 5, _surface!.ImageWidth - x);
                h = (int)Math.Clamp(h + dy, 5, _surface.ImageHeight - y);
                break;
        }

        _cropRect = new Rectangle(x, y, w, h);
        RaiseSurfaceChanged();
    }

    private void EndCropDrag()
    {
        // Mismo descarte que el mouse:up web: si w<10 o h<10 ? ocultar overlay.
        if (_cropRect.Width < 10 || _cropRect.Height < 10)
        {
            CropActive = false;
            _cropRect = Rectangle.Empty;
        }

        _cropDragType = null;
        RaiseSurfaceChanged();
        RefreshStatus();
    }

    /// <summary>
    /// Aplica el recorte (applyCrop web): desplaza elementos, recorta el fondo y
    /// redimensiona el lienzo al tama�o del rect�ngulo.
    /// </summary>
    public void ApplyCrop()
    {
        if (!_cropActive || _surface?.BackgroundBitmap == null || _cropRect.Width < 5 || _cropRect.Height < 5)
        {
            return;
        }

        Rectangle rect = _cropRect;

        // 1. Desplaza todos los elementos: left -= x, top -= y.
        foreach (DrawableObject element in _surface.Elements.ToList())
        {
            element.Left -= rect.X;
            element.Top -= rect.Y;
        }

        // 2. Recorta el fondo al rect�ngulo seleccionado.
        Bitmap cropped = _surface.BackgroundBitmap.Clone(rect, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        // 3. Redimensiona el lienzo: el nuevo fondo es el recortado.
        _surface.BackgroundBitmap = cropped;

        // 4. Limpia estado de recorte y guarda snapshot.
        _cropRect = Rectangle.Empty;
        CropActive = false;
        _cropDragType = null;
        CurrentTool = DrawMode.None;
        SaveSnapshot();
        RaiseSurfaceChanged();
        RefreshStatus();
    }

    /// <summary>Cancela el recorte (Escape).</summary>
    public void CancelCrop()
    {
        if (!_cropActive)
        {
            return;
        }

        _cropRect = Rectangle.Empty;
        CropActive = false;
        _cropDragType = null;
        CurrentTool = DrawMode.None;
        RaiseSurfaceChanged();
        RefreshStatus();
    }

    /// <summary>Hit-test de los 4 handles (tl/tr/bl/br) dentro de la tolerancia.</summary>
    private string? HitTestCropHandle(int x, int y, double tolerance)
    {
        int left = _cropRect.Left;
        int top = _cropRect.Top;
        int right = _cropRect.Right;
        int bottom = _cropRect.Bottom;

        if (Math.Abs(x - left) <= tolerance && Math.Abs(y - top) <= tolerance) return "tl";
        if (Math.Abs(x - right) <= tolerance && Math.Abs(y - top) <= tolerance) return "tr";
        if (Math.Abs(x - left) <= tolerance && Math.Abs(y - bottom) <= tolerance) return "bl";
        if (Math.Abs(x - right) <= tolerance && Math.Abs(y - bottom) <= tolerance) return "br";
        return null;
    }

    // ============================================================
    // Undo / Redo (replicado de la secci�n 8 del m�dulo web)
    // ============================================================

    /// <summary>
    /// Snapshot XML de los elementos (max 10). Equivale al saveSnapshot web
    /// (toObject ? JSON) usando el formato .evidenciasSqa del modelo.
    /// </summary>
    private void SaveSnapshot()
    {
        if (_surface == null)
        {
            return;
        }

        using var stream = new MemoryStream();
        _surface.SaveElementsToStream(stream);
        string snapshot = Convert.ToBase64String(stream.ToArray());

        if (snapshot == _lastSnapshot)
        {
            return;
        }

        _isDirty = true;
        _history.Add(snapshot);
        _lastSnapshot = snapshot;
        if (_history.Count > MaxHistory)
        {
            _history.RemoveAt(0);
        }

        _future.Clear();
        UpdateHistoryCommands();
    }

    private void Undo()
    {
        if (_history.Count <= 1 || _surface == null)
        {
            return;
        }

        string current = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        _future.Add(current);
        LoadSnapshot(_history[^1]);
        UpdateHistoryCommands();
    }

    private void Redo()
    {
        if (_future.Count == 0 || _surface == null)
        {
            return;
        }

        string snapshot = _future[^1];
        _future.RemoveAt(_future.Count - 1);
        _history.Add(snapshot);
        _lastSnapshot = snapshot;
        LoadSnapshot(snapshot);
        UpdateHistoryCommands();
    }

    private void LoadSnapshot(string snapshot)
    {
        if (_surface == null)
        {
            return;
        }

        using var stream = new MemoryStream(Convert.FromBase64String(snapshot));
        _surface.LoadElementsFromStream(stream);
        _selectedElement = null;
        RaiseSurfaceChanged();
        RefreshStatus();
    }

    private void UpdateHistoryCommands()
    {
        // RelayCommand se re-eval�a v�a CommandManager (no con PropertyChanged).
        OnPropertyChanged(nameof(UndoCommand));
        OnPropertyChanged(nameof(RedoCommand));
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    // ============================================================
    // Acciones especiales (duplicar / eliminar / estilos)
    // ============================================================

    /// <summary>Duplicar (Ctrl+D): clone +20,+20 y selecci�n (replicado del web).</summary>
    private void DuplicateSelected()
    {
        if (_selectedElement == null || _surface == null)
        {
            return;
        }

        DrawableObject clone = _selectedElement.Clone();
        clone.Left = _selectedElement.Left + 20;
        clone.Top = _selectedElement.Top + 20;
        _surface.Elements.DeselectAll();
        clone.Selected = true;
        _surface.Elements.Add(clone);
        _selectedElement = clone;
        SaveSnapshot();
        RaiseSurfaceChanged();
        RefreshStatus();
    }

    private void DeleteSelected()
    {
        if (_selectedElement == null || _surface == null)
        {
            return;
        }

        _surface.Elements.Remove(_selectedElement);
        _selectedElement = null;
        SaveSnapshot();
        RaiseSurfaceChanged();
        RefreshStatus();
    }

    /// <summary>Cancelar (modo selección): quita la selección de TODOS los objetos.</summary>
    private void ClearSelection()
    {
        if (_surface == null)
        {
            return;
        }

        _surface.Elements.DeselectAll();
        _selectedElement = null;
        RaiseSurfaceChanged();
        RefreshStatus();
        UpdateHistoryCommands();
    }

    /// <summary>Aplica el color activo al objeto seleccionado (swatch del web).</summary>
    private void ApplyColorToSelection()
    {
        switch (_selectedElement)
        {
            case TextDrawable text:
                text.TextColor = _currentColor;
                break;
            case HighlightDrawable highlight:
                highlight.FillColor = _currentColor;
                break;
            case RectangleDrawable rectangle:
                rectangle.LineColor = _currentColor;
                break;
            case ArrowDrawable arrow:
                arrow.LineColor = _currentColor;
                break;
            default:
                return;
        }

        if (_selectedElement != null)
        {
            SaveSnapshot();
            RaiseSurfaceChanged();
        }
    }

    /// <summary>Aplica el grosor activo (salvo IText, como el web).</summary>
    private void ApplyStrokeWidthToSelection()
    {
        switch (_selectedElement)
        {
            case RectangleDrawable rectangle:
                rectangle.LineThickness = _strokeWidth;
                break;
            case ArrowDrawable arrow:
                arrow.LineThickness = _strokeWidth;
                break;
            default:
                return;
        }

        if (_selectedElement != null)
        {
            SaveSnapshot();
            RaiseSurfaceChanged();
        }
    }

    /// <summary>Aplica el tama�o de fuente al IText seleccionado.</summary>
    private void ApplyFontSizeToSelection()
    {
        if (_selectedElement is TextDrawable text)
        {
            text.FontSize = _fontSize;
            text.ResizeToText();
            SaveSnapshot();
            RaiseSurfaceChanged();
        }
    }

    /// <summary>Sincroniza color/grosor/fuente al seleccionar un objeto existente.</summary>
    private void SyncStyleFromSelection()
    {
        switch (_selectedElement)
        {
            case TextDrawable text:
                _currentColor = text.TextColor;
                _fontSize = text.FontSize;
                break;
            case HighlightDrawable highlight:
                _currentColor = highlight.FillColor;
                break;
            case RectangleDrawable rectangle:
                _currentColor = rectangle.LineColor;
                _strokeWidth = rectangle.LineThickness;
                break;
            case ArrowDrawable arrow:
                _currentColor = arrow.LineColor;
                _strokeWidth = arrow.LineThickness;
                break;
            default:
                return;
        }

        OnPropertyChanged(nameof(CurrentColor));
        OnPropertyChanged(nameof(StrokeWidth));
        OnPropertyChanged(nameof(FontSize));

        // Sincroniza el swatch del toolbar con el objeto seleccionado.
        ColorOption? matching = ColorOptions.FirstOrDefault(o => o.Color.ToArgb() == _currentColor.ToArgb());
        if (matching != null && !ReferenceEquals(_selectedColorOption, matching))
        {
            _selectedColorOption = matching;
            OnPropertyChanged(nameof(SelectedColorOption));
        }
    }

    // ============================================================
    // Privados
    // ============================================================

    private static int Padding2() => TextDrawable.Padding * 2;

    private DrawableObject CreateDrawableForTool(DrawMode tool) => tool switch
    {
        DrawMode.Rectangle => new RectangleDrawable
        {
            LineColor = _currentColor,
            LineThickness = _strokeWidth,
            Shadow = true,
            Selected = true
        },
        DrawMode.Arrow => new ArrowDrawable
        {
            LineColor = _currentColor,
            LineThickness = _strokeWidth,
            Selected = true
        },
        DrawMode.Text => new TextDrawable
        {
            Text = string.Empty,
            TextColor = _currentColor,
            FontSize = _fontSize,
            Selected = true
        },
        DrawMode.Highlight => new HighlightDrawable
        {
            FillColor = _currentColor,
            Selected = true
        },
        DrawMode.Blur => new BlurDrawable { Radius = 4, Selected = true }, // radius=4 (web)
        _ => throw new ArgumentOutOfRangeException(nameof(tool))
    };

    private void ExportTo(int destinationIndex)
    {
        if (_surface == null || destinationIndex >= _destinations.Count)
        {
            return;
        }

ExportInformation result = _destinations[destinationIndex].ExportCapture(
            manuallyInitiated: true,
            _surface,
            _captureDetails);

        StatusText = result.ExportMade
            ? $"Exportado: {result.Filepath ?? result.DestinationDesignation}"
            : $"Exportación cancelada o fallida: {result.ErrorMessage ?? "sin mensaje"}";

        if (result.ExportMade)
        {
            _toast.Show(result.DestinationDesignation == "Portapapeles"
                ? "Imagen copiada al portapapeles"
                : $"Guardado: {result.Filepath ?? result.DestinationDesignation}", ToastType.Success);
        }
        else
        {
            _toast.Show(result.ErrorMessage ?? "No se pudo exportar la imagen", ToastType.Error);
        }
    }

    private bool CanExport() => _surface?.BackgroundBitmap != null;

    /// <summary>
    /// Sustituye el documento actual (disponiendo el anterior) por uno nuevo.
    /// </summary>
    private void ReplaceSurface(Bitmap bitmap)
    {
        SurfaceDocument document = new();
        document.BackgroundBitmap = bitmap;
        ReplaceSurface(document);
    }

    private void ReplaceSurface(SurfaceDocument document)
    {
        SurfaceDocument? previous = _surface;
        _surface = document;
        _surface.RequestRender += OnSurfaceRequestRender;
        _history.Clear();
        _future.Clear();
        _lastSnapshot = null;
        _isDirty = false;
        _selectedElement = null;
        _cropRect = Rectangle.Empty;
        CropActive = false;
        _cropDragType = null;
        Zoom = 1.0;

        previous?.Dispose();
        OnPropertyChanged(nameof(Surface));
        RaiseSurfaceChanged();
        RefreshStatus();
        UpdateHistoryCommands();
    }

    private void OnSurfaceRequestRender(object? sender, EventArgs e) => RaiseSurfaceChanged();

    private void RaiseSurfaceChanged() => SurfaceChanged?.Invoke(this, EventArgs.Empty);

    private void RefreshStatus()
    {
        if (_surface?.BackgroundBitmap == null)
        {
            StatusText = "Sin imagen cargada";
            return;
        }

        StatusText =
            $"{_surface.ImageWidth}x{_surface.ImageHeight} px � {_surface.Elements.Count} elemento(s) � {ZoomPercentText}" +
            (_cropActive ? $" � Recorte {_cropRect.Width}x{_cropRect.Height}" : string.Empty);
    }

    public void Dispose()
    {
        _surface?.Dispose();
        _surface = null;
    }
}