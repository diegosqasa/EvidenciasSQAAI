/*
 * EvidenciasSQA - a free and open source screenshot tool
 * Copyright (C) 2004-2026  Thomas Braun, Jens Klingen, Robin Krom
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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Dapplo.Windows.Dpi;
using EvidenciasSQA.Base;
using EvidenciasSQA.Base.Controls;
using EvidenciasSQA.Base.Core;
using EvidenciasSQA.Configuration;
using EvidenciasSQA.Controls;
using EvidenciasSQA.Helpers;
using log4net;

namespace EvidenciasSQA.Forms
{
    /// <summary>
    /// Settings form with two tabs:
    /// 1. Atajos de teclado - Keyboard shortcuts configuration
    /// 2. Ruta Ext Web - Browser extension path and management
    /// </summary>
    public partial class SettingsForm : BaseForm
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SettingsForm));
        private readonly ToolTip _toolTip = new ToolTip();
        private bool _inHotkey;
        private Keys _lastValidRegionHotkey;
        private Keys _lastValidWindowHotkey;
        private Keys _lastValidFullscreenHotkey;
        private Keys _lastValidRegionModifiers;
        private Keys _lastValidWindowModifiers;
        private Keys _lastValidFullscreenModifiers;

        public SettingsForm()
        {
            InitializeComponent();
            DpiChanged += AdjustToDpi;
            ManualStoreFields = true;
        }

        private void AdjustToDpi(object sender, DpiChangedEventArgs dpiChangedEventArgs)
        {
            DisplaySettings();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Setup hotkey controls
            region_hotkeyControl.Enter += EnterHotkeyControl;
            region_hotkeyControl.Leave += LeaveHotkeyControl;
            window_hotkeyControl.Enter += EnterHotkeyControl;
            window_hotkeyControl.Leave += LeaveHotkeyControl;
            fullscreen_hotkeyControl.Enter += EnterHotkeyControl;
            fullscreen_hotkeyControl.Leave += LeaveHotkeyControl;

            // Setup Ext Web tab controls
            extWebCopyBtn.Click += ExtWebCopyBtn_Click;
            extWebOpenFolderBtn.Click += ExtWebOpenFolderBtn_Click;
            extWebOpenChromeBtn.Click += ExtWebOpenChromeBtn_Click;
            extWebOpenEdgeBtn.Click += ExtWebOpenEdgeBtn_Click;

            // Store initial values for reset functionality
            var regionStr = coreConfiguration.RegionHotkey;
            var windowStr = coreConfiguration.WindowHotkey;
            var fullscreenStr = coreConfiguration.FullscreenHotkey;

            _lastValidRegionHotkey = HotkeyManager.HotkeyFromString(regionStr);
            _lastValidRegionModifiers = HotkeyManager.HotkeyModifiersFromString(regionStr);
            _lastValidWindowHotkey = HotkeyManager.HotkeyFromString(windowStr);
            _lastValidWindowModifiers = HotkeyManager.HotkeyModifiersFromString(windowStr);
            _lastValidFullscreenHotkey = HotkeyManager.HotkeyFromString(fullscreenStr);
            _lastValidFullscreenModifiers = HotkeyManager.HotkeyModifiersFromString(fullscreenStr);

            DisplaySettings();
            UpdateUi();
        }

        private void EnterHotkeyControl(object sender, EventArgs e)
        {
            HotkeyManager.UnregisterHotkeys();
            _inHotkey = true;
        }

        private void LeaveHotkeyControl(object sender, EventArgs e)
        {
            HotkeyHelper.RegisterHotkeys();
            _inHotkey = false;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                if (!_inHotkey)
                {
                    DialogResult = DialogResult.Cancel;
                }
                else
                {
                    return base.ProcessCmdKey(ref msg, keyData);
                }
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void UpdateUi()
        {
            _toolTip.SetToolTip(lblDescription, Language.GetString("settings_hotkeys_description"));
            _toolTip.SetToolTip(extWebDescription, Language.GetString("settings_extweb_description"));
        }

        private void DisplaySettings()
        {
            // Load current hotkey values from configuration
            var regionStr = coreConfiguration.RegionHotkey;
            var windowStr = coreConfiguration.WindowHotkey;
            var fullscreenStr = coreConfiguration.FullscreenHotkey;

            region_hotkeyControl.Hotkey = HotkeyManager.HotkeyFromString(regionStr);
            region_hotkeyControl.HotkeyModifiers = HotkeyManager.HotkeyModifiersFromString(regionStr);
            
            window_hotkeyControl.Hotkey = HotkeyManager.HotkeyFromString(windowStr);
            window_hotkeyControl.HotkeyModifiers = HotkeyManager.HotkeyModifiersFromString(windowStr);
            
            fullscreen_hotkeyControl.Hotkey = HotkeyManager.HotkeyFromString(fullscreenStr);
            fullscreen_hotkeyControl.HotkeyModifiers = HotkeyManager.HotkeyModifiersFromString(fullscreenStr);

            // Load Ext Web path
            string extWebPath = GetExtWebPath();
            extWebPathInput.Text = extWebPath;
        }

        private string GetExtWebPath()
        {
            try
            {
                // Try to get path from Electron main process via IPC
                // For now, calculate locally
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string extPath = Path.Combine(basePath, "Media", "Ext_Web");
                
                if (Directory.Exists(extPath))
                {
                    return extPath;
                }
                
                // Fallback: try relative to resources
                string resourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "Media", "Ext_Web");
                if (Directory.Exists(resourcesPath))
                {
                    return resourcesPath;
                }
                
                return extPath; // Return expected path even if not exists
            }
            catch
            {
                return "Error al obtener la ruta";
            }
        }

        private void SaveSettings()
        {
            coreConfiguration.RegionHotkey = HotkeyManager.HotkeyToString(region_hotkeyControl.HotkeyModifiers, region_hotkeyControl.Hotkey);
            coreConfiguration.WindowHotkey = HotkeyManager.HotkeyToString(window_hotkeyControl.HotkeyModifiers, window_hotkeyControl.Hotkey);
            coreConfiguration.FullscreenHotkey = HotkeyManager.HotkeyToString(fullscreen_hotkeyControl.HotkeyModifiers, fullscreen_hotkeyControl.Hotkey);
        }

        private bool ValidateHotkeys()
        {
            var hotkeys = new List<(string Name, Keys Hotkey, Keys Modifiers, HotkeyControl Control)>
            {
                ("Capturar región", region_hotkeyControl.Hotkey, region_hotkeyControl.HotkeyModifiers, region_hotkeyControl),
                ("Capturar ventana activa", window_hotkeyControl.Hotkey, window_hotkeyControl.HotkeyModifiers, window_hotkeyControl),
                ("Capturar todas las pantallas", fullscreen_hotkeyControl.Hotkey, fullscreen_hotkeyControl.HotkeyModifiers, fullscreen_hotkeyControl)
            };

            // Check for duplicates
            var seen = new HashSet<string>();
            foreach (var (name, hotkey, modifiers, control) in hotkeys)
            {
                var combined = HotkeyManager.HotkeyToString(modifiers, hotkey);
                if (combined != "None" && !seen.Add(combined))
                {
                    MessageBox.Show(
                        string.Format(Language.GetString("error_hotkey_duplicate"), name),
                        Language.GetString("error"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    control.Focus();
                    return false;
                }
            }

            // Check for reserved system hotkeys
            var reserved = new[]
            {
                HotkeyManager.HotkeyToString(Keys.Control | Keys.Alt, Keys.Delete),
                HotkeyManager.HotkeyToString(Keys.Alt, Keys.Tab),
                HotkeyManager.HotkeyToString(Keys.Alt, Keys.F4),
                HotkeyManager.HotkeyToString(Keys.Control, Keys.Escape),
                HotkeyManager.HotkeyToString(Keys.None, Keys.LWin),
                HotkeyManager.HotkeyToString(Keys.None, Keys.RWin),
                HotkeyManager.HotkeyToString(Keys.LWin, Keys.D),
                HotkeyManager.HotkeyToString(Keys.LWin, Keys.E),
                HotkeyManager.HotkeyToString(Keys.LWin, Keys.L),
                HotkeyManager.HotkeyToString(Keys.LWin, Keys.R),
                HotkeyManager.HotkeyToString(Keys.LWin, Keys.Tab),
                HotkeyManager.HotkeyToString(Keys.None, Keys.PrintScreen),
                HotkeyManager.HotkeyToString(Keys.Alt, Keys.PrintScreen),
                HotkeyManager.HotkeyToString(Keys.Control, Keys.PrintScreen)
            };

            foreach (var (name, hotkey, modifiers, control) in hotkeys)
            {
                var combined = HotkeyManager.HotkeyToString(modifiers, hotkey);
                if (reserved.Contains(combined))
                {
                    MessageBox.Show(
                        string.Format(Language.GetString("error_hotkey_reserved"), name),
                        Language.GetString("error"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    control.Focus();
                    return false;
                }
            }

            return true;
        }

        private void ResetToDefaults()
        {
            // Default values for hotkeys
            var defaultRegion = Keys.PrintScreen;
            var defaultRegionMod = Keys.None;
            var defaultWindow = Keys.PrintScreen;
            var defaultWindowMod = Keys.Alt;
            var defaultFullscreen = Keys.PrintScreen;
            var defaultFullscreenMod = Keys.Control;

            region_hotkeyControl.Hotkey = defaultRegion;
            region_hotkeyControl.HotkeyModifiers = defaultRegionMod;
            
            window_hotkeyControl.Hotkey = defaultWindow;
            window_hotkeyControl.HotkeyModifiers = defaultWindowMod;
            
            fullscreen_hotkeyControl.Hotkey = defaultFullscreen;
            fullscreen_hotkeyControl.HotkeyModifiers = defaultFullscreenMod;

            _lastValidRegionHotkey = defaultRegion;
            _lastValidRegionModifiers = defaultRegionMod;
            _lastValidWindowHotkey = defaultWindow;
            _lastValidWindowModifiers = defaultWindowMod;
            _lastValidFullscreenHotkey = defaultFullscreen;
            _lastValidFullscreenModifiers = defaultFullscreenMod;
        }

        private void Settings_cancelClick(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void Settings_okayClick(object sender, EventArgs e)
        {
            if (ValidateHotkeys())
            {
                HotkeyManager.UnregisterHotkeys();
                SaveSettings();
                StoreFields();
                HotkeyHelper.RegisterHotkeys();

                // Update main form UI
                var mainForm = SimpleServiceProvider.Current.GetInstance<MainForm>();
                mainForm.UpdateUi();
                DialogResult = DialogResult.OK;
            }
        }

        private void BtnResetClick(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                Language.GetString("settings_hotkeys_reset_confirm"),
                Language.GetString("warning"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            
            if (result == DialogResult.Yes)
            {
                ResetToDefaults();
            }
        }

        // Ext Web tab event handlers
        private void ExtWebCopyBtn_Click(object sender, EventArgs e)
        {
            string path = extWebPathInput.Text;
            if (!string.IsNullOrEmpty(path) && !path.StartsWith("Error"))
            {
                try
                {
                    Clipboard.SetText(path);
                    MessageBox.Show(
                        Language.GetString("settings_extweb_copied"),
                        Language.GetString("settings_extweb_copied_title"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Log.Error("Error copying Ext Web path", ex);
                    MessageBox.Show(
                        Language.GetString("settings_extweb_copy_error"),
                        Language.GetString("error"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void ExtWebOpenFolderBtn_Click(object sender, EventArgs e)
        {
            string path = extWebPathInput.Text;
            if (!string.IsNullOrEmpty(path) && !path.StartsWith("Error"))
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Process.Start("explorer.exe", path);
                    }
                    else
                    {
                        Directory.CreateDirectory(path);
                        Process.Start("explorer.exe", path);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Error opening Ext Web folder", ex);
                    MessageBox.Show(
                        Language.GetString("settings_extweb_openfolder_error"),
                        Language.GetString("error"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private void ExtWebOpenChromeBtn_Click(object sender, EventArgs e)
        {
            OpenExtensionsPage("chrome");
        }

        private void ExtWebOpenEdgeBtn_Click(object sender, EventArgs e)
        {
            OpenExtensionsPage("edge");
        }

        private void OpenExtensionsPage(string browser)
        {
            try
            {
                string url = browser == "chrome" ? "chrome://extensions/" : "edge://extensions/";
                string exeName = browser == "chrome" ? "chrome.exe" : "msedge.exe";
                
                // Try to find browser executable
                string[] possiblePaths = browser == "chrome" 
                    ? new[] {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe")
                    }
                    : new[] {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe")
                    };

                string exePath = possiblePaths.FirstOrDefault(File.Exists);
                
                if (string.IsNullOrEmpty(exePath))
                {
                    MessageBox.Show(
                        string.Format(Language.GetString("settings_extweb_browser_not_found"), browser == "chrome" ? "Chrome" : "Edge"),
                        Language.GetString("warning"),
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // Open with extensions URL
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "--new-window " + (browser == "chrome" ? "chrome://extensions/" : "edge://extensions/"),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Error($"Error opening {browser} extensions page", ex);
                MessageBox.Show(
                    string.Format(Language.GetString("settings_extweb_open_error"), browser),
                    Language.GetString("error"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (!WndProcDefaults.TryHandleMessage(ref m))
            {
                base.WndProc(ref m);
            }
        }
    }
}