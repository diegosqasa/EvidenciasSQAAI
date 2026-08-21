using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WP = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace EvidenciasSQA.Core.Export;

/// <summary>
/// Generador de documentos Word (.docx) basado en plantilla corporativa Soporte_Evidencias.docx.
/// Replica el comportamiento del worker Electron (docxtemplater + ImageModule):
/// - Reemplaza placeholders: {idHu}, {nombreHu}, {nombreusuario}, {fecha}, {proyecto}, {usuario}
/// - Procesa loop {%image} → lo envuelve en {#captures}...{/captures} si no existe
/// - Inserta imágenes con dimensiones normalizadas (max 6.25", alta resolución para PDF 300 DPI)
/// - Limpia tags fragmentados por Word (placeholders rotos en múltiples runs)
/// - Merge de módulos: primer módulo con tabla HU completa, siguientes sin duplicar tabla HU
/// </summary>
public static class WordReportBuilder
{
    private const long EmuPerInch = 914400;
    private const long MaxWidthEmu = 57 * EmuPerInch / 10;    // 5.7 pulgadas ≈ 600px @ 96 DPI
    private const long MaxHeightEmu = 125 * EmuPerInch / 10;  // 12.5 pulgadas (sin límite práctico para PDF)

    private static readonly string TemplatePath = Path.Combine(
        AppContext.BaseDirectory,
        "Media",
        "Soporte_Evidencias.docx"
    );

    /// <summary>Genera un documento único con todas las evidencias (modo Completo/Seleccionado).</summary>
    public static void BuildDocument(string outputPath, WordHuInfo hu, IReadOnlyList<WordEvidenceItem> items, IProgress<double>? progress = null)
    {
        var modules = new[] { new WordModule("Evidencias", items) };
        Build(modules, hu, outputPath, single: true, progress);
    }

    /// <summary>Genera un documento mergeado con un bloque por caso de prueba (modo Por CP/Módulos).</summary>
    public static void BuildModulesDocument(string outputPath, WordHuInfo hu, IReadOnlyList<WordModule> modules, IProgress<double>? progress = null)
    {
        Build(modules, hu, outputPath, single: false, progress);
    }

    private static void Build(IReadOnlyList<WordModule> modules, WordHuInfo hu, string outputPath, bool single, IProgress<double>? progress)
    {
        if (!File.Exists(TemplatePath))
        {
            throw new FileNotFoundException($"Plantilla no encontrada: {TemplatePath}");
        }

        int totalItems = modules.Sum(m => m.Items.Count);
        int processed = 0;

        // Copiar plantilla a archivo temporal de trabajo
        string tempPath = Path.Combine(Path.GetTempPath(), $"sqa_word_{Guid.NewGuid():N}.docx");
        File.Copy(TemplatePath, tempPath, overwrite: true);

        try
        {
            using (var doc = WordprocessingDocument.Open(tempPath, true))
            {
                var mainPart = doc.MainDocumentPart;
                if (mainPart?.Document?.Body == null)
                {
                    throw new InvalidOperationException("Plantilla inválida: no tiene document body.");
                }

                // Calcular maxRelationshipId inicial desde el documento
                int maxRelId = GetMaxRelationshipId(doc);

                // 1. Limpiar tags fragmentados en todo el documento
                CleanFragmentedTags(mainPart);

                // 2. Reemplazar placeholders simples (HU, usuario, fecha)
                ReplaceSimplePlaceholders(mainPart, hu);

                // 3. Procesar loop de imágenes {%image}
                ProcessImageLoop(mainPart, doc, modules, single, ref processed, totalItems, progress, ref maxRelId);

                // 4. Guardar cambios
                mainPart.Document.Save();
            }

            // 5. Copiar resultado final
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
            File.Move(tempPath, outputPath);
        }
        catch
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }
    }

    private static int GetMaxRelationshipId(WordprocessingDocument doc)
    {
        // Workaround: usar ID alto para evitar conflictos con plantilla (máx ~20 rels en template)
        return 1000;
    }

    private static int GetNextRelationshipId(ref int maxRelId)
    {
        return ++maxRelId;
    }

    // ============================================================
    // 1. Limpieza de tags fragmentados
    // ============================================================

    /// <summary>
    /// Word fragmenta placeholders en múltiples <w:r> (runs).
    /// Ej: { idHu } puede estar en 3 runs: "{", "idHu", "}".
    /// Este método une el texto de todos los runs en un párrafo y reconstruye los placeholders.
    /// </summary>
    private static void CleanFragmentedTags(MainDocumentPart mainPart)
    {
        foreach (var paragraph in mainPart.Document.Body!.Descendants<Paragraph>())
        {
            var runs = paragraph.Elements<Run>().ToList();
            if (runs.Count <= 1) continue;

            // Concatenar todo el texto del párrafo
            var fullText = new StringBuilder();
            foreach (var run in runs)
            {
                foreach (var text in run.Elements<Text>())
                {
                    fullText.Append(text.Text);
                }
            }

            string combined = fullText.ToString();
            if (!combined.Contains('{')) continue; // No hay placeholders

            // Reemplazar placeholders fragmentados en el texto combinado
            string cleaned = combined;

            // Patrón: { seguido de cualquier cosa hasta } (incluyendo tags XML intermedios)
            cleaned = Regex.Replace(cleaned, @"\{([^{}]+)\}", m =>
            {
                string content = m.Groups[1].Value;
                // Eliminar cualquier tag XML dentro del placeholder
                content = Regex.Replace(content, @"<[^>]+>", "");
                return "{" + content + "}";
            });

            if (cleaned == combined) continue;

            // Reconstruir el párrafo: limpiar todos los runs y poner el texto limpio en el primero
            foreach (var run in runs.Skip(1))
            {
                run.Remove();
            }

            var firstRun = runs.First();
            firstRun.RemoveAllChildren<Text>();
            firstRun.Append(new Text(cleaned) { Space = SpaceProcessingModeValues.Preserve });
        }
    }

    // ============================================================
    // 2. Reemplazo de placeholders simples
    // ============================================================

    private static void ReplaceSimplePlaceholders(MainDocumentPart mainPart, WordHuInfo hu)
    {
        string fecha = DateTime.Now.ToString("dd/MM/yyyy");
        // Usar Environment.UserName directamente (usuario de Windows) sin formatear
        string usuarioWindows = Environment.UserName;

        var replacements = new Dictionary<string, string>
        {
            ["{idHu}"] = hu.Id,
            ["{nombreHu}"] = hu.Nombre,
            ["{nombreusuario}"] = usuarioWindows,
            ["{fecha}"] = fecha,  // Fecha del reporte (HU table)
            ["{proyecto}"] = hu.Nombre,
            ["{usuario}"] = usuarioWindows
        };

        foreach (var paragraph in mainPart.Document.Body!.Descendants<Paragraph>())
        {
            foreach (var run in paragraph.Elements<Run>())
            {
                foreach (var text in run.Elements<Text>())
                {
                    string original = text.Text;
                    string replaced = original;
                    foreach (var kvp in replacements)
                    {
                        replaced = replaced.Replace(kvp.Key, kvp.Value);
                    }
                    if (replaced != original)
                    {
                        text.Text = replaced;
                    }
                }
            }
        }

        // También en headers/footers
        foreach (var headerPart in mainPart.HeaderParts)
        {
            ReplaceInPart(headerPart, replacements);
        }
        foreach (var footerPart in mainPart.FooterParts)
        {
            ReplaceInPart(footerPart, replacements);
        }
    }

    private static void ReplaceInPart(OpenXmlPart part, Dictionary<string, string> replacements)
    {
        if (part?.RootElement == null) return;
        foreach (var text in part.RootElement.Descendants<Text>())
        {
            string original = text.Text;
            string replaced = original;
            foreach (var kvp in replacements)
            {
                replaced = replaced.Replace(kvp.Key, kvp.Value);
            }
            if (replaced != original)
            {
                text.Text = replaced;
            }
        }
    }

    private static string FormatUserName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "N/A";
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(raw.ToLower());
    }

    // ============================================================
    // 3. Procesamiento del loop de imágenes
    // ============================================================

    private static void ProcessImageLoop(
        MainDocumentPart mainPart,
        WordprocessingDocument doc,
        IReadOnlyList<WordModule> modules,
        bool single,
        ref int processed,
        int totalItems,
        IProgress<double>? progress,
        ref int maxRelId)
    {
        var body = mainPart.Document.Body!;

        // Buscar el párrafo que contiene {%image} EN TODO EL DOCUMENTO (incluyendo tablas)
        Paragraph? imagePara = null;
        foreach (var para in body.Descendants<Paragraph>())
        {
            string text = string.Concat(para.Descendants<Text>().Select(t => t.Text));
            if (text.Contains("{%image}"))
            {
                imagePara = para;
                break;
            }
        }

        if (imagePara == null)
        {
            // No hay placeholder de imagen: insertar al final del body
            AppendEvidencesAtEnd(body, modules, single, ref processed, totalItems, progress, mainPart, doc, ref maxRelId);
            return;
        }

        // Verificar si ya está envuelto en loop {#captures}...{/captures} en el MISMO CONTENEDOR
        var container = imagePara.Parent;
        bool hasLoop = IsInsideLoop(imagePara, container);

        if (!hasLoop)
        {
            // Envolver el párrafo con tags de loop EN SU CONTENEDOR
            WrapParagraphInLoop(container, imagePara);
        }

        // Expandir el loop en su contenedor
        ExpandLoopInContainer(mainPart, doc, container, modules, single, ref processed, totalItems, progress, ref maxRelId);
    }

    private static bool IsInsideLoop(Paragraph para, OpenXmlElement? container)
    {
        if (container == null) return false;

        var children = container.Elements().ToList();
        int idx = children.IndexOf(para);
        if (idx < 0) return false;

        for (int i = idx - 1; i >= 0; i--)
        {
            string t = string.Concat(children[i].Descendants<Text>().Select(x => x.Text));
            if (t.Contains("{#captures}")) return true;
            if (t.Contains("{/captures}")) break;
        }
        return false;
    }

    private static void WrapParagraphInLoop(OpenXmlElement container, Paragraph imagePara)
    {
        // Insertar párrafo de apertura {#captures} antes
        var openPara = new Paragraph(
            new Run(new Text("{#captures}") { Space = SpaceProcessingModeValues.Preserve })
        );
        imagePara.InsertBeforeSelf(openPara);

        // Insertar párrafo de cierre {/captures} después
        var closePara = new Paragraph(
            new Run(new Text("{/captures}") { Space = SpaceProcessingModeValues.Preserve })
        );
        imagePara.InsertAfterSelf(closePara);
    }

    private static void ExpandLoopInContainer(
        MainDocumentPart mainPart,
        WordprocessingDocument doc,
        OpenXmlElement container,
        IReadOnlyList<WordModule> modules,
        bool single,
        ref int processed,
        int totalItems,
        IProgress<double>? progress,
        ref int maxRelId)
    {
        // Obtener todos los párrafos en este contenedor
        var paragraphs = container.Elements<Paragraph>().ToList();
        int startIdx = -1, endIdx = -1;

        for (int i = 0; i < paragraphs.Count; i++)
        {
            string t = string.Concat(paragraphs[i].Descendants<Text>().Select(x => x.Text));
            if (t.Contains("{#captures}")) startIdx = i;
            if (t.Contains("{/captures}")) { endIdx = i; break; }
        }

        if (startIdx < 0 || endIdx < 0 || startIdx >= endIdx)
        {
            // Fallback: append al final del body
            var body = mainPart.Document.Body!;
            AppendEvidencesAtEnd(body, modules, single, ref processed, totalItems, progress, mainPart, doc, ref maxRelId);
            return;
        }

        // Template es el contenido entre startIdx y endIdx (excluyendo los tags)
        var templateParagraphs = paragraphs
            .Skip(startIdx + 1)
            .Take(endIdx - startIdx - 1)
            .Where(p => !string.Concat(p.Descendants<Text>().Select(x => x.Text)).Contains("{%image}"))
            .ToList();

        // Eliminar el loop original (tags + contenido template)
        for (int i = endIdx; i >= startIdx; i--)
        {
            paragraphs[i].Remove();
        }

        // Insertar contenido expandido para cada evidencia
        int insertIndex = startIdx;
        foreach (var module in modules)
        {
            if (!single && module != modules.First())
            {
                // Salto de página + heading del módulo (solo para módulos 2+)
                var breakPara = new Paragraph(new Run(new Break() { Type = BreakValues.Page }));
                container.InsertAt(breakPara, insertIndex++);
                var headingPara = CreateHeading(module.Name, 28);
                container.InsertAt(headingPara, insertIndex++);
            }

            foreach (var item in module.Items)
            {
                // Para cada evidencia, clonar párrafos template y reemplazar placeholders
                foreach (var tmplPara in templateParagraphs)
                {
                    var clone = (Paragraph)tmplPara.CloneNode(true);
                    ReplaceEvidencePlaceholders(clone, item);
                    container.InsertAt(clone, insertIndex++);
                }

                // Insertar imagen REAL en el mismo contenedor con ID único
                var imagePara = CreateImageParagraph(mainPart, doc, item, GetNextRelationshipId(ref maxRelId));
                container.InsertAt(imagePara, insertIndex++);
                container.InsertAt(new Paragraph(), insertIndex++); // espacio

                processed++;
                progress?.Report((double)processed / Math.Max(1, totalItems) * 100.0);
            }
        }
    }

    private static void AppendEvidencesAtEnd(
        Body body,
        IReadOnlyList<WordModule> modules,
        bool single,
        ref int processed,
        int totalItems,
        IProgress<double>? progress,
        MainDocumentPart mainPart,
        WordprocessingDocument doc,
        ref int maxRelId)
    {
        foreach (var module in modules)
        {
            if (!single && module != modules.First())
            {
                body.Append(new Paragraph(new Run(new Break() { Type = BreakValues.Page })));
                body.Append(CreateHeading(module.Name, 28));
            }

            foreach (var item in module.Items)
            {
                // Heading evidencia
                body.Append(CreateHeading(item.EvidenceCode, 24));

                // Tabla metadatos
                body.Append(CreateMetadataTable(item));

                // Imagen con ID de relación único
                body.Append(CreateImageParagraph(mainPart, doc, item, GetNextRelationshipId(ref maxRelId)));
                body.Append(new Paragraph());

                processed++;
                progress?.Report((double)processed / Math.Max(1, totalItems) * 100.0);
            }
        }
    }

    // ============================================================
    // Helpers de construcción XML
    // ============================================================

    private static Paragraph CreateHeading(string text, int halfPoints)
    {
        return new Paragraph(
            new ParagraphProperties(
                new SpacingBetweenLines() { Before = "120", After = "60" }
            ),
            new Run(
                new RunProperties(
                    new Bold(),
                    new FontSize() { Val = halfPoints.ToString() },
                    new Color() { Val = "004080" }
                ),
                new Text(text) { Space = SpaceProcessingModeValues.Preserve }
            )
        );
    }

    private static Table CreateMetadataTable(WordEvidenceItem item)
    {
        string[] labels = ["Fecha", "Origen", "Título"];
        string[] values = [item.FormattedDate, item.OriginSite, item.Title];

        var table = new Table(
            new TableProperties(
                new TableWidth() { Width = "0", Type = TableWidthUnitValues.Auto },
                new TableBorders(
                    new TopBorder() { Val = BorderValues.Single, Size = 4, Color = "D9D9D9" },
                    new LeftBorder() { Val = BorderValues.Single, Size = 4, Color = "D9D9D9" },
                    new BottomBorder() { Val = BorderValues.Single, Size = 4, Color = "D9D9D9" },
                    new RightBorder() { Val = BorderValues.Single, Size = 4, Color = "D9D9D9" },
                    new InsideHorizontalBorder() { Val = BorderValues.Single, Size = 4, Color = "D9D9D9" },
                    new InsideVerticalBorder() { Val = BorderValues.Single, Size = 4, Color = "D9D9D9" }
                )
            )
        );

        for (int i = 0; i < labels.Length; i++)
        {
            var row = new TableRow(
                new TableCell(
                    new TableCellProperties(
                        new TableCellWidth() { Width = "1600", Type = TableWidthUnitValues.Dxa },
                        new Shading() { Val = ShadingPatternValues.Clear, Fill = "F2F2F2" }
                    ),
                    new Paragraph(
                        new Run(
                            new RunProperties(new Bold(), new FontSize() { Val = "18" }),
                            new Text(labels[i]) { Space = SpaceProcessingModeValues.Preserve }
                        )
                    )
                ),
                new TableCell(
                    new TableCellProperties(
                        new TableCellWidth() { Width = "6600", Type = TableWidthUnitValues.Dxa }
                    ),
                    new Paragraph(
                        new Run(
                            new RunProperties(new FontSize() { Val = "18" }),
                            new Text(values[i]) { Space = SpaceProcessingModeValues.Preserve }
                        )
                    )
                )
            );
            table.Append(row);
        }

        return table;
    }

    private static Paragraph CreateImageParagraph(MainDocumentPart mainPart, WordprocessingDocument doc, WordEvidenceItem item, int relId)
    {
        string ext = Path.GetExtension(item.FilePath).ToLowerInvariant();
        string contentType = ext is ".jpg" or ".jpeg" ? "image/jpeg" : "image/png";

        // Agregar imagen al package
        ImagePart imagePart = mainPart.AddImagePart(
            contentType == "image/jpeg" ? ImagePartType.Jpeg : ImagePartType.Png,
            "rId" + relId
        );

        using (var fs = File.OpenRead(item.FilePath))
        {
            imagePart.FeedData(fs);
        }

        // Calcular dimensiones (leer header sin decodificar bitmap completo)
        var (cx, cy) = ComputeExtent(item.FilePath, ext);

        // Construir drawing XML
        var drawing = new DocumentFormat.OpenXml.Wordprocessing.Drawing(
            new WP.Inline(
                new WP.Extent() { Cx = cx, Cy = cy },
                new WP.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new WP.DocProperties() { Id = (UInt32)relId, Name = "Imagen " + relId },
                new WP.NonVisualGraphicFrameDrawingProperties(
                    new A.GraphicFrameLocks() { NoChangeAspect = true }
                ),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties() { Id = (UInt32)relId, Name = "img" + relId },
                                new PIC.NonVisualPictureDrawingProperties()
                            ),
                            new PIC.BlipFill(
                                new A.Blip() { Embed = "rId" + relId, CompressionState = A.BlipCompressionValues.Print },
                                new A.Stretch(new A.FillRectangle())
                            ),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset() { X = 0L, Y = 0L },
                                    new A.Extents() { Cx = cx, Cy = cy }
                                ),
                                new A.PresetGeometry(
                                    new A.AdjustValueList()
                                ) { Preset = A.ShapeTypeValues.Rectangle }
                            )
                        )
                    ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
                )
            )
        );

        return new Paragraph(
            new ParagraphProperties(new Justification() { Val = JustificationValues.Center }),
            new Run(drawing)
        );
    }

    private static (long Cx, long Cy) ComputeExtent(string filePath, string ext)
    {
        try
        {
            byte[] header = new byte[4096];
            using (var fs = File.OpenRead(filePath))
            {
                int read = fs.Read(header, 0, header.Length);
                if (read < 24) return (MaxWidthEmu, MaxWidthEmu * 3 / 4);

                (int w, int h) = ext switch
                {
                    ".png" => TryParsePng(header),
                    ".jpg" or ".jpeg" => TryParseJpeg(header),
                    _ => (0, 0)
                };

                if (w <= 0 || h <= 0) return (MaxWidthEmu, MaxWidthEmu * 3 / 4);

                long cx = MaxWidthEmu;
                long cy = cx * h / w;
                if (cy > MaxHeightEmu)
                {
                    cy = MaxHeightEmu;
                    cx = cy * w / h;
                }

                return (Math.Max(72000, cx), Math.Max(72000, cy));
            }
        }
        catch
        {
            return (MaxWidthEmu, MaxWidthEmu * 3 / 4);
        }
    }

    private static (int W, int H) TryParsePng(byte[] b)
    {
        if (b.Length < 24 || b[0] != 0x89 || b[1] != 0x50) return (0, 0);
        return (ReadBe32(b, 16), ReadBe32(b, 20));
    }

    private static (int W, int H) TryParseJpeg(byte[] b)
    {
        if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8) return (0, 0);
        int pos = 2;
        while (pos + 4 < b.Length)
        {
            if (b[pos] != 0xFF) return (0, 0);
            byte marker = b[pos + 1];
            if (marker == 0xD8) { pos += 2; continue; }
            if (marker == 0xD9 || marker == 0xDA) return (0, 0);
            if (marker >= 0xC0 && marker <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC))
            {
                int height = ReadBe16(b, pos + 5);
                int width = ReadBe16(b, pos + 7);
                return (width, height);
            }
            int segLen = ReadBe16(b, pos + 2);
            pos += 2 + segLen;
        }
        return (0, 0);
    }

    private static int ReadBe16(byte[] b, int offset) => (b[offset] << 8) | b[offset + 1];
    private static int ReadBe32(byte[] b, int offset) => (b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];

    private static void ReplaceEvidencePlaceholders(Paragraph para, WordEvidenceItem item)
    {
        var replacements = new Dictionary<string, string>
        {
            ["{%image}"] = "", // Placeholder de imagen en plantilla (se reemplaza por imagen real)
            ["{title}"] = item.Title,
            ["{url}"] = item.OriginSite,
            ["{fecha}"] = item.FormattedDate,  // Fecha del archivo de evidencia
            ["{evidenceCode}"] = item.EvidenceCode
        };

        foreach (var text in para.Descendants<Text>())
        {
            string original = text.Text;
            string replaced = original;
            foreach (var kvp in replacements)
            {
                replaced = replaced.Replace(kvp.Key, kvp.Value);
            }
            if (replaced != original)
            {
                text.Text = replaced;
            }
        }
    }
}