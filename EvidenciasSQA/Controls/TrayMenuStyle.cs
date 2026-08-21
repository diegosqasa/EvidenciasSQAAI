/*
 * EvidenciasSQA - a free and open source screenshot tool
 * Copyright (C) 2004-2026  Thomas Braun, Jens Klingen, Robin Krom
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 1 of the License or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 */

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace EvidenciasSQA.Controls
{
    /// <summary>
    /// Kind of vector icon to render for the tray context menu items.
    /// </summary>
    public enum TrayIconKind
    {
        Region,
        Window,
        Monitor,
        WindowList,
        Eye,
        Close,
        Clipboard,
        Settings
    }

    /// <summary>
    /// Factory for the SQASA.co premium style tray menu:
    /// dark translucent blue palette with orange accents (#FF6B00), Segoe UI Variable
    /// typography, linear vector icons, an SQA header bar and the "SQASA" watermark.
    /// </summary>
    public static class TrayMenuStyle
    {
        private static readonly Color IconColor = Color.FromArgb(226, 230, 238);

        /// <summary>SQASA corporate orange accent.</summary>
        public static readonly Color AccentOrange = Color.FromArgb(0xFF, 0x6B, 0x00);

        /// <summary>SQASA corporate dark blue start (header gradient).</summary>
        public static readonly Color HeaderGradientStart = Color.FromArgb(0x00, 0x2B, 0x55);

        /// <summary>SQASA corporate blue end (header gradient).</summary>
        public static readonly Color HeaderGradientEnd = Color.FromArgb(0x00, 0x40, 0x80);

        /// <summary>Translucent deep blue used by the acrylic tray menu surface.</summary>
        public static readonly Color MenuBackgroundColor = Color.FromArgb(246, 11, 21, 38);

        /// <summary>SQASA corporate blue used as the rounded hover selection.</summary>
        public static readonly Color SelectionBackgroundColor = Color.FromArgb(200, 0, 64, 128);

        /// <summary>Corner radius applied to the menu surface and selection.</summary>
        public const int MenuRadius = 12;

        /// <summary>Corner radius applied to the item selection (hover) background.</summary>
        public const int SelectionRadius = 6;

        /// <summary>
        /// Returns the Segoe UI Variable font with a Segoe UI fallback.
        /// </summary>
        public static Font GetMenuFont(float sizeInPoints = 9.25f)
        {
            foreach (string familyName in new[] { "Segoe UI Variable", "Segoe UI" })
            {
                try
                {
                    using (var family = new FontFamily(familyName))
                    {
                        return new Font(family, sizeInPoints);
                    }
                }
                catch (Exception)
                {
                    // Try the next family
                }
            }

            return new Font(FontFamily.GenericSansSerif, sizeInPoints);
        }

        /// <summary>
        /// Creates a linear style vector icon for a tray menu item.
        /// </summary>
        public static Bitmap CreateVectorIcon(TrayIconKind kind, int size = 32)
        {
            var bitmap = new Bitmap(size, size);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                using var pen = new Pen(IconColor, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                float s = size;
                float m = s * 0.125f;
                float x1 = m, y1 = m, x2 = s - m, y2 = s - m;
                float w = x2 - x1, h = y2 - y1;

                switch (kind)
                {
                    case TrayIconKind.Region:
                        pen.DashStyle = DashStyle.Dash;
                        graphics.DrawRectangle(pen, x1, y1, w, h);
                        break;
                    case TrayIconKind.Window:
                        graphics.DrawRectangle(pen, x1, y1, w, h);
                        graphics.DrawLine(pen, x1, y1 + h * 0.22f, x2, y1 + h * 0.22f);
                        float dotR = s * 0.03f;
                        using (var dotBrush = new SolidBrush(IconColor))
                        {
                            graphics.FillEllipse(dotBrush, x1 + h * 0.16f, y1 + h * 0.09f, dotR * 2, dotR * 2);
                            graphics.FillEllipse(dotBrush, x1 + h * 0.30f, y1 + h * 0.09f, dotR * 2, dotR * 2);
                        }
                        break;
                    case TrayIconKind.Monitor:
                        graphics.DrawRectangle(pen, x1, y1, w, h * 0.72f);
                        graphics.DrawLine(pen, s * 0.5f, y1 + h * 0.72f, s * 0.5f, y2);
                        graphics.DrawLine(pen, s * 0.5f - h * 0.16f, y2, s * 0.5f + h * 0.16f, y2);
                        break;
                    case TrayIconKind.WindowList:
                        graphics.DrawRectangle(pen, x1, y1, w, h);
                        graphics.DrawLine(pen, x1 + w * 0.18f, y1 + h * 0.22f, x2 - w * 0.18f, y1 + h * 0.22f);
                        graphics.DrawLine(pen, x1 + w * 0.18f, y1 + h * 0.5f, x2 - w * 0.18f, y1 + h * 0.5f);
                        graphics.DrawLine(pen, x1 + w * 0.18f, y1 + h * 0.78f, x2 - w * 0.18f, y1 + h * 0.78f);
                        break;
                    case TrayIconKind.Eye:
                        graphics.DrawArc(pen, x1, y1 + h * 0.22f, w, h * 0.56f, 0f, 360f);
                        using (var eyePen = new Pen(IconColor, 1.6f))
                        {
                            graphics.DrawEllipse(eyePen, s * 0.38f, y1 + h * 0.38f, w * 0.24f, h * 0.24f);
                        }
                        break;
                    case TrayIconKind.Close:
                        float cx = s * 0.5f, cy = s * 0.5f, r = w * 0.32f;
                        graphics.DrawLine(pen, cx - r, cy - r, cx + r, cy + r);
                        graphics.DrawLine(pen, cx + r, cy - r, cx - r, cy + r);
                        break;
                    case TrayIconKind.Clipboard:
                        graphics.DrawRectangle(pen, x1 + w * 0.14f, y1 + h * 0.2f, w * 0.72f, h * 0.74f);
                        graphics.DrawArc(pen, x1 + w * 0.30f, y1, w * 0.40f, h * 0.30f, 180f, 180f);
                        break;
                    case TrayIconKind.Settings:
                        // Gear: outer ring with four spokes and an inner circle
                        graphics.DrawEllipse(pen, x1 + w * 0.18f, y1 + h * 0.18f, w * 0.64f, h * 0.64f);
                        float gcx = s * 0.5f, gcy = s * 0.5f, gr = w * 0.14f, gs = w * 0.44f;
                        for (int i = 0; i < 4; i++)
                        {
                            float angle = (float)(Math.PI / 180.0 * (45 + i * 90));
                            float cos = (float)Math.Cos(angle), sin = (float)Math.Sin(angle);
                            graphics.DrawLine(pen, gcx + cos * gr, gcy + sin * gr, gcx + cos * gs, gcy + sin * gs);
                        }
                        using (var gearCenter = new SolidBrush(IconColor))
                        {
                            graphics.FillEllipse(gearCenter, gcx - gr * 0.62f, gcy - gr * 0.62f, gr * 1.24f, gr * 1.24f);
                        }
                        break;
                }
            }

            return bitmap;
        }

        /// <summary>
        /// Applies the premium tray menu style: font, vector icons and SQA header.
        /// </summary>
        public static void ApplyTo(ContextMenuStrip menu, int deviceDpi)
        {
            if (menu == null)
            {
                return;
            }

            menu.Font = GetMenuFont();
            menu.BackColor = MenuBackgroundColor;
            menu.Padding = new Padding(4, 2, 4, 2);

            var iconSize = Math.Max(16, menu.ImageScalingSize.Width);
            AssignIcon(menu, "contextmenu_capturearea", TrayIconKind.Region, iconSize);
            AssignIcon(menu, "contextmenu_capturewindow", TrayIconKind.Window, iconSize);
            AssignIcon(menu, "contextmenu_capturefullscreen", TrayIconKind.Monitor, iconSize);
            AssignIcon(menu, "contextmenu_capturewindowfromlist", TrayIconKind.WindowList, iconSize);
            AssignIcon(menu, "contextmenu_openfile", TrayIconKind.Eye, iconSize);
            AssignIcon(menu, "contextmenu_settings", TrayIconKind.Settings, iconSize);
            AssignIcon(menu, "contextmenu_exit", TrayIconKind.Close, iconSize);
            AssignIcon(menu, "contextmenu_captureclipboard", TrayIconKind.Clipboard, iconSize);

            InsertHeader(menu, deviceDpi);
            InsertFooter(menu, deviceDpi);
        }

        private static void InsertFooter(ContextMenuStrip menu, int deviceDpi)
        {
            float dpiFactor = deviceDpi / 96f;
            var footerControl = new FooterControl
            {
                Dock = DockStyle.Fill,
                Size = new Size(0, (int)(22 * dpiFactor))
            };
            var footerHost = new ToolStripControlHost(footerControl)
            {
                Name = "trayFooter",
                AutoSize = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            menu.Items.Add(footerHost);
        }

        private static void AssignIcon(ContextMenuStrip menu, string itemName, TrayIconKind kind, int size)
        {
            foreach (ToolStripItem item in menu.Items)
            {
                if (item.Name == itemName)
                {
                    item.Image = CreateVectorIcon(kind, size);
                    item.ImageScaling = ToolStripItemImageScaling.None;
                    return;
                }
            }
        }

        private static void InsertHeader(ContextMenuStrip menu, int deviceDpi)
        {
            float dpiFactor = deviceDpi / 96f;
            var headerControl = new HeaderControl
            {
                Dock = DockStyle.Fill,
                Size = new Size(0, (int)(48 * dpiFactor))
            };
            var headerHost = new ToolStripControlHost(headerControl)
            {
                Name = "trayHeader",
                AutoSize = false,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            menu.Items.Insert(0, headerHost);
        }

        /// <summary>
        /// Applies a rounded window region to a ToolStripDropDown so the menu surface
        /// follows the Windows 11 rounded corner look (the DWM shadow follows the region).
        /// Best-effort: if anything fails the window keeps its rectangular region.
        /// </summary>
        public static void ApplyRoundedRegion(ToolStripDropDown dropDown, int radius = MenuRadius)
        {
            if (dropDown == null || dropDown.IsDisposed || !dropDown.IsHandleCreated || dropDown.Width <= 0 || dropDown.Height <= 0)
            {
                return;
            }

            try
            {
                var bounds = new Rectangle(0, 0, dropDown.Width, dropDown.Height);
                using var path = CreateRoundedRectanglePath(bounds, radius);
                dropDown.Region = new Region(path);
            }
            catch (Exception)
            {
                // Keep rectangular region on failure
            }
        }

        /// <summary>
        /// Builds a rounded rectangle GraphicsPath for a given bounds and radius.
        /// </summary>
        public static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// Renders the SQA logo and title bar for the tray menu with the SQASA corporate
    /// look: dark blue gradient, orange accent line and a two-line caption.
    /// </summary>
    internal class HeaderControl : Control
    {
        private Image _logo;

        public HeaderControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // SQASA corporate gradient background (#002B55 -> #004080)
            using (var gradientBrush = new LinearGradientBrush(
                       ClientRectangle,
                       TrayMenuStyle.HeaderGradientStart,
                       TrayMenuStyle.HeaderGradientEnd,
                       LinearGradientMode.Horizontal))
            {
                e.Graphics.FillRectangle(gradientBrush, ClientRectangle);
            }

            int logoSize = Math.Max(24, (int)(Height * 0.72f));
            int margin = (int)(Height * 0.18f);
            var logoRect = new Rectangle(margin, (Height - logoSize) / 2, logoSize, logoSize);

            Image logo = GetLogo();
            if (logo != null)
            {
                e.Graphics.DrawImage(logo, logoRect);
            }

            // Caption: "Evidencias SQA" + SQASA subtitle
            int textLeft = logoRect.Right + margin;
            int textWidth = Width - textLeft - margin;
            using var titleFont = TrayMenuStyle.GetMenuFont(11f);
            using var titleBrush = new SolidBrush(Color.White);
            var titleRect = new Rectangle(textLeft, (int)(Height * 0.12f), textWidth, (int)(Height * 0.52f));
            TextRenderer.DrawText(e.Graphics, "Evidencias SQA", titleFont, titleRect, Color.White,
                TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            using var subtitleFont = TrayMenuStyle.GetMenuFont(7.25f);
            var subtitleRect = new Rectangle(textLeft, (int)(Height * 0.56f), textWidth, (int)(Height * 0.34f));
            TextRenderer.DrawText(e.Graphics, "Software Quality Assurance", subtitleFont, subtitleRect,
                Color.FromArgb(205, 214, 230),
                TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            // Orange accent line (SQASA corporate accent)
            using var accentPen = new Pen(TrayMenuStyle.AccentOrange, 2f);
            e.Graphics.DrawLine(accentPen, 0, Height - 1, Width, Height - 1);
        }

        private Image GetLogo()
        {
            if (_logo != null)
            {
                return _logo;
            }

            try
            {
                string candidatesRoot = AppContext.BaseDirectory;
                foreach (string relativePath in new[]
                         {
                             Path.Combine("Media", "SQA1.png"),
                             Path.Combine("..", "Media", "SQA1.png")
                         })
                {
                    string fullPath = Path.GetFullPath(Path.Combine(candidatesRoot, relativePath));
                    if (File.Exists(fullPath))
                    {
                        _logo = Image.FromFile(fullPath);
                        break;
                    }
                }
            }
            catch (Exception)
            {
                // Fallback below
            }

            _logo ??= new Bitmap(1, 1);
            return _logo;
        }
    }

    /// <summary>
    /// Renders the subtle "SQASA" watermark in the bottom-right corner of the tray menu,
    /// using the corporate orange at low opacity as a premium finishing touch.
    /// </summary>
    internal class FooterControl : Control
    {
        public FooterControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using var watermarkBrush = new SolidBrush(Color.FromArgb(64, TrayMenuStyle.AccentOrange));
            using var watermarkFont = TrayMenuStyle.GetMenuFont(7.5f);
            var watermarkRect = new Rectangle(0, 0, Width - 8, Height);
            TextRenderer.DrawText(e.Graphics, "SQASA", watermarkFont, watermarkRect, Color.FromArgb(64, TrayMenuStyle.AccentOrange),
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}