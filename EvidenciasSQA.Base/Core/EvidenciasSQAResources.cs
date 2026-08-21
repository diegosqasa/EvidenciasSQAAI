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

using System.ComponentModel;
using System.Drawing;

namespace EvidenciasSQA.Base.Core
{
    /// <summary>
    /// Centralized storage of the icons & bitmaps
    /// </summary>
    public static class EvidenciasSQAResources
    {
        private static readonly ComponentResourceManager EvidenciasSQAResourceManager = new ComponentResourceManager(typeof(EvidenciasSQAResources));

        public static Image GetImage(string imageName)
        {
            return (Image) EvidenciasSQAResourceManager.GetObject(imageName);
        }

        public static Icon GetIcon(string imageName)
        {
            return (Icon) EvidenciasSQAResourceManager.GetObject(imageName);
        }

        public static Icon GetEvidenciasSQAIcon()
        {
            try
            {
                string[] icoCandidates =
                {
                    System.IO.Path.Combine(System.AppContext.BaseDirectory, "Media", "SQA1.ico"),
                    System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Media", "SQA1.ico"),
                    System.IO.Path.Combine(System.AppContext.BaseDirectory, "icons", "applicationIcon", "icon.ico")
                };

                foreach (string path in icoCandidates)
                {
                    if (System.IO.File.Exists(path))
                    {
                        using (var ico = new Icon(path))
                        {
                            return (Icon)ico.Clone();
                        }
                    }
                }
            }
            catch
            {
            }

            return GetIcon("EvidenciasSQA.Icon");
        }
    }
}