namespace EvidenciasSQA.Core.Persistence;

/// <summary>
/// Resultado transaccional del borrado masivo de evidencias (equivalente del IPC
/// `delete-all-captures` de la app Electron: 5 pasos con try/catch independiente,
/// fallo parcial no aborta el resto — ver especificacion-borrar-todas-capturas.md §2.2).
///
/// Guarda de seguridad de la spec (maxFileNum === 0): si <see cref="RemainingCount"/>
/// es mayor que 0 (archivos bloqueados/sin permisos), el consumidor NO debe limpiar
/// su estado ni reiniciar secuencias: las capturas futuras no deben sobrescribir las
/// que siguen en disco.
/// </summary>
public sealed record ClearAllResult(
    bool Success,
    int DeletedCount,
    int RemainingCount,
    string? Error)
{
    /// <summary>El borrado fue completo: no quedó ningún archivo de evidencia.</summary>
    public bool IsComplete => Success && RemainingCount == 0;
}