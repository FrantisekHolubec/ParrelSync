using System;
using UnityEditor;
using UnityEngine;

namespace ParrelSync
{
    /// <summary>
    /// For preventing assets being modified from the clone instance.
    /// </summary>
    public class ParrelSyncAssetModificationProcessor : AssetModificationProcessor
    {
        public static string[] OnWillSaveAssets(string[] paths)
        {
            var projectSettings = ParrelSyncProjectSettings.GetSerializedSettings();
            if (!ClonesManager.IsClone() || !projectSettings.AssetModPref)
            {
                return paths;
            }

            if (paths is not { Length: > 0 } || EditorQuit.IsQuiting)
            {
                return Array.Empty<string>();
            }

            foreach (var path in paths)
            {
                Debug.Log($"Asset modifications saving detected and blocked: {path}");
            }

            return Array.Empty<string>();
        }
    }
}