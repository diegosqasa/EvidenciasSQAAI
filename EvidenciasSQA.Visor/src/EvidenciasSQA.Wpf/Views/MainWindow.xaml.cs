using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EvidenciasSQA.Core.Services;
using EvidenciasSQA.Wpf.Controls;
using EvidenciasSQA.Wpf.ViewModels;

namespace EvidenciasSQA.Wpf.Views;

public partial class MainWindow : Window
{
    private readonly ViewerViewModel _viewModel;
    private ConfirmationRequest? _pendingConfirmation;

    public MainWindow(ViewerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        ViewerControl.ZoomChanged += (_, percent) => _viewModel.UpdateZoomPercent(percent);

        // Toast in-app: los view models publican mensajes transitorios y el host los anima.
        ToastService.Instance.ToastRequested += ShowToast;
        Closed += (_, _) => ToastService.Instance.ToastRequested -= ShowToast;

        // Confirmación in-app (regla de oro: overlay dentro de la misma ventana,
        // réplica del .confirm-overlay web; sin ventanas nativas nuevas).
        ConfirmationService.Instance.ConfirmationRequested += ShowConfirm;
        Closed += (_, _) => ConfirmationService.Instance.ConfirmationRequested -= ShowConfirm;

        // Info bar dinámica del visor (paridad con updateZoomInfo del Electron):
        // en zoom 100% muestra "Zoom activo | Arrastrar para desplazar | Escape para salir".
        ViewerControl.ZoomModeChanged += mode => _viewModel.ViewerInfoText = mode == ZoomMode.Natural
            ? "Zoom activo | Arrastrar para desplazar | Escape para salir"
            : "Vista de Evidencia | Copiar (Ctrl+C)";

        // Navegación ◀▶: preservar zoom + scroll (spec §3.2).
        // PreserveZoomState se dispara ANTES de cargar la captura siguiente
        // (evento del VM); RestoreZoomState DESPUÉS de que el binding aplicó la
        // imagen nueva (Dispatcher a prioridad DataBind, tras ShowImage del DP).
        _viewModel.NavigatingToCapture += () => ViewerControl.PreserveZoomState();
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewerViewModel.CurrentImage))
            {
                Dispatcher.BeginInvoke(new Action(() => ViewerControl.RestoreZoomState()),
                    DispatcherPriority.DataBind);
            }
        };
    }

    /// <summary>Muestra el toast in-app (reemplazo único, spec web).</summary>
    private void ShowToast(ToastMessage message)
    {
        if (ToastHost != null)
        {
            ToastHost.Show(message);
        }
    }

    /// <summary>Muestra el modal de confirmación como overlay dentro de la ventana (usa BaseModal).</summary>
    private void ShowConfirm(ConfirmationRequest request)
    {
        System.Diagnostics.Debug.WriteLine($"[MainWindow] ShowConfirm called: Title={request.Title}, IsDanger={request.IsDanger}");
        _pendingConfirmation = request;
        ConfirmModal.Title = request.Title;
        if (ConfirmModal.BodyContentElement?.Content is TextBlock messageText)
        {
            messageText.Text = request.Message;
        }
        if (ConfirmModal.FooterBorderElement?.Child is StackPanel footerPanel)
        {
            var acceptButton = footerPanel.Children.OfType<Button>().FirstOrDefault(b => b.Content?.ToString() == "Aceptar");
            if (acceptButton != null)
            {
                acceptButton.Content = request.AcceptLabel;
                acceptButton.Style = request.IsDanger
                    ? (Style)FindResource("ConfirmDangerAcceptButtonStyle")
                    : (Style)FindResource("ConfirmAcceptButtonStyle");
            }
        }
        System.Diagnostics.Debug.WriteLine($"[MainWindow] Setting ConfirmModal.IsOpen = true");
        ConfirmModal.IsOpen = true;
        // Enfocar el botón de aceptar después de abrir
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (ConfirmModal.FooterBorderElement?.Child is StackPanel fp)
            {
                var ab = fp.Children.OfType<Button>().FirstOrDefault(b => b.Content?.ToString() == "Aceptar" || b.Content?.ToString() == "Eliminar" || b.Content?.ToString() == "Eliminar todo" || b.Content?.ToString() == "Exportar a Word" || b.Content?.ToString() == "Comenzar");
                ab?.Focus();
            }
        }), DispatcherPriority.Loaded);
    }

    private void ConfirmModal_Closed(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[MainWindow] ConfirmModal_Closed event received");
        // Se invoca cuando la animación de cierre termina
        if (_pendingConfirmation != null)
        {
            // Solo resolver si no se resolvió ya por un botón
            _pendingConfirmation.TryResolve(false);
            _pendingConfirmation = null;
        }
    }

    private void CloseConfirm(bool accepted)
    {
        System.Diagnostics.Debug.WriteLine($"[MainWindow] CloseConfirm called: accepted={accepted}");
        if (_pendingConfirmation != null)
        {
            _pendingConfirmation.Resolve(accepted);
            _pendingConfirmation = null;
        }

        System.Diagnostics.Debug.WriteLine($"[MainWindow] Setting ConfirmModal.IsOpen = false");
        ConfirmModal.IsOpen = false;
    }

    private void ConfirmAccept_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[MainWindow] ConfirmAccept_Click - ConfirmModal.IsOpen={ConfirmModal.IsOpen}, ConfirmModal.RootGrid.Vis={ConfirmModal.RootGrid.Visibility}");
        CloseConfirm(true);
    }

    private void ConfirmCancel_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[MainWindow] ConfirmCancel_Click - ConfirmModal.IsOpen={ConfirmModal.IsOpen}, ConfirmModal.RootGrid.Vis={ConfirmModal.RootGrid.Visibility}");
        CloseConfirm(false);
    }

    private void ConfirmClose_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[MainWindow] ConfirmClose_Click - ConfirmModal.IsOpen={ConfirmModal.IsOpen}, ConfirmModal.RootGrid.Vis={ConfirmModal.RootGrid.Visibility}");
        CloseConfirm(false);
    }

    // ============================================================
    // Modal HU (usa BaseModal)
    // ============================================================
    private TaskCompletionSource<(bool success, string huId, string huName)>? _huModalTcs;

    public Task<(bool success, string huId, string huName)> ShowHuModalAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[MainWindow] ShowHuModalAsync called");
        _huModalTcs = new TaskCompletionSource<(bool, string, string)>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (HuModal.BodyContentElement?.Content is HuModalContent huContent)
        {
            huContent.ClearErrors();
        }
        System.Diagnostics.Debug.WriteLine($"[MainWindow] Setting HuModal.IsOpen = true");
        HuModal.IsOpen = true;
        if (HuModal.BodyContentElement?.Content is HuModalContent huContent2)
        {
            huContent2.Focus();
        }
        return _huModalTcs.Task;
    }

    private void HuModal_Closed(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[MainWindow] HuModal_Closed event received");
        if (_huModalTcs != null && !_huModalTcs.Task.IsCompleted)
        {
            _huModalTcs.TrySetResult((false, "", ""));
        }
    }

    private void HuModalExport_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[MainWindow] HuModalExport_Click");
        if (HuModal.BodyContentElement?.Content is HuModalContent huContent && huContent.Validate())
        {
            _huModalTcs?.TrySetResult((true, huContent.HuId, huContent.HuName));
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Setting HuModal.IsOpen = false (Export)");
            HuModal.IsOpen = false;
        }
    }

    private void HuModalCancel_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[MainWindow] HuModalCancel_Click - HuModal.IsOpen={HuModal.IsOpen}, HuModal.RootGrid.Vis={HuModal.RootGrid.Visibility}");
        _huModalTcs?.TrySetResult((false, "", ""));
        System.Diagnostics.Debug.WriteLine($"[MainWindow] Setting HuModal.IsOpen = false (Cancel)");
        HuModal.IsOpen = false;
    }

    // ============================================================
    // Modal Module Count (usa BaseModal)
    // ============================================================
    private TaskCompletionSource<(bool success, int count)>? _moduleCountModalTcs;

    public Task<(bool success, int count)> ShowModuleCountModalAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[MainWindow] ShowModuleCountModalAsync called");
        _moduleCountModalTcs = new TaskCompletionSource<(bool, int)>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (ModuleCountModal.BodyContentElement?.Content is ModuleCountModalContent mcContent)
        {
            mcContent.CountErrorText.Visibility = Visibility.Collapsed;
        }
        System.Diagnostics.Debug.WriteLine($"[MainWindow] Setting ModuleCountModal.IsOpen = true");
        ModuleCountModal.IsOpen = true;
        ModuleCountModal.Focus();
        return _moduleCountModalTcs.Task;
    }

    private void ModuleCountModal_Closed(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[MainWindow] ModuleCountModal_Closed event received");
        if (_moduleCountModalTcs != null && !_moduleCountModalTcs.Task.IsCompleted)
        {
            _moduleCountModalTcs.TrySetResult((false, 0));
        }
    }

    private void ModuleCountModalAccept_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[MainWindow] ModuleCountModalAccept_Click - ModuleCountModal.IsOpen={ModuleCountModal.IsOpen}, ModuleCountModal.RootGrid.Vis={ModuleCountModal.RootGrid.Visibility}");
        if (ModuleCountModal.BodyContentElement?.Content is ModuleCountModalContent mcContent && mcContent.Validate())
        {
            _moduleCountModalTcs?.TrySetResult((true, mcContent.ModuleCount));
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Setting ModuleCountModal.IsOpen = false (Accept)");
            ModuleCountModal.IsOpen = false;
        }
    }

    private void ModuleCountModalCancel_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[MainWindow] ModuleCountModalCancel_Click - ModuleCountModal.IsOpen={ModuleCountModal.IsOpen}, ModuleCountModal.RootGrid.Vis={ModuleCountModal.RootGrid.Visibility}");
        _moduleCountModalTcs?.TrySetResult((false, 0));
        System.Diagnostics.Debug.WriteLine($"[MainWindow] Setting ModuleCountModal.IsOpen = false (Cancel)");
        ModuleCountModal.IsOpen = false;
    }

    // ============================================================
    // Event handlers for UI buttons (mantener compatibilidad con XAML existente)
    // ============================================================

    private void BtnRecopilar_Click(object sender, RoutedEventArgs e)
    {
        // Versión Visor-Puro: la captura está deshabilitada.
        // La UI se mantiene intacta, pero la funcionalidad de captura fue eliminada.
    }

    private static void OpenCapturesFolder()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "CapturasQA");
        try
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show("No se pudo abrir la carpeta de capturas", ToastType.Error);
            Debug.WriteLine($"[SQA] Abrir CapturasQA falló: {ex.Message}");
        }
    }

    private void MenuGrabarPantalla_Click(object sender, RoutedEventArgs e)
    {
        // Versión Visor-Puro: la captura está deshabilitada.
    }

    private void MenuExtraerTexto_Click(object sender, RoutedEventArgs e)
    {
        // Versión Visor-Puro: la captura está deshabilitada.
    }

    private void BtnDownloadAll_Click(object sender, RoutedEventArgs e)
    {
        // Llamada directa al ViewModel: no depende del binding del comando (CommandManager).
        if (DataContext is ViewerViewModel vm)
        {
            vm.DownloadAll();
        }
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewerViewModel vm)
        {
            vm.ShowHelp();
        }
    }

    // ============================================================
    // Barra de zoom del visor (estilo Fotos de Windows)
    // ============================================================

    // ============================================================
    // Atajos de teclado del VISOR (solo interactuación del visor)
    // ============================================================

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        // Modal de confirmación abierto: solo Escape cierra (cancelar). El resto de teclas
        // no llegan a las vistas (equivalente al overlay modal de la web).
        if (ConfirmModal.IsOpen)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] OnPreviewKeyDown - ESC pressed, ConfirmModal.IsOpen={ConfirmModal.IsOpen}");
                CloseConfirm(false);
                e.Handled = true;
            }

            return;
        }

        // Editor embebido abierto: TODAS las teclas son del editor (V/F/C/T/H/B/R,
        // Ctrl+Z/Y/D/C, Delete, Enter/Escape para recorte). El visor no interviene.
        if (_viewModel.IsEditorVisible)
        {
            return;
        }

        if (e.Key == System.Windows.Input.Key.Escape)
        {
            // Escape: si estamos en modo selección o informe, cancelar y volver al historial;
            // si estamos en el visor, resetear zoom.
            if (_viewModel is ViewerViewModel vm)
            {
                if (vm.IsSelectionModeActive || vm.IsInformeModeActive)
                {
                    vm.CancelMode();
                }
                else
                {
                    _viewModel?.ResetViewRequested();
                    ViewerControl?.ResetZoom();
                }
            }
            e.Handled = true;
            return;
        }

        // Ctrl+C: copiar evidencias seleccionadas en historial o captura actual en visor
        if (e.Key == System.Windows.Input.Key.C && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
        {
            if (_viewModel.IsSelectionModeActive && _viewModel.SelectedIds.Count > 0)
            {
                if (_viewModel.CopySelectedCommand.CanExecute(null))
                {
                    _viewModel.CopySelectedCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }
            else if (_viewModel.IsViewerMode && _viewModel.CurrentImage != null)
            {
                if (_viewModel.CopyCurrentCommand.CanExecute(null))
                {
                    _viewModel.CopyCurrentCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }
        }

        // Ctrl+O: abrir carpeta de capturas (menú Archivo). Ctrl+E: ruta Ext Web (por definir).
        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
        {
            if (e.Key == System.Windows.Input.Key.O)
            {
                OpenCapturesFolder();
                e.Handled = true;
                return;
            }

            if (e.Key == System.Windows.Input.Key.E)
            {
                ToastService.Instance.Show("Ruta Ext Web: por definir", ToastType.Info);
                e.Handled = true;
                return;
            }
        }

        // Navegación por teclado de la galería (paridad app.js:3120-3190):
        // activa solo con el historial visible y sin diálogo modal encima.
        if (!_viewModel.IsHistoryViewVisible || !IsEnabled)
        {
            return;
        }

        switch (e.Key)
        {
            case System.Windows.Input.Key.Down:
                MoveGalleryFocus(1);
                e.Handled = true;
                break;
            case System.Windows.Input.Key.Up:
                MoveGalleryFocus(-1);
                e.Handled = true;
                break;
            case System.Windows.Input.Key.Home:
                _viewModel.FocusEdge(first: true);
                ScrollFocusedIntoView();
                e.Handled = true;
                break;
            case System.Windows.Input.Key.End:
                _viewModel.FocusEdge(first: false);
                ScrollFocusedIntoView();
                e.Handled = true;
                break;
            case System.Windows.Input.Key.Enter:
            case System.Windows.Input.Key.Space:
                if (e.Key == System.Windows.Input.Key.Space && e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase)
                {
                    return; // no robar el Space de los botones
                }
                _viewModel.SelectFocused();
                e.Handled = true;
                break;
        }
    }

    private void MoveGalleryFocus(int delta)
    {
        _viewModel.MoveFocus(delta);
        ScrollFocusedIntoView();
    }

    private void ScrollFocusedIntoView()
    {
        if (HistoryItemsControl == null)
        {
            return;
        }

        int index = _viewModel.FocusedIndex;
        if (index < 0)
        {
            return;
        }

        FrameworkElement? container = HistoryItemsControl.ItemContainerGenerator
            .ContainerFromIndex(index) as FrameworkElement;
        container?.BringIntoView();
    }
}