using System.IO;

namespace EvidenciasSQA.Core.Persistence;

/// <summary>
/// Repositorio de evidencias basado en el sistema de archivos (sin SQLite):
/// escanea una carpeta en busca de capturas PNG/JPEG y las expone como
/// EvidenceRecord ordenados de más reciente a más antiguo.
///
/// Carpeta por defecto: ~/Capturas_QA (la misma de la app Electron de
/// Evidencias SQA). Si no existe, se usa una carpeta "captures" junto al exe.
/// </summary>
public sealed class EvidenceRepository
{
    private static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg", ".bmp"];

    private readonly string _folderPath;

    public EvidenceRepository(string? folderPath = null)
    {
        _folderPath = ResolveDefaultFolder(folderPath);
    }

    public string FolderPath => _folderPath;

    private static string ResolveDefaultFolder(string? folderPath)
    {
        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            return folderPath;
        }

        string userFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "CapturasQA");
        if (Directory.Exists(userFolder))
        {
            return userFolder;
        }

        // Fallback para ejecución standalone: carpeta local junto al exe.
        string local = Path.Combine(AppContext.BaseDirectory, "captures");
        Directory.CreateDirectory(local);
        return local;
    }

    /// <summary>
    /// Devuelve las N evidencias más recientes. Filtra archivos temporales del
    /// visor (prefijo "editing_") para no contaminar la galería.
    /// </summary>
    public IReadOnlyList<EvidenceRecord> GetRecentEvidences(int limit)
    {
        if (!Directory.Exists(_folderPath))
        {
            return Array.Empty<EvidenceRecord>();
        }

        return Directory
            .EnumerateFiles(_folderPath, "*.*")
            .Where(file => IsSupported(file) && !IsEditorTemp(file))
            .Select(file => new FileInfo(file))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .Take(limit)
            .Select((info, index) => new EvidenceRecord
            {
                Id = index + 1,
                EvidenceCode = Path.GetFileNameWithoutExtension(info.Name),
                FilePath = info.FullName,
                CreatedAt = info.LastWriteTime
            })
            .ToList();
    }

    /// <summary>Elimina el archivo de evidencia. El id se conserva por compatibilidad de API.</summary>
    public bool DeleteEvidence(int id, string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
        }
        catch (Exception)
        {
            // Archivo en uso o sin permisos: la galería simplemente no lo borra.
        }

        return false;
    }

    /// <summary>
    /// Elimina todas las evidencias de la carpeta (acción "Eliminar todo" — flujo A de
    /// la spec). Transaccional con try/catch por archivo: un archivo bloqueado o sin
    /// permisos no aborta el resto. El resultado reporta cuántos quedaron
    /// (<see cref="ClearAllResult.RemainingCount"/>) para que el consumidor aplique la
    /// guarda "solo limpiar estado si la carpeta quedó vacía" (spec §5 #6): nunca
    /// dejar un historial vacío en pantalla con archivos huérfanos en disco, ni
    /// reiniciar el contador de secuencia sobre una carpeta no vacía.
    /// </summary>
    public ClearAllResult ClearAll()
    {
        if (!Directory.Exists(_folderPath))
        {
            return new ClearAllResult(true, 0, 0, null);
        }

        int deleted = 0;
        int failed = 0;
        string? firstError = null;

        foreach (string file in Directory.EnumerateFiles(_folderPath))
        {
            if (!IsSupported(file) || IsEditorTemp(file))
            {
                continue;
            }

            try
            {
                File.Delete(file);
                deleted++;
            }
            catch (Exception ex)
            {
                // Archivo en uso (antivirus/visor) o sin permisos: el resto continúa.
                failed++;
                firstError ??= $"{Path.GetFileName(file)}: {ex.Message}";
            }
        }

        return new ClearAllResult(true, deleted, failed, firstError);
    }

    private static bool IsSupported(string file) =>
        SupportedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);

    private static bool IsEditorTemp(string file) =>
        Path.GetFileName(file).StartsWith("editing_", StringComparison.OrdinalIgnoreCase);
}
