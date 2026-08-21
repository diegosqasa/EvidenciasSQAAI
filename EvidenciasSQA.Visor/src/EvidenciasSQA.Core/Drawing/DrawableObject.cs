using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using EvidenciasSQA.Core.Model;

namespace EvidenciasSQA.Core.Drawing;

/// <summary>
/// Clase base abstracta de todo elemento gráfico del editor.
/// Es el equivalente moderno de EvidenciasSQA.Editor.Drawing.DrawableContainer:
/// geometría (Left/Top/Width/Height), selección, estado, doble vía de renderizado
/// (WPF en pantalla + GDI+ para exportación) y notificación de cambios.
///
/// Doble renderizado (decisión de arquitectura):
///  - Render(DrawingContext): modo retenido de WPF, solo se repinta lo invalidado.
///  - RenderForExport(Graphics): horneado GDI+ idéntico al de EvidenciasSQA para
///    generar el Bitmap final (archivo/portapapeles), con la máxima calidad.
///
/// Gestión de memoria: patrón IDisposable heredado — cada pincel/pen GDI que se
/// crea dentro de un Draw() se envuelve en using (los GDI handles son recursos
/// nativos que el GC no gestiona bien). Un subclase solo debe liberar sus propios
/// recursos si los mantiene vivos entre renderizados (p. ej. BlurDrawable y su caché).
///
/// Serialización XML (XmlInclude) para el formato .evidenciasSqa: cada tipo concreto
/// se declara aquí para que XmlSerializer pueda instanciarlo polimórficamente.
/// </summary>
[XmlInclude(typeof(RectangleDrawable))]
[XmlInclude(typeof(BlurDrawable))]
[XmlInclude(typeof(ArrowDrawable))]
[XmlInclude(typeof(TextDrawable))]
[XmlInclude(typeof(HighlightDrawable))]
public abstract class DrawableObject : INotifyPropertyChanged, IDisposable
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private int _left;
    private int _top;
    private int _width;
    private int _height;
    private bool _selected;
    private EditStatus _status = EditStatus.Undrawn;

    /// <summary>Superficie dueña. No se serializa (se re-engancha al cargar).</summary>
    [XmlIgnore]
    internal SurfaceDocument? Surface { get; set; }

    public int Left
    {
        get => _left;
        set
        {
            if (_left != value)
            {
                _left = value;
                OnPropertyChanged();
                Invalidate();
            }
        }
    }

    public int Top
    {
        get => _top;
        set
        {
            if (_top != value)
            {
                _top = value;
                OnPropertyChanged();
                Invalidate();
            }
        }
    }

    public int Width
    {
        get => _width;
        set
        {
            if (_width != value)
            {
                _width = value;
                OnPropertyChanged();
                Invalidate();
            }
        }
    }

    public int Height
    {
        get => _height;
        set
        {
            if (_height != value)
            {
                _height = value;
                OnPropertyChanged();
                Invalidate();
            }
        }
    }

    public bool Selected
    {
        get => _selected;
        set
        {
            if (_selected != value)
            {
                _selected = value;
                OnPropertyChanged();
                Invalidate();
            }
        }
    }

    [XmlIgnore]
    public EditStatus Status
    {
        get => _status;
        set => _status = value;
    }

    /// <summary>Bounds sin normalizar (tal cual los arrastró el usuario).</summary>
    [XmlIgnore]
    public System.Drawing.Rectangle Bounds => new(_left, _top, _width, _height);

    /// <summary>Bounds normalizado (Left &lt;= 0 ancho positivo), útil para arrastres en diagonal inversa.</summary>
    [XmlIgnore]
    public System.Drawing.Rectangle NormalizedBounds
    {
        get
        {
            int x = Math.Min(_left, _left + _width);
            int y = Math.Min(_top, _top + _height);
            return new System.Drawing.Rectangle(x, y, Math.Abs(_width), Math.Abs(_height));
        }
    }

    /// <summary>
    /// Clona el elemento mediante serialización XML (mismo mecanismo que el
    /// formato .evidenciasSqa). Equivale a canvas.clone() del módulo web
    /// (duplicar con Ctrl+D desplaza +20,+20).
    /// </summary>
    public DrawableObject Clone()
    {
        using var stream = new MemoryStream();
        var serializer = new XmlSerializer(GetType());
        serializer.Serialize(stream, this);
        stream.Position = 0;
        DrawableObject clone = (DrawableObject)serializer.Deserialize(stream)!;
        clone.Selected = false;
        clone.Surface = null;
        return clone;
    }

    /// <summary>
    /// Solicita a la superficie el repintado de la región afectada
    /// (mismo concepto que Surface.InvalidateElements de EvidenciasSQA, pero en el
    /// modelo de composición retenida de WPF invalidamos el control entero por simplicidad).
    /// </summary>
    public void Invalidate() => Surface?.RequestRenderNow();

    /// <summary>
    /// Hit-test: ¿el punto (coordenadas de imagen) cae dentro del elemento?
    /// </summary>
    public virtual bool ClickableAt(System.Drawing.Point point) => NormalizedBounds.Contains(point);

    /// <summary>
    /// Renderizado en pantalla (WPF). En el modo Edit los elementos pueden dibujarse
    /// simplificados para mantener el redibujado instantáneo durante la edición.
    /// </summary>
    public abstract void Render(DrawingContext drawingContext, RenderMode mode);

    /// <summary>
    /// Renderizado GDI+ para horneado final (exportación a PNG/.evidenciasSqa/portapapeles).
    /// Debe producir exactamente lo que se ve en pantalla (o mejor), como EvidenciasSQA.
    /// </summary>
    public abstract void RenderForExport(System.Drawing.Graphics graphics, RenderMode mode);

    /// <summary>Libera recursos nativos retenidos entre renderizados (cachés, pinceles vivos...).</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }

    ~DrawableObject() => Dispose(false);

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
