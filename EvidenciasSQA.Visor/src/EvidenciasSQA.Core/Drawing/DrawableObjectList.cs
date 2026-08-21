using System.Collections.ObjectModel;
using System.Windows.Media;
using EvidenciasSQA.Core.Model;

namespace EvidenciasSQA.Core.Drawing;

/// <summary>
/// Colección de elementos dibujables de una superficie, análoga a
/// EvidenciasSQA.Editor.Drawing.DrawableContainerList.
/// Se encarga de: enganchar el parent (Surface) a cada elemento, renderizar
/// todos los elementos (pantalla y exportación) y serializar XML para .evidenciasSqa.
/// </summary>
public sealed class DrawableObjectList : Collection<DrawableObject>
{
    public void AddRange(IEnumerable<DrawableObject> items)
    {
        foreach (DrawableObject item in items)
        {
            Add(item);
        }
    }

    protected override void InsertItem(int index, DrawableObject item)
    {
        base.InsertItem(index, item);
        item.Surface = Surface;
    }

    protected override void RemoveItem(int index)
    {
        DrawableObject item = Items[index];
        base.RemoveItem(index);
        item.Surface = null;
    }

    /// <summary>Superficie dueña; se propaga a los elementos al insertarlos.</summary>
    public SurfaceDocument? Surface { get; set; }

    /// <summary>Renderiza todos los elementos en el orden de inserción (abajo → arriba).</summary>
    public void Render(DrawingContext drawingContext, RenderMode mode)
    {
        foreach (DrawableObject drawable in Items)
        {
            drawable.Render(drawingContext, mode);
        }
    }

    /// <summary>Hornea todos los elementos sobre un Graphics GDI+ (exportación).</summary>
    public void RenderForExport(System.Drawing.Graphics graphics, RenderMode mode)
    {
        foreach (DrawableObject drawable in Items)
        {
            drawable.RenderForExport(graphics, mode);
        }
    }

    /// <summary>
    /// Hit-test en orden inverso (el elemento superior es el primero en el que se
    /// pulsa). Devuelve null si no hay ningún elemento en el punto.
    /// </summary>
    public DrawableObject? TopMostAt(System.Drawing.Point point)
    {
        for (int i = Items.Count - 1; i >= 0; i--)
        {
            DrawableObject drawable = Items[i];
            if (drawable.Status == EditStatus.Drawn && drawable.ClickableAt(point))
            {
                return drawable;
            }
        }

        return null;
    }

    /// <summary>Deselecciona todos los elementos.</summary>
    public void DeselectAll()
    {
        foreach (DrawableObject drawable in Items)
        {
            drawable.Selected = false;
        }
    }
}
