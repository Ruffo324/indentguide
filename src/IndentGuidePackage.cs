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
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using EnvDTE;
using IndentGuide.Dialogs;
using IndentGuide.Guides;
using IndentGuide.Resources;
using IndentGuide.Settings;
using Microsoft.VisualStudio.Shell;

namespace IndentGuide
{
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "15", IconResourceID = 400)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(DisplayOptions), "IndentGuide", "Display", 110, 120, false)]
    [ProvideOptionPage(typeof(BehaviorOptions), "IndentGuide", "Behavior\\QuickSet", 110, 130, false)]
    [ProvideOptionPage(typeof(CustomBehaviorOptions), "IndentGuide", "Behavior\\Custom", 110, 140, false)]
    [ProvideOptionPage(typeof(CaretOptions), "IndentGuide", "Highlighting", 110, 150, false)]
    [ProvideOptionPage(typeof(PageWidthOptions), "IndentGuide", "PageWidth", 110, 160, false)]
    [ProvideProfile(typeof(ProfileManager), "IndentGuide", "Styles", 110, 220, false, DescriptionResourceID = 230)]
    [ProvideService(typeof(SIndentGuide))]
    [ResourceDescription("IndentGuidePackage")]
    [Guid(Guids.IndentGuidePackageGuid)]
    public sealed class IndentGuidePackage : Package
    {
        private const int cmdidViewIndentGuides = 0x0103;

        // Default version is 10.9.0.0, also known as 11 (beta 1).
        // This was the version prior to the version field being added.
        public const int DEFAULT_VERSION = 0x000A0900;
        private static readonly Guid guidIndentGuideCmdSet = Guid.Parse(Guids.IndentGuideCmdSetGuid);

        private bool CommandVisible;
        private IndentGuideService Service;

        private WindowEvents WindowEvents;

        public static int Version { get; } = GetCurrentVersion();

        protected override void Initialize()
        {
            base.Initialize();

            // Prepare event
            DTE dte = GetService(typeof(DTE)) as DTE;
            if (dte != null)
            {
                CommandVisible = false;
                WindowEvents = dte.Events.WindowEvents;
                WindowEvents.WindowActivated += WindowEvents_WindowActivated;
                WindowEvents.WindowClosing += WindowEvents_WindowClosing;
            }

            // Add our command handlers for menu (commands must exist in the .vsct file)
            if (GetService(typeof(IMenuCommandService)) is OleMenuCommandService mcs)
            {
                // Create the command for the tool window
                CommandID viewIndentCommandID = new CommandID(guidIndentGuideCmdSet, cmdidViewIndentGuides);
                OleMenuCommand menuCmd = new OleMenuCommand(ToggleVisibility, viewIndentCommandID);
                menuCmd.BeforeQueryStatus += BeforeQueryStatus;

                mcs.AddCommand(menuCmd);
            }

            Service = new IndentGuideService(this);
            (this as IServiceContainer).AddService(typeof(SIndentGuide), Service, true);
            Service.Upgrade();
            Service.Load();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Service.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>
        ///     Saves the current settings.
        /// </summary>
        private void DTEEvents_OnBeginShutdown()
        {
            if (Service != null) Service.Save();
        }

        private void WindowEvents_WindowActivated(Window GotFocus, Window LostFocus)
        {
            CommandVisible = GotFocus != null && GotFocus.Kind == "Document";
        }

        private void WindowEvents_WindowClosing(Window Window)
        {
            if (Window.DTE.ActiveWindow == Window) CommandVisible = false;
        }

        private void BeforeQueryStatus(object sender, EventArgs e)
        {
            OleMenuCommand item = (OleMenuCommand) sender;

            item.Enabled = true;
            item.Checked = Service.Visible;
            item.Visible = CommandVisible;
        }

        private void ToggleVisibility(object sender, EventArgs e)
        {
            Service.Visible = !Service.Visible;
        }

        private static int GetCurrentVersion()
        {
            Assembly assembly = typeof(IndentGuideService).Assembly;
            object[] attribs = assembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), false);
            if (attribs.Length == 0) return DEFAULT_VERSION;

            AssemblyFileVersionAttribute attrib = (AssemblyFileVersionAttribute) attribs[0];

            try
            {
                int version = attrib.Version.Split('.')
                    .Select(p => int.Parse(p))
                    .Take(3)
                    .Aggregate(0, (acc, i) => (acc << 8) | i);
                Trace.TraceInformation("IndentGuideService.CURRENT_VERSION == {0:X}", version);
                return version;
            }
            catch (Exception ex)
            {
                Trace.TraceError("IndentGuide::GetCurrentVersion: {0}", ex);
                return DEFAULT_VERSION;
            }
        }
    }
}