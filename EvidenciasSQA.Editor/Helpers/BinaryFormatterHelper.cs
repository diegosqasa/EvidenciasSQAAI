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
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security;
using EvidenciasSQA.Base.Interfaces.Drawing;
using EvidenciasSQA.Editor.Drawing;
using EvidenciasSQA.Editor.Drawing.Emoji;
using EvidenciasSQA.Editor.Drawing.Fields;
using EvidenciasSQA.Editor.Drawing.Filters;
using log4net;
using static EvidenciasSQA.Editor.Drawing.ArrowContainer;
using static EvidenciasSQA.Editor.Drawing.FilterContainer;

namespace EvidenciasSQA.Editor.Helpers
{
    /// <summary>
    /// This helps to map the serialization of the old .evidenciassqa file to the newer.
    /// It also prevents misuse.
    /// </summary>
    internal class BinaryFormatterHelper : SerializationBinder
    {
        private static readonly ILog LOG = LogManager.GetLogger(typeof(BinaryFormatterHelper));
        private static readonly IDictionary<string, Type> TypeMapper = new Dictionary<string, Type>
        {
            {"System.Guid",typeof(Guid) },
            // Used specifically for the .ini configuration (besides the ones already defined)
            {"System.Int32",typeof(int) },
            {"System.Single",typeof(float) },
            {"System.Boolean",typeof(bool) },
            {"System.String",typeof(string) },
            // End ini configuration
            {"System.Drawing.Rectangle",typeof(System.Drawing.Rectangle) },
            {"System.Drawing.Point",typeof(System.Drawing.Point) },
            {"System.Drawing.Color",typeof(System.Drawing.Color) },
            {"System.Drawing.Bitmap",typeof(System.Drawing.Bitmap) },
            {"System.Drawing.Icon",typeof(System.Drawing.Icon) },
            {"System.Drawing.Size",typeof(System.Drawing.Size) },
            {"System.IO.MemoryStream",typeof(System.IO.MemoryStream) },
            {"System.Drawing.StringAlignment",typeof(System.Drawing.StringAlignment) },
            {"System.Collections.Generic.List`1[[EvidenciasSQA.Base.Interfaces.Drawing.IFieldHolder", typeof(List<IFieldHolder>)},
            {"System.Collections.Generic.List`1[[EvidenciasSQA.Base.Interfaces.Drawing.IField", typeof(List<IField>)},
            {"System.Collections.Generic.List`1[[System.Drawing.Point", typeof(List<System.Drawing.Point>)},
            {"EvidenciasSQA.Editor.Drawing.ArrowContainer", typeof(ArrowContainer) },
            {"EvidenciasSQA.Editor.Drawing.ArrowContainer+ArrowHeadCombination", typeof(ArrowContainer.ArrowHeadCombination) },
            {"EvidenciasSQA.Editor.Drawing.LineContainer", typeof(LineContainer) },
            {"EvidenciasSQA.Editor.Drawing.TextContainer", typeof(TextContainer) },
            {"EvidenciasSQA.Editor.Drawing.SpeechbubbleContainer", typeof(SpeechbubbleContainer) },
            {"EvidenciasSQA.Editor.Drawing.RectangleContainer", typeof(RectangleContainer) },
            {"EvidenciasSQA.Editor.Drawing.EllipseContainer", typeof(EllipseContainer) },
            {"EvidenciasSQA.Editor.Drawing.FreehandContainer", typeof(FreehandContainer) },
            {"EvidenciasSQA.Editor.Drawing.HighlightContainer", typeof(HighlightContainer) },
            {"EvidenciasSQA.Editor.Drawing.IconContainer", typeof(IconContainer) },
            {"EvidenciasSQA.Editor.Drawing.ObfuscateContainer", typeof(ObfuscateContainer) },
            {"EvidenciasSQA.Editor.Drawing.StepLabelContainer", typeof(StepLabelContainer) },
            {"EvidenciasSQA.Editor.Drawing.SvgContainer", typeof(SvgContainer) },
            {"EvidenciasSQA.Editor.Drawing.Emoji.EmojiContainer", typeof(EmojiContainer) },
            {"EvidenciasSQA.Editor.Drawing.VectorGraphicsContainer", typeof(VectorGraphicsContainer) },
            {"EvidenciasSQA.Editor.Drawing.MetafileContainer", typeof(MetafileContainer) },
            {"EvidenciasSQA.Editor.Drawing.ImageContainer", typeof(ImageContainer) },
            {"EvidenciasSQA.Editor.Drawing.FilterContainer", typeof(FilterContainer) },
            {"EvidenciasSQA.Editor.Drawing.DrawableContainer", typeof(DrawableContainer) },
            {"EvidenciasSQA.Editor.Drawing.DrawableContainerList", typeof(DrawableContainerList) },
            {"EvidenciasSQA.Editor.Drawing.CursorContainer", typeof(CursorContainer) },
            {"EvidenciasSQA.Editor.Drawing.CursorContainer+CaptureCursorSerializationWrapper", typeof(CursorContainer.CaptureCursorSerializationWrapper) },
            {"EvidenciasSQA.Editor.Drawing.Filters.HighlightFilter", typeof(HighlightFilter) },
            {"EvidenciasSQA.Editor.Drawing.Filters.GrayscaleFilter", typeof(GrayscaleFilter) },
            {"EvidenciasSQA.Editor.Drawing.Filters.MagnifierFilter", typeof(MagnifierFilter) },
            {"EvidenciasSQA.Editor.Drawing.Filters.BrightnessFilter", typeof(BrightnessFilter) },
            {"EvidenciasSQA.Editor.Drawing.Filters.BlurFilter", typeof(BlurFilter) },
            {"EvidenciasSQA.Editor.Drawing.Filters.PixelizationFilter", typeof(PixelizationFilter) },
            {"EvidenciasSQA.Base.Interfaces.Drawing.IDrawableContainer", typeof(IDrawableContainer) },
            {"EvidenciasSQA.Base.Interfaces.Drawing.EditStatus", typeof(EditStatus) },
            {"EvidenciasSQA.Base.Interfaces.Drawing.IFieldHolder", typeof(IFieldHolder) },
            {"EvidenciasSQA.Base.Interfaces.Drawing.IField", typeof(IField) },
            {"EvidenciasSQA.Base.Interfaces.Drawing.FieldFlag", typeof(FieldFlag) },
            {"EvidenciasSQA.Editor.Drawing.Fields.Field", typeof(Field) },
            {"EvidenciasSQA.Editor.Drawing.Fields.FieldType", typeof(FieldType) },
            {"EvidenciasSQA.Editor.Drawing.FilterContainer+PreparedFilter", typeof(PreparedFilter) },
        };

        /// <summary>
        /// Try to match the type for the given type name, this is used to check if the type is allowed to be deserialized, and to map old types to new ones. 
        /// </summary>
        /// <param name="typeName"></param>
        /// <param name="type"></param>
        /// <returns>bool true if the mapping was possible</returns>
        public static bool TryGetType(string typeName, out Type type)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                type = null;
                return false;
            }
            string comparingTypeName = typeName;
            var typeNameCommaLocation = typeName.IndexOf(",");
            if (typeNameCommaLocation > 0)
            {
                comparingTypeName = typeName.Substring(0, typeNameCommaLocation);
            }

            // Correct wrong types (because of refactoring) to the correct ones, this is needed to load old .evidenciassqa files
            comparingTypeName = comparingTypeName.Replace("EvidenciasSQA.Drawing", "EvidenciasSQA.Editor.Drawing");
            comparingTypeName = comparingTypeName.Replace("EvidenciasSQA.Plugin.Drawing", "EvidenciasSQA.Base.Interfaces.Drawing");
            comparingTypeName = comparingTypeName.Replace("EvidenciasSQAPlugin.Interfaces.Drawing", "EvidenciasSQA.Base.Interfaces.Drawing");
            comparingTypeName = comparingTypeName.Replace("EvidenciasSQA.Drawing.Fields", "EvidenciasSQA.Editor.Drawing.Fields");
            comparingTypeName = comparingTypeName.Replace("EvidenciasSQA.Drawing.Filters", "EvidenciasSQA.Editor.Drawing.Filters");
            return TypeMapper.TryGetValue(comparingTypeName, out type);
        }

        /// <summary>
        /// Do the type mapping
        /// </summary>
        /// <param name="assemblyName">Assembly for the type that was serialized</param>
        /// <param name="typeName">Type that was serialized</param>
        /// <returns>Type which was mapped</returns>
        /// <exception cref="SecurityException">If something smells fishy</exception>
        public override Type BindToType(string assemblyName, string typeName)
        {
            if (TryGetType(typeName, out var returnType))
            {
                LOG.Info($"Mapped {assemblyName} - {typeName} to {returnType.FullName}");
                return returnType;
            }
            LOG.Warn($"Unexpected EvidenciasSQA type in .evidenciassqa file detected, maybe vulnerability attack created with ysoserial? Suspicious type: {assemblyName} - {typeName}");
            throw new SecurityException($"Suspicious type in .evidenciassqa file: {assemblyName} - {typeName}");
        }
    }
}
