using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using EvidenciasSQA.Core.Interfaces;
using EvidenciasSQA.Core.Model;

namespace EvidenciasSQA.Core.Export;

/// <summary>
/// Destino portapapeles: copia la captura horneada como PNG en el clipboard.
/// Mismo espíritu que EvidenciasSQA ClipboardHelper: se publica el stream PNG
/// (formato con pérdida cero y soporte alfa) para que las apps lo reciban.
/// </summary>
public sealed class ClipboardDestination : AbstractDestination
{
    public override string Designation => "Clipboard";

    public override string Description => "Copiar al portapapeles";

    public override int Priority => 10;

    public override ExportInformation ExportCapture(bool manuallyInitiated, SurfaceDocument surface, CaptureDetails? captureDetails)
    {
        var exportInformation = new ExportInformation(Designation, Description);

        try
        {
            using Bitmap export = surface.GetImageForExport();
            using MemoryStream pngStream = new();
            export.Save(pngStream, ImageFormat.Png);

            var dataObject = new DataObject();
            // WPF no expone DataFormats.Png: se usa el nombre de formato estándar "PNG"
            // (mismo que usa EvidenciasSQA ClipboardHelper para máxima compatibilidad).
            dataObject.SetData(DataFormats.GetDataFormat("PNG").Name, pngStream);
            dataObject.SetData(DataFormats.Bitmap, export);
            Clipboard.SetDataObject(dataObject, copy: true);

            exportInformation.ExportMade = true;
        }
        catch (Exception ex)
        {
            exportInformation.ErrorMessage = ex.Message;
        }

        ProcessExport(exportInformation, surface);
        return exportInformation;
    }
}
