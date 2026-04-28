using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ParrelSync
{
    [InitializeOnLoad]
    public class ValidateCopiedFoldersIntegrity
    {
        private const string SessionStateKey = "ValidateCopiedFoldersIntegrity_Init";

        /// <summary>
        /// Called once on editor startup.
        /// Validate copied folders integrity in clone project
        /// </summary>
        static ValidateCopiedFoldersIntegrity()
        {
            if (SessionState.GetBool(SessionStateKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionStateKey, true);
            if (!ClonesManager.IsClone())
            {
                return;
            }

            var projectSettings = ParrelSyncProjectSettings.GetSerializedSettings();
            if (projectSettings.CopyPackagesFolders)
            {
                ValidateFolder(ClonesManager.GetCurrentProjectPath(), ClonesManager.GetOriginalProjectPath(), "Packages");
            }
        }

        public static void ValidateFolder(string targetRoot, string originalRoot, string folderName)
        {
            var targetFolderPath = Path.Combine(targetRoot, folderName);
            var targetFolderHash = CreateMd5ForFolder(targetFolderPath);

            var originalFolderPath = Path.Combine(originalRoot, folderName);
            var originalFolderHash = CreateMd5ForFolder(originalFolderPath);

            if (targetFolderHash != originalFolderHash)
            {
                Debug.Log("ParrelSync: Detected changes in '" + folderName + "' directory. Updating cloned project...");
                FileUtil.ReplaceDirectory(originalFolderPath, targetFolderPath);
            }
        }

        private static string CreateMd5ForFolder(string path)
        {
            // assuming you want to include nested folders
            var files = Directory
                .GetFiles(path, "*.*", SearchOption.AllDirectories)
                .OrderBy(p => p)
                .ToList();

            var md5 = MD5.Create();

            for (var i = 0; i < files.Count; i++)
            {
                var file = files[i];

                // hash path
                var relativePath = file.Substring(path.Length + 1);
                var pathBytes = Encoding.UTF8.GetBytes(relativePath.ToLower());
                md5.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

                // hash contents
                var contentBytes = File.ReadAllBytes(file);
                if (i == files.Count - 1)
                {
                    md5.TransformFinalBlock(contentBytes, 0, contentBytes.Length);
                }
                else
                {
                    md5.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);
                }
            }

            return BitConverter.ToString(md5.Hash).ToLower();
        }
    }
}
