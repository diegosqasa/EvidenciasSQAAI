using System;
using System.IO;
using System.Threading;

namespace EvidenciasSQA.Core.Helpers
{
    /// <summary>
    /// Secuencia de identificadores de evidencia (patrón Evidencias_NN.png).
    /// Implementa la especificación completa (especificacion-contador-numeracion.md):
    /// - Lock atómico (_gate) para incrementos concurrentes (IPC, HTTP, nativo).
    /// - Arranque: lastUsedNum = max(archivo .last_id.txt, maxFileNum en disco).
    /// - Incremento monotónico: nunca reutiliza números (huecos aceptables).
    /// - Reset a 0 SOLO si la carpeta quedó vacía (maxFileNum === 0).
    /// - Sync-back (renderer → main): solo sube (if seq > lastUsedNum).
    /// </summary>
    public static class SqaEvidenceSequence
    {
        private const string FileName = ".last_id.txt";
        private static readonly object _gate = new object();

        /// <summary>
        /// Devuelve el siguiente id de evidencia e incrementa el contador persistente.
        /// Thread-safe: serializa todas las fuentes (IPC get-next-sequence, HTTP /api/next-sequence,
        /// captura nativa PrtScn, etc.) para evitar duplicados.
        /// </summary>
        public static int Next(string outputFolder)
        {
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                return 1;
            }

            lock (_gate)
            {
                string filePath = Path.Combine(outputFolder, FileName);
                int lastUsed = LoadOrInitializeCounter(outputFolder, filePath);

                int next = lastUsed + 1;
                try
                {
                    File.WriteAllText(filePath, next.ToString());
                }
                catch
                {
                    // Sin permisos: el id se "pierde" (hueco), pero no rompemos el flujo.
                }

                return next;
            }
        }

        /// <summary>
        /// Devuelve el siguiente id SIN incrementar el contador (GET /api/peek-sequence
        /// de la extensión web: el content script lo usa para pintar el header del
        /// cliente; solo lectura, nunca avanza la secuencia).
        /// </summary>
        public static int Peek(string outputFolder)
        {
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                return 1;
            }

            lock (_gate)
            {
                string filePath = Path.Combine(outputFolder, FileName);
                int lastUsed = LoadOrInitializeCounter(outputFolder, filePath);
                return lastUsed + 1;
            }
        }

        /// <summary>
        /// Sincronización renderer → main (sync-sequence-back): solo sube el contador
        /// si el renderer reporta un número mayor (main nunca baja).
        /// </summary>
        public static void SyncBack(string outputFolder, int rendererSequence)
        {
            if (string.IsNullOrWhiteSpace(outputFolder) || rendererSequence <= 0)
            {
                return;
            }

            lock (_gate)
            {
                string filePath = Path.Combine(outputFolder, FileName);
                int lastUsed = LoadOrInitializeCounter(outputFolder, filePath);

                if (rendererSequence > lastUsed)
                {
                    try
                    {
                        File.WriteAllText(filePath, rendererSequence.ToString());
                    }
                    catch
                    {
                        // Best-effort.
                    }
                }
            }
        }

        /// <summary>
        /// Reset condicional (reset-folder-counter): escanea disco → maxFileNum.
        /// Si maxFileNum === 0 y lastUsedNum > 0 → lastUsedNum = 0 (trunca logs en caller).
        /// Si maxFileNum > lastUsedNum → sube al máximo real (recupera si borraron .last_id.txt).
        /// </summary>
        public static int ResetIfFolderEmpty(string outputFolder)
        {
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                return 0;
            }

            lock (_gate)
            {
                string filePath = Path.Combine(outputFolder, FileName);
                int lastUsed = 0;
                try
                {
                    if (File.Exists(filePath) && int.TryParse(File.ReadAllText(filePath).Trim(), out int stored))
                    {
                        lastUsed = stored;
                    }
                }
                catch { }

                int maxFileNum = ScanMaxFileNumber(outputFolder);

                if (maxFileNum > lastUsed)
                {
                    // Disco gana (archivo borrado manualmente o corrupto): sube al máximo real.
                    lastUsed = maxFileNum;
                    try { File.WriteAllText(filePath, lastUsed.ToString()); } catch { }
                }
                else if (maxFileNum == 0 && lastUsed > 0)
                {
                    // Carpeta vacía: reset a 0. El caller truncará logs.
                    lastUsed = 0;
                    try { File.WriteAllText(filePath, "0"); } catch { }
                }

                return lastUsed;
            }
        }

        private static int LoadOrInitializeCounter(string outputFolder, string filePath)
        {
            int lastUsed = 0;
            try
            {
                if (File.Exists(filePath) && int.TryParse(File.ReadAllText(filePath).Trim(), out int stored))
                {
                    lastUsed = stored;
                }
            }
            catch { }

            // Arranque: max(archivo, disco) — recupera si .last_id.txt se borró/dañó.
            int maxFileNum = ScanMaxFileNumber(outputFolder);
            if (maxFileNum > lastUsed)
            {
                lastUsed = maxFileNum;
                try { File.WriteAllText(filePath, lastUsed.ToString()); } catch { }
            }

            return lastUsed;
        }

        private static int ScanMaxFileNumber(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return 0;
            }

            int max = 0;
            try
            {
                foreach (string file in Directory.EnumerateFiles(folder, "Evidencias_*.png"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    // Formato: Evidencias_NN (NN = número con padding 2 dígitos, pero aceptamos variable)
                    if (name.StartsWith("Evidencias_", StringComparison.OrdinalIgnoreCase))
                    {
                        string numPart = name.Substring("Evidencias_".Length);
                        if (int.TryParse(numPart, out int n) && n > max)
                        {
                            max = n;
                        }
                    }
                }
            }
            catch { }
            return max;
        }
    }
}