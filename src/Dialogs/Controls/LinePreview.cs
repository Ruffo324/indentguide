/* ****************************************************************************
 * Copyright 2015 Steve Dower
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not
 * use this file except in compliance with the License. You may obtain a copy 
 * of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 * ***************************************************************************/

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using IndentGuide.Guides;

namespace IndentGuide.Dialogs.Controls
{
    public partial class LinePreview : UserControl
    {
        private Color _GlowColor = SystemColors.ControlText;

        private LineStyle _Style = LineStyle.Solid;

        private Pen LinePen;

        public LinePreview()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);

            InitializeComponent();
        }

        public LineStyle Style
        {
            get => _Style;
            set
            {
                if (_Style != value)
                {
                    _Style = value;
                    LinePen = null;
                    Invalidate();
                }
            }
        }

        public Color GlowColor
        {
            get => _GlowColor;
            set
            {
                if (_GlowColor != value)
                {
                    _GlowColor = value;
                    Invalidate();
                }
            }
        }

        private bool ShouldSerializeGlowColor()
        {
            return _GlowColor != SystemColors.ControlText;
        }

        public void ResetGlowColor()
        {
            GlowColor = SystemColors.ControlText;
        }

        protected override void OnForeColorChanged(EventArgs e)
        {
            base.OnForeColorChanged(e);
            LinePen = null;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);

            int x = ClientRectangle.Width / 2 + ClientRectangle.Left;
            int y1 = ClientRectangle.Top;
            int y2 = ClientRectangle.Bottom;

            if (LinePen == null)
            {
                LinePen = new Pen(ForeColor, (float) Style.GetStrokeThickness());

                float[] pattern = Style.GetDashPattern();
                if (pattern == null)
                {
                    LinePen.DashStyle = DashStyle.Solid;
                    LinePen.StartCap = LineCap.Flat;
                    LinePen.EndCap = LineCap.Flat;
                }
                else
                {
                    LinePen.DashPattern = pattern;
                }
            }

            e.Graphics.DrawLine(LinePen, x, y1, x, y2);
            if (Style.HasFlag(LineStyle.Glow))
                using (SolidBrush transparentBrush = new SolidBrush(Color.FromArgb(24, GlowColor)))
                {
                    for (int i = 1; i < LineStyle.Thick.GetStrokeThickness(); ++i)
                        e.Graphics.FillRectangle(transparentBrush, x - i, y1, i + i + 1, y2 - y1);
                }
        }
    }
}