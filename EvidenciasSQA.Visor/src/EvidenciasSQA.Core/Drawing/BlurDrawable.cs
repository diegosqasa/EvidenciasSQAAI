using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using EvidenciasSQA.Core.Imaging;
using EvidenciasSQA.Core.Model;

namespace EvidenciasSQA.Core.Drawing;

/// <summary>
/// Región de desenfoque (box blur) sobre la captura, equivalente a la combinación
/// EvidenciasSQA FilterContainer + BlurFilter. Oculta información sensible (contraseñas,
/// datos personales) pixelando el área, en lugar de taparla con un rectángulo.
///
/// Rendimiento: 
///  - El blur se aplica sobre la región exacta (recortada a los límites de la imagen)
///    con LockBits, nunca sobre toda la imagen.
///  - En pantalla se cachea la ImageSource del desenfoque y se regenera solo cuando
///    cambia la geometría, el radio o la imagen de fondo (versión de la superficie).
///  - En exportación se hornea sobre el Graphics GDI+ directamente.
/// </summary>
public sealed class BlurDrawable : DrawableObject
{
    public BlurDrawable()
    {
        Radius = 3; // mismo default que EvidenciasSQA (FieldType.BLUR_RADIUS)
    }

    public int Radius { get; set; }

    // Caché de render en pantalla: key = geometría + radio + versión del fondo.
    private Rectangle _cacheBounds;
    private int _cacheRadius = -1;
    private int _cacheBackgroundVersion = -1;
    private ImageSource? _cachedSource;

    /// <inheritdoc/>
    public override void Render(DrawingContext dc, RenderMode mode)
    {
        if (Surface is not { BackgroundBitmap: not null })
        {
            return;
        }

        Rectangle region = BlurRegionBounds();
        ImageSource? blurred = GetOrCreateBlurredSource(region);
        if (blurred == null)
        {
            return;
        }

        dc.DrawImage(blurred, RenderHelpers.ToWpfRect(region));

        if (Selected)
        {
            RenderHelpers.DrawSelectionAdornment(dc, RenderHelpers.ToWpfRect(region));
        }
    }

    /// <inheritdoc/>
    public override void RenderForExport(Graphics g, RenderMode mode)
    {
        if (Surface is not { BackgroundBitmap: not null })
        {
            return;
        }

        Rectangle region = BlurRegionBounds();
        using Bitmap? blurred = CreateBlurredRegion(Surface.BackgroundBitmap, region, Radius);
        if (blurred == null)
        {
            return;
        }

        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        g.DrawImage(blurred, region, 0, 0, blurred.Width, blurred.Height, GraphicsUnit.Pixel);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cachedSource = null;
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Región efectiva de desenfoque, recortada a los límites de la imagen de fondo.
    /// </summary>
    private Rectangle BlurRegionBounds()
    {
        System.Drawing.Rectangle normalized = NormalizedBounds;
        var imageBounds = new System.Drawing.Rectangle(0, 0, Surface!.BackgroundBitmap!.Width, Surface.BackgroundBitmap.Height);
        return System.Drawing.Rectangle.Intersect(normalized, imageBounds);
    }

    /// <summary>
    /// Devuelve la ImageSource cacheada del desenfoque; si la clave cambió
    /// (mover/redimensionar/radio/fondo), regenera y reemplaza la caché.
    /// </summary>
    private ImageSource? GetOrCreateBlurredSource(Rectangle region)
    {
        if (region.Width == 0 || region.Height == 0)
        {
            return null;
        }

        int backgroundVersion = Surface!.BackgroundVersion;
        if (_cachedSource != null && _cacheBounds == region && _cacheRadius == Radius && _cacheBackgroundVersion == backgroundVersion)
        {
            return _cachedSource;
        }

        using Bitmap? blurred = CreateBlurredRegion(Surface.BackgroundBitmap!, region, Radius);
        if (blurred == null)
        {
            return null;
        }

        // GetHbitmap copia los píxeles: podemos disponer el Bitmap temporal al instante.
        _cachedSource = WicHelper.ToImageSource(blurred);
        _cacheBounds = region;
        _cacheRadius = Radius;
        _cacheBackgroundVersion = backgroundVersion;
        return _cachedSource;
    }

    /// <summary>
    /// Aplica box blur (pasadas de 3x3, borde fijo) sobre la región indicada del fondo.
    /// Técnica inspirada en ImageHelper.ApplyBoxBlur de EvidenciasSQA: el kernel es
    /// pequeño y local, por lo que el coste es O(región × radio), no O(imagen).
    /// </summary>
    internal static Bitmap? CreateBlurredRegion(Bitmap source, Rectangle region, int radius)
    {
        if (region.Width <= 0 || region.Height <= 0 || radius <= 0)
        {
            return null;
        }

        // Clone(rect) copia los píxeles de la región: aislada de la imagen original,
        // segura para LockBits y para que el fondo pueda disponerse antes que esta.
        Bitmap result = source.Clone(region, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        int width = result.Width;
        int height = result.Height;
        BitmapData data = result.LockBits(
            new System.Drawing.Rectangle(0, 0, width, height),
            ImageLockMode.ReadWrite,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        try
        {
            int stride = data.Stride;
            byte[] current = new byte[stride * height];
            byte[] next = new byte[stride * height];
            Marshal.Copy(data.Scan0, current, 0, current.Length);

            for (int pass = 0; pass < radius; pass++)
            {
                ApplyBoxBlurPass(current, next, width, height, stride);
                (current, next) = (next, current);
            }

            Marshal.Copy(current, 0, data.Scan0, current.Length);
        }
        finally
        {
            result.UnlockBits(data);
        }

        return result;
    }

    /// <summary>
    /// Una pasada de box blur 3x3 con borde fijo (el píxel de borde se replica).
    /// Procesa solo los canales de color; el alfa se conserva intacto.
    /// </summary>
    private static void ApplyBoxBlurPass(byte[] src, byte[] dst, int width, int height, int stride)
    {
        const int bytesPerPixel = 4;
        for (int y = 0; y < height; y++)
        {
            int yMin = Math.Max(0, y - 1);
            int yMax = Math.Min(height - 1, y + 1);
            int rowOffset = y * stride;

            for (int x = 0; x < width; x++)
            {
                int xMin = Math.Max(0, x - 1);
                int xMax = Math.Min(width - 1, x + 1);
                int samples = 0;
                int r = 0, g = 0, b = 0;

                for (int yy = yMin; yy <= yMax; yy++)
                {
                    int yyStride = yy * stride;
                    for (int xx = xMin; xx <= xMax; xx++)
                    {
                        int i = yyStride + xx * bytesPerPixel;
                        b += src[i];
                        g += src[i + 1];
                        r += src[i + 2];
                        samples++;
                    }
                }

                int di = rowOffset + x * bytesPerPixel;
                dst[di] = (byte)(b / samples);
                dst[di + 1] = (byte)(g / samples);
                dst[di + 2] = (byte)(r / samples);
                dst[di + 3] = src[di + 3]; // alfa sin tocar
            }
        }
    }
}
