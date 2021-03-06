﻿using EnvDTE;
using ErikEJ.SqlCeToolbox.Helpers;
using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace EFCorePowerTools.Handlers
{
    internal class DgmlNugetHandler
    {
        private readonly EFCorePowerToolsPackage _package;

        public DgmlNugetHandler(EFCorePowerToolsPackage package)
        {
            _package = package;
        }

        public async void InstallDgmlNuget(Project project)
        {
            _package.Dte2.StatusBar.Text = "Installing DbContext Dgml extension package";
            var nuGetHelper = new NuGetHelper();
            await nuGetHelper.InstallPackageAsync("ErikEJ.EntityFrameworkCore.DgmlBuilder", project);
            _package.Dte2.StatusBar.Text = "Dgml package installed";
            var path = Path.GetTempFileName() + ".txt";
            File.WriteAllText(path, GetReadme(), Encoding.UTF8);
            var window = _package.Dte2.ItemOperations.OpenFile(path);
            window.Document.Activate();
            Telemetry.TrackEvent("PowerTools.InstallDgmlNuget");
        }

        private string GetReadme()
        {
            var resourceName = "EFCorePowerTools.DgmlBuilder.readme.txt";

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                using (var reader = new StreamReader(stream ?? throw new InvalidOperationException()))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}