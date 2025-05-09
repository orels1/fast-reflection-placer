﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ORL.Tools
{
    public static class ReleaseExporter
    {
        private readonly static string[] _exportFolders =
        {
            "Packages/sh.orels.fast-reflection-placer",
        };

        [Serializable]
        public class PackageInfo
        {
            public string name;
            public string displayName;
            public string version;
        }

        [MenuItem("Tools/orels1/Export Release")]
        private static void ExportAsUnityPackage()
        {
            var manifestPath = Path.Combine(_exportFolders[0], "package.json");
            var manifest = JsonUtility.FromJson<PackageInfo>(File.ReadAllText(manifestPath));
            if (manifest == null)
            {
                Debug.LogError("Failed to load main package manifest to extract version, aborting");
                return;
            }


            Debug.Log($"Exporting version {manifest.version}");

            var exportDir = Path.Combine(Directory.GetCurrentDirectory(), "Exports");
            Directory.CreateDirectory(exportDir);

            var ignored = new List<string>();

            // Export .unitypackage files
            ExportAsUnityPackage(_exportFolders[0], ignored, Path.Combine(exportDir, $"sh.orels.fast-reflection-placer-{manifest.version}.unitypackage"));

            // Export .zip files
            ExportAsZip(_exportFolders[0], ignored, Path.Combine(exportDir, $"sh.orels.fast-reflection-placer-{manifest.version}.zip"));

            // Open the export folder
            var exportedPath = new FileInfo("Exports").FullName;
            Process.Start(exportedPath);
        }

        private static void ExportAsUnityPackage(string[] baseFolders, List<string> ingored, string exportPath)
        {
            var list = baseFolders.SelectMany(f => Directory.GetFiles(f, "*", SearchOption.AllDirectories))
                .Select(f => f.Replace('/', '\\'))
                .Where(f => !ingored.Any(i => f.Contains(i, StringComparison.InvariantCultureIgnoreCase)))
                .ToArray();
            AssetDatabase.ExportPackage(list, exportPath, ExportPackageOptions.Recurse);
        }

        private static void ExportAsUnityPackage(string baseFolder, List<string> ingored, string exportPath)
        {
            var list = Directory.GetFiles(baseFolder, "*", SearchOption.AllDirectories)
                .Select(f => f.Replace('/', '\\'))
                .Where(f => !ingored.Any(i => f.Contains(i, StringComparison.InvariantCultureIgnoreCase)))
                .ToArray();
            AssetDatabase.ExportPackage(list, exportPath, ExportPackageOptions.Recurse);
        }

        private static void ExportAsZip(string baseFolder, List<string> ingored, string exportPath)
        {
            var list = Directory.GetFiles(baseFolder, "*", SearchOption.AllDirectories)
                .Select(f => f.Replace('/', '\\'))
                .Where(f => !ingored.Any(i => f.Contains(i, StringComparison.InvariantCultureIgnoreCase)))
                .ToArray();

            if (File.Exists(exportPath)) File.Delete(exportPath);
            var basePath = baseFolder.Replace('/', '\\') + '\\';
            using (var zip = new ZipArchive(File.OpenWrite(exportPath), ZipArchiveMode.Create))
            {
                foreach (var file in list)
                {
                    var entry = zip.CreateEntry(file.Replace(basePath, "").Replace('\\', '/'));
                    using (var stream = File.OpenRead(file))
                    {
                        using (var entryStream = entry.Open())
                        {
                            stream.CopyTo(entryStream);
                        }
                    }
                }
            }
        }
    }
}