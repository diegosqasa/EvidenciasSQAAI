using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EvidenciasSQA.Core.Events;
using EvidenciasSQA.Core.Export;
using EvidenciasSQA.Core.Imaging;
using EvidenciasSQA.Core.Mvvm;
using EvidenciasSQA.Core.Helpers;
using EvidenciasSQA.Core.Persistence;
using EvidenciasSQA.Core.Services;
using EvidenciasSQA.Editor.Wpf.ViewModels;
using EvidenciasSQA.Wpf.Models;
using EvidenciasSQA.Wpf.Views;
using Microsoft.Win32;

namespace EvidenciasSQA.Wpf.ViewModels;

/// <summary>
/// ViewModel del MÓDULO VISOR (ultraligero, responsabilidad única).
///
/// Flujo de datos:
///  1. CARGA (archivo / portapapeles / historial) → BitmapSource o file path.
///  2. VISUALIZACIÓN → WicHelper.ToImageSource (GPU) en FastImageViewer.
///  3. GALERÍA → EvidenceRepository (escaneo de carpeta) con miniaturas WIC 320px.
///  4. EXPORTACIÓN → ImageIO.Save (PNG/JPG) y WordReportBuilder (.docx, sin librerías externas).
///
/// GALERÍA / HISTORIAL: vista conmutada alimentada por EvidenceRepository (escaneo de
/// carpeta). Implementa la máquina de estados del módulo Historial (historial.md):
///
///   | informeMode | selectionMode | Toolbar del encabezado            |
///   |-------------|---------------|-----------------------------------|
///   | false       | false         | Normal (Seleccionar / Informe / …)|
///   | true        | false         | Normal + barra de opciones        |
///   | true        | true          | SOLO "Generar informe"            |
///   | false       | true          | Selección completa + contador     |
///
/// selectedIds mantiene el ORDEN de inserción (Set con orden): los badges
/// word-order muestran 1..N y el informe respeta ese orden. Modo informe
/// persiste tras exportar/cancelar (el usuario permanece en el módulo Informe).
///
/// Exportación Word (WordReportBuilder): Completo (todas) / Seleccionado
/// (selectedIds en orden) / Por módulos (casos de prueba con fases de selección),
/// con modal HU (id + nombre, validación) y barra de progreso.
/// </summary>
public sealed class ViewerViewModel : ObservableObject, IDisposable
{
    private const int MaxModules = 20;

    private readonly string _tempDir;
    private readonly EvidenceRepository _repository;
    private readonly IToastService _toast;
    private readonly IConfirmationService _confirmation;

    // Imagen actual del visor (renderizada por FastImageViewer)
    private BitmapSource? _currentSource;

    // Ruta de archivo cuando la imagen vino de la galería (sin bitmap en RAM)
    private string? _currentFilePath;

    private string _statusText = "Listo: abre o pega una imagen para visualizar.";
    private string _headerTitleText = "Visor de Evidencias";
    private string _toggleHistoryButtonText = "Ver historial";
    private bool _isHistoryViewVisible;
    private bool _isSelectionModeActive;
    private bool _isInformeModeActive;
    private bool _isModulePhaseActive;
    private bool _isExporting;
    private double _exportProgress;
    private string _exportStatusText = string.Empty;
    private string? _modulePhaseText;
    private ModuleExportState? _moduleState;
    private int _currentTileIndex = -1; // índice en HistoryTiles cuando la imagen actual vino de la galería
    private bool _isHistoryEnabled;
    private bool _disposed;

    public ViewerViewModel(EvidenceRepository? repository = null, IToastService? toast = null, IConfirmationService? confirmation = null)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sqa-viewer");
        Directory.CreateDirectory(_tempDir);
        _repository = repository ?? new EvidenceRepository();
        _toast = toast ?? ToastService.Instance;
        _confirmation = confirmation ?? ConfirmationService.Instance;

        OpenCommand = new RelayCommand(OpenFile);
        PasteCommand = new RelayCommand(PasteFromClipboard);
        EditCommand = new RelayCommand(EditDelegated, () => _currentSource != null);
        SaveAsCommand = new RelayCommand(SaveAs, () => _currentSource != null || _currentFilePath != null);
        CloseEditorCommand = new RelayCommand(CloseEditor, () => IsEditorVisible);

        // Toolbar del visor (UI original): Editar | Copiar | Descargar | Eliminar | Recopilar
        CopyCurrentCommand = new RelayCommand(CopyCurrent, () => _currentSource != null);
        DownloadCurrentCommand = new RelayCommand(DownloadCurrent, () => _currentSource != null || _currentFilePath != null);
        DeleteCurrentCommand = new RelayCommand(DeleteCurrent, () => _currentFilePath != null);
        PreviousCommand = new RelayCommand(GoPrevious, () => CanNavigate(-1));
        NextCommand = new RelayCommand(GoNext, () => CanNavigate(1));

        ToggleHistoryViewCommand = new RelayCommand(ToggleHistoryView);
        TileClickCommand = new RelayCommand(p => HandleTileClick(p as EvidenceTileModel));
        CopyTileCommand = new RelayCommand(p => CopyTile(p as EvidenceTileModel), _ => !IsSelectionModeActive);
        DownloadTileCommand = new RelayCommand(p => DownloadTile(p as EvidenceTileModel), _ => !IsSelectionModeActive);
        DeleteTileCommand = new RelayCommand(p => DeleteTile(p as EvidenceTileModel), _ => !IsSelectionModeActive);
        DownloadAllCommand = new RelayCommand(DownloadAll);
        ClearAllCommand = new RelayCommand(ClearAll);

        EnterSelectionModeCommand = new RelayCommand(EnterSelectionMode);
        EnterInformeModeCommand = new RelayCommand(EnterInformeMode);
        CancelModeCommand = new RelayCommand(CancelMode);
        InformeOptionCompletoCommand = new RelayCommand(() => StartExport(ExportKind.Completo));
        InformeOptionSeleccionadoCommand = new RelayCommand(() =>
        {
            IsSelectionModeActive = true;
            NotifyModeChanged();
        });
        InformeOptionModulosCommand = new RelayCommand(StartModuleExport);
        ConfirmModulePhaseCommand = new RelayCommand(ConfirmModulePhase);
        GenerateReportSelectedCommand = new RelayCommand(() => StartExport(ExportKind.Seleccionado), () => SelectedIds.Count > 0);
        CopySelectedCommand = new RelayCommand(CopySelected, () => SelectedIds.Count > 0);
        DownloadSelectedCommand = new RelayCommand(DownloadSelected, () => SelectedIds.Count > 0);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected, () => SelectedIds.Count > 0);

        // Bus de eventos: el visor reacciona a la persistencia de nuevas capturas
        // (notificadas por el Tray vía named pipe). La suscripción se revierte en Dispose.
        SqaEvents.CaptureSaved += HandleCaptureSaved;

        // Restauración desde el tray ("Abrir Visor", clic/doble clic): al abrir el
        // visor se recarga la última captura persistida y se asegura la visibilidad.
        // Regla de oro: una captura tomada con el visor oculto se ve al restaurarlo.
        SqaEvents.RestoreViewerRequested += HandleRestoreViewerRequested;

        // Arranque: sin capturas en disco → "Ver historial" deshabilitado (empty state).
        UpdateHistoryEnabled();

        // Pre-carga asíncrona de la galería en segundo plano:
        // cuando el usuario hace clic en "Ver historial", las tarjetas y miniaturas ya están listas en RAM.
        _ = RefreshHistoryGridAsync();
    }

    private enum ExportKind { Completo, Seleccionado }

    /// <summary>Estado interno de la exportación por módulos (casos de prueba).</summary>
    private sealed class ModuleExportState
    {
        public int Total { get; init; }
        public int CurrentIdx { get; set; }
        public List<List<int>> Containers { get; } = new();
    }

    // ============================================================
    // Propiedades de vista
    // ============================================================

    /// <summary>Imagen del visor (bind a FastImageViewer.Source).</summary>
    public BitmapSource? CurrentImage
    {
        get => _currentSource;
        private set
        {
            SetProperty(ref _currentSource, value);
            OnPropertyChanged(nameof(CanNavigatePrevious));
            OnPropertyChanged(nameof(CanNavigateNext));
        }
    }

    /// <summary>Título del encabezado según modo: Historial / Seleccionar / Informe.</summary>
    public string HeaderTitleText
    {
        get => _headerTitleText;
        private set => SetProperty(ref _headerTitleText, value);
    }

    /// <summary>Texto del botón conmutador: "Ver historial" ↔ "Volver al visor".</summary>
    public string ToggleHistoryButtonText
    {
        get => _toggleHistoryButtonText;
        private set => SetProperty(ref _toggleHistoryButtonText, value);
    }

    public bool IsHistoryViewVisible
    {
        get => _isHistoryViewVisible;
        private set
        {
            if (SetProperty(ref _isHistoryViewVisible, value))
            {
                NotifyModeChanged();
            }
        }
    }

    private EditorViewModel? _editor;

    /// <summary>
    /// ViewModel del EDITOR EMBEBIDO (null = cerrado). Se instancia con la imagen
    /// actual del visor SIN tocar disco (preservación de contexto) y se descarta
    /// al volver al visor.
    /// </summary>
    public EditorViewModel? Editor
    {
        get => _editor;
        private set
        {
            if (SetProperty(ref _editor, value))
            {
                OnPropertyChanged(nameof(IsEditorVisible));
                NotifyModeChanged();
            }
        }
    }

    /// <summary>El editor embebido ocupa el área de contenido.</summary>
    public bool IsEditorVisible => _editor != null;

    /// <summary>Vista VISOR activa (historial cerrado y editor cerrado).</summary>
    public bool IsViewerMode => !IsHistoryViewVisible && !IsEditorVisible;

    /// <summary>Vista HISTORIAL activa (editor cerrado).</summary>
    public bool IsHistoryMode => IsHistoryViewVisible && !IsEditorVisible;

    /// <summary>
    /// Habilita el conmutador "Ver historial" SOLO si hay evidencias (galería en
    /// memoria o disco). En empty state (arranque sin capturas o borrado total)
    /// queda false: el botón se deshabilita — paridad con updateUIState(false) de
    /// Electron que desactiva historyLink (especificacion-visor-estado-vacio.md §1.1).
    /// </summary>
    public bool IsHistoryEnabled
    {
        get => _isHistoryEnabled;
        private set => SetProperty(ref _isHistoryEnabled, value);
    }

    /// <summary>Modo selección múltiple (selectionMode de historial.md).</summary>
    public bool IsSelectionModeActive
    {
        get => _isSelectionModeActive;
        private set => SetProperty(ref _isSelectionModeActive, value);
    }

    /// <summary>Modo informe (informeMode de historial.md). Persiste tras exportar.</summary>
    public bool IsInformeModeActive
    {
        get => _isInformeModeActive;
        private set => SetProperty(ref _isInformeModeActive, value);
    }

    /// <summary>Fase de selección activa de un caso de prueba (exportación por módulos).</summary>
    public bool IsModulePhaseActive
    {
        get => _isModulePhaseActive;
        private set => SetProperty(ref _isModulePhaseActive, value);
    }

    // --- Combinaciones de modo (para la conmutación de toolbars) ---

    /// <summary>Toolbar normal del historial (sin selección ni informe).</summary>
    public bool IsNormalMode => IsHistoryViewVisible && !IsSelectionModeActive && !IsInformeModeActive;

    /// <summary>Modo selección simple: toolbar de selección completa + contador.</summary>
    public bool IsPlainSelectionMode => IsHistoryViewVisible && IsSelectionModeActive && !IsInformeModeActive && !IsModulePhaseActive;

    /// <summary>Modo informe con opciones (Completo/Seleccionado/Por módulos).</summary>
    public bool IsInformeOptionsMode => IsHistoryViewVisible && IsInformeModeActive && !IsSelectionModeActive && !IsModulePhaseActive;

    /// <summary>Informe + selección: toolbar con SOLO "Generar informe".</summary>
    public bool IsInformeSelectionMode => IsHistoryViewVisible && IsInformeModeActive && IsSelectionModeActive && !IsModulePhaseActive;

    /// <summary>Fase de selección de un caso de prueba.</summary>
    public bool IsModulePhaseVisible => IsHistoryViewVisible && IsModulePhaseActive;

    /// <summary>
    /// Acciones por tarjeta (Copiar/Descargar/Eliminar de la miniatura) habilitadas
    /// SOLO fuera del modo selección: en modo selección quedan SIEMPRE deshabilitadas,
    /// las acciones en lote se gestionan desde la toolbar (Copiar/Descargar/Eliminar).
    /// </summary>
    public bool IsTileActionsEnabled => IsHistoryViewVisible && !IsSelectionModeActive;

    /// <summary>Texto de la barra de fase: "Caso de prueba X de Y — …".</summary>
    public string? ModulePhaseText
    {
        get => _modulePhaseText;
        private set => SetProperty(ref _modulePhaseText, value);
    }

    /// <summary>Textos de estado mostrados en la UI.</summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private string _viewerInfoText = "Vista de Evidencia | Copiar (Ctrl+C)";

    /// <summary>
    /// Texto de la info bar del visor (paridad con updateZoomInfo del Electron,
    /// spec §7): en Fit "Vista de Evidencia | Copiar (Ctrl+C)"; en zoom 100%
    /// "Zoom activo | Arrastrar para desplazar | Escape para salir".
    /// Lo actualiza el MainWindow vía ZoomViewport.ZoomModeChanged.
    /// </summary>
    public string ViewerInfoText
    {
        get => _viewerInfoText;
        set => SetProperty(ref _viewerInfoText, value);
    }

    /// <summary>
    /// Se dispara ANTES de cargar una captura nueva por navegación ◀▶/historial
    /// (paridad con navigateViewerCapture §3.2): el host hace PreserveZoomState()
    /// para restaurar zoom+scroll sobre la captura siguiente.
    /// </summary>
    public event Action? NavigatingToCapture;

    private double _zoomPercent = 100;

    /// <summary>
    /// Porcentaje de zoom actual del visor (100 = tamaño real), mostrado en la barra
    /// de estado. Lo actualiza FastImageViewer.ZoomChanged (rueda, botones +/-, ajustar).
    /// </summary>
    public string ZoomPercentText => $"Zoom: {Math.Round(_zoomPercent):0}%";

    /// <summary>Actualiza el porcentaje de zoom desde el control del visor.</summary>
    public void UpdateZoomPercent(double percent)
    {
        _zoomPercent = percent;
        OnPropertyChanged(nameof(ZoomPercentText));
    }

    public ObservableCollection<EvidenceTileModel> HistoryTiles { get; } = new();

    /// <summary>IDs seleccionados con ORDEN DE INSERCIÓN (lista ordenada).</summary>
    public List<int> SelectedIds { get; } = new();

    private int _focusedIndex = -1;

    /// <summary>
    /// Índice enfocado para navegación por teclado y Shift+Click (replica
    /// focusedIndex de Electron). -1 = sin foco. El setter sincroniza
    /// IsFocused en las tarjetas (foco visual).
    /// </summary>
    public int FocusedIndex
    {
        get => _focusedIndex;
        private set
        {
            _focusedIndex = value;
            for (int i = 0; i < HistoryTiles.Count; i++)
            {
                HistoryTiles[i].IsFocused = (i == value);
            }
        }
    }

    private int? _rangeAnchor;

    /// <summary>Ancla para selección por rango (Shift+Click). null = sin ancla.</summary>
    private int? RangeAnchor
    {
        get => _rangeAnchor;
        set => _rangeAnchor = value;
    }

    /// <summary>Contador de selección ("N seleccionadas").</summary>
    public string SelectionCounterText => $"{SelectedIds.Count} seleccionadas";

    public bool IsExporting
    {
        get => _isExporting;
        private set => SetProperty(ref _isExporting, value);
    }

    /// <summary>Progreso 0-100 del export Word.</summary>
    public double ExportProgress
    {
        get => _exportProgress;
        private set => SetProperty(ref _exportProgress, value);
    }

    public string ExportStatusText
    {
        get => _exportStatusText;
        private set => SetProperty(ref _exportStatusText, value);
    }

    public bool CanNavigatePrevious => _currentTileIndex >= 0 && HistoryTiles.Count > 0;
    public bool CanNavigateNext => _currentTileIndex >= 0 && HistoryTiles.Count > 0;

    // ============================================================
    // Comandos
    // ============================================================

    public ICommand OpenCommand { get; }
    public ICommand PasteCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand CloseEditorCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand CopyCurrentCommand { get; }
    public ICommand DownloadCurrentCommand { get; }
    public ICommand DeleteCurrentCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }

    public ICommand ToggleHistoryViewCommand { get; }
    public ICommand TileClickCommand { get; }
    public ICommand CopyTileCommand { get; }
    public ICommand DownloadTileCommand { get; }
    public ICommand DeleteTileCommand { get; }
    public ICommand DownloadAllCommand { get; }
    public ICommand ClearAllCommand { get; }

    public ICommand EnterSelectionModeCommand { get; }
    public ICommand EnterInformeModeCommand { get; }
    public ICommand CancelModeCommand { get; }
    public ICommand InformeOptionCompletoCommand { get; }
    public ICommand InformeOptionSeleccionadoCommand { get; }
    public ICommand InformeOptionModulosCommand { get; }
    public ICommand ConfirmModulePhaseCommand { get; }
    public ICommand GenerateReportSelectedCommand { get; }
    public ICommand CopySelectedCommand { get; }
    public ICommand DownloadSelectedCommand { get; }
    public ICommand DeleteSelectedCommand { get; }

    // ============================================================
    // Visor: zoom/pan/reset (para la tecla Escape del MainWindow)
    // ============================================================

    /// <summary>
    /// Señal de reset de zoom/vista para el ZoomViewport (Escape, spec §1.2).
    /// Se invoca desde MainWindow.xaml.cs al presionar Escape.
    /// </summary>
    public void ResetViewRequested()
    {
        // El ResetZoom se ejecuta directamente en el control ZoomViewport desde
        // el code-behind del MainWindow (Escape). Aquí solo se notifica a la UI.
        StatusText = "Zoom restablecido. Clic = alternar 100%, arrastra = pan, Escape = salir.";
    }

    // ============================================================
    // Carga y Visualización (sin captura ni header corporativo)
    // ============================================================

    /// <summary>
    /// Restablece la transformación de la imagen (zoom y desplazamiento) a su estado inicial.
    /// Se invoca al cargar una nueva evidencia desde el historial o el visor.
    /// </summary>
    public void ResetViewState()
    {
        // La lógica vive en el control ZoomViewport (ShowImage siempre resetea a Fit).
        // Mantenido como no-op por compatibilidad; el reset real ocurre en el control.
    }

    private async void OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Abrir evidencia",
            Filter = "Imágenes (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Todos los archivos (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true || !File.Exists(dialog.FileName))
        {
            return;
        }

        try
        {
            StatusText = "Cargando imagen…";

            // Cargar directamente como ImageSource (sin header, sin captura)
            BitmapImage bitmap = new();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(dialog.FileName);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            _currentFilePath = dialog.FileName;
            _currentTileIndex = -1;
            await DisplayImageAsync(bitmap);
            StatusText = $"Listo: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"No se pudo abrir: {ex.Message}";
        }
    }

    private async void PasteFromClipboard()
    {
        if (!Clipboard.ContainsImage())
        {
            StatusText = "El portapapeles no contiene una imagen.";
            return;
        }

        try
        {
            StatusText = "Pegyendo imagen del portapapeles…";

            // Clipboard.GetImage() devuelve directamente un BitmapSource WPF (no captura)
            BitmapSource? source = Clipboard.GetImage();
            if (source == null)
            {
                StatusText = "No se pudo leer la imagen del portapapeles.";
                return;
            }

            _currentFilePath = null;
            _currentTileIndex = -1;
            await DisplayImageAsync(source);
            StatusText = "Listo: imagen pegada del portapapeles.";
            _toast.Show("Imagen pegada del portapapeles", ToastType.Success);
        }
        catch (Exception ex)
        {
            StatusText = $"No se pudo pegar: {ex.Message}";
            _toast.Show($"No se pudo pegar la imagen", ToastType.Error);
        }
    }

    /// <summary>
    /// Muestra la imagen en el visor directamente (visor puro: sin header corporativo, sin captura).
    /// Congela el BitmapSource en background para uso thread-safe desde el hilo de render.
    /// </summary>
    private async Task DisplayImageAsync(BitmapSource source)
    {
        // Congelar en background mantiene el UI fluido durante la primer decodificación.
        await Task.Run(() => source.Freeze());

        ReplaceCurrent(source);
        StatusText = "Listo. Clic = alternar 100%, arrastra = pan, Escape = salir.";
    }

    private void ReplaceCurrent(BitmapSource source)
    {
        CurrentImage = source;
    }

    // ============================================================
    // Bus de eventos: actualización automática ante nuevas capturas
    // ============================================================

/// <summary>
    /// Receptor del bus <see cref="SqaEvents.CaptureSaved"/>. Se invoca desde el
    /// hilo del productor (p. ej. servidor HTTP), por lo que el acceso a la UI se
    /// marisca obligatoriamente con Dispatcher.
    /// Regla de oro: el visor NUNCA se trae al frente por una captura — el usuario
    /// captura sin necesidad de interactuar con el visor. Si el visor está visible,
    /// la captura se carga en vivo (sin robar foco); si está oculto/minimizado, la
    /// carga se difiere y se muestra al restaurar (ShowLastCapture lee la última
    /// captura desde disco).
    /// </summary>
    private void HandleCaptureSaved(string filePath)
    {
        // Log diagnóstico integrado con prefijo [SQA-INTEGRATION]
        System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] HandleCaptureSaved: ENTRADA - filepath: " + filePath);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            // ASINCRONO (BeginInvoke, nunca Invoke): el productor puede ser el hilo de
            // guardado en background (Task.Run del flujo directo del tray) que el hilo
            // de UI espera con Task.WaitAll. Un Invoke síncrono desde ese hilo causaría
            // deadlock (UI espera al task, el task espera a la UI) → app congelada.
            dispatcher.BeginInvoke(() => HandleCaptureSaved(filePath));
            return;
        }

        // Verificar que el archivo existe antes de intentar cargar
        bool fileExists = File.Exists(filePath);
        System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] HandleCaptureSaved: archivo existe en disco: " + fileExists);

        // ¿El visor está realmente en uso? (visible y no minimizado).
        var mainWindow = Application.Current?.MainWindow as Window;
        bool windowActive = mainWindow != null && mainWindow.IsVisible && mainWindow.WindowState != WindowState.Minimized;

        if (!windowActive)
        {
            // Visor oculto al tray o minimizado: NO traer al frente ni activar.
            // La captura ya está persistida en disco; ShowLastCapture la cargará
            // desde el repositorio cuando el usuario restaure el visor. Se limpia
            // _currentFilePath para que la recarga desde disco no sea descartada
            // por el dedupe (el archivo pudo re-guardarse con contenido nuevo).
            System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] HandleCaptureSaved: visor oculto/minimizado - carga diferida al restaurar");
            _currentFilePath = null;
            StatusText = $"Captura guardada: {Path.GetFileName(filePath)} (se mostrará al restaurar el visor)";
            System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] HandleCaptureSaved: SALIDA - método completado (diferido)");
            return;
        }

        // Visor visible: actualización en vivo SIN traer al frente ni robar foco.
        System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] HandleCaptureSaved: visor visible - cargando captura en vivo");
        LoadImageFromFile(filePath);
        StatusText = $"Captura cargada: {Path.GetFileName(filePath)}";
        UpdateHistoryEnabled();
        System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] HandleCaptureSaved: SALIDA - método completado");
    }

    /// <summary>
    /// Receptor del bus <see cref="SqaEvents.RestoreViewerRequested"/>. Se dispara
    /// desde el tray ("Abrir Visor", clic/doble clic en el icono) o el trigger UI-only
    /// del listener HTTP. Delega en <see cref="ShowLastCapture"/> (regla de oro:
    /// al traer el visor al frente se muestra la última captura guardada en disco).
    /// </summary>
    private void HandleRestoreViewerRequested()
    {
        System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] HandleRestoreViewerRequested: ENTRADA");
        ShowLastCapture();
        System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] HandleRestoreViewerRequested: SALIDA");
    }

    /// <summary>
    /// Regla de oro: cuando el usuario trae el visor al frente, se visualiza la última
    /// captura realizada/guardada en disco. Carga la última evidencia persistida
    /// (repositorio → disco → visor) y asegura la visibilidad de la ventana: Show()
    /// si está oculta al tray, WindowState Normal si está minimizada, y Activate()
    /// para traerla al frente. Seguro para invocar desde cualquier hilo (mariscal
    /// al Dispatcher). No-op de carga si no hay evidencias: solo muestra la ventana.
    /// </summary>
    public void ShowLastCapture()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(ShowLastCapture);
            return;
        }

        System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] ShowLastCapture: ENTRADA");

        // 1. Última captura desde el repositorio (ordenado por fecha descendente).
        IReadOnlyList<EvidenceRecord> recent = _repository.GetRecentEvidences(1);
        if (recent.Count > 0)
        {
            LoadImageFromFile(recent[0].FilePath);
        }
        else
        {
            StatusText = "Sin capturas: aún no hay evidencias en " + _repository.FolderPath;
        }

        // 2. Asegurar visibilidad: Show + Normal + Activate (visor oculto al tray o minimizado).
        if (Application.Current?.MainWindow is Window mainWindow)
        {
            if (!mainWindow.IsVisible || mainWindow.WindowState == WindowState.Minimized)
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
            }
            mainWindow.Activate();
        }

        UpdateHistoryEnabled();

        System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] ShowLastCapture: SALIDA");
    }

    private void LoadImageFromFile(string filePath)
    {
        System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] LoadImageFromFile: ENTRADA - filepath: " + filePath);

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            StatusText = "La captura notificada ya no existe en disco.";
            System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] LoadImageFromFile: archivo no existe o path nulo, retornando");
            return;
        }

        // Dedupe: la misma captura ya está visible.
        if (_currentFilePath != null &&
            string.Equals(_currentFilePath, filePath, StringComparison.OrdinalIgnoreCase))
        {
            System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] LoadImageFromFile: DEDUPE - misma captura ya visible (_currentFilePath: " + _currentFilePath + "), retornando sin recargar");
            return;
        }

        try
        {
            StatusText = "Nueva captura recibida, cargando…";

            BitmapImage bitmap = new();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            _currentFilePath = filePath;
            _currentTileIndex = -1;
            ShowViewer();
            ReplaceCurrent(bitmap);
StatusText = $"Nueva captura cargada: {Path.GetFileName(filePath)}";
            System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] LoadImageFromFile: EXITOSO - imagen cargada y MostrarViewer llamado");
        }
        catch (Exception ex)
        {
            StatusText = $"No se pudo cargar la captura: {ex.Message}";
            System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] LoadImageFromFile: ERROR - " + ex.Message);
        }
    }

    // ============================================================
    // Editor embebido (misma ventana: sin parpadeos ni ventanas secundarias)
    // ============================================================

    /// <summary>
    /// Abre el EDITOR EMBEBIDO con la imagen actual del visor. No hay proceso
    /// separado, ni copia temporal, ni redibujado: el BitmapSource ya decodificado
    /// se entrega al EditorViewModel (preservación de contexto, cero flickeo).
    /// "Guardar (in-place)" escribe sobre el archivo original; al volver, el visor
    /// recarga el resultado desde disco si hubo cambios.
    /// </summary>
    private void EditDelegated()
    {
        if (_currentSource == null || IsEditorVisible)
        {
            return;
        }

        ResetModes();
        var editorVm = new EditorViewModel(toast: _toast);
        editorVm.LoadFromBitmapSource(_currentSource, _currentFilePath);
        Editor = editorVm;
        HeaderTitleText = "Editor de capturas";
        StatusText = "Editor: dibuja anotaciones, recorta y guarda (Ctrl+S). Usa V/F/C/T/H/B/R para las herramientas.";
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>Cierra el editor embebido y vuelve al visor, recargando si hubo cambios.</summary>
    public void CloseEditor()
    {
        if (!IsEditorVisible)
        {
            return;
        }

        EditorViewModel? vm = Editor;
        Editor = null; // oculta la vista ANTES de liberar (evita renderizar un VM muerto)
        string? savedPath = vm?.FilePath;
        bool dirty = vm?.IsDirty ?? false;
        vm?.Dispose();

        HeaderTitleText = IsHistoryViewVisible ? "Historial de Evidencias" : "Visor de Evidencias";
        ToggleHistoryButtonText = IsHistoryViewVisible ? "Volver al visor" : "Ver historial";
        NotifyModeChanged();

        if (dirty && savedPath != null && File.Exists(savedPath))
        {
            _ = ReloadFromFileAsync(savedPath); // recarga la versión editada (guardado in-place)
        }
        else
        {
            StatusText = "Editor cerrado.";
        }
    }

    /// <summary>Cierra el editor SIN tocar títulos/estado (lo hace el caller: ShowViewer/ResetModes).</summary>
    private void ForceCloseEditor()
    {
        if (!IsEditorVisible)
        {
            return;
        }

        EditorViewModel? vm = Editor;
        Editor = null;
        vm?.Dispose();
    }

    private async Task ReloadFromFileAsync(string file)
    {
        if (!File.Exists(file))
        {
            return;
        }

        try
        {
            StatusText = "Recargando imagen editada…";

            BitmapImage bitmap = new();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(file);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            _currentFilePath = file;
            await DisplayImageAsync(bitmap);
            StatusText = "Listo: imagen editada recargada.";
        }
        catch (Exception ex)
        {
            StatusText = $"No se pudo recargar la edición: {ex.Message}";
        }
    }

    // ============================================================
    // Exportación (imagen individual)
    // ============================================================

    private async void SaveAs()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Guardar evidencia",
            Filter = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg",
            DefaultExt = ".png",
            InitialDirectory = _repository.FolderPath
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            if (_currentSource is BitmapSource bmp)
            {
                await Task.Run(() => WicHelper.SaveBitmapSource(bmp, dialog.FileName));
            }
            else if (_currentFilePath != null)
            {
                File.Copy(_currentFilePath, dialog.FileName, overwrite: true);
            }

            StatusText = $"Guardado: {dialog.FileName}";
            _toast.Show($"Guardado: {Path.GetFileName(dialog.FileName)}", ToastType.Success);
        }
        catch (Exception ex)
        {
            StatusText = $"No se pudo guardar: {ex.Message}";
            _toast.Show($"No se pudo guardar", ToastType.Error);
        }
    }

    // ============================================================
    // Galería / Historial (vista conmutada)
    // ============================================================

    /// <summary>Guía rápida del visor (accesible desde el botón "?" del encabezado).</summary>
    public void ShowHelp()
    {
        StatusText = "Guía: Abrir… (archivo) · Pegar (Ctrl+V, imágenes) · Editar… (módulo editor) · "
                     + "Guardar como… · Ver historial (galería de evidencias). "
                     + "En el visor: clic = alternar 100% / ajustar, arrastra = pan, Escape = reset zoom.";
    }

    private void ToggleHistoryView()
    {
        if (IsHistoryViewVisible)
        {
            ShowViewer();
        }
        else
        {
            // Transición instantánea: se activa la vista inmediatamente
            IsHistoryViewVisible = true;
            ResetModes();
            StatusText = $"Galería: {HistoryTiles.Count} evidencias en {_repository.FolderPath}";
            // Sincronización delta no bloqueante en background
            _ = RefreshHistoryGridAsync();
        }
    }

    private void ShowViewer()
    {
        System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] ShowViewer: ENTRADA - IsHistoryViewVisible actual: " + IsHistoryViewVisible);
        ForceCloseEditor(); // si el editor embebido estaba abierto (p.ej. nueva captura)
        IsHistoryViewVisible = false;
        ResetModes();
        System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] ShowViewer: SALIDA - IsHistoryViewVisible ahora: " + IsHistoryViewVisible);
    }

    /// <summary>Resetea la máquina de estados de modos (títulos incluidos).</summary>
    private void ResetModes()
    {
        System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] ResetModes: ENTRADA - IsInformeModeActive: " + IsInformeModeActive + ", IsSelectionModeActive: " + IsSelectionModeActive);
        _moduleState = null;
        IsModulePhaseActive = false;
        ModulePhaseText = null;
        IsSelectionModeActive = false;
        IsInformeModeActive = false;
        SelectedIds.Clear();
        foreach (EvidenceTileModel tile in HistoryTiles)
        {
            tile.IsSelected = false;
            tile.SelectionOrder = null;
            tile.IsFocused = false;
        }

        FocusedIndex = -1;
        RangeAnchor = null;

        HeaderTitleText = IsHistoryViewVisible ? "Historial de Evidencias" : "Visor de Evidencias";
        ToggleHistoryButtonText = IsHistoryViewVisible ? "Volver al visor" : "Ver historial";
        NotifyModeChanged();
        System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] ResetModes: SALIDA - modos reseteados");
    }

    private void NotifyModeChanged()
    {
        OnPropertyChanged(nameof(SelectionCounterText));
        OnPropertyChanged(nameof(IsNormalMode));
        OnPropertyChanged(nameof(IsPlainSelectionMode));
        OnPropertyChanged(nameof(IsInformeOptionsMode));
        OnPropertyChanged(nameof(IsInformeSelectionMode));
        OnPropertyChanged(nameof(IsModulePhaseVisible));
        OnPropertyChanged(nameof(IsTileActionsEnabled));
        OnPropertyChanged(nameof(IsViewerMode));
        OnPropertyChanged(nameof(IsHistoryMode));
        OnPropertyChanged(nameof(IsEditorVisible));
        // Re-evalúa los CanExecute (Copiar/Descargar/Eliminar/Generar informe)
        // al cambiar la selección (RequerySuggested no se dispara solo con
        // propiedades no-DP; paridad con btn.disabled de updateHistoryUI).
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Reescanea la carpeta y sincroniza las tarjetas de forma inteligente y asíncrona (delta update):
    /// - Escaneo de disco en hilo secundario (cero congelamiento de UI).
    /// - Si la galería ya está al día, NO recrea elementos visuales (transición 100% instantánea a 60/120fps).
    /// - Miniaturas decodificadas y cacheadas en background sin bloquear la navegación.
    /// </summary>
    private int _historyLoadGeneration;

    public void RefreshHistoryGrid() => _ = RefreshHistoryGridAsync();

    public async Task RefreshHistoryGridAsync()
    {
        int generation = ++_historyLoadGeneration;
        string thumbDir = Path.Combine(_repository.FolderPath, ".thumbs");

        // 1. Escaneo I/O en background thread
        IReadOnlyList<EvidenceRecord> records = await Task.Run(() =>
        {
            try
            {
                return _repository.GetRecentEvidences(75);
            }
            catch
            {
                return (IReadOnlyList<EvidenceRecord>)Array.Empty<EvidenceRecord>();
            }
        });

        if (generation != _historyLoadGeneration)
        {
            return;
        }

        // 2. Comprobar si los registros coinciden exactamente con lo que ya está en memoria
        bool identical = (records.Count == HistoryTiles.Count);
        if (identical)
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (!string.Equals(records[i].FilePath, HistoryTiles[i].FilePath, StringComparison.OrdinalIgnoreCase) ||
                    records[i].Id != HistoryTiles[i].Id)
                {
                    identical = false;
                    break;
                }
            }
        }

        if (identical)
        {
            UpdateHistoryEnabled();
            // Si hay miniaturas pendientes por decodificar, lanzarlas en background
            var pendingThumbs = HistoryTiles.Where(t => t.Thumbnail == null).ToList();
            if (pendingThumbs.Count > 0)
            {
                _ = LoadThumbnailsBackgroundAsync(pendingThumbs, thumbDir, generation);
            }
            return;
        }

        // 3. Sincronización delta conservando miniaturas ya cargadas
        var existingMap = HistoryTiles.ToDictionary(t => t.FilePath, StringComparer.OrdinalIgnoreCase);
        List<EvidenceTileModel> newTiles = new();
        List<EvidenceTileModel> tilesNeedingThumb = new();

        foreach (EvidenceRecord rec in records)
        {
            if (existingMap.TryGetValue(rec.FilePath, out EvidenceTileModel? existing))
            {
                existing.Id = rec.Id;
                existing.EvidenceCode = rec.EvidenceCode;
                existing.FormattedDate = rec.CreatedAt.ToString("g");
                newTiles.Add(existing);
                if (existing.Thumbnail == null)
                {
                    tilesNeedingThumb.Add(existing);
                }
            }
            else
            {
var tile = new EvidenceTileModel
                {
                    Id = rec.Id,
                    EvidenceCode = rec.EvidenceCode,
                    FilePath = rec.FilePath,
                    FormattedDate = rec.CreatedAt.ToString("g"),
                    OriginSite = rec.OriginUrl,
                    Thumbnail = null
                };
                newTiles.Add(tile);
                tilesNeedingThumb.Add(tile);
            }
        }

        // Actualizar ObservableCollection de forma limpia
        HistoryTiles.Clear();
        foreach (var tile in newTiles)
        {
            HistoryTiles.Add(tile);
        }

        StatusText = $"Galería: {HistoryTiles.Count} evidencias en {_repository.FolderPath}";
        UpdateHistoryEnabled();

        // 4. Decodificación de miniaturas en paralelo en background
        if (tilesNeedingThumb.Count > 0)
        {
            _ = LoadThumbnailsBackgroundAsync(tilesNeedingThumb, thumbDir, generation);
        }
    }

    private async Task LoadThumbnailsBackgroundAsync(List<EvidenceTileModel> tiles, string thumbDir, int generation)
    {
        try
        {
            await Task.Run(() =>
            {
                Parallel.ForEach(tiles,
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    tile =>
                    {
                        if (generation != _historyLoadGeneration || !File.Exists(tile.FilePath))
                        {
                            return;
                        }

                        BitmapSource? thumb = WicHelper.LoadThumbnailCached(tile.FilePath, thumbDir, maxPixelWidth: 320);
                        if (thumb != null)
                        {
                            Application.Current?.Dispatcher.BeginInvoke(() =>
                            {
                                if (generation == _historyLoadGeneration && HistoryTiles.Contains(tile))
                                {
                                    tile.Thumbnail = thumb;
                                }
                            }, System.Windows.Threading.DispatcherPriority.Background);
                        }
                    });
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SQA-THUMB] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Recalcula si el historial tiene contenido (habilita el botón "Ver historial").
    /// Fuente: la galería en memoria + un escaneo ligero del disco (1 evidencia).
    /// Barato: el repositorio escanea la carpeta local, no decodifica imágenes.
    /// </summary>
    private void UpdateHistoryEnabled()
    {
        IsHistoryEnabled = HistoryTiles.Count > 0 || _repository.GetRecentEvidences(1).Count > 0;
    }

    // ============================================================
    // Máquina de estados: Selección / Informe
    // ============================================================

    /// <summary>Entra al modo selección (toolbar completa de selección).</summary>
    private void EnterSelectionMode()
    {
        if (HistoryTiles.Count == 0)
        {
            StatusText = "No hay evidencias para seleccionar.";
            return;
        }

        ClearSelection();
        IsSelectionModeActive = true;
        HeaderTitleText = "Seleccionar Evidencias";
        StatusText = "Modo selección: haz clic en las tarjetas para marcarlas.";
        NotifyModeChanged();
    }

    /// <summary>Entra al modo informe (barra de opciones Completo/Seleccionado/Por módulos).</summary>
    private void EnterInformeMode()
    {
        if (HistoryTiles.Count == 0)
        {
            StatusText = "No hay evidencias para exportar.";
            return;
        }

        ClearSelection();
        IsSelectionModeActive = false;
        IsInformeModeActive = true;
        HeaderTitleText = "Informe de Evidencias";
        StatusText = "Modo informe: elige Completo, Seleccionado o Por módulos.";
        NotifyModeChanged();
    }

    /// <summary>
    /// "Cancelar" contextual (paridad con Electron: Escape/btn Cancel en módulo Informe):
    /// - Fase módulos → cancela export por módulos (vuelve a opciones).
    /// - Selección DENTRO de informe → vuelve a barra de opciones (mantiene IsInformeModeActive).
    /// - Opciones de informe (sin selección) → SALE del modo informe, vuelve al historial.
    /// - Selección simple (historial normal) → cancela selección, vuelve a normal.
    /// </summary>
    public void CancelMode()
    {
        if (IsModulePhaseActive || _moduleState != null)
        {
            CancelModuleExport();
            return;
        }

        if (IsInformeModeActive)
        {
            if (IsSelectionModeActive)
            {
                // Selección dentro de informe → volver a opciones (mantiene IsInformeModeActive).
                IsSelectionModeActive = false;
                ClearSelection();
                HeaderTitleText = "Informe de Evidencias";
                StatusText = "Modo informe: elige Completo, Seleccionado o Por módulos.";
                NotifyModeChanged();
            }
            else
            {
                // Opciones de informe (sin selección) → SALIR del modo informe, volver al historial.
                IsHistoryViewVisible = true; // asegura vista historial
                ResetModes();                // apaga IsInformeModeActive, limpia selección, título historial
                StatusText = "Modo informe cancelado.";
            }
        }
        else
        {
            // Selección simple en historial normal.
            IsSelectionModeActive = false;
            ClearSelection();
            HeaderTitleText = "Historial de Evidencias";
            StatusText = "Modo selección cancelado.";
        }
    }

    /// <summary>Limpia selectedIds y las tarjetas (orden, check, foco, ancla).</summary>
    private void ClearSelection()
    {
        SelectedIds.Clear();
        foreach (EvidenceTileModel tile in HistoryTiles)
        {
            tile.IsSelected = false;
            tile.SelectionOrder = null;
        }

        FocusedIndex = -1;
        RangeAnchor = null;
        NotifyModeChanged();
    }

    /// <summary>
    /// Clic en una tarjeta: en modo selección alterna la selección (con Shift
    /// selecciona por RANGO, replicando app.js:3180-3205); fuera de modo
    /// selección abre el visor. El clic siempre actualiza el foco DESPUÉS de
    /// calcular el ancla del rango (el ancla es el foco PREVIO, spec §4).
    /// </summary>
    private void HandleTileClick(EvidenceTileModel? tile)
    {
        if (tile == null)
        {
            return;
        }

        int index = HistoryTiles.IndexOf(tile);

        if (IsSelectionModeActive)
        {
            if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
            {
                SelectRange(tile, index);
            }
            else
            {
                ToggleSelect(tile);
                RangeAnchor = null; // nuevo ancla en el próximo Shift+Click
            }
            if (index >= 0)
            {
                FocusedIndex = index;
            }
            return;
        }

        if (index >= 0)
        {
            FocusedIndex = index;
        }
        OpenTileInViewer(tile);
    }

    /// <summary>
    /// Selección por rango (Shift+Click): desde el ancla (rango previo o el
    /// último item enfocado) hasta el item clickeado, ambos inclusive.
    /// Los items ya seleccionados conservan su posición de orden original.
    /// </summary>
    private void SelectRange(EvidenceTileModel clickedTile, int clickedIndex)
    {
        int anchor = RangeAnchor ?? FocusedIndex;
        if (anchor < 0)
        {
            anchor = clickedIndex;
        }

        int start = Math.Min(anchor, clickedIndex);
        int end = Math.Max(anchor, clickedIndex);
        for (int i = start; i <= end; i++)
        {
            EvidenceTileModel tile = HistoryTiles[i];
            if (!SelectedIds.Contains(tile.Id))
            {
                SelectedIds.Add(tile.Id);
            }
            tile.IsSelected = true;
        }

        RangeAnchor = anchor; // expansión desde el MISMO ancla en Shift+Click sucesivos
        RefreshSelectionOrders();
        NotifyModeChanged();
        StatusText = $"{SelectedIds.Count} seleccionadas.";
    }

    /// <summary>Alterna la selección de un item; mantiene el orden de inserción en SelectedIds.</summary>
    private void ToggleSelect(EvidenceTileModel tile)
    {
        if (SelectedIds.Remove(tile.Id))
        {
            tile.IsSelected = false;
            tile.SelectionOrder = null;
            RefreshSelectionOrders(); // reindexa 1..N: cierra el gap (spec §10 "badges se reordenan")
        }
        else
        {
            SelectedIds.Add(tile.Id);
            tile.IsSelected = true;
            tile.SelectionOrder = SelectedIds.Count;
        }

        NotifyModeChanged();
    }

    /// <summary>Reindexa SelectionOrder (1..N) de todas las tarjetas según SelectedIds.</summary>
    private void RefreshSelectionOrders()
    {
        foreach (EvidenceTileModel tile in HistoryTiles)
        {
            tile.SelectionOrder = null;
        }

        int order = 1;
        foreach (int id in SelectedIds)
        {
            EvidenceTileModel? tile = HistoryTiles.FirstOrDefault(t => t.Id == id);
            if (tile != null)
            {
                tile.SelectionOrder = order++;
            }
        }
    }

    // ============================================================
    // Navegación por teclado de la galería (app.js:3120-3190)
    // ============================================================

    /// <summary>Mueve el foco +-delta (clamp a [0, Count-1]).</summary>
    public void MoveFocus(int delta)
    {
        if (HistoryTiles.Count == 0)
        {
            return;
        }

        int target = FocusedIndex < 0 ? 0 : FocusedIndex + delta;
        FocusedIndex = Math.Clamp(target, 0, HistoryTiles.Count - 1);
    }

    /// <summary>Home/End: foco al primer/último item.</summary>
    public void FocusEdge(bool first)
    {
        if (HistoryTiles.Count == 0)
        {
            return;
        }

        FocusedIndex = first ? 0 : HistoryTiles.Count - 1;
    }

    /// <summary>
    /// Enter/Space: toggle selección del item enfocado (si hay foco). Fuera
    /// de modo selección el Enter abre el item en el visor (paridad natural).
    /// </summary>
    public void SelectFocused()
    {
        if (FocusedIndex < 0 || FocusedIndex >= HistoryTiles.Count)
        {
            return;
        }

        EvidenceTileModel tile = HistoryTiles[FocusedIndex];
        if (IsSelectionModeActive)
        {
            ToggleSelect(tile);
            RangeAnchor = null;
        }
        else
        {
            OpenTileInViewer(tile);
        }
    }

    /// <summary>Abre una evidencia de la galería en el visor (image directa, sin header).</summary>
    private void OpenTileInViewer(EvidenceTileModel? tile)
    {
        if (tile == null || !File.Exists(tile.FilePath))
        {
            StatusText = "La evidencia ya no existe en disco.";
            return;
        }

        try
        {
            StatusText = "Cargando evidencia…";

            // Cargar imagen directamente (sin header corporativo)
            BitmapImage bitmap = new();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(tile.FilePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            _currentFilePath = tile.FilePath;
            _currentTileIndex = HistoryTiles.IndexOf(tile);
            CurrentImage = bitmap;
            ShowViewer();
            StatusText = $"Abierto: {tile.EvidenceCode} ({tile.FormattedDate})";
        }
        catch (Exception ex)
        {
            StatusText = $"No se pudo abrir la evidencia: {ex.Message}";
        }
    }

    // ============================================================
    // Toolbar del visor: Anterior/Siguiente (navegación corta)
    // ============================================================

    /// <summary>Navegación previa/siguiente sobre la galería cargada (solo visor).</summary>
    private bool CanNavigate(int delta)
    {
        if (HistoryTiles.Count == 0)
        {
            return false;
        }

        int target = _currentTileIndex < 0 ? 0 : _currentTileIndex + delta;
        return target >= 0 && target < HistoryTiles.Count;
    }

    private void GoPrevious() => NavigateTo(_currentTileIndex - 1);

    private void GoNext() => NavigateTo(_currentTileIndex + 1);

    private void NavigateTo(int index)
    {
        if (HistoryTiles.Count == 0)
        {
            RefreshHistoryGrid();
        }

        if (HistoryTiles.Count == 0)
        {
            StatusText = "No hay evidencias en el historial para navegar.";
            return;
        }

        index = _currentTileIndex < 0 ? 0 : Math.Clamp(index, 0, HistoryTiles.Count - 1);
        NavigatingToCapture?.Invoke();   // preserve zoom+scroll (spec §3.2)
        OpenTileInViewer(HistoryTiles[index]);
    }

    // ============================================================
    // Toolbar del visor: Copiar / Descargar / Eliminar (imagen actual)
    // ============================================================

    /// <summary>Copia la imagen actual del visor al portapapeles.</summary>
    private void CopyCurrent()
    {
        if (_currentSource == null)
        {
            StatusText = "No hay imagen en el visor.";
            return;
        }

        Clipboard.SetImage(_currentSource!);
        StatusText = "Imagen actual copiada al portapapeles.";
        _toast.Show("Imagen copiada al portapapeles", ToastType.Success);
    }

    /// <summary>Descarga (guarda) la imagen actual del visor como PNG/JPG.</summary>
    private async void DownloadCurrent()
    {
        if (_currentSource == null && _currentFilePath == null)
        {
            StatusText = "No hay imagen en el visor.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Descargar imagen actual",
            FileName = _currentFilePath != null ? Path.GetFileName(_currentFilePath) : "evidencia.png",
            Filter = "PNG (*.png)|*.png|Todos los archivos (*.*)|*.*",
            DefaultExt = ".png"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            if (_currentSource is BitmapSource bmp)
            {
                await Task.Run(() => WicHelper.SaveBitmapSource(bmp, dialog.FileName));
            }
            else if (_currentFilePath != null)
            {
                File.Copy(_currentFilePath, dialog.FileName, overwrite: true);
            }

            StatusText = $"Descargada: {dialog.FileName}";
            _toast.Show($"Descargada: {Path.GetFileName(dialog.FileName)}", ToastType.Success);
        }
        catch (Exception ex)
        {
            StatusText = $"No se pudo descargar: {ex.Message}";
            _toast.Show($"No se pudo descargar", ToastType.Error);
        }
    }

    /// <summary>Elimina la evidencia que el visor está mostrando (si vino de la galería).</summary>
    private void DeleteCurrent()
    {
        if (_currentFilePath == null)
        {
            StatusText = "La captura actual aún no está guardada en la galería; no se puede eliminar.";
            return;
        }

        EvidenceTileModel? tile = HistoryTiles.FirstOrDefault(t =>
            string.Equals(t.FilePath, _currentFilePath, StringComparison.OrdinalIgnoreCase));
        if (tile == null)
        {
            StatusText = "La evidencia actual ya no está en el historial.";
            return;
        }

        DeleteTile(tile);
        _currentFilePath = null;
        _currentTileIndex = -1;

        if (HistoryTiles.Count == 0)
        {
            // Flujo C (última evidencia, spec §3.2): convergencia al estado vacío:
            // visor en empty state + navegación al visor (loadCapture sin capturas).
            ApplyEmptyViewerState();
            SqaEvents.RaiseCapturesCleared();
        }
        else
        {
            // Regla estándar de visores: PRIORIDAD A LA SIGUIENTE captura.
            // 1) Si existe captura POSTERIOR a la eliminada -> mostrar la siguiente inmediata.
            // 2) Si NO existe posterior pero existe ANTERIOR -> mostrar la anterior inmediata.
            // 3) Si no queda ninguna -> estado vacío.
            static bool TryGetNumber(EvidenceTileModel t, out int n)
            {
                n = 0;
                string code = t.EvidenceCode;
                int idx = code.LastIndexOf('_');
                if (idx < 0 || idx >= code.Length - 1) return false;
                return int.TryParse(code.Substring(idx + 1), out n);
            }

            if (TryGetNumber(tile, out int deletedNumber))
            {
                // 1) Buscar captura SIGUIENTE (número > eliminado), la más cercana (mínimo mayor)
                EvidenceTileModel? nextTile = HistoryTiles
                    .Where(t => TryGetNumber(t, out int n) && n > deletedNumber && File.Exists(t.FilePath))
                    .OrderBy(t => { TryGetNumber(t, out int n); return n; })
                    .FirstOrDefault();

                if (nextTile != null)
                {
                    OpenTileInViewer(nextTile);
                }
                else
                {
                    // 2) No hay siguiente -> buscar ANTERIOR inmediata (máximo menor)
                    EvidenceTileModel? prevTile = HistoryTiles
                        .Where(t => TryGetNumber(t, out int n) && n < deletedNumber && File.Exists(t.FilePath))
                        .OrderByDescending(t => { TryGetNumber(t, out int n); return n; })
                        .FirstOrDefault();

                    if (prevTile != null)
                    {
                        OpenTileInViewer(prevTile);
                    }
                    else
                    {
                        // 3) Fallback: primera disponible por número
                        EvidenceTileModel? fallbackTile = HistoryTiles
                            .Where(t => File.Exists(t.FilePath))
                            .OrderBy(t => { TryGetNumber(t, out int n); return n; })
                            .FirstOrDefault();
                        if (fallbackTile != null)
                        {
                            OpenTileInViewer(fallbackTile);
                        }
                        else
                        {
                            ApplyEmptyViewerState();
                        }
                    }
                }
            }
            else
            {
                // Sin número parseable -> fallback por Id ascendente
                EvidenceTileModel? fallbackTile = HistoryTiles
                    .Where(t => File.Exists(t.FilePath))
                    .OrderBy(t => t.Id)
                    .FirstOrDefault();
                if (fallbackTile != null)
                {
                    OpenTileInViewer(fallbackTile);
                }
                else
                {
                    ApplyEmptyViewerState();
                }
            }
        }
    }

    /// <summary>Copia la imagen de la evidencia al portapapeles.</summary>
    private void CopyTile(EvidenceTileModel? tile)
    {
        if (IsSelectionModeActive)
        {
            return;
        }

        if (tile == null || !File.Exists(tile.FilePath))
        {
            return;
        }

        try
        {
            BitmapSource source = WicHelper.LoadFrozenImageSource(tile.FilePath);
            Clipboard.SetImage(source);
            StatusText = $"Copiada: {tile.EvidenceCode}";
            _toast.Show($"Copiada: {tile.EvidenceCode}", ToastType.Success);
        }
        catch (Exception ex)
        {
            StatusText = $"No se pudo copiar: {ex.Message}";
            _toast.Show($"No se pudo copiar", ToastType.Error);
        }
    }

    private void DownloadTile(EvidenceTileModel? tile)
    {
        if (IsSelectionModeActive)
        {
            return;
        }

        if (tile == null || !File.Exists(tile.FilePath))
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Descargar evidencia",
            FileName = Path.GetFileName(tile.FilePath),
            Filter = "PNG (*.png)|*.png|Todos los archivos (*.*)|*.*",
            DefaultExt = ".png"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            File.Copy(tile.FilePath, dialog.FileName, overwrite: true);
            StatusText = $"Descargada: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"No se pudo descargar: {ex.Message}";
        }
    }

    private async void DeleteTile(EvidenceTileModel? tile)
    {
        if (IsSelectionModeActive)
        {
            return;
        }

        if (tile == null)
        {
            return;
        }

        if (!await _confirmation.AskAsync(
                $"¿Eliminar la evidencia \"{tile.EvidenceCode}\"?\nEsta acción no se puede deshacer.",
                "Eliminar captura", "Eliminar", danger: true))
        {
            return;
        }

        _repository.DeleteEvidence(tile.Id, tile.FilePath);
        SelectedIds.Remove(tile.Id);
        HistoryTiles.Remove(tile);
        RefreshSelectionOrders();
        NotifyModeChanged();
        UpdateHistoryEnabled();
        StatusText = $"Eliminada: {tile.EvidenceCode}";
        _toast.Show($"Eliminada: {tile.EvidenceCode}", ToastType.Success);
    }

    /// <summary>
    /// Descarga todas las evidencias como un archivo .zip nombrado con la
    /// convención Evidencias_DDMMAA.ZIP. Igual que la descarga individual: dispara
    /// el diálogo nativo "Guardar como..." (SaveFileDialog) con la carpeta Downloads
    /// preseleccionada por defecto, y el usuario elige la ubicación de destino.
    /// </summary>
    public void DownloadAll()
    {
        LogDownload("[SQA-DOWNLOAD] DownloadAll() START");
        LogDownload($"[SQA-DOWNLOAD] HistoryTiles.Count = {HistoryTiles.Count}");

        if (HistoryTiles.Count == 0)
        {
            StatusText = "No hay evidencias para descargar.";
            LogDownload("[SQA-DOWNLOAD] No tiles - exit");
            return;
        }

        try
        {
            string downloadsFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string? downloadsPath = Path.Combine(downloadsFolder, "Downloads");
            if (!Directory.Exists(downloadsPath))
            {
                downloadsPath = downloadsFolder;
            }

            LogDownload($"[SQA-DOWNLOAD] downloadsPath = {downloadsPath}");

            string dateStamp = DateTime.Now.ToString("ddMMyy");
            string zipName = $"Evidencias_{dateStamp}.zip";

            // Diálogo nativo "Guardar como...": Downloads preseleccionada por defecto.
            var dialog = new SaveFileDialog
            {
                Title = "Descargar todas las evidencias",
                FileName = zipName,
                InitialDirectory = downloadsPath,
                Filter = "ZIP (*.zip)|*.zip|Todos los archivos (*.*)|*.*",
                DefaultExt = ".zip"
            };

            if (dialog.ShowDialog() != true)
            {
                LogDownload("[SQA-DOWNLOAD] Dialog cancelled");
                return;
            }

            string zipPath = dialog.FileName;
            LogDownload($"[SQA-DOWNLOAD] zipPath = {zipPath}");

            // Si el archivo ya existe, se sobrescribe (zip con nombre de fecha actual).
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
                LogDownload("[SQA-DOWNLOAD] Deleted existing file");
            }

            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            LogDownload($"[SQA-DOWNLOAD] tempDir = {tempDir}");

            int copied = 0;
            try
            {
                foreach (EvidenceTileModel tile in HistoryTiles)
                {
                    LogDownload($"[SQA-DOWNLOAD] Processing tile: {tile.FilePath}");
                    if (!File.Exists(tile.FilePath)) 
                    {
                        LogDownload($"[SQA-DOWNLOAD] File not found: {tile.FilePath}");
                        continue;
                    }
                    try
                    {
                        string destFile = Path.Combine(tempDir, Path.GetFileName(tile.FilePath));
                        File.Copy(tile.FilePath, destFile, overwrite: true);
                        copied++;
                        LogDownload($"[SQA-DOWNLOAD] Copied {tile.FilePath} to {destFile}");
                    }
                    catch (Exception copyEx)
                    {
                        LogDownload($"[SQA-DOWNLOAD] Copy failed for {tile.FilePath}: {copyEx.Message}");
                    }
                }

                LogDownload($"[SQA-DOWNLOAD] Total copied: {copied}");

                if (copied > 0)
                {
                    System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, zipPath);
                    StatusText = $"Descargado: {copied} evidencias a {zipPath}";
                    _toast.Show($"Descargado: {copied} evidencias", ToastType.Success);
                    LogDownload($"[SQA-DOWNLOAD] ZIP created successfully at {zipPath}");
                }
                else
                {
                    StatusText = "No se pudieron copiar las evidencias al zip.";
                    LogDownload("[SQA-DOWNLOAD] No files were copied");
                    return;
                }
            }
            finally
            {
                try { Directory.Delete(tempDir, true); }
                catch (Exception delEx)
                {
                    LogDownload($"[SQA-DOWNLOAD] Failed to delete temp dir: {delEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"No se pudo crear el zip: {ex.Message}";
            LogDownload($"[SQA-DOWNLOAD] Exception: {ex}");
        }
    }

    /// <summary>
    /// Traza de diagnóstico de descarga: escribe SIEMPRE a %TEMP%\sqa_download.log
    /// (visible sin depurador) y además a Debug.
    /// </summary>
    private static void LogDownload(string message)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(Path.GetTempPath(), "sqa_download.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\r\n");
        }
        catch
        {
            // El log nunca debe romper la descarga.
        }
        System.Diagnostics.Debug.WriteLine(message);
    }

    private async void ClearAll()
    {
        if (HistoryTiles.Count == 0)
        {
            return;
        }

        if (!await _confirmation.AskAsync(
                $"¿Eliminar TODAS las evidencias ({HistoryTiles.Count})?\nEsta acción no se puede deshacer.",
                "Eliminar captura", "Eliminar todo", danger: true))
        {
            return; // cancelado: no-op total, nada se muta (spec §2.1)
        }

        // Bloqueo de UI durante la operación (equivale al showLoading de Electron,
        // spec §2.1/§6.4): cursor de espera + borrado fuera del hilo UI.
        var mainWindow = Application.Current?.MainWindow;
        Cursor? previousCursor = mainWindow?.Cursor;
        if (mainWindow != null)
        {
            mainWindow.Cursor = Cursors.Wait;
        }

        try
        {
            ClearAllResult outcome = await Task.Run(_repository.ClearAll);

            if (!outcome.Success || outcome.RemainingCount > 0)
            {
                // Borrado parcial (archivos bloqueados/sin permisos): NO se vacía el
                // estado (guarda maxFileNum === 0, spec §5 #6). La galería se
                // re-escanea para reflejar la realidad del disco — nunca un historial
                // vacío en pantalla con archivos huérfanos (spec §4).
                RefreshHistoryGrid();
                StatusText = outcome.RemainingCount > 0
                    ? $"Eliminadas {outcome.DeletedCount}; {outcome.RemainingCount} no se pudieron eliminar (en uso o sin permisos)."
                    : $"Error al eliminar todo: {outcome.Error}";
                return;
            }

            // Camino feliz: limpieza transaccional en un solo lugar + navegación
            // incondicional al visor (spec §2.3/§4).
            ApplyEmptyViewerState();
            SqaEvidenceSequence.ResetIfFolderEmpty(_repository.FolderPath);
            SqaEvents.RaiseCapturesCleared();
            StatusText = $"Historial vaciado: {outcome.DeletedCount} evidencias eliminadas.";
            _toast.Show($"Historial vaciado: {outcome.DeletedCount} evidencias eliminadas", ToastType.Success);
        }
        catch (Exception ex)
        {
            // Operación falló: no mutar nada (spec §5 #2).
            StatusText = $"Error al eliminar todo: {ex.Message}";
            _toast.Show($"Error al eliminar todo", ToastType.Error);
        }
        finally
        {
            if (mainWindow != null)
            {
                mainWindow.Cursor = previousCursor ?? Cursors.Arrow;
            }
        }
    }

    /// <summary>
    /// Estado final convergente del borrado TOTAL (spec §1: los flujos A, B y C
    /// terminan igual): historial vacío + visor en empty state + navegación
    /// SIEMPRE al visor, venga de donde venga. Único lugar que vacía el visor;
    /// idempotente (doble invocación no rompe nada). Solo se llama cuando la
    /// carpeta quedó realmente vacía (guarda maxFileNum === 0).
    /// </summary>
    private void ApplyEmptyViewerState()
    {
        HistoryTiles.Clear();
        SelectedIds.Clear();
        ResetModes();                       // selección / informe / módulo + títulos
        _currentFilePath = null;
        _currentTileIndex = -1;
        CurrentImage = null;                // visor en empty state (Source null)
        ShowViewer();                       // switch incondicional al visor
        IsHistoryEnabled = false;           // sin evidencias: conmutador deshabilitado
        CommandManager.InvalidateRequerySuggested();
    }

    // ============================================================
    // Acciones en lote (modo selección)
    // ============================================================

    private IEnumerable<EvidenceTileModel> GetSelectedTiles()
    {
        // selectedIds mantiene el orden de inserción: se recorre en ese orden.
        foreach (int id in SelectedIds)
        {
            EvidenceTileModel? tile = HistoryTiles.FirstOrDefault(t => t.Id == id);
            if (tile != null && File.Exists(tile.FilePath))
            {
                yield return tile;
            }
        }
    }

    /// <summary>
    /// Copia TODAS las seleccionadas al portapapeles en el ORDEN de selección,
    /// construyendo un DataObject multipropósito para compatibilidad máxima:
    ///   - FileDropList con las rutas en orden (chats web: Teams/WhatsApp/Slack, Explorer).
    ///   - CF_HTML con imágenes base64 (editores de texto enriquecido).
    ///   - CF_RTF con UN \pict (imagen incrustada) por evidencia, apiladas una debajo
    ///     de otra en párrafos separados: formato nativo de Microsoft Word, que no
    ///     interpreta FileDrop ni data-URIs de HTML como gráficos.
    ///   - La primera imagen como mapa de bits (BitmapSource nativo con respaldo GDI+)
    ///     para editores gráficos (Paint) y habilitar "Pegar" en Word.
    ///   - Formato propio "SQA_MULTI_IMAGE".
    /// La asignación se hace en el Dispatcher de UI con flush (copy: true) para que la
    /// aplicación destino lea los datos completos de inmediato; ante fallos transitorios
    /// de OLE/CLIPBRD_E_CANT_OPEN se reintenta con backoff corto.
    /// </summary>
    public async void CopySelected()
    {
        var tiles = GetSelectedTiles().ToList();
        if (tiles.Count == 0)
        {
            StatusText = "No hay evidencias seleccionadas.";
            return;
        }

        try
        {
            StatusText = $"Copiando {tiles.Count} evidencias…";

            var fileList = new System.Collections.Specialized.StringCollection();
            foreach (var tile in tiles)
            {
                fileList.Add(tile.FilePath);
            }

            (string htmlContent, string rtfContent) = await Task.Run(() =>
            {
                // Builder puro en Core: HTML con <div> por imagen (bloque) y RTF con
                // \pict por párrafo + \par explícito → apilado vertical en el pegado.
                EvidenciasSQA.Core.ClipboardBuilder.MultiImageClipboardContent content =
                    EvidenciasSQA.Core.ClipboardBuilder.MultiImageClipboardBuilder.Build(
                        tiles.Select(t => t.FilePath).ToArray(),
                        tiles.Select(t => t.EvidenceCode).ToArray());
                return (content.HtmlFragment, content.RtfContent);
            });

            var data = new DataObject();
            data.SetFileDropList(fileList);
            data.SetData(DataFormats.Html, htmlContent);
            data.SetData(DataFormats.Rtf, rtfContent);
            data.SetData("SQA_MULTI_IMAGE", tiles.Select(t => t.FilePath).ToArray());

            // Mapa de bits de la primera imagen: Word habilita "Pegar" solo si el
            // portapapeles expone un formato gráfico estándar (CF_DIB/CF_BITMAP).
            // Respaldo con GDI+ si la decodificación WIC fallara; nunca en silencio.
            System.Drawing.Bitmap? gdiBitmap = null;
            try
            {
                BitmapSource firstBmp = WicHelper.LoadFrozenImageSource(tiles[0].FilePath);
                data.SetImage(firstBmp);
            }
            catch (Exception bmpEx)
            {
                System.Diagnostics.Debug.WriteLine($"[SQA-COPY] WIC fallo para bitmap del portapapeles: {bmpEx.Message}");
                try
                {
                    gdiBitmap = new System.Drawing.Bitmap(tiles[0].FilePath);
                    data.SetData(DataFormats.Bitmap, gdiBitmap);
                }
                catch (Exception gdiEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[SQA-COPY] GDI+ fallo para bitmap del portapapeles: {gdiEx.Message}");
                    gdiBitmap = null;
                }
            }

            // CLIPBRD_E_CANT_OPEN es un fallo transitorio común cuando otra app retiene el
            // clipboard: se reintenta con backoff corto antes de rendirse. La asignación se
            // envuelve en el Dispatcher de la UI (seguridad de hilos ante invocación desde
            // flujos asíncronos) y se usa copy: true (flush) para que el destino —Word en
            // particular— lea los datos serializados completos en lugar de servirlos perezosamente.
            // Si el flush fallara de forma persistente, el último intento cae a copy: false
            // (servido perezoso) para no perder el pegado en chats por un fallo de serialización.
            bool ok = false;
            bool flushedOk = false;
            Exception? lastError = null;
            for (int attempt = 0; attempt < 5 && !ok; attempt++)
            {
                try
                {
                    bool flush = attempt < 4;
                    System.Windows.Application.Current.Dispatcher.Invoke(() => Clipboard.SetDataObject(data, flush));
                    ok = true;
                    flushedOk = flush;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    await Task.Delay(100 * (attempt + 1));
                }
            }

            // Con flush el clipboard ya copió los datos: el bitmap GDI+ se puede liberar.
            // En el fallback perezoso (copy: false) debe permanecer vivo mientras la app
            // sirva el formato (liberación mínima y acotada a ese camino excepcional).
            if (flushedOk && gdiBitmap != null)
            {
                gdiBitmap.Dispose();
            }

            StatusText = ok
                ? $"Copiadas {tiles.Count} evidencias al portapapeles (orden de selección)."
                : $"No se pudieron copiar: {lastError?.Message}";
            if (ok)
            {
                _toast.Show($"Copiadas {tiles.Count} evidencias al portapapeles", ToastType.Success);
            }
            else
            {
                _toast.Show($"No se pudieron copiar", ToastType.Error);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"No se pudieron copiar: {ex.Message}";
            _toast.Show($"No se pudieron copiar", ToastType.Error);
        }
}
    /// <summary>
    /// Descarga las evidencias SELECCIONADAS como un archivo .zip con el MISMO
    /// flujo, tipo y nombre que "Descargar todo" (Fase 17): diálogo nativo
    /// "Guardar como..." (SaveFileDialog) con la carpeta Downloads preseleccionada,
    /// nombre por convención Evidencias_DDMMAA.zip y sobrescritura del archivo
    /// existente.
    /// </summary>
    private void DownloadSelected()
    {
        LogDownload("[SQA-DOWNLOAD] DownloadSelected() START");
        var tiles = GetSelectedTiles().ToList();
        LogDownload($"[SQA-DOWNLOAD] Selected tiles = {tiles.Count}");
        if (tiles.Count == 0)
        {
            StatusText = "No hay evidencias seleccionadas.";
            LogDownload("[SQA-DOWNLOAD] No selected tiles - exit");
            return;
        }

        try
        {
            string downloadsFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string? downloadsPath = Path.Combine(downloadsFolder, "Downloads");
            if (!Directory.Exists(downloadsPath))
            {
                downloadsPath = downloadsFolder;
            }

            LogDownload($"[SQA-DOWNLOAD] downloadsPath = {downloadsPath}");

            string dateStamp = DateTime.Now.ToString("ddMMyy");
            string zipName = $"Evidencias_{dateStamp}.zip";

            // Diálogo nativo "Guardar como...": Downloads preseleccionada por defecto
            // (mismo flujo que DownloadAll).
            var dialog = new SaveFileDialog
            {
                Title = "Descargar evidencias seleccionadas",
                FileName = zipName,
                InitialDirectory = downloadsPath,
                Filter = "ZIP (*.zip)|*.zip|Todos los archivos (*.*)|*.*",
                DefaultExt = ".zip"
            };

            if (dialog.ShowDialog() != true)
            {
                LogDownload("[SQA-DOWNLOAD] Dialog cancelled");
                return;
            }

            string zipPath = dialog.FileName;
            LogDownload($"[SQA-DOWNLOAD] zipPath = {zipPath}");

            // Si el archivo ya existe, se sobrescribe (zip con nombre de fecha actual).
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
                LogDownload("[SQA-DOWNLOAD] Deleted existing file");
            }

            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            LogDownload($"[SQA-DOWNLOAD] tempDir = {tempDir}");

            int copied = 0;
            try
            {
                foreach (EvidenceTileModel tile in tiles)
                {
                    LogDownload($"[SQA-DOWNLOAD] Processing tile: {tile.FilePath}");
                    if (!File.Exists(tile.FilePath))
                    {
                        LogDownload($"[SQA-DOWNLOAD] File not found: {tile.FilePath}");
                        continue;
                    }
                    try
                    {
                        string destFile = Path.Combine(tempDir, Path.GetFileName(tile.FilePath));
                        File.Copy(tile.FilePath, destFile, overwrite: true);
                        copied++;
                        LogDownload($"[SQA-DOWNLOAD] Copied {tile.FilePath} to {destFile}");
                    }
                    catch (Exception copyEx)
                    {
                        LogDownload($"[SQA-DOWNLOAD] Copy failed for {tile.FilePath}: {copyEx.Message}");
                    }
                }

                LogDownload($"[SQA-DOWNLOAD] Total copied: {copied}");

                if (copied > 0)
                {
                    System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, zipPath);
                    StatusText = $"Descargado: {copied} evidencias a {zipPath}";
                    _toast.Show($"Descargado: {copied} evidencias", ToastType.Success);
                    LogDownload($"[SQA-DOWNLOAD] ZIP created successfully at {zipPath}");
                }
                else
                {
                    StatusText = "No se pudieron copiar las evidencias al zip.";
                    LogDownload("[SQA-DOWNLOAD] No files were copied");
                    return;
                }
            }
            finally
            {
                try { Directory.Delete(tempDir, true); }
                catch (Exception delEx)
                {
                    LogDownload($"[SQA-DOWNLOAD] Failed to delete temp dir: {delEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = $"No se pudo crear el zip: {ex.Message}";
            LogDownload($"[SQA-DOWNLOAD] Exception: {ex}");
        }
    }

    private async void DeleteSelected()
    {
        var tiles = GetSelectedTiles().ToList();
        if (tiles.Count == 0)
        {
            StatusText = "No hay evidencias seleccionadas.";
            return;
        }

        if (!await _confirmation.AskAsync(
                $"¿Eliminar las {tiles.Count} evidencias seleccionadas?\nEsta acción no se puede deshacer.",
                "Eliminar captura", "Eliminar", danger: true))
        {
            return;
        }

        int deleted = 0;
        foreach (EvidenceTileModel tile in tiles)
        {
            try
            {
                _repository.DeleteEvidence(tile.Id, tile.FilePath);
                HistoryTiles.Remove(tile);
                SelectedIds.Remove(tile.Id);
                deleted++;
            }
            catch
            {
                // Skip errors during deletion
            }
        }

        ClearSelection();
        IsSelectionModeActive = false;
        HeaderTitleText = "Historial de Evidencias";
        NotifyModeChanged();

        if (HistoryTiles.Count == 0)
        {
            // Flujo B (selección completa, spec §3.1): convergencia al estado vacío.
            ApplyEmptyViewerState();
            SqaEvents.RaiseCapturesCleared();
        }
        else
        {
            // Borrado parcial: si la captura activa del visor fue eliminada, el visor
            // no debe conservar una imagen sin archivo físico (spec §4, consistencia).
            if (_currentFilePath != null && !File.Exists(_currentFilePath))
            {
                _currentFilePath = null;
                _currentTileIndex = -1;
                CurrentImage = null;
            }

            UpdateHistoryEnabled();
            StatusText = $"Eliminadas {deleted} evidencias.";
            _toast.Show($"Eliminadas {deleted} evidencias", ToastType.Success);
        }
    }

    // ============================================================
    // Exportación Word (modal HU + progreso)
    // ============================================================

    /// <summary>Arranca la exportación según el modo elegido en la barra de informe.</summary>
    private async void StartExport(ExportKind kind)
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        if (mainWindow == null)
        {
            StatusText = "Error: ventana principal no disponible.";
            return;
        }

        var result = await mainWindow.ShowHuModalAsync();
        if (!result.success)
        {
            return;
        }

        var hu = new WordHuInfo(result.huId, result.huName, Environment.UserName);
        string outputPath = Environment.GetEnvironmentVariable("SQA_TEST_SAVEPATH") ?? string.Empty;

        if (string.IsNullOrEmpty(outputPath))
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "Guardar documento de evidencias",
                Filter = "Word (*.docx)|*.docx",
                DefaultExt = ".docx",
                FileName = $"Soporte_Evidencias_{result.huId}.docx"
            };

            if (saveDialog.ShowDialog() != true)
            {
                return;
            }

            outputPath = saveDialog.FileName;
        }

        if (kind == ExportKind.Completo)
        {
            ExportWordAsync(outputPath, hu, HistoryTiles.ToList());
        }
        else
        {
            ExportWordAsync(outputPath, hu, GetSelectedTiles().ToList());
        }
    }

    private async void ExportWordAsync(string outputPath, WordHuInfo hu, IReadOnlyList<EvidenceTileModel> tiles)
    {
        if (tiles.Count == 0)
        {
            StatusText = "No hay evidencias para exportar.";
            return;
        }

        IsExporting = true;
        ExportProgress = 0;
        ExportStatusText = "Generando documento Word…";

        var progress = new Progress<double>(p => ExportProgress = p);

        // Ordenar por ID de captura ascendente (menor a mayor) para informe completo
        // Extraer número de secuencia del EvidenceCode (ej: "Evidencias_17" -> 17)
        var orderedTiles = tiles
            .Select(t => new { Tile = t, Seq = ExtractSequenceNumber(t.EvidenceCode) })
            .OrderBy(x => x.Seq)
            .Select(x => x.Tile)
            .ToList();

        var items = orderedTiles.Select(t => new WordEvidenceItem(
            t.FilePath,
            t.EvidenceCode,
            t.FormattedDate,
            t.OriginSite,
            "Evidencia SQA")).ToList();

        try
        {
            await Task.Run(() => WordReportBuilder.BuildDocument(outputPath, hu, items, progress));
            ExportStatusText = $"Documento generado: {Path.GetFileName(outputPath)}";
            StatusText = $"Informe generado: {outputPath}";
            _toast.Show($"Informe generado: {Path.GetFileName(outputPath)}", ToastType.Success);
        }
        catch (Exception ex)
        {
            ExportStatusText = $"Error al generar el documento: {ex.Message}";
            StatusText = $"Error al exportar: {ex.Message}";
            _toast.Show($"Error al generar el documento", ToastType.Error);
        }
        finally
        {
            IsExporting = false;
            ExportProgress = 0;
        }
    }

    // ============================================================
    // Exportación por módulos (casos de prueba)
    // ============================================================

    private async void StartModuleExport()
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        if (mainWindow == null)
        {
            StatusText = "Error: ventana principal no disponible.";
            return;
        }

        var result = await mainWindow.ShowModuleCountModalAsync();
        if (!result.success)
        {
            return;
        }

        _moduleState = new ModuleExportState { Total = result.count };
        ClearSelection();
        IsSelectionModeActive = true;
        StartModulePhase();
    }

    private void StartModulePhase()
    {
        if (_moduleState == null || _moduleState.Containers.Count > MaxModules)
        {
            return;
        }

        int modNum = _moduleState.CurrentIdx + 1;
        IsModulePhaseActive = true;
        ModulePhaseText = $"Caso de prueba {modNum} de {_moduleState.Total} — selecciona sus evidencias y presiona OK";
        HeaderTitleText = "Informe de Evidencias";
        StatusText = ModulePhaseText;
        NotifyModeChanged();
    }

    private void ConfirmModulePhase()
    {
        if (_moduleState == null)
        {
            return;
        }

        if (SelectedIds.Count == 0)
        {
            StatusText = "Debes seleccionar al menos una evidencia para este caso de prueba.";
            return;
        }

        _moduleState.Containers.Add(SelectedIds.ToList());
        _moduleState.CurrentIdx++;

        if (_moduleState.CurrentIdx < _moduleState.Total)
        {
            ClearSelection();
            StartModulePhase();
        }
        else
        {
            FinishModuleSelection();
        }
    }

    private async void FinishModuleSelection()
    {
        if (_moduleState == null || _moduleState.Containers.Count == 0)
        {
            CancelModuleExport();
            return;
        }

        IsModulePhaseActive = false;
        ModulePhaseText = null;
        IsSelectionModeActive = false;
        ClearSelection();
        NotifyModeChanged();

        var mainWindow = Application.Current.MainWindow as MainWindow;
        if (mainWindow == null)
        {
            CancelModuleExport();
            return;
        }

        var result = await mainWindow.ShowHuModalAsync();
        if (!result.success)
        {
            CancelModuleExport();
            return;
        }

        var hu = new WordHuInfo(result.huId, result.huName, Environment.UserName);
        string outputPath = Environment.GetEnvironmentVariable("SQA_TEST_SAVEPATH") ?? string.Empty;

        if (string.IsNullOrEmpty(outputPath))
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "Guardar documento de casos de prueba",
                Filter = "Word (*.docx)|*.docx",
                DefaultExt = ".docx",
                FileName = $"Soporte_Evidencias_{result.huId}.docx"
            };

            if (saveDialog.ShowDialog() != true)
            {
                CancelModuleExport();
                return;
            }

            outputPath = saveDialog.FileName;
        }

        ExportModulesAsync(outputPath, hu);
    }

    private async void ExportModulesAsync(string outputPath, WordHuInfo hu)
    {
        if (_moduleState == null || _moduleState.Containers.Count == 0)
        {
            return;
        }

        var modules = new List<WordModule>();
        for (int i = 0; i < _moduleState.Containers.Count; i++)
        {
            var tiles = new List<EvidenceTileModel>();
            foreach (int id in _moduleState.Containers[i])
            {
                EvidenceTileModel? tile = HistoryTiles.FirstOrDefault(t => t.Id == id);
                if (tile != null && File.Exists(tile.FilePath))
                {
                    tiles.Add(tile);
                }
            }

            if (tiles.Count > 0)
            {
                modules.Add(new WordModule($"Caso de prueba {i + 1}",
                    tiles.Select(t => new WordEvidenceItem(t.FilePath, t.EvidenceCode, t.FormattedDate, t.OriginSite, "Evidencia SQA")).ToList()));
            }
        }

        if (modules.Count == 0)
        {
            StatusText = "No hay capturas válidas para exportar.";
            return;
        }

        IsExporting = true;
        ExportProgress = 0;
        ExportStatusText = "Generando documento por casos de prueba…";

        var progress = new Progress<double>(p => ExportProgress = p);
        try
        {
            await Task.Run(() => WordReportBuilder.BuildModulesDocument(outputPath, hu, modules, progress));
            ExportStatusText = $"Documento generado: {Path.GetFileName(outputPath)}";
            StatusText = $"Informe por módulos generado: {outputPath}";
        }
        catch (Exception ex)
        {
            ExportStatusText = $"Error al generar el documento: {ex.Message}";
            StatusText = $"Error al exportar: {ex.Message}";
        }
        finally
        {
            IsExporting = false;
            ExportProgress = 0;
        }
    }

    /// <summary>Cancela la exportación por módulos (replica cancelModuleExport de la app Electron).</summary>
    private void CancelModuleExport()
    {
        _moduleState = null;
        IsModulePhaseActive = false;
        ModulePhaseText = null;
        IsSelectionModeActive = false;
        ClearSelection();
        HeaderTitleText = "Historial de Evidencias";
        StatusText = "Exportación por casos de prueba cancelada.";
        NotifyModeChanged();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SqaEvents.CaptureSaved -= HandleCaptureSaved;
        SqaEvents.RestoreViewerRequested -= HandleRestoreViewerRequested;
        _editor?.Dispose();
        _editor = null;
    }

    /// <summary>Extrae el número de secuencia del código de evidencia (ej: "Evidencias_17" -> 17).</summary>
    private static int ExtractSequenceNumber(string evidenceCode)
    {
        if (string.IsNullOrWhiteSpace(evidenceCode))
            return int.MaxValue;

        int idx = evidenceCode.LastIndexOf('_');
        if (idx < 0 || idx >= evidenceCode.Length - 1)
            return int.MaxValue;

        if (int.TryParse(evidenceCode.Substring(idx + 1), out int num))
            return num;

        return int.MaxValue;
    }
}
