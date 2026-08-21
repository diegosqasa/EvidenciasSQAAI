/*
 * EvidenciasSQA - a free and open source screenshot tool
 * Copyright (C) 2004-2026  Thomas Braun, Jens Klingen, Robin Krom
 *
 * For more information see: https://evidenciassqa.com/
 * The EvidenciasSQA project is hosted on GitHub https://github.com/evidenciassqa/evidenciassqa
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
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using EvidenciasSQA.Base.Core;

namespace EvidenciasSQA.Controls
{
    /// <summary>
    /// Custom ContextMenuStrip renderer that implements a modern Windows 11 style
    /// with rounded corners, a soft blue rounded selection and a dark color palette.
    /// </summary>
    public class AcrylicContextMenuRenderer : ToolStripProfessionalRenderer
    {
        private static readonly Color MenuBackgroundColor = TrayMenuStyle.MenuBackgroundColor;
        private static readonly Color SelectionBackgroundColor = TrayMenuStyle.SelectionBackgroundColor;
        private static readonly Color TextColor = Color.White;
        private static readonly Color SeparatorColor = Color.FromArgb(60, 60, 60);
        private static readonly Color BorderColor = Color.FromArgb(70, 70, 70);
        private static readonly int SelectionRadius = TrayMenuStyle.SelectionRadius;
        private static readonly int MenuRadius = TrayMenuStyle.MenuRadius;

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is ToolStripDropDown)
            {
                using var path = CreateRoundedRectangle(e.AffectedBounds, MenuRadius);
                using var brush = new SolidBrush(MenuBackgroundColor);
                e.Graphics.FillPath(brush, path);
            }
            else
            {
                using var brush = new SolidBrush(MenuBackgroundColor);
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is ToolStripDropDown)
            {
                using var path = CreateRoundedRectangle(
                    new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), MenuRadius);
                using var pen = new Pen(BorderColor, 1);
                e.Graphics.DrawPath(pen, path);
            }
            else
            {
                base.OnRenderToolStripBorder(e);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || e.Item is ToolStripControlHost)
            {
                return;
            }

            var bounds = e.Item.ContentRectangle;
            bounds.Inflate(-1, 0);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            using var path = CreateRoundedRectangle(bounds, SelectionRadius);
            using var brush = new SolidBrush(SelectionBackgroundColor);
            e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var textFont = e.TextFont ?? e.Item.Font;
            TextRenderer.DrawText(e.Graphics, e.Text, textFont, e.TextRectangle, TextColor, e.TextFormat);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(SeparatorColor, 1);
            var startPoint = new Point(e.Item.ContentRectangle.Left + 10, e.Item.ContentRectangle.Top + (e.Item.ContentRectangle.Height / 2));
            var endPoint = new Point(e.Item.ContentRectangle.Right - 10, e.Item.ContentRectangle.Top + (e.Item.ContentRectangle.Height / 2));
            e.Graphics.DrawLine(pen, startPoint, endPoint);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                using var solidBrush = new SolidBrush(TextColor);
                e.Graphics.FillPolygon(solidBrush, new[]
                {
                    new PointF(e.ArrowRectangle.Left + 8, e.ArrowRectangle.Top),
                    new PointF(e.ArrowRectangle.Left + 8 + 5, e.ArrowRectangle.Top + 5),
                    new PointF(e.ArrowRectangle.Left + 8 + 5, e.ArrowRectangle.Bottom - 5)
                });
            }
            else
            {
                base.OnRenderArrow(e);
            }
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            // Default check rendering is fine, just ensure colors work with our palette
            base.OnRenderItemCheck(e);
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
        {
            return TrayMenuStyle.CreateRoundedRectanglePath(bounds, radius);
        }
    }
}