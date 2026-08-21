using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using EvidenciasSQA.Core.Model;
using EvidenciasSQA.Editor.Wpf.ViewModels;

namespace EvidenciasSQA.Editor.Wpf.Views;

/// <summary>
/// Vista del MÓDULO EDITOR EMBEBIDO (UserControl dentro del MainWindow unificado).
/// El code-behind se limita al pegamento de la vista: conectar los eventos del
/// canvas con el ViewModel, atajos de teclado (replicados del módulo web) y el
/// TextBox overlay de edición de texto. Toda la lógica vive en EditorViewModel /
/// SurfaceDocument / DrawableObject.
///
/// El DataContext (EditorViewModel) lo asigna el host (MainWindow) vía binding;
/// el wiring se hace en Loaded y se revierte en Unloaded para no filtrar eventos
/// entre aperturas (el ViewModel se recrea en cada apertura del editor).
/// </summary>
public partial class EditorView : UserControl
{
    private EditorViewModel? _viewModel;
    private bool _wired;
    private bool _committingText;
    private SurfaceDocument? _lastFittedSurface;

    public EditorView()
    {
        InitializeComponent();

        // Los eventos del propio control (teclado/rueda/TextBox) se suscriben una
        // sola vez en el constructor: el control vive tanto como la ventana.
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseWheel += OnPreviewMouseWheel;
        TextEditBox.KeyDown += OnTextEditBoxKeyDown;
        TextEditBox.LostFocus += OnTextEditBoxLostFocus;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        // El DataContext (EditorViewModel) lo asigna el host y puede cambiar con el
        // control YA cargado (Editor del visor pasa de null a instancia al abrir):
        // el wiring se hace aquí, no solo en Loaded.
        DataContextChanged += (_, _) =>
        {
            UnwireViewModel();
            WireViewModel();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WireViewModel();
        FitZoomToViewport();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnwireViewModel();
    }

    private void WireViewModel()
    {
        if (_wired)
        {
            return;
        }

        if (DataContext is not EditorViewModel vm)
        {
            return;
        }

        _viewModel = vm;

        // El canvas solo entrega coordenadas de imagen; el ViewModel decide la semántica.
        EditorCanvas.MouseDownOnCanvas += _viewModel.CanvasMouseDown;
        EditorCanvas.MouseMoveOnCanvas += _viewModel.CanvasMouseMove;
        EditorCanvas.MouseUpOnCanvas += _viewModel.CanvasMouseUp;
        EditorCanvas.DoubleClickOnCanvas += _viewModel.CanvasDoubleClick;

        // Repintado bajo demanda: solo cuando el modelo lo solicita.
        _viewModel.SurfaceChanged += OnSurfaceChanged;

        // Edición de texto en línea: la vista posiciona el TextBox sobre el elemento.
        _viewModel.TextEditRequested += OnTextEditRequested;

        _wired = true;

        // El Surface pudo cargarse ANTES de suscribirnos (flujo embebido: el visor
        // llama LoadFromBitmapSource y solo después asigna DataContext). El evento
        // SurfaceChanged inicial se pierde: forzamos repintado + fit-to-area inicial
        // (mismo comportamiento que el initEditor web: fitFactor al abrir).
        _lastFittedSurface = null;
        if (_viewModel.Surface != null)
        {
            EditorCanvas.InvalidateVisual();
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, FitZoomToViewport);
        }
    }

    private void UnwireViewModel()
    {
        if (_viewModel == null)
        {
            return;
        }

        EditorCanvas.MouseDownOnCanvas -= _viewModel.CanvasMouseDown;
        EditorCanvas.MouseMoveOnCanvas -= _viewModel.CanvasMouseMove;
        EditorCanvas.MouseUpOnCanvas -= _viewModel.CanvasMouseUp;
        EditorCanvas.DoubleClickOnCanvas -= _viewModel.CanvasDoubleClick;
        _viewModel.SurfaceChanged -= OnSurfaceChanged;
        _viewModel.TextEditRequested -= OnTextEditRequested;

        _viewModel = null;
        _wired = false;
        _lastFittedSurface = null;
    }

    private void OnSurfaceChanged(object? sender, EventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        EditorCanvas.InvalidateVisual();

        // Zoom inicial fit-to-area solo al cargar una imagen NUEVA (initEditor web),
        // no en cada repintado: así no pisa el zoom manual del usuario.
        if (!ReferenceEquals(_viewModel.Surface, _lastFittedSurface))
        {
            _lastFittedSurface = _viewModel.Surface;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, FitZoomToViewport);
        }
    }

    /// <summary>
    /// Ajusta el zoom al área visible (fitFactor = min(area/w, area/h, 1.0)),
    /// replicando el zoom inicial del initEditor web. No salta el zoom si el
    /// usuario ya lo ajustó manualmente y solo es un repintado.
    /// </summary>
    private void FitZoomToViewport()
    {
        double viewportW = CanvasScroll.ViewportWidth;
        double viewportH = CanvasScroll.ViewportHeight;
        if (viewportW > 0 && viewportH > 0 && _viewModel?.Surface?.BackgroundBitmap != null)
        {
            _viewModel.FitZoom(viewportW, viewportH);
        }
    }

    // ============================================================
    // Edición de texto en línea (TextBox overlay)
    // ============================================================

    private void OnTextEditRequested(object? sender, System.Drawing.Rectangle bounds)
    {
        if (_viewModel == null)
        {
            return;
        }

        double zoom = _viewModel.Zoom;
        TextEditBox.Visibility = Visibility.Visible;
        TextEditBox.Margin = new Thickness(bounds.X * zoom, bounds.Y * zoom, 0, 0);
        TextEditBox.Width = Math.Max(120, bounds.Width * zoom);
        TextEditBox.Height = Math.Max(28, bounds.Height * zoom);
        TextEditBox.Text = string.Empty;
        TextEditBox.Focus();
        TextEditBox.SelectAll();
    }

    private void OnTextEditBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitText();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _viewModel?.CancelTextEdit();
            HideTextEditBox();
            e.Handled = true;
        }
    }

    private void OnTextEditBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (TextEditBox.Visibility == Visibility.Visible)
        {
            CommitText();
        }
    }

    private void CommitText()
    {
        if (_committingText)
        {
            return;
        }

        _committingText = true;
        try
        {
            _viewModel?.CommitTextEdit(TextEditBox.Text);
        }
        finally
        {
            HideTextEditBox();
            _committingText = false;
        }
    }

    private void HideTextEditBox()
    {
        TextEditBox.Visibility = Visibility.Collapsed;
        TextEditBox.Text = string.Empty;
    }

    // ============================================================
    // Atajos de teclado (replicados de la tabla del módulo web)
    // ============================================================

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        if (TextEditBox.Visibility == Visibility.Visible)
        {
            return; // el TextBox gestiona sus propias teclas
        }

        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        switch (e.Key)
        {
            // Herramientas: F/C/T/H/B/R/V/S
            case Key.F:
                _viewModel.ArrowToolCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.C when !ctrl:
                _viewModel.RectangleToolCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.T:
                _viewModel.TextToolCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.H:
                _viewModel.HighlightToolCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.B:
                _viewModel.BlurToolCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.R:
                _viewModel.CropToolCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.V:
            case Key.S when !ctrl:
                _viewModel.SelectToolCommand.Execute(null);
                e.Handled = true;
                break;

            // Undo/Redo/Duplicar/Copiar
            case Key.Z when ctrl && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift):
                _viewModel.UndoCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Y when ctrl:
            case Key.Z when ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift):
                _viewModel.RedoCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.D when ctrl:
                _viewModel.DuplicateSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.C when ctrl:
                _viewModel.CopyCommand.Execute(null);
                e.Handled = true;
                break;

            // Eliminar objeto activo
            case Key.Delete:
            case Key.Back when _viewModel.DeleteSelectedCommand.CanExecute(null):
                _viewModel.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
                break;

            // Enter: aplicar recorte (web: Enter en crop). Escape: cancelar recorte.
            case Key.Enter when _viewModel.CropActive:
                _viewModel.ApplyCrop();
                e.Handled = true;
                break;
            case Key.Escape when _viewModel.CropActive:
                _viewModel.CancelCrop();
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Ctrl+Wheel = zoom (replicado del mouse:wheel web: zoom * 0.999^deltaY).
    /// </summary>
    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_viewModel == null)
        {
            return;
        }

        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        _viewModel.ZoomByWheel(e.Delta);
        e.Handled = true;
    }
}