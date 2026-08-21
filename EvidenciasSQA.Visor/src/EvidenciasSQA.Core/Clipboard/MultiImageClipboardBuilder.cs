using System.Drawing;
using System.Text;

namespace EvidenciasSQA.Core.ClipboardBuilder;

/// <summary>
/// Contenido de portapapeles multi-imagen (HTML + RTF) generado a partir de rutas
/// de archivo, preservando el orden de selección.
///
/// Regla de bloque: cada imagen se inserta como contenedor de BLOQUE independiente
/// (no inline) para que las aplicaciones destino (Word, Outlook, Teams, OneNote)
/// apilen las evidencias una debajo de otra al pegar:
///   - HTML: un &lt;div&gt; por imagen (bloque).
///   - RTF: un \pict por párrafo, con \par explícito tras cada imagen.
/// </summary>
public sealed record MultiImageClipboardContent(string HtmlFragment, string RtfContent);

public static class MultiImageClipboardBuilder
{
    /// <summary>
    /// Construye el fragmento CF_HTML y el contenido CF_RTF para las imágenes dadas,
    /// en el orden exacto de la lista (orden de selección).
    /// </summary>
    /// <param name="imagePaths">Rutas de las imágenes en orden de selección.</param>
    /// <param name="altNames">Nombres alternativos (por defecto: nombre de archivo).</param>
    public static MultiImageClipboardContent Build(
        IReadOnlyList<string> imagePaths, IReadOnlyList<string>? altNames = null)
    {
        if (imagePaths.Count == 0)
        {
            throw new ArgumentException("Se requiere al menos una imagen.", nameof(imagePaths));
        }

        var body = new StringBuilder();
        body.AppendLine("<html><body>");
        body.AppendLine("<!--StartFragment-->");

        var rtf = new StringBuilder(@"{\rtf1\ansi\deff0");
        for (int i = 0; i < imagePaths.Count; i++)
        {
            string path = imagePaths[i];
            string alt = altNames is not null && i < altNames.Count
                ? altNames[i]
                : Path.GetFileNameWithoutExtension(path);

            byte[] bytes = File.ReadAllBytes(path);

            // HTML: contenedor de bloque <div> por imagen → apilado vertical al pegar.
            body.AppendLine(
                $"<div><img src=\"data:image/png;base64,{Convert.ToBase64String(bytes)}\" alt=\"{alt}\" /></div>");

            // RTF: \pict por imagen en párrafo propio con \par explícito DESPUÉS de cada
            // imagen → cada evidencia es un bloque independiente en Word.
            using var dims = new Bitmap(path);
            string blip = Path.GetExtension(path)?.ToLowerInvariant() is ".jpg" or ".jpeg"
                ? @"\jpegblip"
                : @"\pngblip";
            rtf.Append(@"{\pict").Append(blip)
               .Append(@"\picw").Append(dims.Width)
               .Append(@"\pich").Append(dims.Height)
               .Append(@"\picwgoal").Append(dims.Width * 15)
               .Append(@"\pichgoal").Append(dims.Height * 15)
               .Append(' ').Append(Convert.ToHexString(bytes).ToLowerInvariant()).Append('}')
               .Append(@"\par");
        }

        rtf.Append('}');
        body.AppendLine("<!--EndFragment-->");
        body.AppendLine("</body></html>");

        return new MultiImageClipboardContent(BuildHtmlFragment(body.ToString()), rtf.ToString());
    }

    /// <summary>
    /// Envuelve el HTML en el formato CF_HTML estándar de Windows (cabecera con
    /// offsets absolutos en el string final) para que Word/Chrome lo reconozcan al pegar.
    /// </summary>
    public static string BuildHtmlFragment(string html)
    {
        const string startFragment = "<!--StartFragment-->";
        const string endFragment = "<!--EndFragment-->";
        const string headerTemplate =
            "Version:0.9\r\nStartHTML:{0:000000}\r\nEndHTML:{1:000000}\r\n" +
            "StartFragment:{2:000000}\r\nEndFragment:{3:000000}\r\n";

        // 1) Localizar marcadores en el body (html ya los incluye)
        int bodyFragmentStart = html.IndexOf(startFragment, StringComparison.Ordinal);
        int bodyFragmentEnd = html.IndexOf(endFragment, StringComparison.Ordinal);
        if (bodyFragmentStart < 0 || bodyFragmentEnd < 0)
        {
            throw new InvalidOperationException("El HTML debe contener <!--StartFragment--> y <!--EndFragment-->");
        }
        bodyFragmentEnd += endFragment.Length; // incluir el marcador de cierre

        // 2) Header provisional para medir su longitud real
        string draftHeader = string.Format(headerTemplate, 0, 0, 0, 0);
        int headerLen = draftHeader.Length;

        // 3) Offsets absolutos en el string final (header + body)
        int startHtml = headerLen;
        int endHtml = headerLen + html.Length;
        int startFragmentAbs = headerLen + bodyFragmentStart;
        int endFragmentAbs = headerLen + bodyFragmentEnd;

        // 4) Header definitivo con offsets correctos
        string finalHeader = string.Format(
            headerTemplate,
            startHtml,
            endHtml,
            startFragmentAbs,
            endFragmentAbs);

        // Sanidad: header definitivo debe tener misma longitud que el provisional
        if (finalHeader.Length != headerLen)
        {
            // Recalcular si la longitud cambió (poco probable con formato fijo 6 dígitos)
            int delta = finalHeader.Length - headerLen;
            startHtml += delta;
            endHtml += delta;
            startFragmentAbs += delta;
            endFragmentAbs += delta;
            finalHeader = string.Format(headerTemplate, startHtml, endHtml, startFragmentAbs, endFragmentAbs);
        }

        return finalHeader + html;
    }
}