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
using System.Globalization;
using System.Linq;
using System.Text;
using IndentGuide.Settings;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;

namespace IndentGuide.Guides
{
    internal sealed class CaretHandlerInfo : ICaretHandlerInfo
    {
        public string DisplayName;
        public string Documentation;
        public int SortOrder;
        public string TypeName;

        string ICaretHandlerInfo.DisplayName => DisplayName;
        string ICaretHandlerInfo.Documentation => Documentation;
        string ICaretHandlerInfo.TypeName => TypeName;
    }

    /// <summary>
    ///     Implementation of the service supporting Indent Guides.
    /// </summary>
    internal class IndentGuideService : SIndentGuide, IIndentGuide2, IDisposable
    {
        private const string SUBKEY_NAME = "IndentGuide";
        private const string CARETHANDLERS_SUBKEY_NAME = "Caret Handlers";

        private const string DefaultCollection = "IndentGuide";
        private const string CaretHandlersCollection = DefaultCollection + "\\Caret Handlers";

        private readonly Dictionary<string, IndentTheme> _Themes = new Dictionary<string, IndentTheme>();
        private readonly Stack<TemporarySettingStore> Preserved = new Stack<TemporarySettingStore>();

        private List<CaretHandlerInfo> _CaretHandlerNames;
        private bool _Visible;
        private bool IsDisposed;

        public IndentGuideService(IndentGuidePackage package)
        {
            Package = package;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public int Version => IndentGuidePackage.Version;

        public IndentGuidePackage Package { get; }

        public bool Visible
        {
            get => _Visible;
            set
            {
                if (_Visible != value)
                {
                    _Visible = value;

                    // Save the setting immediately.
                    SaveVisibleSettingToStorage();

                    EventHandler evt = VisibleChanged;
                    if (evt != null) evt(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler VisibleChanged;

        [Obsolete("CaretHandler has been moved to IndentTheme")]
        public string CaretHandler
        {
            get => null;
            set { }
        }

        [Obsolete("CaretHandlerChanged has been moved to IndentTheme")]
        public event EventHandler CaretHandlerChanged
        {
            add { }
            remove { }
        }

        public IDictionary<string, IndentTheme> Themes => _Themes;
        public IndentTheme DefaultTheme { get; set; }

        public event EventHandler ThemesChanged;

        public void Save()
        {
            lock (Preserved)
            {
                if (Preserved.Count > 0) return;
            }

            using (RegistryKey root = Package.UserRegistryRoot)
            using (RegistryKey reg = root != null ? root.CreateSubKey(SUBKEY_NAME) : null)
            {
                if (reg != null)
                    try
                    {
                        SaveToRegistry(reg);
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(string.Format("IndentGuideService::Save: {0}", ex));
                    }
            }
        }

        public void Save(IVsSettingsWriter writer)
        {
            lock (_Themes)
            {
                StringBuilder sb = new StringBuilder();
                if (DefaultTheme != null)
                {
                    sb.Append(DefaultTheme.Save(writer));
                    sb.Append(";");
                }

                foreach (IndentTheme theme in _Themes.Values)
                {
                    sb.Append(theme.Save(writer));
                    sb.Append(";");
                }

                writer.WriteSettingLong("Version", Version);
                writer.WriteSettingString("Themes", sb.ToString());
                writer.WriteSettingLong("Visible", Visible ? 1 : 0);
            }
        }

        public void Load()
        {
            lock (Preserved)
            {
                if (Preserved.Count > 0) return;
            }

            using (RegistryKey root = Package.UserRegistryRoot)
            using (RegistryKey reg = root != null ? root.OpenSubKey(SUBKEY_NAME) : null)
            {
                if (reg != null)
                    try
                    {
                        LoadFromRegistry(reg);
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(string.Format("IndentGuideService::Load: {0}", ex));
                    }
                else
                    Visible = true;
            }
        }

        public void Load(IVsSettingsReader reader)
        {
            lock (_Themes)
            {
                _Themes.Clear();
                DefaultTheme = new IndentTheme();

                string themeKeysString;
                reader.ReadSettingString("Themes", out themeKeysString);

                foreach (string key in themeKeysString.Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(key)) continue;

                    try
                    {
                        IndentTheme theme = IndentTheme.Load(reader, key);
                        if (theme.IsDefault)
                            DefaultTheme = theme;
                        else
                            _Themes[theme.ContentType] = theme;
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(string.Format("IndentGuide::LoadSettingsFromXML: {0}", ex));
                    }
                }

                int tempInt;
                reader.ReadSettingLong("Visible", out tempInt);
                Visible = tempInt != 0;
            }

            OnThemesChanged();
        }

        public void Reset()
        {
            Package.UserRegistryRoot.DeleteSubKeyTree(SUBKEY_NAME, false);
            Load();
        }

        public IEnumerable<ICaretHandlerInfo> CaretHandlerNames
        {
            get
            {
                if (_CaretHandlerNames == null)
                    _CaretHandlerNames = LoadRegisteredCaretHandlers()
                        .Select(n =>
                        {
                            ICaretHandlerMetadata md = CaretHandlerBase.MetadataFromName(n);
                            return md == null
                                ? null
                                : new CaretHandlerInfo
                                {
                                    DisplayName = md.GetDisplayName(CultureInfo.CurrentUICulture),
                                    Documentation = md.GetDocumentation(CultureInfo.CurrentUICulture),
                                    TypeName = n,
                                    SortOrder = md.GetSortOrder(CultureInfo.CurrentUICulture)
                                };
                        })
                        .Where(h => h != null)
                        .OrderBy(h => h.SortOrder)
                        .ThenBy(h => h.TypeName)
                        .ToList();
                return _CaretHandlerNames;
            }
        }

        ~IndentGuideService()
        {
            Dispose(false);
        }

        protected void Dispose(bool isDisposing)
        {
            if (!IsDisposed && isDisposing) IsDisposed = true;
        }

        public bool PreserveSettings()
        {
            lock (Preserved)
            {
                TemporarySettingStore preserved = new TemporarySettingStore();
                Save(preserved);
                Preserved.Push(preserved);
            }

            return true;
        }

        public bool AcceptSettings()
        {
            bool result = false;
            lock (Preserved)
            {
                Preserved.Pop();
                result = Preserved.Count == 0;
            }

            if (result) OnThemesChanged();
            return result;
        }

        public bool RollbackSettings()
        {
            bool result = false;
            lock (Preserved)
            {
                if (Preserved.Count > 0)
                {
                    Load(Preserved.Pop());
                    result = true;
                }
            }

            if (result) OnThemesChanged();
            return result;
        }

        private void SaveVisibleSettingToStorage()
        {
            using (RegistryKey reg = Package.UserRegistryRoot.OpenSubKey(SUBKEY_NAME, true))
            {
                if (reg != null)
                {
                    // Key already exists, so just update this setting.
                    reg.SetValue("Visible", Visible ? 1 : 0);
                    return;
                }
            }

            // Key doesn't exist, so save all settings.
            Save();
        }

        public void OnThemesChanged()
        {
            Save();
            EventHandler evt = ThemesChanged;
            if (evt != null) evt(this, EventArgs.Empty);
        }

        private void SaveToRegistry(RegistryKey reg)
        {
            Debug.Assert(reg != null, "reg cannot be null");

            lock (_Themes)
            {
                reg.SetValue("Version", Version);
                reg.SetValue("Visible", Visible ? 1 : 0);

                foreach (string key in reg.GetSubKeyNames())
                {
                    if (CARETHANDLERS_SUBKEY_NAME.Equals(key, StringComparison.InvariantCulture)) continue;
                    reg.DeleteSubKeyTree(key);
                }

                if (DefaultTheme != null) DefaultTheme.Save(reg);

                foreach (IndentTheme theme in _Themes.Values) theme.Save(reg);
            }
        }

        private void LoadFromRegistry(RegistryKey reg)
        {
            Debug.Assert(reg != null, "reg cannot be null");

            lock (_Themes)
            {
                _Themes.Clear();
                DefaultTheme = new IndentTheme();
                foreach (string themeName in reg.GetSubKeyNames())
                {
                    if (CARETHANDLERS_SUBKEY_NAME.Equals(themeName, StringComparison.InvariantCulture)) continue;
                    IndentTheme theme = IndentTheme.Load(reg, themeName);
                    if (theme.IsDefault)
                        DefaultTheme = theme;
                    else
                        _Themes[theme.ContentType] = theme;
                }

                Visible = (int) reg.GetValue("Visible", 1) != 0;
            }

            OnThemesChanged();
        }

        internal bool Upgrade()
        {
            UpgradeManager upgrade = new UpgradeManager();
            using (RegistryKey root = Package.UserRegistryRoot)
            {
                return upgrade.Upgrade(this, root, SUBKEY_NAME);
            }
        }


        private List<string> LoadRegisteredCaretHandlers()
        {
            List<string> result = new List<string>();
            result.Add(typeof(CaretNone).FullName);
            result.Add(typeof(CaretNearestLeft).FullName);
            result.Add(typeof(CaretNearestLeft2).FullName);
            result.Add(typeof(CaretAdjacent).FullName);
            result.Add(typeof(CaretAboveBelowEnds).FullName);

            using (RegistryKey reg = Package.UserRegistryRoot.OpenSubKey(SUBKEY_NAME))
            {
                if (reg != null)
                    using (RegistryKey subreg = reg.OpenSubKey(CARETHANDLERS_SUBKEY_NAME))
                    {
                        if (subreg != null)
                            foreach (string name in subreg.GetValueNames())
                                result.Add(subreg.GetValue(name) as string ?? name);
                    }
            }

            return result;
        }
    }
}