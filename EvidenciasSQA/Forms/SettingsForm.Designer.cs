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
	partial class SettingsForm {
		/// <summary>
		/// Designer variable used to keep track of non-visual components.
		/// </summary>
		private System.ComponentModel.IContainer components = null;
		
		/// <summary>
		/// Disposes resources used by the form.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing) {
				if (components != null) {
					components.Dispose();
				}
			}
			base.Dispose(disposing);
		}
		
		/// <summary>
		/// This method is required for Windows Forms designer support.
		/// Do not change the method contents inside the source code editor. The Forms designer might
		/// not be able to load this method if it was changed manually.
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsForm));
			this.tabControl = new System.Windows.Forms.TabControl();
			this.tabHotkeys = new System.Windows.Forms.TabPage();
			this.pnlHeader = new System.Windows.Forms.Panel();
			this.btnClose = new EvidenciasSQAButton();
			this.lblTitle = new EvidenciasSQALabel();
			this.imgLogo = new System.Windows.Forms.PictureBox();
			this.pnlBody = new System.Windows.Forms.Panel();
			this.lblDescription = new EvidenciasSQALabel();
			this.pnlHotkeys = new System.Windows.Forms.TableLayoutPanel();
			this.lblRegion = new EvidenciasSQALabel();
			this.region_hotkeyControl = new EvidenciasSQA.Base.Controls.HotkeyControl();
			this.lblWindow = new EvidenciasSQALabel();
			this.window_hotkeyControl = new EvidenciasSQA.Base.Controls.HotkeyControl();
			this.lblFullscreen = new EvidenciasSQALabel();
			this.fullscreen_hotkeyControl = new EvidenciasSQA.Base.Controls.HotkeyControl();
			this.pnlFooter = new System.Windows.Forms.Panel();
			this.btnReset = new EvidenciasSQAButton();
			this.settings_cancel = new EvidenciasSQAButton();
			this.settings_confirm = new EvidenciasSQAButton();
			this.tabExtWeb = new System.Windows.Forms.TabPage();
			this.pnlExtWebHeader = new System.Windows.Forms.Panel();
			this.btnCloseExtWeb = new EvidenciasSQAButton();
			this.lblExtWebTitle = new EvidenciasSQALabel();
			this.imgExtWebLogo = new System.Windows.Forms.PictureBox();
			this.pnlExtWebBody = new System.Windows.Forms.Panel();
			this.extWebDescription = new EvidenciasSQALabel();
			this.pnlExtWebPath = new System.Windows.Forms.Panel();
			this.lblExtWebPathLabel = new EvidenciasSQALabel();
			this.extWebPathInput = new EvidenciasSQATextBox();
			this.extWebCopyBtn = new EvidenciasSQAButton();
			this.pnlExtWebGuide = new System.Windows.Forms.Panel();
			this.lblExtWebGuideTitle = new EvidenciasSQALabel();
			this.lblExtWebGuideSteps = new EvidenciasSQALabel();
			this.pnlExtWebFooter = new System.Windows.Forms.Panel();
			this.extWebOpenFolderBtn = new EvidenciasSQAButton();
			this.extWebOpenChromeBtn = new EvidenciasSQAButton();
			this.extWebOpenEdgeBtn = new EvidenciasSQAButton();
			this.extWebCloseBtn = new EvidenciasSQAButton();
			this.tabControl.SuspendLayout();
			this.tabHotkeys.SuspendLayout();
			this.pnlHeader.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.imgLogo)).BeginInit();
			this.pnlBody.SuspendLayout();
			this.pnlHotkeys.SuspendLayout();
			this.pnlFooter.SuspendLayout();
			this.tabExtWeb.SuspendLayout();
			this.pnlExtWebHeader.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.imgExtWebLogo)).BeginInit();
			this.pnlExtWebBody.SuspendLayout();
			this.pnlExtWebPath.SuspendLayout();
			this.pnlExtWebGuide.SuspendLayout();
			this.pnlExtWebFooter.SuspendLayout();
			this.SuspendLayout();
			// 
			// tabControl
			// 
			this.tabControl.Controls.Add(this.tabHotkeys);
			this.tabControl.Controls.Add(this.tabExtWeb);
			this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabControl.Location = new System.Drawing.Point(0, 0);
			this.tabControl.Name = "tabControl";
			this.tabControl.SelectedIndex = 0;
			this.tabControl.Size = new System.Drawing.Size(480, 382);
			this.tabControl.TabIndex = 0;
			// 
			// tabHotkeys
			// 
			this.tabHotkeys.BackColor = System.Drawing.Color.White;
			this.tabHotkeys.Controls.Add(this.pnlFooter);
			this.tabHotkeys.Controls.Add(this.pnlBody);
			this.tabHotkeys.Controls.Add(this.pnlHeader);
			this.tabHotkeys.Location = new System.Drawing.Point(4, 22);
			this.tabHotkeys.Name = "tabHotkeys";
			this.tabHotkeys.Padding = new System.Windows.Forms.Padding(3);
			this.tabHotkeys.Size = new System.Drawing.Size(472, 356);
			this.tabHotkeys.TabIndex = 0;
			this.tabHotkeys.Text = "Atajos de teclado";
			this.tabHotkeys.UseVisualStyleBackColor = true;
			// 
			// pnlHeader
			// 
			this.pnlHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(48)))), ((int)(((byte)(96)))));
			this.pnlHeader.Controls.Add(this.btnClose);
			this.pnlHeader.Controls.Add(this.lblTitle);
			this.pnlHeader.Controls.Add(this.imgLogo);
			this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
			this.pnlHeader.Location = new System.Drawing.Point(3, 3);
			this.pnlHeader.Name = "pnlHeader";
			this.pnlHeader.Padding = new System.Windows.Forms.Padding(20, 12, 20, 12);
			this.pnlHeader.Size = new System.Drawing.Size(466, 56);
			this.pnlHeader.TabIndex = 0;
			// 
			// btnClose
			// 
			this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.btnClose.BackColor = System.Drawing.Color.Transparent;
			this.btnClose.FlatAppearance.BorderSize = 0;
			this.btnClose.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(107)))), ((int)(((byte)(0)))));
			this.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.btnClose.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.btnClose.ForeColor = System.Drawing.Color.White;
			this.btnClose.Location = new System.Drawing.Point(426, 10);
			this.btnClose.Name = "btnClose";
			this.btnClose.Size = new System.Drawing.Size(30, 30);
			this.btnClose.TabIndex = 2;
			this.btnClose.Text = "✕";
			this.btnClose.UseVisualStyleBackColor = false;
			this.btnClose.Click += new System.EventHandler(this.Settings_cancelClick);
			// 
			// lblTitle
			// 
			this.lblTitle.AutoSize = true;
			this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lblTitle.ForeColor = System.Drawing.Color.White;
			this.lblTitle.LanguageKey = "settings_hotkeys_title";
			this.lblTitle.Location = new System.Drawing.Point(60, 15);
			this.lblTitle.Name = "lblTitle";
			this.lblTitle.Size = new System.Drawing.Size(220, 25);
			this.lblTitle.TabIndex = 1;
			this.lblTitle.Text = "Configurar atajos de teclado";
			// 
			// imgLogo
			// 
			this.imgLogo.Image = ((System.Drawing.Image)(resources.GetObject("imgLogo.Image")));
			this.imgLogo.Location = new System.Drawing.Point(20, 10);
			this.imgLogo.Name = "imgLogo";
			this.imgLogo.Size = new System.Drawing.Size(34, 34);
			this.imgLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
			this.imgLogo.TabIndex = 0;
			this.imgLogo.TabStop = false;
			// 
			// pnlBody
			// 
			this.pnlBody.BackColor = System.Drawing.Color.White;
			this.pnlBody.Controls.Add(this.lblDescription);
			this.pnlBody.Controls.Add(this.pnlHotkeys);
			this.pnlBody.Dock = System.Windows.Forms.DockStyle.Fill;
			this.pnlBody.Location = new System.Drawing.Point(3, 59);
			this.pnlBody.Name = "pnlBody";
			this.pnlBody.Padding = new System.Windows.Forms.Padding(24, 20, 24, 20);
			this.pnlBody.Size = new System.Drawing.Size(466, 260);
			this.pnlBody.TabIndex = 1;
			// 
			// lblDescription
			// 
			this.lblDescription.AutoSize = true;
			this.lblDescription.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lblDescription.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(55)))), ((int)(((byte)(65)))), ((int)(((byte)(81)))));
			this.lblDescription.LanguageKey = "settings_hotkeys_description";
			this.lblDescription.Location = new System.Drawing.Point(24, 20);
			this.lblDescription.Name = "lblDescription";
			this.lblDescription.Size = new System.Drawing.Size(300, 19);
			this.lblDescription.TabIndex = 0;
			this.lblDescription.Text = "Haz clic en un atajo y presiona la combinación deseada.";
			// 
			// pnlHotkeys
			// 
			this.pnlHotkeys.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.pnlHotkeys.ColumnCount = 2;
			this.pnlHotkeys.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 55F));
			this.pnlHotkeys.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 45F));
			this.pnlHotkeys.Controls.Add(this.lblRegion, 0, 0);
			this.pnlHotkeys.Controls.Add(this.region_hotkeyControl, 1, 0);
			this.pnlHotkeys.Controls.Add(this.lblWindow, 0, 1);
			this.pnlHotkeys.Controls.Add(this.window_hotkeyControl, 1, 1);
			this.pnlHotkeys.Controls.Add(this.lblFullscreen, 0, 2);
			this.pnlHotkeys.Controls.Add(this.fullscreen_hotkeyControl, 1, 2);
			this.pnlHotkeys.Location = new System.Drawing.Point(24, 56);
			this.pnlHotkeys.Name = "pnlHotkeys";
			this.pnlHotkeys.RowCount = 3;
			this.pnlHotkeys.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33F));
			this.pnlHotkeys.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33F));
			this.pnlHotkeys.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33F));
			this.pnlHotkeys.Size = new System.Drawing.Size(418, 184);
			this.pnlHotkeys.TabIndex = 1;
			// 
			// lblRegion
			// 
			this.lblRegion.Anchor = System.Windows.Forms.AnchorStyles.Left;
			this.lblRegion.AutoSize = true;
			this.lblRegion.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lblRegion.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(41)))), ((int)(((byte)(55)))));
			this.lblRegion.LanguageKey = "contextmenu_capturearea";
			this.lblRegion.Location = new System.Drawing.Point(3, 22);
			this.lblRegion.Name = "lblRegion";
			this.lblRegion.Size = new System.Drawing.Size(110, 20);
			this.lblRegion.TabIndex = 0;
			this.lblRegion.Text = "Capturar región";
			// 
			// region_hotkeyControl
			// 
			this.region_hotkeyControl.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
			this.region_hotkeyControl.BackColor = System.Drawing.SystemColors.Window;
			this.region_hotkeyControl.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.region_hotkeyControl.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.region_hotkeyControl.Hotkey = System.Windows.Forms.Keys.None;
			this.region_hotkeyControl.HotkeyModifiers = System.Windows.Forms.Keys.None;
			this.region_hotkeyControl.Location = new System.Drawing.Point(234, 18);
			this.region_hotkeyControl.MinimumSize = new System.Drawing.Size(180, 28);
			this.region_hotkeyControl.Name = "region_hotkeyControl";
			this.region_hotkeyControl.PropertyName = "RegionHotkey";
			this.region_hotkeyControl.Size = new System.Drawing.Size(181, 28);
			this.region_hotkeyControl.TabIndex = 1;
			// 
			// lblWindow
			// 
			this.lblWindow.Anchor = System.Windows.Forms.AnchorStyles.Left;
			this.lblWindow.AutoSize = true;
			this.lblWindow.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lblWindow.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(41)))), ((int)(((byte)(55)))));
			this.lblWindow.LanguageKey = "contextmenu_capturewindow";
			this.lblWindow.Location = new System.Drawing.Point(3, 86);
			this.lblWindow.Name = "lblWindow";
			this.lblWindow.Size = new System.Drawing.Size(140, 20);
			this.lblWindow.TabIndex = 2;
			this.lblWindow.Text = "Capturar ventana activa";
			// 
			// window_hotkeyControl
			// 
			this.window_hotkeyControl.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
			this.window_hotkeyControl.BackColor = System.Drawing.SystemColors.Window;
			this.window_hotkeyControl.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.window_hotkeyControl.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.window_hotkeyControl.Hotkey = System.Windows.Forms.Keys.None;
			this.window_hotkeyControl.HotkeyModifiers = System.Windows.Forms.Keys.None;
			this.window_hotkeyControl.Location = new System.Drawing.Point(234, 82);
			this.window_hotkeyControl.MinimumSize = new System.Drawing.Size(180, 28);
			this.window_hotkeyControl.Name = "window_hotkeyControl";
			this.window_hotkeyControl.PropertyName = "WindowHotkey";
			this.window_hotkeyControl.Size = new System.Drawing.Size(181, 28);
			this.window_hotkeyControl.TabIndex = 3;
			// 
			// lblFullscreen
			// 
			this.lblFullscreen.Anchor = System.Windows.Forms.AnchorStyles.Left;
			this.lblFullscreen.AutoSize = true;
			this.lblFullscreen.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lblFullscreen.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(41)))), ((int)(((byte)(55)))));
			this.lblFullscreen.LanguageKey = "contextmenu_capturefullscreen";
			this.lblFullscreen.Location = new System.Drawing.Point(3, 150);
			this.lblFullscreen.Name = "lblFullscreen";
			this.lblFullscreen.Size = new System.Drawing.Size(150, 20);
			this.lblFullscreen.TabIndex = 4;
			this.lblFullscreen.Text = "Capturar todas las pantallas";
			// 
			// fullscreen_hotkeyControl
			// 
			this.fullscreen_hotkeyControl.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
			this.fullscreen_hotkeyControl.BackColor = System.Drawing.SystemColors.Window;
			this.fullscreen_hotkeyControl.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.fullscreen_hotkeyControl.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.fullscreen_hotkeyControl.Hotkey = System.Windows.Forms.Keys.None;
			this.fullscreen_hotkeyControl.HotkeyModifiers = System.Windows.Forms.Keys.None;
			this.fullscreen_hotkeyControl.Location = new System.Drawing.Point(234, 146);
			this.fullscreen_hotkeyControl.MinimumSize = new System.Drawing.Size(180, 28);
			this.fullscreen_hotkeyControl.Name = "fullscreen_hotkeyControl";
			this.fullscreen_hotkeyControl.PropertyName = "FullscreenHotkey";
			this.fullscreen_hotkeyControl.Size = new System.Drawing.Size(181, 28);
			this.fullscreen_hotkeyControl.TabIndex = 5;
			// 
			// pnlFooter
			// 
			this.pnlFooter.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(251)))));
			this.pnlFooter.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.pnlFooter.Controls.Add(this.btnReset);
			this.pnlFooter.Controls.Add(this.settings_cancel);
			this.pnlFooter.Controls.Add(this.settings_confirm);
			this.pnlFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.pnlFooter.Location = new System.Drawing.Point(3, 319);
			this.pnlFooter.Name = "pnlFooter";
			this.pnlFooter.Padding = new System.Windows.Forms.Padding(20, 12, 20, 12);
			this.pnlFooter.Size = new System.Drawing.Size(466, 58);
			this.pnlFooter.TabIndex = 2;
			// 
			// btnReset
			// 
			this.btnReset.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)));
			this.btnReset.BackColor = System.Drawing.Color.Transparent;
			this.btnReset.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(156)))), ((int)(((byte)(163)))), ((int)(((byte)(175)))));
			this.btnReset.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
			this.btnReset.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.btnReset.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.btnReset.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(116)))), ((int)(((byte)(139)))));
			this.btnReset.LanguageKey = "settings_hotkeys_reset";
			this.btnReset.Location = new System.Drawing.Point(20, 10);
			this.btnReset.Name = "btnReset";
			this.btnReset.Size = new System.Drawing.Size(130, 34);
			this.btnReset.TabIndex = 2;
			this.btnReset.Text = "Restablecer valores";
			this.btnReset.UseVisualStyleBackColor = true;
			this.btnReset.Click += new System.EventHandler(this.BtnResetClick);
			// 
			// settings_cancel
			// 
			this.settings_cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.settings_cancel.BackColor = System.Drawing.Color.Transparent;
			this.settings_cancel.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(156)))), ((int)(((byte)(163)))), ((int)(((byte)(175)))));
			this.settings_cancel.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
			this.settings_cancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.settings_cancel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.settings_cancel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(116)))), ((int)(((byte)(139)))));
			this.settings_cancel.LanguageKey = "CANCEL";
			this.settings_cancel.Location = new System.Drawing.Point(276, 10);
			this.settings_cancel.Name = "settings_cancel";
			this.settings_cancel.Size = new System.Drawing.Size(80, 34);
			this.settings_cancel.TabIndex = 1;
			this.settings_cancel.Text = "Cancelar";
			this.settings_cancel.UseVisualStyleBackColor = true;
			this.settings_cancel.Click += new System.EventHandler(this.Settings_cancelClick);
			// 
			// settings_confirm
			// 
			this.settings_confirm.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.settings_confirm.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(48)))), ((int)(((byte)(96)))));
			this.settings_confirm.FlatAppearance.BorderSize = 0;
			this.settings_confirm.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(30)))), ((int)(((byte)(60)))));
			this.settings_confirm.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.settings_confirm.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.settings_confirm.ForeColor = System.Drawing.Color.White;
			this.settings_confirm.LanguageKey = "OK";
			this.settings_confirm.Location = new System.Drawing.Point(366, 10);
			this.settings_confirm.Name = "settings_confirm";
			this.settings_confirm.Size = new System.Drawing.Size(80, 34);
			this.settings_confirm.TabIndex = 0;
			this.settings_confirm.Text = "Guardar";
			this.settings_confirm.UseVisualStyleBackColor = false;
			this.settings_confirm.Click += new System.EventHandler(this.Settings_okayClick);
			// 
			// tabExtWeb
			// 
			this.tabExtWeb.BackColor = System.Drawing.Color.White;
			this.tabExtWeb.Controls.Add(this.pnlExtWebFooter);
			this.tabExtWeb.Controls.Add(this.pnlExtWebBody);
			this.tabExtWeb.Controls.Add(this.pnlExtWebHeader);
			this.tabExtWeb.Location = new System.Drawing.Point(4, 22);
			this.tabExtWeb.Name = "tabExtWeb";
			this.tabExtWeb.Padding = new System.Windows.Forms.Padding(3);
			this.tabExtWeb.Size = new System.Drawing.Size(472, 356);
			this.tabExtWeb.TabIndex = 1;
			this.tabExtWeb.Text = "Ruta Ext Web";
			this.tabExtWeb.UseVisualStyleBackColor = true;
			// 
			// pnlExtWebHeader
			// 
			this.pnlExtWebHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(48)))), ((int)(((byte)(96)))));
			this.pnlExtWebHeader.Controls.Add(this.btnCloseExtWeb);
			this.pnlExtWebHeader.Controls.Add(this.lblExtWebTitle);
			this.pnlExtWebHeader.Controls.Add(this.imgExtWebLogo);
			this.pnlExtWebHeader.Dock = System.Windows.Forms.DockStyle.Top;
			this.pnlExtWebHeader.Location = new System.Drawing.Point(3, 3);
			this.pnlExtWebHeader.Name = "pnlExtWebHeader";
			this.pnlExtWebHeader.Padding = new System.Windows.Forms.Padding(20, 12, 20, 12);
			this.pnlExtWebHeader.Size = new System.Drawing.Size(466, 56);
			this.pnlExtWebHeader.TabIndex = 0;
			// 
			// btnCloseExtWeb
			// 
			this.btnCloseExtWeb.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.btnCloseExtWeb.BackColor = System.Drawing.Color.Transparent;
			this.btnCloseExtWeb.FlatAppearance.BorderSize = 0;
			this.btnCloseExtWeb.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(107)))), ((int)(((byte)(0)))));
			this.btnCloseExtWeb.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.btnCloseExtWeb.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.btnCloseExtWeb.ForeColor = System.Drawing.Color.White;
			this.btnCloseExtWeb.Location = new System.Drawing.Point(426, 10);
			this.btnCloseExtWeb.Name = "btnCloseExtWeb";
			this.btnCloseExtWeb.Size = new System.Drawing.Size(30, 30);
			this.btnCloseExtWeb.TabIndex = 2;
			this.btnCloseExtWeb.Text = "✕";
			this.btnCloseExtWeb.UseVisualStyleBackColor = false;
			this.btnCloseExtWeb.Click += new System.EventHandler(this.Settings_cancelClick);
			// 
			// lblExtWebTitle
			// 
			this.lblExtWebTitle.AutoSize = true;
			this.lblExtWebTitle.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lblExtWebTitle.ForeColor = System.Drawing.Color.White;
			this.lblExtWebTitle.LanguageKey = "settings_extweb_title";
			this.lblExtWebTitle.Location = new System.Drawing.Point(60, 15);
			this.lblExtWebTitle.Name = "lblExtWebTitle";
			this.lblExtWebTitle.Size = new System.Drawing.Size(220, 25);
			this.lblExtWebTitle.TabIndex = 1;
			this.lblExtWebTitle.Text = "Extensión de Navegador [Ext_Web]";
			// 
			// imgExtWebLogo
			// 
			this.imgExtWebLogo.Image = ((System.Drawing.Image)(resources.GetObject("imgExtWebLogo.Image")));
			this.imgExtWebLogo.Location = new System.Drawing.Point(20, 10);
			this.imgExtWebLogo.Name = "imgExtWebLogo";
			this.imgExtWebLogo.Size = new System.Drawing.Size(34, 34);
			this.imgExtWebLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
			this.imgExtWebLogo.TabIndex = 0;
			this.imgExtWebLogo.TabStop = false;
			// 
			// pnlExtWebBody
			// 
			this.pnlExtWebBody.BackColor = System.Drawing.Color.White;
			this.pnlExtWebBody.Controls.Add(this.pnlExtWebGuide);
			this.pnlExtWebBody.Controls.Add(this.pnlExtWebPath);
			this.pnlExtWebBody.Controls.Add(this.extWebDescription);
			this.pnlExtWebBody.Dock = System.Windows.Forms.DockStyle.Fill;
			this.pnlExtWebBody.Location = new System.Drawing.Point(3, 59);
			this.pnlExtWebBody.Name = "pnlExtWebBody";
			this.pnlExtWebBody.Padding = new System.Windows.Forms.Padding(24, 20, 24, 20);
			this.pnlExtWebBody.Size = new System.Drawing.Size(466, 260);
			this.pnlExtWebBody.TabIndex = 1;
			// 
			// extWebDescription
			// 
			this.extWebDescription.AutoSize = true;
			this.extWebDescription.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.extWebDescription.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(55)))), ((int)(((byte)(65)))), ((int)(((byte)(81)))));
			this.extWebDescription.LanguageKey = "settings_extweb_description";
			this.extWebDescription.Location = new System.Drawing.Point(24, 20);
			this.extWebDescription.Name = "extWebDescription";
			this.extWebDescription.Size = new System.Drawing.Size(418, 38);
			this.extWebDescription.TabIndex = 0;
			this.extWebDescription.Text = "Para poder realizar capturas de pantalla y capturar evidencias de forma directa y automática desde Google Chrome o Microsoft Edge, debes cargar la extensión web desde su carpeta en tu ordenador.";
			// 
			// pnlExtWebPath
			// 
			this.pnlExtWebPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.pnlExtWebPath.Controls.Add(this.extWebCopyBtn);
			this.pnlExtWebPath.Controls.Add(this.extWebPathInput);
			this.pnlExtWebPath.Controls.Add(this.lblExtWebPathLabel);
			this.pnlExtWebPath.Location = new System.Drawing.Point(24, 78);
			this.pnlExtWebPath.Name = "pnlExtWebPath";
			this.pnlExtWebPath.Size = new System.Drawing.Size(418, 68);
			this.pnlExtWebPath.TabIndex = 1;
			// 
			// lblExtWebPathLabel
			// 
			this.lblExtWebPathLabel.AutoSize = true;
			this.lblExtWebPathLabel.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lblExtWebPathLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(48)))), ((int)(((byte)(96)))));
			this.lblExtWebPathLabel.LanguageKey = "settings_extweb_path_label";
			this.lblExtWebPathLabel.Location = new System.Drawing.Point(0, 0);
			this.lblExtWebPathLabel.Name = "lblExtWebPathLabel";
			this.lblExtWebPathLabel.Size = new System.Drawing.Size(150, 21);
			this.lblExtWebPathLabel.TabIndex = 0;
			this.lblExtWebPathLabel.Text = "Ruta de la Extensión:";
			// 
			// extWebPathInput
			// 
			this.extWebPathInput.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.extWebPathInput.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(243)))), ((int)(((byte)(244)))), ((int)(((byte)(246)))));
			this.extWebPathInput.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.extWebPathInput.Font = new System.Drawing.Font("Courier New", 10.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.extWebPathInput.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(41)))), ((int)(((byte)(55)))));
			this.extWebPathInput.Location = new System.Drawing.Point(0, 28);
			this.extWebPathInput.Name = "extWebPathInput";
			this.extWebPathInput.ReadOnly = true;
			this.extWebPathInput.Size = new System.Drawing.Size(330, 25);
			this.extWebPathInput.TabIndex = 1;
			this.extWebPathInput.Text = "Cargando ruta...";
			// 
			// extWebCopyBtn
			// 
			this.extWebCopyBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.extWebCopyBtn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(48)))), ((int)(((byte)(96)))));
			this.extWebCopyBtn.FlatAppearance.BorderSize = 0;
			this.extWebCopyBtn.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(30)))), ((int)(((byte)(60)))));
			this.extWebCopyBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.extWebCopyBtn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.extWebCopyBtn.ForeColor = System.Drawing.Color.White;
			this.extWebCopyBtn.LanguageKey = "settings_extweb_copy";
			this.extWebCopyBtn.Location = new System.Drawing.Point(336, 26);
			this.extWebCopyBtn.Name = "extWebCopyBtn";
			this.extWebCopyBtn.Size = new System.Drawing.Size(82, 29);
			this.extWebCopyBtn.TabIndex = 2;
			this.extWebCopyBtn.Text = "Copiar";
			this.extWebCopyBtn.UseVisualStyleBackColor = false;
			// 
			// pnlExtWebGuide
			// 
			this.pnlExtWebGuide.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.pnlExtWebGuide.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(239)))), ((int)(((byte)(246)))), ((int)(((byte)(255)))));
			this.pnlExtWebGuide.Controls.Add(this.lblExtWebGuideSteps);
			this.pnlExtWebGuide.Controls.Add(this.lblExtWebGuideTitle);
			this.pnlExtWebGuide.Location = new System.Drawing.Point(24, 152);
			this.pnlExtWebGuide.Name = "pnlExtWebGuide";
			this.pnlExtWebGuide.Padding = new System.Windows.Forms.Padding(14, 10, 14, 10);
			this.pnlExtWebGuide.Size = new System.Drawing.Size(418, 108);
			this.pnlExtWebGuide.TabIndex = 2;
			// 
			// lblExtWebGuideTitle
			// 
			this.lblExtWebGuideTitle.AutoSize = true;
			this.lblExtWebGuideTitle.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lblExtWebGuideTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(64)))), ((int)(((byte)(175)))));
			this.lblExtWebGuideTitle.LanguageKey = "settings_extweb_guide_title";
			this.lblExtWebGuideTitle.Location = new System.Drawing.Point(14, 10);
			this.lblExtWebGuideTitle.Name = "lblExtWebGuideTitle";
			this.lblExtWebGuideTitle.Size = new System.Drawing.Size(150, 21);
			this.lblExtWebGuideTitle.TabIndex = 0;
			this.lblExtWebGuideTitle.Text = "💡 ¿Cómo cargar la extensión?";
			// 
			// lblExtWebGuideSteps
			// 
			this.lblExtWebGuideSteps.AutoSize = true;
			this.lblExtWebGuideSteps.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.lblExtWebGuideSteps.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(58)))), ((int)(((byte)(138)))));
			this.lblExtWebGuideSteps.LanguageKey = "settings_extweb_guide_steps";
			this.lblExtWebGuideSteps.Location = new System.Drawing.Point(14, 36);
			this.lblExtWebGuideSteps.MaximumSize = new System.Drawing.Size(380, 0);
			this.lblExtWebGuideSteps.Name = "lblExtWebGuideSteps";
			this.lblExtWebGuideSteps.Size = new System.Drawing.Size(380, 72);
			this.lblExtWebGuideSteps.TabIndex = 1;
			this.lblExtWebGuideSteps.Text = "1. Pulsa \"Abrir Chrome\" o \"Abrir Edge\" para ir a la página de extensiones del navegador.\r\n2. Activa el \"Modo de desarrollador\" (esquina superior derecha).\r\n3. Haz clic en \"Cargar descomprimida\" (o \"Load unpacked\") y selecciona la carpeta abierta por este visor.\r\n4. Si el navegador muestra \"Disable developer mode extensions\", actívala o desactiva esa advertencia para poder cargar la extensión.";
			// 
			// pnlExtWebFooter
			// 
			this.pnlExtWebFooter.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(251)))));
			this.pnlExtWebFooter.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.pnlExtWebFooter.Controls.Add(this.extWebCloseBtn);
			this.pnlExtWebFooter.Controls.Add(this.extWebOpenEdgeBtn);
			this.pnlExtWebFooter.Controls.Add(this.extWebOpenChromeBtn);
			this.pnlExtWebFooter.Controls.Add(this.extWebOpenFolderBtn);
			this.pnlExtWebFooter.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.pnlExtWebFooter.Location = new System.Drawing.Point(3, 319);
			this.pnlExtWebFooter.Name = "pnlExtWebFooter";
			this.pnlExtWebFooter.Padding = new System.Windows.Forms.Padding(20, 12, 20, 12);
			this.pnlExtWebFooter.Size = new System.Drawing.Size(466, 58);
			this.pnlExtWebFooter.TabIndex = 2;
			// 
			// extWebOpenFolderBtn
			// 
			this.extWebOpenFolderBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)));
			this.extWebOpenFolderBtn.BackColor = System.Drawing.Color.Transparent;
			this.extWebOpenFolderBtn.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(156)))), ((int)(((byte)(163)))), ((int)(((byte)(175)))));
			this.extWebOpenFolderBtn.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
			this.extWebOpenFolderBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.extWebOpenFolderBtn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.extWebOpenFolderBtn.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(116)))), ((int)(((byte)(139)))));
			this.extWebOpenFolderBtn.LanguageKey = "settings_extweb_open_folder";
			this.extWebOpenFolderBtn.Location = new System.Drawing.Point(20, 10);
			this.extWebOpenFolderBtn.Name = "extWebOpenFolderBtn";
			this.extWebOpenFolderBtn.Size = new System.Drawing.Size(92, 34);
			this.extWebOpenFolderBtn.TabIndex = 0;
			this.extWebOpenFolderBtn.Text = "📂\nAbrir Carpeta";
			this.extWebOpenFolderBtn.UseVisualStyleBackColor = true;
			// 
			// extWebOpenChromeBtn
			// 
			this.extWebOpenChromeBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)));
			this.extWebOpenChromeBtn.BackColor = System.Drawing.Color.Transparent;
			this.extWebOpenChromeBtn.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(156)))), ((int)(((byte)(163)))), ((int)(((byte)(175)))));
			this.extWebOpenChromeBtn.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
			this.extWebOpenChromeBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.extWebOpenChromeBtn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.extWebOpenChromeBtn.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(116)))), ((int)(((byte)(139)))));
			this.extWebOpenChromeBtn.LanguageKey = "settings_extweb_open_chrome";
			this.extWebOpenChromeBtn.Location = new System.Drawing.Point(122, 10);
			this.extWebOpenChromeBtn.Name = "extWebOpenChromeBtn";
			this.extWebOpenChromeBtn.Size = new System.Drawing.Size(92, 34);
			this.extWebOpenChromeBtn.TabIndex = 1;
			this.extWebOpenChromeBtn.Text = "🌐\nAbrir Chrome";
			this.extWebOpenChromeBtn.UseVisualStyleBackColor = true;
			// 
			// extWebOpenEdgeBtn
			// 
			this.extWebOpenEdgeBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)));
			this.extWebOpenEdgeBtn.BackColor = System.Drawing.Color.Transparent;
			this.extWebOpenEdgeBtn.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(156)))), ((int)(((byte)(163)))), ((int)(((byte)(175)))));
			this.extWebOpenEdgeBtn.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(245)))), ((int)(((byte)(249)))));
			this.extWebOpenEdgeBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.extWebOpenEdgeBtn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.extWebOpenEdgeBtn.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(116)))), ((int)(((byte)(139)))));
			this.extWebOpenEdgeBtn.LanguageKey = "settings_extweb_open_edge";
			this.extWebOpenEdgeBtn.Location = new System.Drawing.Point(224, 10);
			this.extWebOpenEdgeBtn.Name = "extWebOpenEdgeBtn";
			this.extWebOpenEdgeBtn.Size = new System.Drawing.Size(92, 34);
			this.extWebOpenEdgeBtn.TabIndex = 2;
			this.extWebOpenEdgeBtn.Text = "🌐\nAbrir Edge";
			this.extWebOpenEdgeBtn.UseVisualStyleBackColor = true;
			// 
			// extWebCloseBtn
			// 
			this.extWebCloseBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.extWebCloseBtn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(48)))), ((int)(((byte)(96)))));
			this.extWebCloseBtn.FlatAppearance.BorderSize = 0;
			this.extWebCloseBtn.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(30)))), ((int)(((byte)(60)))));
			this.extWebCloseBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
			this.extWebCloseBtn.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.extWebCloseBtn.ForeColor = System.Drawing.Color.White;
			this.extWebCloseBtn.LanguageKey = "OK";
			this.extWebCloseBtn.Location = new System.Drawing.Point(366, 10);
			this.extWebCloseBtn.Name = "extWebCloseBtn";
			this.extWebCloseBtn.Size = new System.Drawing.Size(80, 34);
			this.extWebCloseBtn.TabIndex = 3;
			this.extWebCloseBtn.Text = "Aceptar";
			this.extWebCloseBtn.UseVisualStyleBackColor = false;
			this.extWebCloseBtn.Click += new System.EventHandler(this.Settings_cancelClick);
			// 
			// SettingsForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
			this.BackColor = System.Drawing.Color.White;
			this.ClientSize = new System.Drawing.Size(480, 382);
			this.Controls.Add(this.tabControl);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.Name = "SettingsForm";
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "Configuración";
			this.tabControl.ResumeLayout(false);
			this.tabHotkeys.ResumeLayout(false);
			this.pnlHeader.ResumeLayout(false);
			this.pnlHeader.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.imgLogo)).EndInit();
			this.pnlBody.ResumeLayout(false);
			this.pnlBody.PerformLayout();
			this.pnlHotkeys.ResumeLayout(false);
			this.pnlHotkeys.PerformLayout();
			this.pnlFooter.ResumeLayout(false);
			this.tabExtWeb.ResumeLayout(false);
			this.pnlExtWebHeader.ResumeLayout(false);
			this.pnlExtWebHeader.PerformLayout();
			((System.ComponentModel.ISupportInitialize)(this.imgExtWebLogo)).EndInit();
			this.pnlExtWebBody.ResumeLayout(false);
			this.pnlExtWebBody.PerformLayout();
			this.pnlExtWebPath.ResumeLayout(false);
			this.pnlExtWebPath.PerformLayout();
			this.pnlExtWebGuide.ResumeLayout(false);
			this.pnlExtWebGuide.PerformLayout();
			this.pnlExtWebFooter.ResumeLayout(false);
			this.ResumeLayout(false);

		}
		private System.Windows.Forms.TabControl tabControl;
		private System.Windows.Forms.TabPage tabHotkeys;
		private System.Windows.Forms.TabPage tabExtWeb;
		private System.Windows.Forms.Panel pnlHeader;
		private EvidenciasSQAButton btnClose;
		private EvidenciasSQALabel lblTitle;
		private System.Windows.Forms.PictureBox imgLogo;
		private System.Windows.Forms.Panel pnlBody;
		private EvidenciasSQALabel lblDescription;
		private System.Windows.Forms.TableLayoutPanel pnlHotkeys;
		private EvidenciasSQALabel lblRegion;
		private EvidenciasSQA.Base.Controls.HotkeyControl region_hotkeyControl;
		private EvidenciasSQALabel lblWindow;
		private EvidenciasSQA.Base.Controls.HotkeyControl window_hotkeyControl;
		private EvidenciasSQALabel lblFullscreen;
		private EvidenciasSQA.Base.Controls.HotkeyControl fullscreen_hotkeyControl;
		private System.Windows.Forms.Panel pnlFooter;
		private EvidenciasSQAButton btnReset;
		private EvidenciasSQAButton settings_cancel;
		private EvidenciasSQAButton settings_confirm;
		private System.Windows.Forms.Panel pnlExtWebHeader;
		private EvidenciasSQAButton btnCloseExtWeb;
		private EvidenciasSQALabel lblExtWebTitle;
		private System.Windows.Forms.PictureBox imgExtWebLogo;
		private System.Windows.Forms.Panel pnlExtWebBody;
		private EvidenciasSQALabel extWebDescription;
		private System.Windows.Forms.Panel pnlExtWebPath;
		private EvidenciasSQALabel lblExtWebPathLabel;
		private EvidenciasSQATextBox extWebPathInput;
		private EvidenciasSQAButton extWebCopyBtn;
		private System.Windows.Forms.Panel pnlExtWebGuide;
		private EvidenciasSQALabel lblExtWebGuideTitle;
		private EvidenciasSQALabel lblExtWebGuideSteps;
		private System.Windows.Forms.Panel pnlExtWebFooter;
		private EvidenciasSQAButton extWebOpenFolderBtn;
		private EvidenciasSQAButton extWebOpenChromeBtn;
		private EvidenciasSQAButton extWebOpenEdgeBtn;
		private EvidenciasSQAButton extWebCloseBtn;
	}
}