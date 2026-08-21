using System;

namespace EvidenciasSQA.Wpf.Services
{
    /// <summary>
    /// Helper para merge de documentos Word (LEGACY - ya no se usa).
    /// La nueva implementación en WordReportBuilder.BuildModulesDocument genera
    /// el documento mergeado directamente desde la plantilla en una sola pasada,
    /// sin necesidad de merge post-generación.
    /// </summary>
    public static class ModuleExportHelper
    {
        /// <summary>
        /// Merge de módulos (OBSOLETO - usar WordReportBuilder.BuildModulesDocument).
        /// </summary>
        [Obsolete("Usar WordReportBuilder.BuildModulesDocument que genera el documento mergeado directamente desde la plantilla.")]
        public static byte[] MergeModuleDocuments(byte[][] moduleBuffers, string[]? moduleNames = null)
        {
            throw new NotSupportedException(
                "ModuleExportHelper.MergeModuleDocuments está obsoleto. " +
                "Usar WordReportBuilder.BuildModulesDocument que genera el documento completo con todos los módulos en una sola pasada usando la plantilla corporativa."
            );
        }
    }
}