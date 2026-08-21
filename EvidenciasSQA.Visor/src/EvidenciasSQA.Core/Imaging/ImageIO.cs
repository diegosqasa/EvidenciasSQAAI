using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using EvidenciasSQA.Core.Export;
using EvidenciasSQA.Core.Model;

namespace EvidenciasSQA.Core.Imaging;

/// <summary>
/// Motor de persistencia de superficies, análogo a EvidenciasSQA.Base.Core.ImageIO.
///
/// Formato .evidenciasSqa (idéntico al sobre usado por EvidenciasSQAFileFormatHandler):
///   [PNG de la imagen de fondo][XML de los elementos][Int64 longitud del XML]["EvidenciasSQA01.00" (14 bytes)]
/// La imagen PNG tolera datos extra al final (GDI+ los ignora), por lo que el
/// archivo sigue siendo un PNG válido para cualquier visor.
/// </summary>
public static class ImageIO
{
    /// <summary>Marcador de 14 bytes al final del archivo .evidenciasSqa.</summary>
    private const string EvidenciasSqaMarker = "EvidenciasSQA01.00";

    /// <summary>
    /// Guarda la superficie según el formato configurado en settings.
    /// Sobrescribe el archivo si ya existe (el destino ya validó con el usuario).
    /// </summary>
    public static void Save(SurfaceDocument surface, string fullPath, SurfaceOutputSettings settings)
    {
        string extension = Path.GetExtension(fullPath).ToLowerInvariant();
        OutputFormat format = settings.Format;

        if (extension == ".evidenciasSqa" || format == OutputFormat.evidenciasSqa)
        {
            SaveEvidenciasSqaFormat(surface, fullPath);
            return;
        }

        // PNG/JPG: se hornea la superficie completa (fondo + anotaciones).
        using Bitmap export = settings.SaveBackgroundOnly
            ? CloneBitmap(surface.BackgroundBitmap!)
            : surface.GetImageForExport();

        switch (format)
        {
            case OutputFormat.jpg:
                export.Save(fullPath, GetJpegEncoder(), GetJpegParameters(settings.JpgQuality));
                break;
            default:
                export.Save(fullPath, ImageFormat.Png);
                break;
        }
    }

    /// <summary>
    /// Guarda una imagen plana (Bitmap) según el formato configurado.
    /// Pensado para el Visor, que no trabaja con anotaciones: PNG/JPG directos.
    /// </summary>
    public static void Save(Bitmap bitmap, string fullPath, SurfaceOutputSettings settings)
    {
        switch (settings.Format)
        {
            case OutputFormat.jpg:
                bitmap.Save(fullPath, GetJpegEncoder(), GetJpegParameters(settings.JpgQuality));
                break;
            default:
                bitmap.Save(fullPath, ImageFormat.Png);
                break;
        }
    }

    /// <summary>
    /// Carga un archivo (PNG/JPG plano o .evidenciasSqa con anotaciones editables).
    /// El SurfaceDocument devuelto es el dueño del Bitmap de fondo.
    /// </summary>
    public static SurfaceDocument Load(string fullPath)
    {
        if (Path.GetExtension(fullPath).Equals(".evidenciasSqa", StringComparison.OrdinalIgnoreCase))
        {
            return LoadEvidenciasSqaFormat(fullPath);
        }

        using Bitmap loaded = new(fullPath);
        SurfaceDocument surface = new();
        surface.BackgroundBitmap = CloneBitmap(loaded);
        return surface;
    }

    /// <summary>
    /// Guarda el sobre .evidenciasSqa: PNG del fondo + XML + longitud + marcador.
    /// </summary>
    private static void SaveEvidenciasSqaFormat(SurfaceDocument surface, string fullPath)
    {
        if (surface.BackgroundBitmap == null)
        {
            throw new InvalidOperationException("No hay imagen de fondo que guardar.");
        }

        using FileStream file = new(fullPath, FileMode.Create, FileAccess.Write);
        // 1) Fondo como PNG (un visor cualquiera sigue pudiendo abrir el archivo).
        surface.BackgroundBitmap.Save(file, ImageFormat.Png);

        // 2) Metadatos de anotación al final del stream.
        using MemoryStream metadata = new();
        long bytesWritten = surface.SaveElementsToStream(metadata);
        using (BinaryWriter writer = new(metadata, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(bytesWritten);
            writer.Write(Encoding.ASCII.GetBytes(EvidenciasSqaMarker));
        }

        metadata.WriteTo(file);
    }

    /// <summary>
    /// Carga el sobre .evidenciasSqa: PNG de fondo + metadatos de anotación.
    /// </summary>
    private static SurfaceDocument LoadEvidenciasSqaFormat(string fullPath)
    {
        using FileStream file = new(fullPath, FileMode.Open, FileAccess.Read);

        // El PNG tolera la cola de metadatos; GDI+ simplemente la ignora.
        // (validateImageData + color management, como hace EvidenciasSQA).
        using Image tmpImage = Image.FromStream(file, true, true);
        SurfaceDocument surface = new();
        surface.BackgroundBitmap = CloneBitmap(tmpImage);

        // Verificación del marcador.
        file.Position = file.Length - EvidenciasSqaMarker.Length;
        byte[] markerBytes = new byte[EvidenciasSqaMarker.Length];
        file.ReadExactly(markerBytes);
        if (!Encoding.ASCII.GetString(markerBytes).StartsWith("EvidenciasSQA", StringComparison.Ordinal))
        {
            throw new ArgumentException("El archivo no es un .evidenciasSqa válido (marcador ausente).");
        }

        // Longitud del XML y desplazamiento al inicio del bloque de metadatos.
        file.Position = file.Length - EvidenciasSqaMarker.Length - sizeof(long);
        byte[] lengthBytes = new byte[sizeof(long)];
        file.ReadExactly(lengthBytes);
        long bytesWritten = BitConverter.ToInt64(lengthBytes, 0);

        if (bytesWritten <= 0 || bytesWritten > file.Length)
        {
            throw new ArgumentException("El archivo .evidenciasSqa tiene metadatos corruptos.");
        }

        // XmlSerializer debe leer un stream delimitado: se copian exactamente
        // los bytes del XML a un MemoryStream (a diferencia de BinaryFormatter,
        // no puede ignorar los 22 bytes de cola).
        file.Position = file.Length - EvidenciasSqaMarker.Length - sizeof(long) - bytesWritten;
        byte[] xmlBytes = new byte[bytesWritten];
        file.ReadExactly(xmlBytes);

        using MemoryStream xmlStream = new(xmlBytes);
        surface.LoadElementsFromStream(xmlStream);
        return surface;
    }

    /// <summary>
    /// Clona la imagen para que el SurfaceDocument sea el único propietario
    /// (GDI+ mantiene bloqueado el archivo de origen hasta el dispose).
    /// </summary>
    private static Bitmap CloneBitmap(Image source)
    {
        Bitmap clone = new(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(clone);
        g.DrawImage(source, 0, 0, source.Width, source.Height);
        return clone;
    }

    private static ImageCodecInfo GetJpegEncoder()
    {
        ImageCodecInfo[] encoders = ImageCodecInfo.GetImageEncoders();
        return encoders.First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
    }

    private static EncoderParameters GetJpegParameters(int quality)
    {
        EncoderParameters parameters = new(1);
        parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
        return parameters;
    }
}
