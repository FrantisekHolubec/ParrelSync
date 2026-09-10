using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ParrelSync
{
    /// <summary>
    /// Gives a clone its own ProjectSettings copy with a per-clone product name,
    /// so PlayerPrefs and persistentDataPath are not shared with the original project.
    /// </summary>
    public static class ProjectSettingsIsolation
    {
        private const string ProjectSettingsFolderName = "ProjectSettings";
        private const string ProjectSettingsAssetFileName = "ProjectSettings.asset";

        private static readonly Regex ProductNameRegex = new Regex(
            @"^(?<prefix>[ \t]*productName:[ \t]*)(?<value>.*?)(?<suffix>[ \t]*)$",
            RegexOptions.Multiline);

        /// <summary>
        /// True when the clone has its own ProjectSettings folder instead of a link to the original.
        /// </summary>
        public static bool IsIsolated(string cloneProjectPath)
        {
            var path = Path.Combine(cloneProjectPath, ProjectSettingsFolderName);
            return Directory.Exists(path) && !IsLink(path);
        }

        /// <summary>
        /// True when the source project's ProjectSettings.asset is text serialized and its product name can be patched.
        /// </summary>
        public static bool CanIsolate(string sourceProjectPath)
        {
            var assetPath = Path.Combine(sourceProjectPath, ProjectSettingsFolderName, ProjectSettingsAssetFileName);
            return File.Exists(assetPath) && ProductNameRegex.IsMatch(File.ReadAllText(assetPath));
        }

        /// <summary>
        /// Suffix appended to the product name of the clone, e.g. "_clone_0".
        /// </summary>
        public static string GetProductNameSuffix(string cloneProjectPath)
        {
            var index = cloneProjectPath.LastIndexOf(ClonesManager.CloneNameSuffix, StringComparison.Ordinal);
            return index > 0 ? cloneProjectPath.Substring(index) : ClonesManager.CloneNameSuffix;
        }

        /// <summary>
        /// Copies the original ProjectSettings folder into the clone, patching the product name.
        /// Only files that differ are written; files missing in the original are removed.
        /// </summary>
        public static void Sync(string sourceProjectPath, string cloneProjectPath)
        {
            var sourceFolder = Path.Combine(sourceProjectPath, ProjectSettingsFolderName);
            var targetFolder = Path.Combine(cloneProjectPath, ProjectSettingsFolderName);

            if (string.Equals(Path.GetFullPath(sourceFolder), Path.GetFullPath(targetFolder), StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("ParrelSync: Cannot sync ProjectSettings into itself.");
                return;
            }

            if (!Directory.Exists(sourceFolder))
            {
                Debug.LogError("ParrelSync: Original ProjectSettings folder not found: " + sourceFolder);
                return;
            }

            if (IsLink(targetFolder))
            {
                Debug.LogWarning("ParrelSync: ProjectSettings of '" + cloneProjectPath + "' is linked to the original, not a copy. "
                                 + "Delete and re-create the clone to isolate PlayerPrefs.");
                return;
            }

            var suffix = GetProductNameSuffix(cloneProjectPath);
            Directory.CreateDirectory(targetFolder);

            var expectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var changedFiles = 0;

            foreach (var sourceFile in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
            {
                var relativePath = sourceFile.Substring(sourceFolder.Length + 1);
                var targetFile = Path.GetFullPath(Path.Combine(targetFolder, relativePath));
                expectedFiles.Add(targetFile);

                var content = File.ReadAllBytes(sourceFile);
                if (relativePath == ProjectSettingsAssetFileName)
                {
                    content = PatchProductName(content, suffix) ?? content;
                }

                if (File.Exists(targetFile) && content.AsSpan().SequenceEqual(File.ReadAllBytes(targetFile)))
                {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                File.WriteAllBytes(targetFile, content);
                changedFiles++;
            }

            foreach (var targetFile in Directory.GetFiles(targetFolder, "*", SearchOption.AllDirectories))
            {
                if (expectedFiles.Contains(Path.GetFullPath(targetFile)))
                {
                    continue;
                }

                File.Delete(targetFile);
                changedFiles++;
            }

            if (changedFiles > 0)
            {
                Debug.Log("ParrelSync: ProjectSettings synced to '" + targetFolder + "' (" + changedFiles + " file(s) updated, product name suffix '" + suffix + "').");
            }
        }

        private static byte[] PatchProductName(byte[] content, string suffix)
        {
            var text = Encoding.UTF8.GetString(content);
            var match = ProductNameRegex.Match(text);
            if (!match.Success)
            {
                Debug.LogWarning("ParrelSync: Could not find productName in " + ProjectSettingsAssetFileName
                                 + ". Enable 'Force Text' asset serialization to isolate PlayerPrefs.");
                return null;
            }

            var patched = text.Substring(0, match.Index)
                          + match.Groups["prefix"].Value
                          + AppendSuffix(match.Groups["value"].Value, suffix)
                          + match.Groups["suffix"].Value
                          + text.Substring(match.Index + match.Length);

            return new UTF8Encoding(false).GetBytes(patched);
        }

        private static string AppendSuffix(string value, string suffix)
        {
            var isQuoted = value.Length >= 2
                           && (value[0] == '\'' || value[0] == '"')
                           && value[value.Length - 1] == value[0];

            return isQuoted
                ? value.Substring(0, value.Length - 1) + suffix + value[0]
                : value + suffix;
        }

        private static bool IsLink(string path)
            => Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }
}
