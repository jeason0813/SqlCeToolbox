﻿using EFCorePowerTools.Extensions;
using EnvDTE;
using ErikEJ.SqlCeToolbox.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace EFCorePowerTools.Handlers
{
    internal class ModelAnalyzerHandler
    {
        private readonly EFCorePowerToolsPackage _package;
        private readonly ProcessLauncher _processLauncher = new ProcessLauncher();

        public ModelAnalyzerHandler(EFCorePowerToolsPackage package)
        {
            _package = package;
        }

        public void Generate(string outputPath, Project project, bool generateDdl = false)
        {
            try
            {
                if (project.Properties.Item("TargetFrameworkMoniker") == null)
                {
                    EnvDteHelper.ShowError("The selected project type has no TargetFrameworkMoniker");
                    return;
                }

                if (!project.Properties.Item("TargetFrameworkMoniker").Value.ToString().Contains(".NETFramework")
                    && !project.Properties.Item("TargetFrameworkMoniker").Value.ToString().Contains(".NETCoreApp,Version=v2.0"))
                {
                    EnvDteHelper.ShowError("Currently only .NET Framework and .NET Core 2.0 projects are supported - TargetFrameworkMoniker: " + project.Properties.Item("TargetFrameworkMoniker").Value);
                    return;
                }

                bool isNetCore = project.Properties.Item("TargetFrameworkMoniker").Value.ToString().Contains(".NETCoreApp,Version=v2.0");

                var processResult = _processLauncher.GetOutput(outputPath, isNetCore, generateDdl);

                if (processResult.StartsWith("Error:"))
                {
                    throw new ArgumentException(processResult);
                }

                if (generateDdl)
                {
                    GenerateDatabaseScripts(processResult, project);
                    Telemetry.TrackEvent("PowerTools.GenerateSqlCreate");
                }
                else
                {
                    GenerateDgml(processResult, project);
                    Telemetry.TrackEvent("PowerTools.GenerateModelDgml");
                }
            }
            catch (Exception exception)
            {
                _package.LogError(new List<string>(), exception);
            }
        }

        private void GenerateDgml(string processResult, Project project)
        {
            var dgmlBuilder = new DgmlBuilder.DgmlBuilder();
            var result = BuildModelResult(processResult);
            ProjectItem item = null;

            foreach (var info in result)
            {
                var dgmlText = dgmlBuilder.Build(info.Item2, info.Item1, GetTemplate());

                var path = Path.GetTempPath() + info.Item1 + ".dgml";
                File.WriteAllText(path, dgmlText, Encoding.UTF8);
                item = project.ProjectItems.GetItem(Path.GetFileName(path));
                if (item != null)
                {
                    item.Delete();
                }
                item = project.ProjectItems.AddFromFileCopy(path);
            }

            if (item != null)
            {
                var window = item.Open();
                window.Document.Activate();
            }
        }

        public void GenerateDatabaseScripts(string processResult, Project project)
        {
            var result = BuildModelResult(processResult);

            foreach (var item in result)
            {
                var filePath = Path.Combine(Path.GetTempPath(),
                    item.Item1 + ".sql");

                if (File.Exists(filePath))
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                }
                File.WriteAllText(filePath, item.Item2);
                File.SetAttributes(filePath, FileAttributes.ReadOnly);

                _package.Dte2.ItemOperations.OpenFile(filePath);
            }
        }

        private List<Tuple<string, string>> BuildModelResult(string modelInfo)
        {
            var result = new List<Tuple<string, string>>();

            var contexts = modelInfo.Split(new[] { "DbContext:" + Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var context in contexts)
            {
                var parts = context.Split(new[] { "DebugView:" + Environment.NewLine }, StringSplitOptions.None);
                result.Add(new Tuple<string, string>(parts[0].Trim(), parts[1].Trim()));
            }

            return result;
        }

        private string GetTemplate()
        {
            var resourceName = "EFCorePowerTools.DgmlBuilder.template.dgml";

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