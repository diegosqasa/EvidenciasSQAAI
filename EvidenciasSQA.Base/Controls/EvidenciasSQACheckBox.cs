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
using System.Windows.Forms;

namespace EvidenciasSQA.Base.Controls
{
    /// <summary>
    /// Description of EvidenciasSQACheckbox.
    /// </summary>
    public class EvidenciasSQACheckBox : CheckBox, IEvidenciasSQALanguageBindable, IEvidenciasSQAConfigBindable
    {
        [Category("EvidenciasSQA"), DefaultValue(null), Description("Specifies key of the language file to use when displaying the text.")]
        public string LanguageKey { get; set; }

        [Category("EvidenciasSQA"), DefaultValue("Core"), Description("Specifies the Ini-Section to map this control with.")]
        public string SectionName { get; set; } = "Core";

        [Category("EvidenciasSQA"), DefaultValue(null), Description("Specifies the property name to map the configuration.")]
        public string PropertyName { get; set; }
    }
}