using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Xml.Serialization;
using EvidenciasSQA.Core.Drawing;
using EvidenciasSQA.Core.Imaging;

namespace EvidenciasSQA.Core.Model;

/// <summary>
/// Documento de edición: la imagen de fondo (captura) + la lista de elementos de
/// anotación. Es el análogo moderno de EvidenciasSQA.Editor.Drawing.Surface (ISurface).
///
/// Responsabilidades:
///  - Ser DUEÑO del Bitmap de fondo: lo dispone al reemplazarlo (evita fugas GDI).
///  - Exponer una ImageSource congelada para el render WPF (WIC, liberando HBITMAP).
///  - Renderizar el conjunto fondo+elementos en pantalla y hornear el export GDI+.
///  - Serializar/deserializar los elementos (XML) para el formato .evidenciasSqa.
///
/// Avisa a la vista mediante el evento RequestRender para que invalide el canvas
/// (mismo papel que Surface.Invalidate de EvidenciasSQA).
/// </summary>
public sealed class SurfaceDocument : INotifyPropertyChanged, IDisposable
{
    private Bitmap? _backgroundBitmap;
    private ImageSource? _backgroundImageSource;
    private int _backgroundVersion;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Notifica a la vista que debe repintar (equivalente a Surface.Invalidate).</summary>
    public event EventHandler? RequestRender;

    public SurfaceDocument()
    {
        Elements = new DrawableObjectList { Surface = this };
    }

    /// <summary>Elementos de anotación superpuestos al fondo.</summary>
    public DrawableObjectList Elements { get; }

    /// <summary>
    /// Imagen de fondo (captura). Al reemplazarla se dispone la anterior y se
    /// incrementa la versión para invalidar cachés dependientes (BlurDrawable).
    /// </summary>
    public Bitmap? BackgroundBitmap
    {
        get => _backgroundBitmap;
        set
        {
            if (ReferenceEquals(_backgroundBitmap, value))
            {
                return;
            }

            Bitmap? previous = _backgroundBitmap;
            _backgroundBitmap = value;
            _backgroundImageSource = null; // se regenera bajo demanda

            if (value != null)
            {
                _backgroundVersion++;
            }

            previous?.Dispose();
            OnPropertyChanged(nameof(BackgroundBitmap));
            OnPropertyChanged(nameof(ImageWidth));
            OnPropertyChanged(nameof(ImageHeight));
        }
    }

    /// <summary>
    /// Versión de la imagen de fondo; cualquier caché dependiente de los píxeles
    /// (el blur en pantalla) debe invalidarse cuando cambia.
    /// </summary>
    public int BackgroundVersion => _backgroundVersion;

    public int ImageWidth => _backgroundBitmap?.Width ?? 0;

    public int ImageHeight => _backgroundBitmap?.Height ?? 0;

    /// <summary>
    /// ImageSource WPF del fondo, creada perezosamente y cacheada (WIC).
    /// Al reemplazar el fondo la referencia anterior queda para el GC.
    /// </summary>
    public ImageSource? BackgroundImageSource
    {
        get
        {
            if (_backgroundBitmap == null)
            {
                return null;
            }

            return _backgroundImageSource ??= WicHelper.ToImageSource(_backgroundBitmap);
        }
    }

    /// <summary>
    /// Render de pantalla: fondo + elementos (modo retenido WPF).
    /// </summary>
    public void Render(DrawingContext dc, RenderMode mode)
    {
        if (_backgroundBitmap == null || BackgroundImageSource == null)
        {
            return;
        }

        dc.DrawImage(BackgroundImageSource, new System.Windows.Rect(0, 0, _backgroundBitmap.Width, _backgroundBitmap.Height));
        Elements.Render(dc, mode);
    }

    /// <summary>
    /// Hornea la superficie final en un nuevo Bitmap GDI+ (fondo + anotaciones).
    /// El llamador es el DUEÑO del Bitmap devuelto y debe disponerlo (mismo
    /// contrato que Surface.GetImageForExport de EvidenciasSQA).
    /// </summary>
    public Bitmap GetImageForExport()
    {
        if (_backgroundBitmap == null)
        {
            throw new InvalidOperationException("No hay imagen de fondo que exportar.");
        }

        Bitmap export = new(_backgroundBitmap.Width, _backgroundBitmap.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(export))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            g.DrawImage(_backgroundBitmap, 0, 0, _backgroundBitmap.Width, _backgroundBitmap.Height);
            Elements.RenderForExport(g, RenderMode.Export);
        }

        return export;
    }

    /// <summary>
    /// Serializa los elementos a XML (formato .evidenciasSqa, parte de metadatos).
    /// EvidenciasSQA usaba BinaryFormatter; aquí usamos XmlSerializer, moderno y seguro.
    /// Devuelve los bytes escritos para poder componer el sobre del archivo.
    /// </summary>
    public long SaveElementsToStream(Stream streamWrite)
    {
        long before = streamWrite.Position;
        XmlSerializer serializer = new(typeof(List<DrawableObject>));
        serializer.Serialize(streamWrite, Elements.ToList());
        return streamWrite.Position - before;
    }

    /// <summary>
    /// Carga los elementos desde XML y los re-engancha a esta superficie.
    /// </summary>
    public void LoadElementsFromStream(Stream streamRead)
    {
        XmlSerializer serializer = new(typeof(List<DrawableObject>));
        if (serializer.Deserialize(streamRead) is List<DrawableObject> loaded)
        {
            Elements.Clear();
            foreach (DrawableObject item in loaded)
            {
                // Add → InsertItem re-engancha el parent (Surface) del elemento.
                Elements.Add(item);
            }
        }
    }

    /// <summary>Solicita repintado al host (ViewModel → EditorCanvas).</summary>
    public void RequestRenderNow() => RequestRender?.Invoke(this, EventArgs.Empty);

    /// <summary>Libera el Bitmap de fondo (último propietario).</summary>
    public void Dispose()
    {
        _backgroundBitmap?.Dispose();
        _backgroundBitmap = null;
        _backgroundImageSource = null;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
