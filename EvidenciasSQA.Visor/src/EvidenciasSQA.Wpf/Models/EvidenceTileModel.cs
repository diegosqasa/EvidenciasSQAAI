using System.Windows.Media.Imaging;
using EvidenciasSQA.Core.Mvvm;

namespace EvidenciasSQA.Wpf.Models;

/// <summary>
/// Modelo de tarjeta de la galería de evidencias. Deriva de ObservableObject
/// (Core.Mvvm, sin dependencias externas) para la selección visual.
/// </summary>
public partial class EvidenceTileModel : ObservableObject
{
    public int Id { get; set; }

    /// <summary>Ej: "Evidencia_14".</summary>
    public string EvidenceCode { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    /// <summary>Ej: "14/8/2026, 6:19:38 p.m."</summary>
    public string FormattedDate { get; set; } = string.Empty;

    /// <summary>Ej: "avalpaycenter.com" (siempre "Captura Local" en el escaneo de carpeta).</summary>
    public string OriginSite { get; init; } = string.Empty;

    /// <summary>
    /// Miniatura ligera (decodificada a 320px, congelada). null = todavía cargando
    /// (la galería se puebla de forma asíncrona y el tile se actualiza en vuelo).
    /// </summary>
    private BitmapSource? _thumbnail;

    public BitmapSource? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }

    /// <summary>true = la evidencia fue editada (badge ✏️). El escaneo de carpeta no lo conoce.</summary>
    public bool IsEdited { get; init; }

    private bool _isSelected;

    /// <summary>Indicador de selección (check del select-badge).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private int? _selectionOrder;

    /// <summary>
    /// Posición de selección (1..N) según el orden de inserción en selectedIds.
    /// null = no seleccionada. Alimenta el badge word-order del modo informe.
    /// </summary>
    public int? SelectionOrder
    {
        get => _selectionOrder;
        set => SetProperty(ref _selectionOrder, value);
    }

    private bool _isFocused;

    /// <summary>Foco visual de navegación por teclado (replica .focused de Electron).</summary>
    public bool IsFocused
    {
        get => _isFocused;
        set => SetProperty(ref _isFocused, value);
    }
}
