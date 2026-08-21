/*
 * EvidenciasSQA - a free and open source screenshot tool
 * Copyright (C) 2004-2026 Thomas Braun, Jens Klingen, Robin Krom
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 1 of the License, or
 * (at your option) any later version.
 */

using System;
using System.IO;
using System.Linq;
using EvidenciasSQA.Base.Interfaces;
using EvidenciasSQA.Core.Helpers;

namespace EvidenciasSQA.Helpers
{
    /// <summary>
    /// Metadatos del header corporativo horneado en las capturas del Tray.
    /// Contraparte GDI+ de EvidenciasSQA.Core.Header.HeaderMetadata (visor).
    /// </summary>
    public sealed class CorporateHeaderMeta
    {
        /// <summary>Origen de la captura (título de la ventana / URL en extensiones).</summary>
        public string Origin { get; set; }

        /// <summary>Título de la evidencia.</summary>
        public string Title { get; set; }

        /// <summary>Identificador numérico de evidencia (EV-{id:D2}).</summary>
        public int EvidenceId { get; set; }

        /// <summary>Navegador o modo de captura (desktop: "Escritorio").</summary>
        public string Browser { get; set; }

        /// <summary>Sistema operativo.</summary>
        public string Os { get; set; }

        /// <summary>Timestamp REAL de la captura (nunca la hora de procesamiento).</summary>
        public DateTime CaptureTimestamp { get; set; }

        /// <summary>
        /// Construye la meta a partir de los datos de captura del Tray.
        /// El timestamp SIEMPRE es <see cref="ICaptureDetails.DateTime"/>: si la
        /// captura se difiere (editor, cola), la fecha del header sigue siendo la real.
        /// El sequenceNumber se pasa desde FileDestination (ya asignado) para evitar
        /// doble incremento del contador (spec contador: nunca reutiliza, incrementa 1 a 1).
        /// </summary>
        public static CorporateHeaderMeta FromCaptureDetails(ICaptureDetails captureDetails, string outputFolder, int? sequenceNumber = null)
        {
            var meta = new CorporateHeaderMeta
            {
                Title = "Evidencia de prueba QA",
                Origin = string.Empty,
                Browser = "App Desktop",
                Os = GetOsLabel(),
                CaptureTimestamp = DateTime.Now,
                EvidenceId = sequenceNumber ?? (outputFolder != null ? ExtractSequenceFromLatestFile(outputFolder) : 0)
            };

            if (captureDetails == null)
            {
                return meta;
            }

            if (!string.IsNullOrWhiteSpace(captureDetails.Title))
            {
                meta.Origin = captureDetails.Title;
            }

            if (captureDetails.DateTime != default(DateTime))
            {
                meta.CaptureTimestamp = captureDetails.DateTime;
            }

            return meta;
        }

        /// <summary>"ID: EV-07" (o "ID: ---" si no hay id).</summary>
        public string BuildEvidenceIdString()
        {
            return EvidenceId > 0 ? $"ID: EV-{EvidenceId:D2}" : "ID: ---";
        }

        private static string GetOsLabel()
        {
            try
            {
                Version version = Environment.OSVersion.Version;
                if (version.Major >= 10)
                {
                    // Windows 10 y Windows 11 comparten major 10 (NT 10.0); build 22000 = Windows 11.
                    return version.Build >= 22000 ? "Windows 11" : "Windows 10";
                }

                return $"Windows {version.Major}";
            }
            catch
            {
                return "Windows";
            }
        }

        /// <summary>
        /// Fallback: extrae el número de secuencia del archivo más reciente en la carpeta
        /// (patrón Evidencias_NN.png). Solo se usa si no se pasó sequenceNumber explícito.
        /// </summary>
        private static int ExtractSequenceFromLatestFile(string outputFolder)
        {
            if (string.IsNullOrWhiteSpace(outputFolder) || !Directory.Exists(outputFolder))
            {
                return 0;
            }

            try
            {
                var latest = Directory.EnumerateFiles(outputFolder, "Evidencias_*.png")
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .Where(n => n.StartsWith("Evidencias_", StringComparison.OrdinalIgnoreCase))
                    .Select(n => n.Substring("Evidencias_".Length))
                    .Where(n => int.TryParse(n, out _))
                    .Select(int.Parse)
                    .DefaultIfEmpty(0)
                    .Max();
                return latest;
            }
            catch
            {
                return 0;
            }
        }
    }
}