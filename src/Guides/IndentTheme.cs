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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using IndentGuide.Dialogs.Controls;
using IndentGuide.Resources;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using Color = System.Drawing.Color;
using ColorConverter = System.Drawing.ColorConverter;

namespace IndentGuide.Guides
{
    /// <summary>
    ///     The supported styles of guideline.
    /// </summary>
    [Flags]
    public enum LineStyle
    {
        Solid = 1,
        Thick = 2,
        Glow = 4,
        Dotted = 0x100,
        DottedThick = Dotted | Thick,
        DottedGlow = Dotted | Glow,
        Dashed = 0x200,
        DashedThick = Dashed | Thick,
        DashedGlow = Dashed | Glow
    }

    public static class LineStyleExtensions
    {
        public static double GetStrokeThickness(this LineStyle style)
        {
            if (style.HasFlag(LineStyle.Thick))
                return 3.0;
            return 1.0;
        }

        public static DoubleCollection GetStrokeDashArray(this LineStyle style)
        {
            if (style.HasFlag(LineStyle.Dotted))
                return new DoubleCollection {1.0, 2.0, 1.0, 2.0};
            if (style.HasFlag(LineStyle.Dashed))
                return new DoubleCollection {3.0, 3.0, 3.0, 3.0};
            return null;
        }

        public static float[] GetDashPattern(this LineStyle style)
        {
            DoubleCollection dashArray = style.GetStrokeDashArray();
            if (dashArray == null)
                return null;
            return dashArray.Select(i => (float) i).ToArray();
        }
    }

    /// <summary>
    ///     The format of a particular type of guideline.
    /// </summary>
    public class LineFormat
    {
        public const int DefaultFormatIndex = int.MinValue;
        public const int UnalignedFormatIndex = -1;

        public const int FirstIndentFormatIndex = UnalignedFormatIndex, LastIndentFormatIndex = 999;
        private static readonly LineFormat DefaultLineFormat = new LineFormat();

        protected LineFormat()
        {
            Theme = null;
            FormatIndex = DefaultFormatIndex;
            Visible = true;
            LineStyle = LineStyle.Dotted;
            LineColor = Color.DimGray;
            HighlightStyle = LineStyle.DottedGlow;
            HighlightColor = Color.DodgerBlue;
        }

        public LineFormat(IndentTheme theme)
            : this()
        {
            Theme = theme;
        }

        protected virtual LineFormat Default => DefaultLineFormat;

        internal IndentTheme Theme { get; set; }

        internal LineFormat BaseFormat => (Theme != null ? Theme.DefaultLineFormat : null) ?? Default;

        [Browsable(false)] public int? FormatIndex { get; set; }

        [Browsable(false)]
        public string FormatIndexName
        {
            get
            {
                if (!FormatIndex.HasValue)
                    return null;
                if (FormatIndex == DefaultFormatIndex)
                    return "Default";
                if (FormatIndex == UnalignedFormatIndex)
                    return "Unaligned";
                return FormatIndex.GetValueOrDefault().ToString(CultureInfo.InvariantCulture);
            }
            set
            {
                int result;
                if (value == "Default")
                    FormatIndex = DefaultFormatIndex;
                else if (value == "Unaligned")
                    FormatIndex = UnalignedFormatIndex;
                else if (int.TryParse(value, out result))
                    FormatIndex = result;
                else
                    FormatIndex = null;
            }
        }

        [ResourceDescription("VisibilityDescription")]
        [ResourceCategory("Appearance")]
        public bool Visible { get; set; }

        [ResourceDisplayName("LineStyleDisplayName")]
        [ResourceDescription("LineStyleDescription")]
        [ResourceCategory("Appearance")]
        [TypeConverter(typeof(LineStyleConverter))]
        public LineStyle LineStyle { get; set; }

        [ResourceDisplayName("LineColorDisplayName")]
        [ResourceDescription("LineColorDescription")]
        [ResourceCategory("Appearance")]
        [TypeConverter(typeof(ColorConverter))]
        public Color LineColor { get; set; }

        [ResourceDisplayName("HighlightStyleDisplayName")]
        [ResourceDescription("HighlightStyleDescription")]
        [ResourceCategory("Appearance")]
        [TypeConverter(typeof(LineStyleConverter))]
        public LineStyle HighlightStyle { get; set; }

        [ResourceDisplayName("HighlightColorDisplayName")]
        [ResourceDescription("HighlightColorDescription")]
        [ResourceCategory("Appearance")]
        [TypeConverter(typeof(ColorConverter))]
        public Color HighlightColor { get; set; }

        public virtual LineFormat Clone(IndentTheme theme)
        {
            return new LineFormat(theme)
            {
                FormatIndex = FormatIndex,
                Visible = Visible,
                LineStyle = LineStyle,
                LineColor = LineColor,
                HighlightStyle = HighlightStyle,
                HighlightColor = HighlightColor
            };
        }

        internal virtual bool ShouldSerialize()
        {
            return Theme == null ||
                   (FormatIndex ?? DefaultFormatIndex) == DefaultFormatIndex ||
                   ShouldSerializeVisible() ||
                   ShouldSerializeLineStyle() ||
                   ShouldSerializeLineColor() ||
                   ShouldSerializeHighlightStyle() ||
                   ShouldSerializeHighlightColor();
        }

        internal virtual void Reset()
        {
            ResetVisible();
            ResetLineStyle();
            ResetLineColor();
            ResetHighlightStyle();
            ResetHighlightColor();
        }

        private bool ShouldSerializeVisible()
        {
            if (FormatIndex == 0)
                return Visible;
            return Visible != BaseFormat.Visible;
        }

        private void ResetVisible()
        {
            if (FormatIndex == 0)
                Visible = false;
            else
                Visible = BaseFormat.Visible;
        }

        private bool ShouldSerializeLineStyle()
        {
            return LineStyle != BaseFormat.LineStyle;
        }

        private void ResetLineStyle()
        {
            LineStyle = BaseFormat.LineStyle;
        }

        private bool ShouldSerializeLineColor()
        {
            return LineColor != BaseFormat.LineColor;
        }

        private void ResetLineColor()
        {
            LineColor = BaseFormat.LineColor;
        }

        private bool ShouldSerializeHighlightStyle()
        {
            return HighlightStyle != BaseFormat.HighlightStyle;
        }

        private void ResetHighlightStyle()
        {
            HighlightStyle = BaseFormat.HighlightStyle;
        }

        private bool ShouldSerializeHighlightColor()
        {
            return HighlightColor != BaseFormat.HighlightColor;
        }

        private void ResetHighlightColor()
        {
            HighlightColor = BaseFormat.HighlightColor;
        }

        public static LineFormat FromInvariantStrings(IndentTheme theme, Dictionary<string, string> values)
        {
            Type subclass = typeof(LineFormat);
            string subclassName;
            if (values.TryGetValue("TypeName", out subclassName))
                subclass = Type.GetType(subclassName, false) ?? typeof(LineFormat);

            LineFormat inst = subclass.InvokeMember(null, BindingFlags.CreateInstance, null, null,
                new[] {theme}) as LineFormat;
            if (inst == null) throw new InvalidOperationException("Unable to create instance of " + subclass.FullName);

            foreach (KeyValuePair<string, string> kv in values)
            {
                if (kv.Key == "TypeName") continue;

                PropertyInfo prop = subclass.GetProperty(kv.Key);
                if (prop != null)
                    try
                    {
                        prop.SetValue(inst,
                            TypeDescriptor.GetConverter(prop.PropertyType).ConvertFromInvariantString(kv.Value));
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError("Error setting {0} to {1}:\n", kv.Key, kv.Value, ex);
                    }
                else
                    Trace.TraceWarning("Unable to find property {0} on type {1}", kv.Key, subclass.FullName);
            }

            return inst;
        }

        public Dictionary<string, string> ToInvariantStrings()
        {
            Dictionary<string, string> values = new Dictionary<string, string>();

            Type subclass = GetType();
            if (subclass != typeof(LineFormat))
                if (subclass.Assembly == typeof(LineFormat).Assembly)
                    values["TypeName"] = subclass.FullName;
                else
                    values["TypeName"] = subclass.AssemblyQualifiedName;

            foreach (PropertyInfo prop in subclass.GetProperties())
            {
                BrowsableAttribute browsable = prop.GetCustomAttribute<BrowsableAttribute>();
                if (browsable == null || browsable.Browsable)
                    values[prop.Name] = TypeDescriptor.GetConverter(prop.PropertyType)
                        .ConvertToInvariantString(prop.GetValue(this));
            }

            return values;
        }

        private class LineStyleConverter : EnumResourceTypeConverter<LineStyle>
        {
        }
    }

    public class PageWidthMarkerFormat : LineFormat
    {
        public const int FirstPageWidthIndex = 1000, LastPageWidthIndex = 1999;
        private static readonly LineFormat DefaultPageWidthMarkerFormat = new PageWidthMarkerFormat();

        protected PageWidthMarkerFormat()
        {
            Position = 80;
        }

        public PageWidthMarkerFormat(IndentTheme theme)
            : this()
        {
            Theme = theme;
        }

        protected override LineFormat Default => DefaultPageWidthMarkerFormat;

        [ResourceDisplayName("PageWidthPositionDisplayName")]
        [ResourceDescription("PageWidthPositionDescription")]
        [ResourceCategory("Appearance")]
        public int Position { get; set; }

        internal int GetFormatIndex()
        {
            return Position + FirstPageWidthIndex;
        }

        private bool ShouldSerializePosition()
        {
            return true;
        }

        private void ResetPosition()
        {
            Position = 80;
        }

        public override LineFormat Clone(IndentTheme theme)
        {
            return new PageWidthMarkerFormat(theme)
            {
                FormatIndex = FormatIndex,
                Visible = Visible,
                LineColor = LineColor,
                LineStyle = LineStyle,
                HighlightColor = HighlightColor,
                HighlightStyle = HighlightStyle,
                Position = Position
            };
        }


        internal override bool ShouldSerialize()
        {
            return base.ShouldSerialize() || ShouldSerializePosition();
        }

        internal override void Reset()
        {
            base.Reset();
            ResetPosition();
        }
    }

    public class LineBehavior : IEquatable<LineBehavior>
    {
        public LineBehavior()
        {
            ExtendInwardsOnly = true;
            VisibleAligned = true;
            VisibleUnaligned = false;
            VisibleAtTextEnd = false;
            VisibleEmpty = true;
            VisibleEmptyAtEnd = true;
        }

        /// <summary>
        ///     True to require guides to appear on both sides of empty lines.
        /// </summary>
        [ResourceDisplayName("ExtendInwardsOnlyDisplayName")]
        [ResourceDescription("ExtendInwardsOnlyDescription")]
        [SortOrder(4)]
        public bool ExtendInwardsOnly { get; set; }

        /// <summary>
        ///     True to copy guidelines from the previous non-empty line into empty
        ///     lines.
        /// </summary>
        [ResourceDisplayName("VisibleEmptyDisplayName")]
        [ResourceDescription("VisibleEmptyDescription")]
        [SortOrder(3)]
        public bool VisibleEmpty { get; set; }

        /// <summary>
        ///     True to copy 'at end' guidelines from the previous non-empty line
        ///     into empty lines.
        /// </summary>
        [ResourceDisplayName("VisibleEmptyAtEndDisplayName")]
        [ResourceDescription("VisibleEmptyAtEndDescription")]
        [SortOrder(6)]
        public bool VisibleEmptyAtEnd { get; set; }

        /// <summary>
        ///     True to display guidelines at transitions from whitespace to text.
        /// </summary>
        [ResourceDisplayName("VisibleAtTextEndDisplayName")]
        [ResourceDescription("VisibleAtTextEndDescription")]
        [SortOrder(5)]
        public bool VisibleAtTextEnd { get; set; }

        /// <summary>
        ///     True to always display guidelines at multiples of indent size.
        /// </summary>
        [ResourceDisplayName("VisibleAlignedDisplayName")]
        [ResourceDescription("VisibleAlignedDescription")]
        [SortOrder(1)]
        public bool VisibleAligned { get; set; }

        /// <summary>
        ///     True to always display guidelines at textual indents.
        /// </summary>
        [ResourceDisplayName("VisibleUnalignedDisplayName")]
        [ResourceDescription("VisibleUnalignedDescription")]
        [SortOrder(2)]
        public bool VisibleUnaligned { get; set; }

        public bool Equals(LineBehavior other)
        {
            return other != null &&
                   ExtendInwardsOnly == other.ExtendInwardsOnly &&
                   VisibleAligned == other.VisibleAligned &&
                   VisibleAtTextEnd == other.VisibleAtTextEnd &&
                   VisibleEmpty == other.VisibleEmpty &&
                   VisibleEmptyAtEnd == other.VisibleEmptyAtEnd &&
                   VisibleUnaligned == other.VisibleUnaligned;
        }

        public LineBehavior Clone()
        {
            return new LineBehavior
            {
                ExtendInwardsOnly = ExtendInwardsOnly,
                VisibleAligned = VisibleAligned,
                VisibleUnaligned = VisibleUnaligned,
                VisibleAtTextEnd = VisibleAtTextEnd,
                VisibleEmpty = VisibleEmpty,
                VisibleEmptyAtEnd = VisibleEmptyAtEnd
            };
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LineBehavior);
        }

        public override int GetHashCode()
        {
            return (ExtendInwardsOnly ? 1 : 0) |
                   (VisibleAligned ? 2 : 0) |
                   (VisibleAtTextEnd ? 4 : 0) |
                   (VisibleEmpty ? 8 : 0) |
                   (VisibleEmptyAtEnd ? 16 : 0) |
                   (VisibleUnaligned ? 32 : 0);
        }

        internal void Load(RegistryKey key)
        {
            ExtendInwardsOnly = (int) key.GetValue("ExtendInwardsOnly", 1) != 0;
            VisibleAligned = (int) key.GetValue("VisibleAligned", 1) != 0;
            VisibleUnaligned = (int) key.GetValue("VisibleUnaligned", 0) != 0;
            VisibleAtTextEnd = (int) key.GetValue("VisibleAtTextEnd", 0) != 0;
            VisibleEmpty = (int) key.GetValue("VisibleEmpty", 1) != 0;
            VisibleEmptyAtEnd = (int) key.GetValue("VisibleEmptyAtEnd", 1) != 0;
        }

        internal void Load(IVsSettingsReader reader, string key)
        {
            string temp;
            reader.ReadSettingAttribute(key, "ExtendInwardsOnly", out temp);
            ExtendInwardsOnly = bool.Parse(temp);
            reader.ReadSettingAttribute(key, "VisibleAligned", out temp);
            VisibleAligned = bool.Parse(temp);
            reader.ReadSettingAttribute(key, "VisibleUnaligned", out temp);
            VisibleUnaligned = bool.Parse(temp);
            reader.ReadSettingAttribute(key, "VisibleAtTextEnd", out temp);
            VisibleAtTextEnd = bool.Parse(temp);
            reader.ReadSettingAttribute(key, "VisibleEmpty", out temp);
            VisibleEmpty = bool.Parse(temp);
            reader.ReadSettingAttribute(key, "VisibleEmptyAtEnd", out temp);
            VisibleEmptyAtEnd = bool.Parse(temp);
        }

        internal void Save(RegistryKey key)
        {
            key.SetValue("ExtendInwardsOnly", ExtendInwardsOnly ? 1 : 0);
            key.SetValue("VisibleAligned", VisibleAligned ? 1 : 0);
            key.SetValue("VisibleUnaligned", VisibleUnaligned ? 1 : 0);
            key.SetValue("VisibleAtTextEnd", VisibleAtTextEnd ? 1 : 0);
            key.SetValue("VisibleEmpty", VisibleEmpty ? 1 : 0);
            key.SetValue("VisibleEmptyAtEnd", VisibleEmptyAtEnd ? 1 : 0);
        }

        internal void Save(IVsSettingsWriter writer, string key)
        {
            writer.WriteSettingAttribute(key, "ExtendInwardsOnly", ExtendInwardsOnly.ToString());
            writer.WriteSettingAttribute(key, "VisibleAligned", VisibleAligned.ToString());
            writer.WriteSettingAttribute(key, "VisibleUnaligned", VisibleUnaligned.ToString());
            writer.WriteSettingAttribute(key, "VisibleAtTextEnd", VisibleAtTextEnd.ToString());
            writer.WriteSettingAttribute(key, "VisibleEmpty", VisibleEmpty.ToString());
            writer.WriteSettingAttribute(key, "VisibleEmptyAtEnd", VisibleEmptyAtEnd.ToString());
        }
    }

    /// <summary>
    ///     A theme for a particular language or document type.
    /// </summary>
    public class IndentTheme : IComparable<IndentTheme>, IEquatable<IndentTheme>
    {
        public static readonly string DefaultThemeName = ResourceLoader.LoadString("DefaultThemeName");
        public static readonly string DefaultCaretHandler = typeof(CaretNearestLeft).FullName;

        public IndentTheme()
        {
            ContentType = null;
            LineFormats = new Dictionary<int, LineFormat>();
            PageWidthMarkers = new PageWidthMarkerGetter(this);
            DefaultLineFormat = new LineFormat(this);
            Behavior = new LineBehavior();
            CaretHandler = DefaultCaretHandler;

            // Ensure format for indent 0 is hidden by default
            LineFormat format = new LineFormat(this);
            format.FormatIndex = 0;
            format.Reset();
            LineFormats[0] = format;
        }

        [ResourceDisplayName("ContentTypeDisplayName")]
        [ResourceDescription("ContentTypeDescription")]
        public string ContentType { get; set; }

        public bool IsDefault => ContentType == null;

        [TypeConverter(typeof(ExpandableObjectConverter))]
        public LineFormat DefaultLineFormat
        {
            get
            {
                LineFormat format;
                if (LineFormats.TryGetValue(LineFormat.DefaultFormatIndex, out format)) return format;

                return LineFormats[LineFormat.DefaultFormatIndex] = new LineFormat(this);
            }
            set => LineFormats[LineFormat.DefaultFormatIndex] = value;
        }

        [Browsable(false)] public IDictionary<int, LineFormat> LineFormats { get; private set; }

        [Browsable(false)] public PageWidthMarkerGetter PageWidthMarkers { get; }

        [TypeConverter(typeof(ExpandableObjectConverter))]
        public LineBehavior Behavior { get; set; }

        [Browsable(false)] public string CaretHandler { get; set; }

        public int CompareTo(IndentTheme other)
        {
            if (null == other) return -1;
            if (IsDefault && other.IsDefault) return 0;
            if (IsDefault) return -1;
            if (other.IsDefault) return 1;
            return string.Compare(ContentType, other.ContentType, StringComparison.OrdinalIgnoreCase);
        }

        public bool Equals(IndentTheme other)
        {
            if (other == null) return false;
            if (IsDefault && other.IsDefault) return true;
            return string.Equals(ContentType, other.ContentType, StringComparison.OrdinalIgnoreCase);
        }

        public event EventHandler Updated;

        internal void OnUpdated()
        {
            EventHandler evt = Updated;
            if (evt != null) Updated(this, EventArgs.Empty);
        }

        public IndentTheme Clone()
        {
            IndentTheme inst = new IndentTheme();
            inst.ContentType = ContentType;
            foreach (KeyValuePair<int, LineFormat> item in LineFormats) inst.LineFormats[item.Key] = item.Value.Clone(inst);
            inst.Behavior = Behavior.Clone();
            inst.CaretHandler = CaretHandler;
            return inst;
        }

        public static IndentTheme Load(RegistryKey reg, string themeKey)
        {
            IndentTheme theme = new IndentTheme();

            using (RegistryKey key = reg.OpenSubKey(themeKey))
            {
                theme.ContentType = themeKey == DefaultThemeName ? null : themeKey;
                theme.Behavior.Load(key);
                theme.CaretHandler = (string) key.GetValue("CaretHandler") ?? DefaultCaretHandler;

                foreach (string subkeyName in key.GetSubKeyNames())
                {
                    Dictionary<string, string> values;
                    using (RegistryKey subkey = key.OpenSubKey(subkeyName))
                    {
                        values = subkey.GetValueNames()
                            .ToDictionary(name => name, name => (string) subkey.GetValue(name));
                    }

                    LineFormat format = LineFormat.FromInvariantStrings(theme, values);
                    format.FormatIndexName = subkeyName;
                    if (format.FormatIndex.HasValue)
                        theme.LineFormats[format.FormatIndex.GetValueOrDefault()] = format;
                    else
                        Trace.TraceWarning("{0}.{1} is not a valid format index.", themeKey, subkeyName);
                }
            }

            return theme;
        }

        public static IndentTheme Load(IVsSettingsReader reader, string themeKey)
        {
            IndentTheme theme = new IndentTheme();

            theme.ContentType = themeKey == DefaultThemeName ? null : themeKey;
            theme.Behavior.Load(reader, themeKey);
            string caretHandler;
            reader.ReadSettingString("CaretHandler", out caretHandler);
            theme.CaretHandler = caretHandler ?? DefaultCaretHandler;

            string subkeyNames, settingNames;
            ErrorHandler.ThrowOnFailure(reader.ReadSettingString(themeKey, out subkeyNames));
            if (!string.IsNullOrEmpty(subkeyNames))
                foreach (string subkeyName in subkeyNames.Split(';'))
                {
                    if (string.IsNullOrEmpty(subkeyName)) continue;

                    int i = subkeyName.LastIndexOf('.');
                    if (i < 0) continue;

                    Dictionary<string, string> values = new Dictionary<string, string>();
                    if (ErrorHandler.Failed(reader.ReadSettingAttribute(subkeyName, "Keys", out settingNames)) ||
                        string.IsNullOrEmpty(settingNames))
                        settingNames = "LineStyle;LineColor;HighlightStyle;HighlightColor;Visible";
                    foreach (string setting in settingNames.Split(';'))
                    {
                        if (string.IsNullOrEmpty(subkeyName)) continue;
                        string value;
                        ErrorHandler.ThrowOnFailure(reader.ReadSettingAttribute(subkeyName, setting, out value));
                        if (!string.IsNullOrEmpty(value)) values[setting] = value;
                    }

                    LineFormat format = LineFormat.FromInvariantStrings(theme, values);
                    format.FormatIndexName = subkeyName.Substring(i + 1);
                    if (format.FormatIndex.HasValue)
                        theme.LineFormats[format.FormatIndex.GetValueOrDefault()] = format;
                    else
                        Trace.TraceWarning("{0}.{1} is not a valid format index.", themeKey, subkeyName);
                }

            return theme;
        }

        public string Save(RegistryKey reg)
        {
            using (RegistryKey key = reg.CreateSubKey(ContentType ?? DefaultThemeName))
            {
                Behavior.Save(key);
                key.SetValue("CaretHandler", CaretHandler ?? DefaultCaretHandler);

                foreach (string subkey in key.GetSubKeyNames()) key.DeleteSubKeyTree(subkey, false);

                foreach (LineFormat item in LineFormats.Values.Where(item => item.ShouldSerialize()))
                    using (RegistryKey subkey = key.CreateSubKey(item.FormatIndexName))
                    {
                        foreach (KeyValuePair<string, string> kv in item.ToInvariantStrings()) subkey.SetValue(kv.Key, kv.Value);
                    }
            }

            return ContentType;
        }

        public string Save(IVsSettingsWriter writer)
        {
            string key = ContentType ?? DefaultThemeName;
            string subkeys = string.Join(";", LineFormats.Values
                .Where(item => item.ShouldSerialize())
                .Select(item => key + "." + item.FormatIndexName));
            writer.WriteSettingString(key, subkeys);

            Behavior.Save(writer, key);
            writer.WriteSettingString("CaretHandler", CaretHandler ?? DefaultCaretHandler);

            foreach (LineFormat item in LineFormats.Values.Where(item => item.ShouldSerialize()))
            {
                string subkeyName = key + "." + item.FormatIndexName;

                writer.WriteSettingString(subkeyName, "");

                Dictionary<string, string> values = item.ToInvariantStrings();
                writer.WriteSettingAttribute(subkeyName, "Keys", string.Join(";", values.Keys));
                foreach (KeyValuePair<string, string> kv in values) writer.WriteSettingAttribute(subkeyName, kv.Key, kv.Value);
            }

            return key;
        }

        public void Delete(RegistryKey reg)
        {
            reg.DeleteSubKeyTree(ContentType);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as IndentTheme);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        internal void Apply()
        {
            LineFormats = LineFormats.Values
                .Where(item => item != null && item.ShouldSerialize())
                .Where(item => item.FormatIndex.HasValue)
                .ToDictionary(item => item.FormatIndex.Value);
        }

        public class PageWidthMarkerGetter : IEnumerable<PageWidthMarkerFormat>
        {
            private readonly IndentTheme Theme;

            internal PageWidthMarkerGetter(IndentTheme theme)
            {
                Theme = theme;
            }

            public PageWidthMarkerFormat this[int index] =>
                Theme.LineFormats[index + PageWidthMarkerFormat.FirstPageWidthIndex] as PageWidthMarkerFormat;

            public IEnumerator<PageWidthMarkerFormat> GetEnumerator()
            {
                return Theme.LineFormats.Values.OfType<PageWidthMarkerFormat>().GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return Theme.LineFormats.Values.OfType<PageWidthMarkerFormat>().GetEnumerator();
            }

            public bool TryGetValue(int index, out LineFormat value)
            {
                return Theme.LineFormats.TryGetValue(index + PageWidthMarkerFormat.FirstPageWidthIndex, out value) &&
                       value is PageWidthMarkerFormat;
            }
        }
    }
}