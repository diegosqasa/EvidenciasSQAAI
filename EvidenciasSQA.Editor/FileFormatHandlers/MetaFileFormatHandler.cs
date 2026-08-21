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

using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using EvidenciasSQA.Base.Core;
using EvidenciasSQA.Base.Interfaces;
using EvidenciasSQA.Base.Interfaces.Drawing;
using EvidenciasSQA.Base.Interfaces.Plugin;
using EvidenciasSQA.Editor.Drawing;

namespace EvidenciasSQA.Editor.FileFormatHandlers
{
    /// <summary>
    /// This handles the Windows metafile files
    /// </summary>
    public class MetaFileFormatHandler : AbstractFileFormatHandler, IFileFormatHandler
    {
        private readonly IReadOnlyCollection<string> _ourExtensions = new[] { ".wmf", ".emf" };
        
        public MetaFileFormatHandler()
        {
            SupportedExtensions[FileFormatHandlerActions.LoadDrawableFromStream] = _ourExtensions;
            SupportedExtensions[FileFormatHandlerActions.LoadFromStream] = _ourExtensions;
            SupportedExtensions[FileFormatHandlerActions.LoadFromFile] = _ourExtensions;
        }

        /// <inheritdoc />
        public override bool TrySaveToStream(Bitmap bitmap, Stream destination, string extension, ISurface surface = null, SurfaceOutputSettings surfaceOutputSettings = null)
        {
            return false;
        }

        /// <inheritdoc />
        public override bool TryLoadFromStream(Stream stream, string extension, out Bitmap bitmap)
        {
            try
            {
                if (Image.FromStream(stream, true, true) is Metafile metaFile)
                {
                    bitmap = ImageHelper.Clone(metaFile, PixelFormat.Format32bppArgb);
                    return true;
                }
            }
            catch
            {
                // Ignore
            }
            bitmap = null;
            return false;
        }

        /// <inheritdoc />
        public override IEnumerable<IDrawableContainer> LoadDrawablesFromStream(Stream stream, string extension, ISurface surface = null)
        {
            if (Image.FromStream(stream, true, true) is Metafile metaFile)
            {
                yield return new MetafileContainer(metaFile, surface);
            }
        }
    }
}
