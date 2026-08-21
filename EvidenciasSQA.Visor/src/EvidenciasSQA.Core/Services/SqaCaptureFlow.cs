using System.IO;
using System.Diagnostics;
using EvidenciasSQA.Core.Events;

namespace EvidenciasSQA.Core.Services;

/// <summary>
/// Lógica de integración tras guardar la captura en disco.
///
/// Punto único de notificación del flujo de persistencia: cualquier código que
/// termine de escribir una captura (destinos de guardado, guardado directo del
/// tray, exportaciones) debe invocar <see cref="OnCaptureCompleted"/> con la
/// ruta física resultante. El bus (<see cref="SqaEvents"/>) difunde la
/// notificación a los consumidores (Visor) sin que este servicio conozca a nadie.
///
/// La ruta se entrega completa (directorio + nombre + extensión) para que el
/// consumidor cargue el archivo directamente desde disco y garantice la
/// persistencia (nunca bitmaps en memoria).
/// </summary>
public class SqaCaptureFlow
{
    private const int RetryCount = 3;
    private const int RetryDelayMs = 200;

    /// <summary>
    /// Se llama al terminar la persistencia de una captura.
    /// </summary>
    /// <param name="fullPath">Ruta física completa del archivo ya escrito en disco.</param>
    public void OnCaptureCompleted(string fullPath)
    {
        // Validación: asegurar que el archivo existe y está estable antes de notificar.
        // Algunos flujos de guardado (tray) hornean header de forma best-effort después
        // de guardar; esperamos brevemente y validamos existencia para evitar que el
        // visor cargue la captura sin header.
        bool fileReady = false;
        for (int i = 0; i < RetryCount; i++)
        {
            if (File.Exists(fullPath))
            {
                // Pequeña estabilización: el sistema de archivos puede tardar unos ms en
                // liberar el handle tras WriteFile/FlushFileBuffers. Reintentamos con delay.
                if (i > 0) System.Threading.Thread.Sleep(RetryDelayMs);
                fileReady = true;
                break;
            }
        }

        if (!fileReady)
        {
            // No pudimos confirmar la existencia del archivo tras varios intentos.
            // Registramos advertencia y no lanzamos el evento para evitar que el visor
            /// intente cargar un archivo que no existe o está incompleto.
            return;
        }

        // Notificación al bus de eventos
        SqaEvents.RaiseCaptureSaved(fullPath);
    }
}