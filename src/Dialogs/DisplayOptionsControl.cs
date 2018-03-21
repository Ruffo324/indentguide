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
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using IndentGuide.Guides;
using IndentGuide.Resources;
using IndentGuide.Utils;

namespace IndentGuide.Dialogs
{
    public partial class DisplayOptionsControl : UserControl, IThemeAwareDialog
    {
        public DisplayOptionsControl()
        {
            InitializeComponent();

            lstOverrides.BeginUpdate();
            lstOverrides.Items.Clear();
            lstOverrides.Items.Add(new OverrideInfo
            {
                Index = LineFormat.DefaultFormatIndex,
                Text = ResourceLoader.LoadString("DefaultFormatName")
            });
            lstOverrides.Items.Add(new OverrideInfo
            {
                Index = LineFormat.UnalignedFormatIndex,
                Text = ResourceLoader.LoadString("UnalignedFormatName")
            });
            for (int key = 0; key <= 30; ++key)
            {
                string name = string.Format(CultureInfo.CurrentCulture, "#{0}", key);
                lstOverrides.Items.Add(new OverrideInfo
                {
                    Index = key,
                    Text = name
                });
            }

            lstOverrides.EndUpdate();
        }

        private void gridLineStyle_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            if (gridLineStyle.SelectedObject is LineFormat format)
            {
                linePreview.ForeColor = format.LineColor;
                linePreview.GlowColor = format.LineColor;
                linePreview.Style = format.LineStyle;
                linePreviewHighlight.ForeColor = format.HighlightStyle.HasFlag(LineStyle.Glow)
                    ? format.LineColor
                    : format.HighlightColor;
                linePreviewHighlight.GlowColor = format.HighlightColor;
                linePreviewHighlight.Style = format.HighlightStyle;
            }

            OnThemeChanged(ActiveTheme);
        }

        private void lstOverrides_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstOverrides.SelectedItem == null)
                return;
            OverrideInfo oi = (OverrideInfo)lstOverrides.SelectedItem;
            Debug.Assert(oi != null);

            if (!ActiveTheme.LineFormats.TryGetValue(oi.Index, out LineFormat format))
            {
                ActiveTheme.LineFormats[oi.Index] = format = ActiveTheme.DefaultLineFormat.Clone(ActiveTheme);
                format.FormatIndex = oi.Index;
            }

            gridLineStyle.SelectedObject = format;
            linePreview.ForeColor = format.LineColor;
            linePreview.GlowColor = format.LineColor;
            linePreview.Style = format.LineStyle;
            linePreviewHighlight.ForeColor = format.HighlightStyle.HasFlag(LineStyle.Glow)
                ? format.LineColor
                : format.HighlightColor;
            linePreviewHighlight.GlowColor = format.HighlightColor;
            linePreviewHighlight.Style = format.HighlightStyle;
        }

        private void lstOverrides_Format(object sender, ListControlConvertEventArgs e)
        {
            OverrideInfo oi = e.ListItem as OverrideInfo;
            Debug.Assert(oi != null);

            e.Value = oi.Text;
        }

        private class OverrideInfo
        {
            public int Index;
            public string Text;
        }

        #region IThemeAwareDialog Members

        public IndentTheme ActiveTheme { get; set; }
        public IIndentGuide Service { get; set; }

        public void Activate()
        {
            EditorFontAndColors fac = new EditorFontAndColors();

            linePreview.BackColor = fac.BackColor;
            linePreviewHighlight.BackColor = fac.BackColor;
        }

        public void Apply()
        {
        }

        public void Close()
        {
        }

        public void Update(IndentTheme active, IndentTheme previous)
        {
            if (active != null)
            {
                int previousIndex = lstOverrides.SelectedIndex;
                lstOverrides.SelectedItem = null; // ensure a change event occurs
                if (0 <= previousIndex && previousIndex < lstOverrides.Items.Count)
                    lstOverrides.SelectedIndex = previousIndex;
                else if (lstOverrides.Items.Count > 0)
                    lstOverrides.SelectedIndex = 0;
                else
                    lstOverrides.SelectedIndex = -1;
            }
        }

        private void OnThemeChanged(IndentTheme theme)
        {
            if (theme == null) return;
            EventHandler<ThemeEventArgs> evt = ThemeChanged;
            evt?.Invoke(this, new ThemeEventArgs(theme));
        }

        public event EventHandler<ThemeEventArgs> ThemeChanged;

        #endregion
    }

    [Guid("05491866-4ED1-44FE-BDFF-FB14246BDABB")]
    public sealed class DisplayOptions : GenericOptions<DisplayOptionsControl>
    {
    }
}