namespace EvidenciasSQA.Core.Drawing;

/// <summary>
/// Ciclo de vida de un elemento sobre la superficie, análogo al EditStatus de EvidenciasSQA.
/// UNDRAWN: creado por la ViewModel pero todavía sin geometría (arrastre en curso).
/// DRAWN:   geometría confirmada y visible.
/// </summary>
public enum EditStatus
{
    Undrawn,
    Drawing,
    Drawn,
    Moving,
    Resizing
}
