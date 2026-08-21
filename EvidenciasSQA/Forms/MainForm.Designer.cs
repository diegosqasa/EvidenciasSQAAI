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

using EvidenciasSQA.Base.Controls;

namespace EvidenciasSQA.Forms {
	partial class MainForm {
		/// <summary>
		/// Designer variable used to keep track of non-visual components.
		/// </summary>
		private System.ComponentModel.IContainer components = null;
		
		/// <summary>
		/// Disposes resources used by the form.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing) {
			if (disposing) {
				if (components != null) {
					components.Dispose();
				}
				if (_copyData != null) {
					_copyData.Dispose();
				}
			}
			base.Dispose(disposing);
		}
		
		/// <summary>
		/// This method is required for Windows Forms designer support.
		/// Do not change the method contents inside the source code editor. The Forms designer might
		/// not be able to load this method if it was changed manually.
		/// </summary>
		private void InitializeComponent() {
			this.components = new System.ComponentModel.Container();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
			this.contextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.contextmenu_capturearea = new EvidenciasSQAToolStripMenuItem();
			this.contextmenu_capturelastregion = new EvidenciasSQAToolStripMenuItem();
			this.contextmenu_capturewindow = new EvidenciasSQAToolStripMenuItem();
			this.contextmenu_capturefullscreen = new EvidenciasSQAToolStripMenuItem();
			this.contextmenu_capturewindowfromlist = new EvidenciasSQAToolStripMenuItem();
			this.contextmenu_captureclipboard = new EvidenciasSQAToolStripMenuItem();
			
			this.contextmenu_openrecentcapture = new EvidenciasSQAToolStripMenuItem();
			this.contextmenu_quicksettings = new EvidenciasSQAToolStripMenuItem();
			this.contextmenu_settings = new EvidenciasSQAToolStripMenuItem();
			this.contextmenu_help = new EvidenciasSQAToolStripMenuItem();
			this.contextmenu_donate = new EvidenciasSQAToolStripMenuItem();
			this.contextmenu_about = new EvidenciasSQAToolStripMenuItem();
            this.contextmenu_exit = new EvidenciasSQAToolStripMenuItem();
            this.toolStripListCaptureSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripOtherSourcesSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripOpenFolderSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripPluginSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripMiscSeparator = new System.Windows.Forms.ToolStripSeparator();
			this.toolStripCloseSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.contextMenu.SuspendLayout();
            this.SuspendLayout();
			// 
			// contextMenu
			// 
			//
// ToolStripItem array for the context menu items
//
            this.contextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.contextmenu_capturearea,
                this.contextmenu_capturewindow,
                this.contextmenu_capturefullscreen,
                this.contextmenu_capturewindowfromlist,
                this.toolStripListCaptureSeparator,
                this.toolStripOpenFolderSeparator,
                this.contextmenu_openrecentcapture,
                this.toolStripPluginSeparator,
                this.contextmenu_quicksettings,
                this.toolStripPluginSeparator,
                this.contextmenu_grabarpantalla,
                this.contextmenu_extraertexto,
                this.toolStripPluginSeparator,
                this.contextmenu_quicksettings,
                this.toolStripPluginSeparator,
                this.contextmenu_settings,
                this.toolStripCloseSeparator,
                this.contextmenu_exit});
			this.contextMenu.Name = "contextMenu";
			this.contextMenu.Closing += new System.Windows.Forms.ToolStripDropDownClosingEventHandler(this.ContextMenuClosing);
			this.contextMenu.Opening += new System.ComponentModel.CancelEventHandler(this.ContextMenuOpening);
			this.contextMenu.Renderer = new EvidenciasSQA.Controls.AcrylicContextMenuRenderer();
			// 
			// contextmenu_capturearea
			// 
			this.contextmenu_capturearea.Image = ((System.Drawing.Image)(resources.GetObject("contextmenu_capturearea.Image")));
			this.contextmenu_capturearea.Name = "contextmenu_capturearea";
			this.contextmenu_capturearea.Text = "Capturar región";
			this.contextmenu_capturearea.ShortcutKeyDisplayString = "Print";
			this.contextmenu_capturearea.Size = new System.Drawing.Size(170, 22);
			this.contextmenu_capturearea.Click += new System.EventHandler(this.CaptureAreaToolStripMenuItemClick);
			// 
			// contextmenu_capturelastregion
			// 
			this.contextmenu_capturelastregion.Enabled = false;
			this.contextmenu_capturelastregion.Image = ((System.Drawing.Image)(resources.GetObject("contextmenu_capturelastregion.Image")));
			this.contextmenu_capturelastregion.Name = "contextmenu_capturelastregion";
			this.contextmenu_capturelastregion.ShortcutKeyDisplayString = "Shift + Print";
			this.contextmenu_capturelastregion.Size = new System.Drawing.Size(170, 22);
			this.contextmenu_capturelastregion.Click += new System.EventHandler(this.Contextmenu_CaptureLastRegionClick);
			// 
			// contextmenu_capturewindow
			// 
			this.contextmenu_capturewindow.Image = ((System.Drawing.Image)(resources.GetObject("contextmenu_capturewindow.Image")));
			this.contextmenu_capturewindow.Name = "contextmenu_capturewindow";
			this.contextmenu_capturewindow.Text = "Capturar ventana activa";
			this.contextmenu_capturewindow.ShortcutKeyDisplayString = "Alt + Print";
			this.contextmenu_capturewindow.Size = new System.Drawing.Size(170, 22);
			this.contextmenu_capturewindow.Click += new System.EventHandler(this.Contextmenu_CaptureWindow_Click);
			// 
			// contextmenu_capturefullscreen
			// 
			this.contextmenu_capturefullscreen.Image = ((System.Drawing.Image)(resources.GetObject("contextmenu_capturefullscreen.Image")));
			this.contextmenu_capturefullscreen.Name = "contextmenu_capturefullscreen";
			this.contextmenu_capturefullscreen.Text = "Capturar pantalla";
			this.contextmenu_capturefullscreen.ShortcutKeyDisplayString = "Ctrl + Print";
			this.contextmenu_capturefullscreen.Size = new System.Drawing.Size(170, 22);
			// 
			// toolStripListCaptureSeparator
			// 
			this.toolStripListCaptureSeparator.Name = "toolStripListCaptureSeparator";
			this.toolStripListCaptureSeparator.Size = new System.Drawing.Size(167, 6);
			// 
			// contextmenu_capturewindowfromlist
			// 
			this.contextmenu_capturewindowfromlist.Name = "contextmenu_capturewindowfromlist";
			this.contextmenu_capturewindowfromlist.Text = "Capturar ventana de lista";
			this.contextmenu_capturewindowfromlist.DropDownClosed += new System.EventHandler(this.CaptureWindowFromListMenuDropDownClosed);
			this.contextmenu_capturewindowfromlist.DropDownOpening += new System.EventHandler(this.CaptureWindowFromListMenuDropDownOpening);
			// 
			// toolStripOtherSourcesSeparator
			// 
			this.toolStripOtherSourcesSeparator.Name = "toolStripOtherSourcesSeparator";
			this.toolStripOtherSourcesSeparator.Size = new System.Drawing.Size(167, 6);
			// 
			// contextmenu_captureclipboard
			// 
			this.contextmenu_captureclipboard.Image = ((System.Drawing.Image)(resources.GetObject("contextmenu_captureclipboard.Image")));
			this.contextmenu_captureclipboard.Name = "contextmenu_captureclipboard";
			this.contextmenu_captureclipboard.Size = new System.Drawing.Size(170, 22);
			this.contextmenu_captureclipboard.Click += new System.EventHandler(this.CaptureClipboardToolStripMenuItemClick);
			// 
			// contextmenu_openrecentcapture
			// 
			this.contextmenu_openrecentcapture.Name = "contextmenu_openrecentcapture";
			this.contextmenu_openrecentcapture.Size = new System.Drawing.Size(170, 22);
			this.contextmenu_openrecentcapture.Click += new System.EventHandler(this.Contextmenu_OpenRecent);
			// 
			// toolStripPluginSeparator
			// 
this.toolStripPluginSeparator.Name = "toolStripPluginSeparator";
            this.toolStripPluginSeparator.Size = new System.Drawing.Size(167, 6);
            this.toolStripPluginSeparator.Tag = "PluginsAreAddedBefore";
            // 
            // contextmenu_grabarpantalla
            // 
            this.contextmenu_grabarpantalla.Name = "contextmenu_grabarpantalla";
            this.contextmenu_grabarpantalla.Text = "Grabar pantalla";
            this.contextmenu_grabarpantalla.Size = new System.Drawing.Size(170, 22);
            this.contextmenu_grabarpantalla.Click += new System.EventHandler(this.MenuGrabarPantalla_Click);
            // 
            // contextmenu_extraertexto
            // 
            this.contextmenu_extraertexto.Name = "contextmenu_extraertexto";
            this.contextmenu_extraertexto.Text = "Extraer texto";
            this.contextmenu_extraertexto.Size = new System.Drawing.Size(170, 22);
            this.contextmenu_extraertexto.Click += new System.EventHandler(this.MenuExtraerTexto_Click);
            // 
            // contextmenu_quicksettings
			// 
			this.contextmenu_quicksettings.Name = "contextmenu_quicksettings";
			this.contextmenu_quicksettings.Size = new System.Drawing.Size(170, coreConfiguration.IconSize.Height + 8);
			// 
			// contextmenu_settings
			// 
			this.contextmenu_settings.Image = ((System.Drawing.Image)(resources.GetObject("contextmenu_settings.Image")));
			this.contextmenu_settings.Name = "contextmenu_settings";
			this.contextmenu_settings.Text = "Configuración";
			this.contextmenu_settings.Size = new System.Drawing.Size(170, 22);
			this.contextmenu_settings.Click += new System.EventHandler(this.Contextmenu_SettingsClick);
			// 
			// toolStripMiscSeparator
			// 
			this.toolStripMiscSeparator.Name = "toolStripMiscSeparator";
			this.toolStripMiscSeparator.Size = new System.Drawing.Size(167, 6);
			// 
			// contextmenu_help
			// 
			this.contextmenu_help.Image = ((System.Drawing.Image)(resources.GetObject("contextmenu_help.Image")));
			this.contextmenu_help.Name = "contextmenu_help";
			this.contextmenu_help.Size = new System.Drawing.Size(170, 22);
			this.contextmenu_help.Click += new System.EventHandler(this.Contextmenu_HelpClick);
			// 
			// contextmenu_donate
			// 
			this.contextmenu_donate.Image = ((System.Drawing.Image)(resources.GetObject("contextmenu_donate.Image")));
			this.contextmenu_donate.Name = "contextmenu_donate";
			this.contextmenu_donate.Size = new System.Drawing.Size(170, 22);
			this.contextmenu_donate.Click += new System.EventHandler(this.Contextmenu_DonateClick);
			// 
			// contextmenu_about
			// 
			this.contextmenu_about.Name = "contextmenu_about";
			this.contextmenu_about.Size = new System.Drawing.Size(170, 22);
			this.contextmenu_about.Click += new System.EventHandler(this.Contextmenu_AboutClick);
			// 
			// toolStripCloseSeparator
			// 
			this.toolStripCloseSeparator.Name = "toolStripCloseSeparator";
			this.toolStripCloseSeparator.Size = new System.Drawing.Size(167, 6);
			// 
			// contextmenu_exit
			// 
			this.contextmenu_exit.Image = ((System.Drawing.Image)(resources.GetObject("contextmenu_exit.Image")));
			this.contextmenu_exit.Name = "contextmenu_exit";
			this.contextmenu_exit.Text = "Salir";
			this.contextmenu_exit.Click += new System.EventHandler(this.Contextmenu_ExitClick);
			// 
			// notifyIcon
			// 
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
			this.notifyIcon.ContextMenuStrip = this.contextMenu;
			this.notifyIcon.Text = "EvidenciasSQA";
			this.notifyIcon.MouseUp += new System.Windows.Forms.MouseEventHandler(this.NotifyIconClickTest);
            // 
			// MainForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
			this.ClientSize = new System.Drawing.Size(0, 0);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
			this.LanguageKey = "application_title";
			this.Name = "MainForm";
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.WindowState = System.Windows.Forms.FormWindowState.Minimized;
			this.Activated += new System.EventHandler(this.MainFormActivated);
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainFormFormClosing);
			this.contextMenu.ResumeLayout(false);
			this.ResumeLayout(false);
		}
		private System.Windows.Forms.ToolStripSeparator toolStripOtherSourcesSeparator;
		private EvidenciasSQAToolStripMenuItem contextmenu_capturewindowfromlist;
		private System.Windows.Forms.ToolStripSeparator toolStripListCaptureSeparator;
		private EvidenciasSQAToolStripMenuItem contextmenu_openrecentcapture;
		private EvidenciasSQAToolStripMenuItem contextmenu_donate;
		private EvidenciasSQAToolStripMenuItem contextmenu_grabarpantalla;
		private EvidenciasSQAToolStripMenuItem contextmenu_extraertexto;
		private System.Windows.Forms.ToolStripSeparator toolStripPluginSeparator;
		private EvidenciasSQAToolStripMenuItem contextmenu_captureclipboard;
		private EvidenciasSQAToolStripMenuItem contextmenu_quicksettings;
		private System.Windows.Forms.ToolStripSeparator toolStripMiscSeparator;
		private EvidenciasSQAToolStripMenuItem contextmenu_help;
		private EvidenciasSQAToolStripMenuItem contextmenu_capturewindow;
		private EvidenciasSQAToolStripMenuItem contextmenu_about;
		private EvidenciasSQAToolStripMenuItem contextmenu_capturefullscreen;
		private EvidenciasSQAToolStripMenuItem contextmenu_capturelastregion;
		private EvidenciasSQAToolStripMenuItem contextmenu_capturearea;
		private System.Windows.Forms.NotifyIcon notifyIcon;
		private System.Windows.Forms.ToolStripSeparator toolStripCloseSeparator;
		private System.Windows.Forms.ToolStripSeparator toolStripOpenFolderSeparator;
		private EvidenciasSQAToolStripMenuItem contextmenu_exit;
		private System.Windows.Forms.ContextMenuStrip contextMenu;
		private EvidenciasSQAToolStripMenuItem contextmenu_settings;
	}
}
