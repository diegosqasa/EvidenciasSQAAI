/*
 * EvidenciasSQA - a free and open source screenshot tool
 * Copyright (C) 2004-2026 Thomas Braun, Jens Klingen, Robin Krom
 * 
 * For more information see: https://evidenciassqa.com/
 * The EvidenciasSQA project is hosted on GitHub https://github.com/evidenciassqa/evidenciassqa
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 1 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using EvidenciasSQA.Base;
using EvidenciasSQA.Base.Controls;
using EvidenciasSQA.Base.Core;
using Dapplo.Ini;
using EvidenciasSQA.Base.Interfaces;
using EvidenciasSQA.Base.Interfaces.Plugin;
using EvidenciasSQA.Configuration;
using EvidenciasSQA.Core.Events;
using EvidenciasSQA.Forms;
using EvidenciasSQA.Core.Helpers;
using EvidenciasSQA.Helpers;
using log4net;

namespace EvidenciasSQA.Destinations
{
    /// <summary>
    /// This is the destination which saves the capture to the default location (no dialog)
    /// </summary>
    public class FileDestination : AbstractDestination
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(FileDestination));
        private static readonly ICoreConfiguration CoreConfig = IniConfigRegistry.GetSection<ICoreConfiguration>();

        public override string Designation => nameof(WellKnownDestinations.FileNoDialog);

        public override string Description => Language.GetString(LangKey.quicksettings_destination_file);

        public override int Priority => 0;

        public override Keys EditorShortcutKeys => Keys.Control | Keys.S;

        public override Image DisplayIcon => EvidenciasSQAResources.GetImage("Save.Image");

        public override ExportInformation ExportCapture(bool manuallyInitiated, ISurface surface, ICaptureDetails captureDetails)
        {
            var exportInformation = new ExportInformation(Designation, Description);
            bool outputMade;
            bool overwrite;
            string fullPath;
            // Get output settings from the configuration
            var outputSettings = new SurfaceOutputSettings();

            if (captureDetails?.Filename != null)
            {
                // As we save a pre-selected file, allow to overwrite.
                overwrite = true;
                Log.InfoFormat("Using previous filename");
                fullPath = captureDetails.Filename;
                outputSettings.Format = ImageIO.FormatForFilename(fullPath);
            }
            else
            {
                fullPath = CreateNewFilename(captureDetails);
                // As we generate a file, the configuration tells us if we allow to overwrite
                overwrite = CoreConfig.OutputFileAllowOverwrite;
            }

            if (CoreConfig.OutputFilePromptQuality)
            {
                var qualityDialog = new QualityDialog(outputSettings);
                qualityDialog.ShowDialog();
            }

            // Catching any exception to prevent that the user can't write in the directory.
            // This is done for e.g. bugs #2974608, #2963943, #2816163, #2795317, #2789218, #3004642
            try
            {
                ImageIO.Save(surface, fullPath, overwrite, outputSettings, CoreConfig.OutputFileCopyPathToClipboard);
                outputMade = true;
            }
            catch (ArgumentException ex1)
            {
                // Our generated filename exists, display 'save-as'
                Log.InfoFormat("Not overwriting: {0}", ex1.Message);
                // when we don't allow to overwrite present a new SaveWithDialog
                fullPath = ImageIO.SaveWithDialog(surface, captureDetails);
                outputMade = fullPath != null;
            }
            catch (Exception ex2)
            {
                Log.Error("Error saving screenshot!", ex2);
                // Show the problem
                MessageBox.Show(Language.GetString(LangKey.error_save), Language.GetString(LangKey.error));
                // when save failed we present a SaveWithDialog
                fullPath = ImageIO.SaveWithDialog(surface, captureDetails);
                outputMade = fullPath != null;
            }

            // Don't overwrite filename if no output is made
if (outputMade)
                {
                    exportInformation.ExportMade = true;
                    exportInformation.Filepath = fullPath;
                    if (captureDetails != null)
                    {
                        captureDetails.Filename = fullPath;
                    }

                    CoreConfig.OutputFileAsFullpath = fullPath;

// Header corporativo: se hornea sobre el PNG ya persistido (best-effort,
                // nunca rompe el flujo; idempotente si la captura ya lo trae).
                // Secuencia obligatoria: (A) Escritura → (B) BakeCorporateHeader síncrono →
                // (C) File.Exists + tamaño verification → (D) RaiseCaptureSaved.
                // Logs integrados con prefijo [SQA-INTEGRATION].
                System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] Inicio horneado header corporativo en: " + fullPath);
                BakeCorporateHeader(fullPath, captureDetails);
                System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] Finalizado horneado header corporativo en: " + fullPath);

                // Validación: asegurar que el archivo existe y tiene tamaño (>0) después del horneado.
                if (File.Exists(fullPath))
                {
                    var fileInfo = new FileInfo(fullPath);
                    if (fileInfo.Length > 0)
                    {
                        System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] Archivo validado: " + fileInfo.Length + " bytes en " + fullPath);
                    }
                    else
                    {
                        Log.Warn("[SQA-INTEGRATION] El archivo de captura tiene tamaño 0 después del horneado: " + fullPath);
                    }
                }
                else
                {
                    Log.Warn("[SQA-INTEGRATION] El archivo de captura no existe después del horneado de header: " + fullPath);
                }

                // Bus de eventos (mismo proceso): notifica al Visor que la captura
                // quedo persistida en disco. Best-effort y nunca bloquea.
                // Disparo despues de validar la integridad del archivo.
                SqaEvents.RaiseCaptureSaved(fullPath);
                }

            ProcessExport(exportInformation, surface);
            return exportInformation;
        }

internal static string CreateNewFilename(ICaptureDetails captureDetails)
        {
            string fullPath;
            string filename;
            Log.InfoFormat("Creating new filename");
            
            // SIEMPRE se usa el patrón estandarizado Evidencias_XX.png
            // Esto garantiza consistencia en todos los archivos de captura
            string outputFolder = CoreConfig.OutputFilePath;
            int sequenceNumber = SqaEvidenceSequence.Next(outputFolder);
            filename = $"Evidencias_{sequenceNumber:D2}.png";

            CoreConfig.ValidateAndCorrectOutputFilePath();
            string filepath = FilenameHelper.FillVariables(CoreConfig.OutputFilePath, false);
            try
            {
                fullPath = Path.Combine(filepath, filename);
            }
            catch (ArgumentException)
            {
                // configured filename or path not valid, show error message...
                Log.InfoFormat("Generated path or filename not valid: {0}, {1}", filepath, filename);

                MessageBox.Show(Language.GetString(LangKey.error_save_invalid_chars), Language.GetString(LangKey.error));
                // ... lets get the pattern fixed....
                var dialogResult = new SettingsForm().ShowDialog();
                if (dialogResult == DialogResult.OK)
                {
                    // ... OK -> then try again:
                    fullPath = CreateNewFilename(captureDetails);
                }
                else
                {
                    // ... cancelled.
                    fullPath = null;
                }
            }

            return fullPath;
        }

        /// <summary>
        /// Hornea el header corporativo sobre el archivo PNG recien guardado.
        /// Bloqueante: carga el archivo, dibuja el header, guarda sobrescribiendo el PNG
        /// y libera el handle antes de devolver el control.
        /// Sequencía inquebrantable: (1) Cargar archivo → (2) Dibujar header GDI+ →
        // (3) Guardar PNG → (4) Liberar recursos.
        /// Logs integrados con prefijo [SQA-INTEGRATION].
        /// Internal para que el flujo directo de guardado (CaptureHelper) lo reutilice.
        /// </summary>
        internal static void BakeCorporateHeader(string fullPath, ICaptureDetails captureDetails)
        {
            try
            {
                if (string.IsNullOrEmpty(fullPath) ||
                    !string.Equals(Path.GetExtension(fullPath), ".png", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                // Extraer el número de secuencia del filename (Evidencias_NN.png) — evita
                // doble incremento del contador (FileDestination ya llamó a Next()).
                int sequenceNumber = ExtractSequenceFromFilename(fullPath);

                // --- PASO 1: Cargar archivo ---
                System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] Paso 1: Cargando archivo PNG para horneado: " + fullPath);
                string? tempPath = null;
                using (var original = new Bitmap(fullPath))
                {
                    // --- PASO 2: Dibujar header GDI+ ---
                    System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] Paso 2: Aplicando header corporativo GDI+...");
                    CorporateHeaderMeta meta = CorporateHeaderMeta.FromCaptureDetails(captureDetails, Path.GetDirectoryName(fullPath), sequenceNumber);
                    using (Bitmap baked = CorporateHeaderBaker.Bake(original, meta))
                    {
                        // --- PASO 3: Guardar PNG (sobreescribe el archivo) ---
                        // Se guarda en un archivo temporal y se reemplaza al final: GDI+ no
                        // puede sobrescribir un archivo abierto por el propio Bitmap (0x80004005).
                        // baked == null cuando la imagen supera 16384px (guard anti-OOM del
                        // baker, replicando MAX_DIMENSION del worker): se omite el header.
                        if (baked != null && !ReferenceEquals(baked, original))
                        {
                            System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] Paso 3: Guardando header horneado sobre: " + fullPath);
                            tempPath = fullPath + "." + Guid.NewGuid().ToString("N").Substring(0, 6) + ".tmp";
                            baked.Save(tempPath, ImageFormat.Png);
                            System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] Paso 3 completado: header guardado en temporal.");
                        }
                        else if (baked != null)
                        {
                            System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] Paso 3 omitido: captura ya tiene header (idempotente).");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] Paso 3 omitido: imagen supera 16384px (guard anti-OOM).");
                        }
                    }
                    // --- PASO 4: Recursos liberados automáticamente por 'using' ---
                    System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] Paso 4: Handles liberados (using block).");
                }

                // --- PASO 5: Reemplazo atómico tras liberar el lock del Bitmap original ---
                if (tempPath != null)
                {
                    File.Move(tempPath, fullPath, true);
                    System.Diagnostics.Debug.WriteLine("[SQA-INTEGRATION] Paso 5: Archivo reemplazado: " + fullPath);
                }

                // --- PASO 6: pHYs 96 DPI (replica de injectPhysDpi del worker) ---
                // Se inyecta en el PNG final; con 96 DPI el visor WPF renderiza el
                // zoom 1:1 a escala real (pHYs 300 renderizaba al 32 %).
                PngPhysChunk.Inject96Dpi(fullPath);
            }
            catch (Exception ex)
            {
                Log.Warn("[SQA-INTEGRATION] No se pudo hornear el header corporativo (best-effort).", ex);
            }
        }

        /// <summary>
        /// Extrae el número de secuencia del filename Evidencias_NN.png.
        /// Evita depender de SqaEvidenceSequence.Next() dos veces.
        /// </summary>
        private static int ExtractSequenceFromFilename(string fullPath)
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(fullPath);
                if (name.StartsWith("Evidencias_", StringComparison.OrdinalIgnoreCase))
                {
                    string numPart = name.Substring("Evidencias_".Length);
                    if (int.TryParse(numPart, out int n))
                    {
                        return n;
                    }
                }
            }
            catch { }
            return 0;
        }
    }
}
namespace EvidenciasSQA.Destinations { internal static class MarkerFDC_XYZA { } }
