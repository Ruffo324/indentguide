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
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace IndentGuide.Dialogs.Controls
{
    public partial class CheckedPropertyGrid : UserControl
    {
        private readonly List<PropertyBox> CheckBoxes;

        private Type _SelectableType;

        private object _SelectedObject;

        public CheckedPropertyGrid()
        {
            InitializeComponent();
            panel.HorizontalScroll.Visible = false;

            CheckBoxes = new List<PropertyBox>();
        }

        [Browsable(false)]
        public object SelectedObject
        {
            get => _SelectedObject;
            set
            {
                _SelectedObject = value;
                if (InvokeRequired)
                    BeginInvoke((Action) UpdateObject);
                else
                    UpdateObject();
            }
        }

        [Browsable(false)]
        public Type SelectableType
        {
            get => _SelectableType;
            set
            {
                _SelectableType = value;
                if (InvokeRequired)
                    BeginInvoke((Action) UpdateType);
                else
                    UpdateType();
            }
        }

        public event EventHandler PropertyValueChanged;

        private void OnPropertyValueChanged()
        {
            EventHandler evt = PropertyValueChanged;
            if (evt != null)
                evt(this, EventArgs.Empty);
        }

        private void UpdateObject()
        {
            object obj = SelectedObject;
            if (obj == null) return;
            if (SelectableType != obj.GetType()) return;

            foreach (PropertyBox check in CheckBoxes) check.GetValue(obj);
        }

        private void UpdateType()
        {
            SuspendLayout();
            try
            {
                Type type = SelectableType;
                if (type == null)
                {
                    table.Controls.Clear();
                    CheckBoxes.Clear();
                    toolTip.RemoveAll();
                    table.RowCount = 1;
                }
                else
                {
                    CheckBoxes.Clear();
                    foreach (PropertyInfo prop in type.GetProperties())
                    {
                        if (!prop.CanWrite || !prop.GetSetMethod().IsPublic) continue;

                        PropertyBox check = new PropertyBox(prop);
                        CheckBoxes.Add(check);
                        check.CheckBox.CheckedChanged += check_CheckedChanged;
                    }

                    table.Controls.Clear();
                    toolTip.RemoveAll();
                    table.RowCount = CheckBoxes.Count;
                    int row = 0;
                    foreach (PropertyBox check in CheckBoxes.OrderBy(v => v.SortPriority))
                    {
                        table.RowStyles[row].SizeType = SizeType.AutoSize;
                        table.Controls.Add(check.CheckBox, 0, row++);
                        toolTip.SetToolTip(check.CheckBox, check.Description);
                    }
                }
            }
            finally
            {
                ResumeLayout(true);
            }
        }

        private void check_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox check = sender as CheckBox;
            Debug.Assert(check != null);
            if (check == null) return;

            WeakReference weakref = check.Tag as WeakReference;
            Debug.Assert(weakref != null);
            if (weakref == null) return;

            PropertyBox propInfo = weakref.Target as PropertyBox;
            if (propInfo == null) return;

            propInfo.SetValue(SelectedObject);

            OnPropertyValueChanged();
        }

        private class PropertyBox
        {
            private readonly PropertyInfo PropInfo;

            public PropertyBox(PropertyInfo propInfo)
            {
                PropInfo = propInfo;
                Name = "chk" + propInfo.Name;
                SortPriority = GetSortOrder();
                DisplayName = GetDisplayName();
                Description = GetDescription();

                CheckBox = new CheckBox
                {
                    Name = Name,
                    Text = DisplayName,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoSize = true,
                    Margin = new Padding(3, 1, 3, 2)
                };
                CheckBox.Tag = new WeakReference(this);
            }

            public CheckBox CheckBox { get; }
            public string Name { get; }
            public int SortPriority { get; }
            public string DisplayName { get; }
            public string Description { get; }

            public void SetValue(object target)
            {
                try
                {
                    PropInfo.SetValue(target, CheckBox.Checked, null);
                }
                catch
                {
                    CheckBox.Enabled = false;
                }
            }

            public void GetValue(object source)
            {
                try
                {
                    CheckBox.Checked = (bool) PropInfo.GetValue(source, null);
                    CheckBox.Enabled = true;
                }
                catch
                {
                    CheckBox.Enabled = false;
                }
            }

            private int GetSortOrder()
            {
                try
                {
                    SortOrderAttribute sort = PropInfo.GetCustomAttributes(false).OfType<SortOrderAttribute>().First();
                    return sort.Priority;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("CheckedPropertyGrid.GetSortOrder Exception: " + ex);
                    return 0;
                }
            }

            private string GetDisplayName()
            {
                try
                {
                    DisplayNameAttribute name = PropInfo.GetCustomAttributes(false).OfType<DisplayNameAttribute>().First();
                    return name.DisplayName;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("CheckedPropertyGrid.GetDisplayName Exception: " + ex);
                    return PropInfo.Name;
                }
            }

            private string GetDescription()
            {
                try
                {
                    DescriptionAttribute descr = PropInfo.GetCustomAttributes(false).OfType<DescriptionAttribute>().First();
                    return descr.Description;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("CheckedPropertyGrid.GetDescription Exception: " + ex);
                    return PropInfo.Name;
                }
            }
        }
    }

    [AttributeUsage(AttributeTargets.All, Inherited = false)]
    internal sealed class SortOrderAttribute : Attribute
    {
        public SortOrderAttribute()
        {
            Priority = 0;
        }

        public SortOrderAttribute(int priority)
        {
            Priority = priority;
        }

        public int Priority { get; set; }
    }
}