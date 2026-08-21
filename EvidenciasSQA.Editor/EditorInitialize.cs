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

using Dapplo.Ini;
using Dapplo.Ini.Converters;
using EvidenciasSQA.Base.Core;
using EvidenciasSQA.Base.Interfaces;
using EvidenciasSQA.Editor.Configuration;
using EvidenciasSQA.Editor.FileFormatHandlers;

namespace EvidenciasSQA.Editor
{
    public static class EditorInitialize
    {
        private static readonly ICoreConfiguration CoreConfig = IniConfigRegistry.GetSection<ICoreConfiguration>();

        public static void Initialize()
        {
            // Make sure the value converter for the editor is registered, so we can use it in the configuration
            ValueConverterRegistry.Register(new EvidenciasSQAEditorObjectValueConverter());

            SimpleServiceProvider.Current.AddService<IFileFormatHandler>(
                    // All generic things, like gif, png, jpg etc.
                    CoreConfig.IsBetaTester? new ImageSharpFileFormatHandler() : new DefaultFileFormatHandler(),
                    // EvidenciasSQA format
                    new EvidenciasSQAFileFormatHandler(),
                    // For .svg support
                    new SvgFileFormatHandler(),
                    // For clipboard support
                    new DibFileFormatHandler(),
                    // .ico
                    new IconFileFormatHandler(),
                    // EMF & WMF
                    new MetaFileFormatHandler(),
                    // JPG XR
                    new WpfFileFormatHandler()
                );
        }
    }
}
