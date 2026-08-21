using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using EvidenciasSQA.Core.ClipboardBuilder;
using Xunit;

namespace EvidenciasSQA.Core.Tests;

public class MultiImageClipboardBuilderTests
{
    private static string CreateTempPng(int width, int height, string? label = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"sqatest_{Guid.NewGuid():N}.png");
        using var bmp = new Bitmap(width, height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            if (!string.IsNullOrEmpty(label))
            {
                using var font = new Font("Arial", 12);
                g.DrawString(label, font, Brushes.Black, 10, 10);
            }
        }
        bmp.Save(path, ImageFormat.Png);
        return path;
    }

    private static void Cleanup(params string[] paths)
    {
        foreach (var p in paths)
        {
            try { if (File.Exists(p)) File.Delete(p); } catch { }
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void Build_Html_ContainsBlockDivPerImage_InOrder(int count)
    {
        var paths = new string[count];
        for (int i = 0; i < count; i++)
            paths[i] = CreateTempPng(100, 100, $"img{i + 1}");

        try
        {
            var content = MultiImageClipboardBuilder.Build(paths);
            string html = content.HtmlFragment;

            // Verificar cabecera CF_HTML presente
            Assert.Contains("Version:0.9", html);
            Assert.Contains("<!--StartFragment-->", html);
            Assert.Contains("<!--EndFragment-->", html);

            // Verificar estructura: un <div> por imagen, en orden
            var divMatches = Regex.Matches(html, @"<div>\s*<img[^>]*>\s*</div>");
            Assert.Equal(count, divMatches.Count);

            // Verificar orden y contenido base64 de cada imagen
            for (int i = 0; i < count; i++)
            {
                var expectedPath = paths[i];
                byte[] expectedBytes = File.ReadAllBytes(expectedPath);
                string expectedB64 = Convert.ToBase64String(expectedBytes);
                string pattern = $@"<div>\s*<img\s+src=""data:image/png;base64,{Regex.Escape(expectedB64)}""[^>]*>\s*</div>";
                Assert.Matches(pattern, html);
            }
        }
        finally
        {
            Cleanup(paths);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void Build_Rtf_ContainsPictPerImage_WithParagraphBreak(int count)
    {
        var paths = new string[count];
        for (int i = 0; i < count; i++)
            paths[i] = CreateTempPng(100 + i * 10, 100 + i * 10, $"img{i + 1}");

        try
        {
            var content = MultiImageClipboardBuilder.Build(paths);
            string rtf = content.RtfContent;

            // Cabecera RTF
            Assert.StartsWith(@"{\rtf1\ansi\deff0", rtf);
            Assert.EndsWith("}", rtf);

            // Contar \pict (una por imagen)
            int pictCount = Regex.Matches(rtf, @"\\pict").Count;
            Assert.Equal(count, pictCount);

            // Cada \pict debe ir seguido de \par (salto de párrafo explícito)
            int parAfterPictCount = Regex.Matches(rtf, @"\\pict[^}]*\}\s*\\par").Count;
            Assert.Equal(count, parAfterPictCount);

            // Verificar que cada \pict tiene firma PNG (89504e47 en hex en el stream)
            int pngSigCount = Regex.Matches(rtf, @"89504e47", RegexOptions.IgnoreCase).Count;
            Assert.Equal(count, pngSigCount);

            // Verificar \picwgoal y \pichgoal (twips = px * 15)
            for (int i = 0; i < count; i++)
            {
                using var bmp = new Bitmap(paths[i]);
                int expectedW = bmp.Width * 15;
                int expectedH = bmp.Height * 15;
                Assert.Contains($@"\picwgoal{expectedW}", rtf);
                Assert.Contains($@"\pichgoal{expectedH}", rtf);
            }
        }
        finally
        {
            Cleanup(paths);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void Build_HtmlFragmentOffsets_AreValid(int count)
    {
        var paths = new string[count];
        for (int i = 0; i < count; i++)
            paths[i] = CreateTempPng(50, 50);

        try
        {
            var content = MultiImageClipboardBuilder.Build(paths);
            string html = content.HtmlFragment;

            // Extraer offsets de la cabecera (posiciones absolutas en el string final)
            int startHtml = ExtractOffset(html, "StartHTML:");
            int endHtml = ExtractOffset(html, "EndHTML:");
            int startFragment = ExtractOffset(html, "StartFragment:");
            int endFragment = ExtractOffset(html, "EndFragment:");

            Assert.True(startHtml > 0);
            Assert.True(endHtml > startHtml);
            Assert.True(startFragment >= startHtml);
            Assert.True(endFragment <= endHtml);
            Assert.True(startFragment < endFragment);

            // Verificar que el fragmento efectivamente está en el rango (offsets absolutos)
            string fragment = html.Substring(startFragment, endFragment - startFragment);
            Assert.Contains("<!--StartFragment-->", fragment);
            Assert.Contains("<!--EndFragment-->", fragment);

            // Verificar que el contenido HTML está en el rango StartHTML..EndHTML
            string htmlContent = html.Substring(startHtml, endHtml - startHtml);
            Assert.Contains("<html><body>", htmlContent);
            Assert.Contains("</body></html>", htmlContent);
        }
        finally
        {
            Cleanup(paths);
        }
    }

    private static int ExtractOffset(string html, string key)
    {
        int idx = html.IndexOf(key, StringComparison.Ordinal);
        if (idx < 0) return -1;
        int start = idx + key.Length;
        int end = html.IndexOf('\r', start);
        return int.Parse(html.Substring(start, end - start).Trim());
    }

    [Fact]
    public void Build_PreservesAltNames_WhenProvided()
    {
        var paths = new[] { CreateTempPng(50, 50, "img1"), CreateTempPng(50, 50, "img2") };
        var altNames = new[] { "Evidencia_10", "Evidencia_11" };

        try
        {
            var content = MultiImageClipboardBuilder.Build(paths, altNames);
            string html = content.HtmlFragment;

            Assert.Contains(@"alt=""Evidencia_10""", html);
            Assert.Contains(@"alt=""Evidencia_11""", html);
        }
        finally
        {
            Cleanup(paths);
        }
    }

    [Fact]
    public void Build_Throws_WhenEmptyPaths()
    {
        Assert.Throws<ArgumentException>(() => MultiImageClipboardBuilder.Build(Array.Empty<string>()));
    }
}