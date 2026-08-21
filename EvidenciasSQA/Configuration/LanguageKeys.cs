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

using System.Diagnostics.CodeAnalysis;

namespace EvidenciasSQA.Configuration
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum LangKey
    {
        none,
        contextmenu_capturefullscreen_all,
        contextmenu_capturefullscreen_left,
        contextmenu_capturefullscreen_top,
        contextmenu_capturefullscreen_right,
        contextmenu_capturefullscreen_bottom,
        editor_clipboardfailed,
        editor_close_on_save,
        editor_close_on_save_title,
        editor_copytoclipboard,
        editor_cuttoclipboard,
        editor_deleteelement,
        editor_downonelevel,
        editor_downtobottom,
        editor_duplicate,
        editor_email,
        editor_imagesaved,
        editor_title,
        editor_uponelevel,
        editor_uptotop,
        editor_undo,
        editor_redo,
        editor_resetsize,
        error,
        error_multipleinstances,
        error_openfile,
        error_openlink,
        error_save,
        error_save_invalid_chars,
        print_error,
        quicksettings_destination_file,
        settings_destination,
        settings_destination_clipboard,
        settings_destination_editor,
        settings_destination_fileas,
        settings_destination_printer,
        settings_destination_picker,
        settings_filenamepattern,
        settings_message_filenamepattern,
        settings_printoptions,
        settings_tooltip_filenamepattern,
        settings_tooltip_language,
        settings_tooltip_primaryimageformat,
        settings_tooltip_storagelocation,
        settings_storagelocation_folder_error,
        settings_storagelocation_folder_error_title,
        settings_visualization,
        settings_window_capture_mode,
        tooltip_firststart,
        warning,
        warning_hotkeys,
        update_found,
        error_nowindowtocapture,
        settings_hotkeys_title,
        settings_hotkeys_description,
        settings_hotkeys_reset,
        settings_hotkeys_reset_confirm,
        error_hotkey_duplicate,
        error_hotkey_reserved,
        settings_extweb_title,
        settings_extweb_description,
        settings_extweb_path_label,
        settings_extweb_copy,
        settings_extweb_copied,
        settings_extweb_copied_title,
        settings_extweb_copy_error,
        settings_extweb_open_folder,
        settings_extweb_openfolder_error,
        settings_extweb_open_chrome,
        settings_extweb_open_edge,
        settings_extweb_browser_not_found,
        settings_extweb_open_error,
        settings_extweb_guide_title,
        settings_extweb_guide_steps
    }
}