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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using IndentGuide.Guides;
using IndentGuide.Resources;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace IndentGuide.Dialogs
{
    public partial class ThemeOptionsControl : UserControl
    {
        private static IndentTheme _ActiveTheme;

        private readonly IThemeAwareDialog Child;
        private readonly IVsEditorAdaptersFactoryService EditorAdapters;
        private readonly IndentGuideService Service;
        private readonly IVsTextManager TextManagerService;

        private string _CurrentContentType;

        private bool Suppress_cmbTheme_SelectedIndexChanged;

        public ThemeOptionsControl(IThemeAwareDialog child)
        {
            InitializeComponent();

            Child = child;
            Control control = child as Control;
            Debug.Assert(Child != null);
            Debug.Assert(control != null);

            tableContent.Controls.Add(control);
            tableContent.SetColumn(control, 0);
            tableContent.SetRow(control, 1);
            control.Dock = DockStyle.Fill;

            ServiceProvider provider = ServiceProvider.GlobalProvider;
            Service = provider.GetService(typeof(SIndentGuide)) as IndentGuideService;
            Child.Service = Service;

            TextManagerService = (IVsTextManager) provider.GetService(typeof(SVsTextManager));

            IComponentModel componentModel = (IComponentModel) provider.GetService(typeof(SComponentModel));
            EditorAdapters = componentModel
                .GetService<IVsEditorAdaptersFactoryService>();

            ActiveThemeChanged += ActiveTheme_Changed;
        }

        protected IndentTheme ActiveTheme
        {
            get => _ActiveTheme;
            set
            {
                ActiveThemeChangedEventArgs args = new ActiveThemeChangedEventArgs();
                args.Previous = _ActiveTheme;
                if (_ActiveTheme != value) _ActiveTheme = value;
                args.Current = _ActiveTheme;
                EventHandler<ActiveThemeChangedEventArgs> evt = ActiveThemeChanged;
                if (evt != null) evt(this, args);
            }
        }

        internal string CurrentContentType
        {
            get => _CurrentContentType;
            set
            {
                btnCustomizeThisContentType.Text = value ?? "";
                btnCustomizeThisContentType.Visible = value != null;
                _CurrentContentType = value;
            }
        }

        internal void Activate()
        {
            try
            {
                IVsTextView view = null;
                IWpfTextView wpfView = null;
                TextManagerService.GetActiveView(0, null, out view);
                if (view == null)
                {
                    CurrentContentType = null;
                }
                else
                {
                    wpfView = EditorAdapters.GetWpfTextView(view);
                    CurrentContentType = wpfView.TextDataModel.ContentType.DisplayName;
                }
            }
            catch
            {
                CurrentContentType = null;
            }

            Child.Activate();

            if (ActiveTheme == null)
            {
                IndentTheme activeTheme;
                if (CurrentContentType == null ||
                    !Service.Themes.TryGetValue(CurrentContentType, out activeTheme) ||
                    activeTheme == null)
                    activeTheme = Service.DefaultTheme;
                if (activeTheme == null) activeTheme = Service.DefaultTheme = new IndentTheme();
                ActiveTheme = activeTheme;
            }

            UpdateThemeList();
            UpdateDisplay();
        }

        internal void Apply()
        {
            Child.Apply();
        }

        internal void Close()
        {
            Child.Close();
            _ActiveTheme = null;
        }

        private static event EventHandler<ActiveThemeChangedEventArgs> ActiveThemeChanged;

        private void ActiveTheme_Changed(object sender, ActiveThemeChangedEventArgs e)
        {
            IndentTheme value = e.Current;
            IndentTheme old = e.Previous;
            Child.ActiveTheme = value;
            if (value == null)
                cmbTheme.SelectedIndex = cmbTheme.Items.Count == 0 ? -1 : 0;
            else if (cmbTheme.SelectedItem != value && cmbTheme.Items.Contains(value)) cmbTheme.SelectedItem = value;
            UpdateDisplay(value, old);
        }

        private void btnCustomizeThisContentType_Click(object sender, EventArgs e)
        {
            try
            {
                IndentTheme theme;
                if (!Service.Themes.TryGetValue(CurrentContentType, out theme))
                {
                    if (ActiveTheme == null)
                        theme = new IndentTheme();
                    else
                        theme = ActiveTheme.Clone();
                    theme.ContentType = CurrentContentType;
                    Service.Themes[CurrentContentType] = theme;
                    UpdateThemeList();
                }

                cmbTheme.SelectedItem = theme;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(string.Format("IndentGuide::btnCustomizeThisContentType_Click: {0}", ex));
            }
        }

        protected void UpdateThemeList()
        {
            try
            {
                cmbTheme.Items.Clear();
                cmbTheme.Items.Add(Service.DefaultTheme);
                foreach (IndentTheme theme in Service.Themes.Values) cmbTheme.Items.Add(theme);

                if (cmbTheme.Items.Contains(ActiveTheme)) cmbTheme.SelectedItem = ActiveTheme;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(string.Format("IndentGuide::UpdateThemeList: {0}", ex));
            }
        }

        protected void LoadControlStrings(IEnumerable<Control> controls)
        {
            try
            {
                foreach (Control control in controls)
                {
                    try
                    {
                        control.Text = ResourceLoader.LoadString(control.Name) ?? control.Text;
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    if (control.Controls.Count > 0) LoadControlStrings(control.Controls.OfType<Control>());
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(string.Format("IndentGuide::LoadControlStrings: {0}", ex));
            }
        }

        private void cmbTheme_Format(object sender, ListControlConvertEventArgs e)
        {
            try
            {
                e.Value = ((IndentTheme) e.ListItem).ContentType ?? IndentTheme.DefaultThemeName;
            }
            catch
            {
                e.Value = (e.ListItem ?? "(null)").ToString();
            }
        }

        protected void UpdateDisplay()
        {
            UpdateDisplay(ActiveTheme, ActiveTheme);
        }

        protected void UpdateDisplay(IndentTheme active, IndentTheme previous)
        {
            Suppress_cmbTheme_SelectedIndexChanged = true;
            try
            {
                if (active != null && cmbTheme.Items.Contains(active))
                    cmbTheme.SelectedItem = active;
                else
                    cmbTheme.SelectedItem = null;
            }
            finally
            {
                Suppress_cmbTheme_SelectedIndexChanged = false;
            }

            Child.Update(active, previous);
        }

        private void ThemeOptionsControl_Load(object sender, EventArgs e)
        {
            LoadControlStrings(Controls.OfType<Control>());
            toolTip.SetToolTip(btnCustomizeThisContentType,
                ResourceLoader.LoadString("tooltipCustomizeThisContentType"));
        }

        private void cmbTheme_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Suppress_cmbTheme_SelectedIndexChanged) return;
            ActiveTheme = cmbTheme.SelectedItem as IndentTheme;

            if (ActiveTheme != null)
            {
                btnThemeDelete.Enabled = true;
                btnThemeDelete.Text =
                    ResourceLoader.LoadString(ActiveTheme.IsDefault ? "btnThemeReset" : "btnThemeDelete");
            }
            else
            {
                btnThemeDelete.Enabled = false;
                btnThemeDelete.Text = ResourceLoader.LoadString("btnThemeReset");
            }

            UpdateDisplay();
        }

        private void btnThemeDelete_Click(object sender, EventArgs e)
        {
            if (ActiveTheme == null) return;

            try
            {
                if (ActiveTheme.IsDefault)
                {
                    IndentTheme theme = Service.DefaultTheme = new IndentTheme();
                    UpdateThemeList();
                    ActiveTheme = theme;
                }
                else
                {
                    if (Service.Themes.Remove(ActiveTheme.ContentType))
                    {
                        int i = cmbTheme.SelectedIndex;
                        cmbTheme.Items.Remove(ActiveTheme);
                        if (i < cmbTheme.Items.Count) cmbTheme.SelectedIndex = i;
                        else cmbTheme.SelectedIndex = cmbTheme.Items.Count - 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(string.Format("IndentGuide::btnThemeDelete_Click: {0}", ex));
            }
        }

        private class ActiveThemeChangedEventArgs : EventArgs
        {
            public IndentTheme Previous { get; set; }
            public IndentTheme Current { get; set; }
        }
    }
}