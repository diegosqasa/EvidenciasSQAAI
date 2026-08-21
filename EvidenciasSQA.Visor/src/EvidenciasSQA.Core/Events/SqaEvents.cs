namespace EvidenciasSQA.Core.Events;

/// <summary>
/// Bus de eventos global desacoplado del flujo de captura.
///
/// Comunicación unidireccional basada en tipos funcionales (Action):
/// el productor (guardado de capturas) solo conoce <see cref="RaiseCaptureSaved"/>,
/// el consumidor (Visor) solo conoce <see cref="CaptureSaved"/>. No existen
/// dependencias circulares: nadie referencia al emisor ni al receptor por tipo.
///
/// El evento puede dispararse desde cualquier hilo (p. ej. el hilo del servidor
/// de named pipe); los suscriptores de UI deben mariscar al hilo de la interfaz
/// (Dispatcher.Invoke) antes de tocar elementos visuales.
/// </summary>
public static class SqaEvents
{
    /// <summary>
    /// Se dispara cuando una captura queda persistida en disco.
    /// El parámetro es la ruta física completa del archivo (autoritativa:
    /// el consumidor carga SIEMPRE desde esa ruta, nunca de memoria).
    /// </summary>
    public static event Action<string>? CaptureSaved;

    /// <summary>
    /// Notifica la persistencia de una nueva captura a todos los suscriptores.
    /// Invocación síncrona y en el hilo del llamador (no bloquea el flujo
    /// de guardado; los suscriptores son responsables de su propio hilo).
    /// </summary>
    public static void RaiseCaptureSaved(string filePath)
    {
        CaptureSaved?.Invoke(filePath);
    }

    /// <summary>
    /// Se dispara cuando el usuario pide restaurar/abrir el visor (menú
    /// "Abrir Visor" del tray, clic/doble clic en el icono, trigger UI-only
    /// del listener HTTP). El consumidor (Visor) carga la última captura
    /// persistida desde disco y asegura la visibilidad de la ventana.
    /// </summary>
    public static event Action? RestoreViewerRequested;

    /// <summary>
    /// Notifica la solicitud de restauración del visor a todos los suscriptores.
    /// Invocación síncrona y en el hilo del llamador.
    /// </summary>
    public static void RaiseRestoreViewerRequested()
    {
        RestoreViewerRequested?.Invoke();
    }

    /// <summary>
    /// Se dispara cuando el historial quedó VACÍO tras un borrado (flujo A "Eliminar
    /// todo", flujo B selección completa o flujo C última evidencia — recomendación
    /// §6.5 de especificacion-borrar-todas-capturas.md: un ÚNICO evento de dominio
    /// que visor, historial y módulo de secuencia consumen, evitando la divergencia
    /// de flujos). SOLO se emite si la carpeta quedó realmente vacía (guarda
    /// maxFileNum === 0); un borrado parcial no lo dispara.
    /// </summary>
    public static event Action? CapturesCleared;

    /// <summary>
    /// Notifica que el historial quedó vacío a todos los suscriptores.
    /// Invocación síncrona y en el hilo del llamador.
    /// </summary>
    public static void RaiseCapturesCleared()
    {
        CapturesCleared?.Invoke();
    }
}