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
using System.Globalization;
using IndentGuide.Guides;
using Microsoft.Win32;

namespace IndentGuide.Settings
{
    internal class UpgradeManager
    {
        public bool Upgrade(IIndentGuide service, RegistryKey root, string subkeyName)
        {
            int version;
            using (RegistryKey reg = root.OpenSubKey(subkeyName, false))
            {
                version = reg == null ? 0 : (int) reg.GetValue("Version", IndentGuidePackage.DEFAULT_VERSION);
            }

            if (version == 0 || version == IndentGuidePackage.Version) return false;

            using (RegistryKey reg = root.CreateSubKey(subkeyName))
            {
                if (version >= 0x000C0903)
                {
                    // Nothing to upgrade
                }
                else if (version == 0x000C0902)
                {
                    UpgradeFrom_12_9_2(reg);
                }
                else if (version >= 0x000C0000)
                {
                    // Nothing to upgrade
                }
                else if (version >= 0x000B0901)
                {
                    UpgradeFrom_11_9_0(reg);
                }
                else if (version >= 0x000A0901)
                {
                    UpgradeFrom_10_9_1(reg);
                }
                else
                {
                    UpgradeFrom_Earlier(reg);
                }

                // Upgrading will make guides visible regardless of the
                // previous setting.
                reg.SetValue("Visible", 1);
            }

            return true;
        }

        /// <summary>
        ///     Upgrades from v13 (Beta 3).
        /// </summary>
        private static void UpgradeFrom_12_9_2(RegistryKey reg)
        {
            reg.DeleteValue("CaretHandlers", false);

            foreach (string themeName in reg.GetSubKeyNames())
                using (RegistryKey themeKey = reg.OpenSubKey(themeName, true))
                {
                    if (themeKey == null) continue;

                    foreach (string keyName in themeKey.GetSubKeyNames())
                    {
                        int value;
                        if (!int.TryParse(keyName, out value) || value < PageWidthMarkerFormat.FirstPageWidthIndex)
                            continue;

                        using (RegistryKey key = themeKey.OpenSubKey(keyName, true))
                        {
                            if (key == null) continue;

                            key.SetValue("TypeName", typeof(PageWidthMarkerFormat).FullName);
                            key.SetValue("Position",
                                (value - PageWidthMarkerFormat.FirstPageWidthIndex).ToString(CultureInfo
                                    .InvariantCulture));
                        }
                    }
                }
        }

        /// <summary>
        ///     Upgrades from v12 (Beta 1).
        /// </summary>
        private static void UpgradeFrom_11_9_0(RegistryKey reg)
        {
            foreach (string themeName in reg.GetSubKeyNames())
                using (RegistryKey themeKey = reg.OpenSubKey(themeName, true))
                {
                    if (themeKey == null) continue;

                    themeKey.SetValue("ExtendInwardsOnly", 1);
                    themeKey.DeleteValue("TopToBottom", false);
                }
        }

        /// <summary>
        ///     Upgrades from v11 (Beta 2) through to v11.
        /// </summary>
        private static void UpgradeFrom_10_9_1(RegistryKey reg)
        {
            // v11 (beta 2) (10.9.1) and later need highlighting settings to be copied.
            foreach (string themeName in reg.GetSubKeyNames())
                using (RegistryKey themeKey = reg.OpenSubKey(themeName, true))
                {
                    if (themeKey == null) continue;

                    themeKey.SetValue("ExtendInwardsOnly", 1);
                    themeKey.DeleteValue("TopToBottom", false);

                    string highlightColor = null, highlightStyle = null;

                    using (RegistryKey key = themeKey.OpenSubKey("Caret"))
                    {
                        if (key != null)
                        {
                            highlightColor = (string) key.GetValue("LineColor");
                            highlightStyle = (string) key.GetValue("LineStyle");
                        }
                    }

                    themeKey.DeleteSubKeyTree("Caret", false);

                    foreach (string keyName in themeKey.GetSubKeyNames())
                        using (RegistryKey key = themeKey.OpenSubKey(keyName, true))
                        {
                            if (key == null) continue;

                            if (key.GetValue("HighlightColor") == null) key.SetValue("HighlightColor", highlightColor);
                            if (key.GetValue("HighlightStyle") == null) key.SetValue("HighlightStyle", highlightStyle);
                        }
                }
        }

        /// <summary>
        ///     Upgrades from versions up to and including v11 (beta 1)
        /// </summary>
        private static void UpgradeFrom_Earlier(RegistryKey reg)
        {
            foreach (string themeName in reg.GetSubKeyNames())
            {
                using (RegistryKey themeKey = reg.OpenSubKey(themeName))
                {
                    if (themeKey == null) continue;
                    string name = themeKey.GetValue("Name") as string;
                    if (string.IsNullOrEmpty(name)) continue;

                    using (RegistryKey newKey = reg.CreateSubKey(name))
                    {
                        newKey.SetValue("VisibleAligned", 1);
                        newKey.SetValue("VisibleUnaligned", 0);
                        newKey.SetValue("ExtendInwardsOnly", 1);

                        // Upgrade the old EmptyLineMode enumeration
                        string elm = themeKey.GetValue("EmptyLineMode") as string ?? "SameAsAboveLogical";
                        switch (elm)
                        {
                            case "NoGuides":
                                newKey.SetValue("VisibleEmpty", 0);
                                newKey.SetValue("VisibleEmptyAtEnd", 0);
                                break;
                            case "SameAsLineAboveActual":
                                newKey.SetValue("VisibleEmpty", 1);
                                newKey.SetValue("VisibleEmptyAtEnd", 0);
                                break;
                            case "SameAsLineAboveLogical":
                                newKey.SetValue("VisibleEmpty", 1);
                                newKey.SetValue("VisibleEmptyAtEnd", 1);
                                break;
                            case "SameAsLineBelowActual":
                                newKey.SetValue("VisibleEmpty", 1);
                                newKey.SetValue("VisibleEmptyAtEnd", 0);
                                break;
                            case "SameAsLineBelowLogical":
                                newKey.SetValue("VisibleEmpty", 1);
                                newKey.SetValue("VisibleEmptyAtEnd", 1);
                                break;
                        }

                        // Upgrade the old VisibleAtText setting (default to false)
                        bool ate = string.Equals("True", themeKey.GetValue("VisibleAtText") as string,
                            StringComparison.InvariantCultureIgnoreCase);
                        newKey.SetValue("VisibleAtTextEnd", ate ? 1 : 0);

                        // Copy the default color/style to Default, Unaligned and Caret themes.
                        // Change the Caret theme color to Red, or Teal if it was already red.
                        using (RegistryKey subKey1 = newKey.CreateSubKey("Default"))
                        using (RegistryKey subKey2 = newKey.CreateSubKey("Unaligned"))
                        {
                            bool visible = !string.Equals("False", themeKey.GetValue("Visible") as string,
                                StringComparison.InvariantCultureIgnoreCase);
                            string color = themeKey.GetValue("LineColor") as string ?? "Teal";
                            string style = themeKey.GetValue("LineStyle") as string ?? "Dotted";
                            subKey1.SetValue("Visible", visible ? 1 : 0);
                            subKey2.SetValue("Visible", visible ? 1 : 0);
                            subKey1.SetValue("LineColor", color);
                            subKey2.SetValue("LineColor", color);
                            subKey1.SetValue("LineStyle", style);
                            subKey2.SetValue("LineStyle", style);
                            subKey1.SetValue("HighlightColor", color == "Red" ? "Teal" : "Red");
                            subKey1.SetValue("HighlightStyle", style);
                            subKey2.SetValue("HighlightColor", color == "Red" ? "Teal" : "Red");
                            subKey2.SetValue("HighlightStyle", style);
                        }

                        // Copy the existing indent overrides.
                        foreach (string subkeyName in themeKey.GetSubKeyNames())
                        {
                            int formatIndex;
                            if (!int.TryParse(subkeyName, out formatIndex))
                                continue;

                            using (RegistryKey subKey1 = themeKey.OpenSubKey(subkeyName))
                            using (RegistryKey subKey2 = newKey.CreateSubKey(subkeyName))
                            {
                                bool visible = !string.Equals("False", subKey1.GetValue("Visible") as string,
                                    StringComparison.InvariantCultureIgnoreCase);
                                string color = subKey1.GetValue("LineColor") as string ?? "Teal";
                                string style = subKey1.GetValue("LineStyle") as string ?? "Dotted";
                                subKey2.SetValue("Visible", visible ? 1 : 0);
                                subKey2.SetValue("LineColor", color);
                                subKey2.SetValue("LineStyle", style);
                                subKey2.SetValue("HighlightColor", color == "Red" ? "Teal" : "Red");
                                subKey2.SetValue("HighlightStyle", style);
                            }
                        }
                    }
                }

                reg.DeleteSubKeyTree(themeName, false);
            }
        }
    }
}