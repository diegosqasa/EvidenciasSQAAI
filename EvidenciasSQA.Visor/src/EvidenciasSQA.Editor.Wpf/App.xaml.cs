using System.Windows;
using EvidenciasSQA.Editor.Wpf.ViewModels;
using EvidenciasSQA.Editor.Wpf.Views;

namespace EvidenciasSQA.Editor.Wpf;

/// <summary>
/// Punto de entrada del MÓDULO EDITOR.
/// Modo delegado (lanzado por el Visor): argumento "-file ruta" abre la captura y
/// fija el archivo de guardado in-place para que el Visor recargue el resultado.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var viewModel = new EditorViewModel();
        var window = new EditorWindow(viewModel);

        for (int i = 0; i < e.Args.Length; i++)
        {
            if (e.Args[i] == "--file" && i + 1 < e.Args.Length)
            {
                viewModel.LoadFile(e.Args[i + 1]);
            }
        }

        window.Show();
    }
}
