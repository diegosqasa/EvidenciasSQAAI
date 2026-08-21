using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EvidenciasSQA.Core.Imaging;

/// <summary>
/// Puente GDI+ → WPF (WIC). La fuente de verdad de los píxeles es el Bitmap GDI+
/// (filosofía EvidenciasSQA), y solo para mostrarlo en pantalla se crea una ImageSource.
///
/// GESTIÓN DE MEMORIA (importante):
/// GetHbitmap() crea una copia HBITMAP nativa que el GC NO conoce.
/// Si no se libera con DeleteObject() (gdi32), cada conversión fuga un GDI handle,
/// y el proceso termina agotando la mesa de GDI (muy común en apps de captura).
/// El finally garantiza la liberación incluso si CreateBitmapSourceFromHBitmap falla.
/// </summary>
public static class WicHelper
{
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    /// <summary>
    /// Convierte un Bitmap GDI+ a una ImageSource WPF congelada (thread-safe, lista para
    /// OnRender) y libera el HBITMAP intermedio inmediatamente.
    /// </summary>
    public static ImageSource ToImageSource(Bitmap bitmap)
    {
        IntPtr hBitmap = bitmap.GetHbitmap();
        try
        {
            // Calificación completa: "Imaging" aquí chocaría con nuestro propio namespace.
            ImageSource source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            // Freeze: permite usarla desde el hilo de render sin copia defensiva.
            if (source.CanFreeze)
            {
                source.Freeze();
            }

            return source;
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    /// <summary>
    /// Carga una miniatura ligera desde archivo usando el decodificador WIC con
    /// DecodePixelWidth: el JPEG/PNG solo se decodifica a la resolución pedida,
    /// sin descomprimir el bitmap completo en RAM. Resultado congelado.
    /// </summary>
    public static BitmapSource LoadThumbnail(string filePath, int maxPixelWidth = 320)
    {
        var bitmap = new BitmapImage();
        using (FileStream fs = File.OpenRead(filePath))
        {
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = fs;
            bitmap.DecodePixelWidth = maxPixelWidth;
            bitmap.EndInit();
        }

        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Miniatura CON CACHÉ EN DISCO. La primera llamada decodifica el original y
    /// guarda un JPEG 320px en <paramref name="thumbDir"/>; las siguientes cargan
    /// el JPEG cacheado (muchísimo más rápido que re-decodificar el PNG full-HD).
    /// El nombre incluye el timestamp del original: si el archivo cambia, se
    /// regenera sola. Devuelve null si no se pudo generar/leer (el caller decide).
    /// </summary>
    public static BitmapSource? LoadThumbnailCached(string filePath, string? thumbDir, int maxPixelWidth = 320)
    {
        if (string.IsNullOrEmpty(thumbDir) || !File.Exists(filePath))
        {
            return LoadThumbnail(filePath, maxPixelWidth);
        }

        try
        {
            Directory.CreateDirectory(thumbDir);
            long ticks = File.GetLastWriteTimeUtc(filePath).Ticks;
            string cachePath = Path.Combine(thumbDir, $"{ComputeThumbKey(filePath, ticks)}.jpg");

            if (!File.Exists(cachePath))
            {
                BitmapSource fresh = LoadThumbnail(filePath, maxPixelWidth);
                SaveJpeg(fresh, cachePath);
            }

            return LoadThumbnail(cachePath, maxPixelWidth);
        }
        catch
        {
            // Archivo bloqueado o corrupto: fallback al decode directo (o null).
            try
            {
                return LoadThumbnail(filePath, maxPixelWidth);
            }
            catch
            {
                return null;
            }
        }
    }

    private static string ComputeThumbKey(string filePath, long lastWriteTicks)
    {
        // Hash estable del path + timestamp: la miniatura se invalida al cambiar el archivo.
        var hash = System.Security.Cryptography.SHA1.HashData(
            System.Text.Encoding.UTF8.GetBytes(filePath + "|" + lastWriteTicks));
        return Convert.ToHexString(hash)[..16];
    }

    private static void SaveJpeg(BitmapSource source, string filePath)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
        encoder.Frames.Add(BitmapFrame.Create(source));
        using FileStream fs = new(filePath, FileMode.Create, FileAccess.Write);
        encoder.Save(fs);
    }

    /// <summary>
    /// Guarda un BitmapSource WPF a disco usando el codec según la extensión
    /// del nombre de archivo (.png → PngBitmapEncoder, .jpg/.jpeg → JpegBitmapEncoder).
    /// Congela el source de origen y dispara el encoder en Task.Run (libera el hilo UI).
    /// </summary>
    public static void SaveBitmapSource(BitmapSource source, string filePath)
    {
        string ext = Path.GetExtension(filePath)?.ToLowerInvariant() ?? ".png";
        BitmapEncoder encoder = ext == ".jpg" || ext == ".jpeg"
            ? new JpegBitmapEncoder { QualityLevel = 95 }
            : new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));

        using FileStream fs = new(filePath, FileMode.Create, FileAccess.Write);
        encoder.Save(fs);
    }

    /// <summary>
    /// Convierte un BitmapSource WPF a un Bitmap GDI+ (source of truth del editor),
    /// evitando el lock del archivo original del portapapeles.
    /// </summary>
    public static Bitmap ToBitmap(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        ms.Position = 0;
        return new Bitmap(ms);
    }

    /// <summary>
    /// Carga una captura completa desde archivo como ImageSource congelada,
    /// lista para el visor (decodificación WIC OnLoad + Freeze).
    /// </summary>
    public static BitmapSource LoadFrozenImageSource(string filePath)
    {
        var bitmap = new BitmapImage();
        using (FileStream fs = File.OpenRead(filePath))
        {
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = fs;
            bitmap.EndInit();
        }

        bitmap.Freeze();
        return bitmap;
    }
}
