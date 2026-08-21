using System.IO;
using EvidenciasSQA.Core.Imaging;
using EvidenciasSQA.Core.Interfaces;
using EvidenciasSQA.Core.Model;

namespace EvidenciasSQA.Core.Export;

/// <summary>
/// Destino de archivo: guarda la captura como PNG, JPG o .evidenciasSqa
/// (imagen + anotaciones reeditables). Es el FileDestination de EvidenciasSQA,
/// con generación de nombre por patrón y SaveFileDialog cuando el usuario
/// lo inicia manualmente.
/// </summary>
public sealed class FileDestination : AbstractDestination
{
    private readonly OutputFormat _preferredFormat;

    public FileDestination(OutputFormat preferredFormat = OutputFormat.png)
    {
        _preferredFormat = preferredFormat;
    }

    public override string Designation => "File";

    public override string Description => "Guardar en archivo";

    public override int Priority => 0;

    /// <summary>
    /// Punto de entrada del destino. Si ya hay un nombre de archivo conocido
    /// (modo "--file" del Visor o captura con nombre previo) guarda directamente
    /// sin diálogo — mismo comportamiento que el FileDestination de EvidenciasSQA;
    /// en caso contrario presenta el diálogo guardar-como.
    /// </summary>
    public override ExportInformation ExportCapture(bool manuallyInitiated, SurfaceDocument surface, CaptureDetails? captureDetails)
    {
        var exportInformation = new ExportInformation(Designation, Description);

        string? fullPath;
        if (captureDetails?.Filename != null)
        {
            fullPath = captureDetails.Filename; // sobrescritura in-place (modo editor delegado)
        }
        else
        {
            if (!TryShowSaveDialog(out fullPath))
            {
                return exportInformation; // usuario canceló: ExportMade = false
            }
        }

        try
        {
            var outputSettings = new SurfaceOutputSettings { Format = _preferredFormat };
            ImageIO.Save(surface, fullPath, outputSettings);
            exportInformation.ExportMade = true;
            exportInformation.Filepath = fullPath;
            if (captureDetails != null)
            {
                captureDetails.Filename = fullPath;
            }
        }
        catch (Exception ex)
        {
            exportInformation.ErrorMessage = ex.Message;
        }

        ProcessExport(exportInformation, surface);
        return exportInformation;
    }

    /// <summary>
    /// Diálogo guardar-como (Microsoft.Win32.SaveFileDialog de WPF).
    /// En EvidenciasSQA este diálogo vive en la capa WinForms del main app;
    /// aquí se mantiene en el destino para que el prototipo sea autocontenido.
    /// </summary>
    private bool TryShowSaveDialog(out string fullPath)
    {
        string extension = _preferredFormat switch
        {
            OutputFormat.jpg => ".jpg",
            OutputFormat.evidenciasSqa => ".evidenciasSqa",
            _ => ".png"
        };

        string filter = _preferredFormat == OutputFormat.evidenciasSqa
            ? "EvidenciasSQA editable (*.evidenciasSqa)|*.evidenciasSqa"
            : $"{extension.TrimStart('.').ToUpperInvariant()} (*{extension})|*{extension}";

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Guardar captura",
            FileName = CreateNewFilename(),
            Filter = filter,
            DefaultExt = extension,
            AddExtension = true
        };

        if (dialog.ShowDialog() != true)
        {
            fullPath = string.Empty;
            return false;
        }

        fullPath = dialog.FileName;
        return true;
    }

    /// <summary>
    /// Genera un nombre por patrón, como EvidenciasSQA (${capturetime} por defecto).
    /// </summary>
    public static string CreateNewFilename() =>
        $"captura_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
}
