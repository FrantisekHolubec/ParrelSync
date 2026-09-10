using UnityEditor;

namespace ParrelSync
{
    /// <summary>
    /// Re-syncs isolated ProjectSettings copies at a few editor events, so clones pick up
    /// changes made in the original without a restart.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSettingsSyncPoints
    {
        static ProjectSettingsSyncPoints()
        {
            if (ClonesManager.IsClone())
            {
                SyncCurrentClone();
                EditorApplication.focusChanged += OnFocusChanged;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            }
            else
            {
                SyncAllClones();
            }
        }

        private static void OnFocusChanged(bool hasFocus)
        {
            if (hasFocus)
            {
                SyncCurrentClone();
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                SyncCurrentClone();
            }
        }

        private static void SyncCurrentClone()
        {
            var currentProjectPath = ClonesManager.GetCurrentProjectPath();
            var originalProjectPath = ClonesManager.GetOriginalProjectPath();
            if (string.IsNullOrEmpty(originalProjectPath) || !ProjectSettingsIsolation.IsIsolated(currentProjectPath))
            {
                return;
            }

            ProjectSettingsIsolation.Sync(originalProjectPath, currentProjectPath);
        }

        private static void SyncAllClones()
        {
            var originalProjectPath = ClonesManager.GetCurrentProjectPath();
            foreach (var cloneProjectPath in ClonesManager.GetCloneProjectsPath())
            {
                if (ProjectSettingsIsolation.IsIsolated(cloneProjectPath))
                {
                    ProjectSettingsIsolation.Sync(originalProjectPath, cloneProjectPath);
                }
            }
        }
    }
}
