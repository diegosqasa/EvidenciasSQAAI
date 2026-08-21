using System.Windows;
using EvidenciasSQA.Editor.Wpf.ViewModels;

namespace EvidenciasSQA.Editor.Wpf.Views;

/// <summary>
/// Ventana standalone del MÓDULO EDITOR (exe autónomo). Solo entrega el
/// EditorViewModel a la vista embebible (EditorView) y vigila su ciclo de vida.
/// </summary>
public partial class EditorWindow : Window
{
    private readonly EditorViewModel _viewModel;

    public EditorWindow(EditorViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        EditorHost.DataContext = _viewModel;
        Closed += (_, _) => _viewModel.Dispose();
    }
}