using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ParrelSync
{
    // With ScriptableObject derived classes, .cs and .asset filenames MUST be identical
    public class ParrelSyncProjectSettings : ScriptableObject
    {
        private const string ParrelSyncScriptableObjectsDirectory = "Assets/Plugins/ParrelSync/ScriptableObjects";
        private const string ParrelSyncSettingsPath = ParrelSyncScriptableObjectsDirectory + "/" + nameof(ParrelSyncProjectSettings) + ".asset";

        [SerializeField, HideInInspector] private bool assetModPref = true;
        [SerializeField, HideInInspector] private bool copyPackagesFolders;
        [SerializeField, HideInInspector] private bool alsoCheckUnityLockFileStaPref = true;
        [SerializeField, HideInInspector] private List<string> optionalSymbolicLinkFolders;

        public List<string> OptionalSymbolicLinkFolders
            => optionalSymbolicLinkFolders;

        public bool AssetModPref
        {
            get => assetModPref;
            set => assetModPref = value;
        }

        public bool AlsoCheckUnityLockFileStaPref
        {
            get => alsoCheckUnityLockFileStaPref;
            set => alsoCheckUnityLockFileStaPref = value;
        }

        public bool CopyPackagesFolders
        {
            get => copyPackagesFolders;
            set => copyPackagesFolders = value;
        }

        private static ParrelSyncProjectSettings GetOrCreateSettings()
        {
            ParrelSyncProjectSettings projectSettings;
            if (File.Exists(ParrelSyncSettingsPath))
            {
                projectSettings = AssetDatabase.LoadAssetAtPath<ParrelSyncProjectSettings>(ParrelSyncSettingsPath);

                if (projectSettings == null)
                {
                    Debug.LogError("File Exists, but failed to load: " + ParrelSyncSettingsPath);
                }

                return projectSettings;
            }

            projectSettings = CreateInstance<ParrelSyncProjectSettings>();
            projectSettings.optionalSymbolicLinkFolders = new List<string>();

            if (!Directory.Exists(ParrelSyncScriptableObjectsDirectory))
            {
                Directory.CreateDirectory(ParrelSyncScriptableObjectsDirectory);
            }

            AssetDatabase.CreateAsset(projectSettings, ParrelSyncSettingsPath);
            AssetDatabase.SaveAssets();
            return projectSettings;
        }

        public static ParrelSyncProjectSettings GetSerializedSettings()
            => GetOrCreateSettings();
    }

    public class ParrelSyncSettingsProvider : SettingsProvider
    {
        private const string MenuLocationInProjectSettings = "Project/ParrelSync";

        private ParrelSyncProjectSettings _settings;

        private ParrelSyncSettingsProvider(string path, SettingsScope scope = SettingsScope.User)
            : base(path, scope)
        {
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            // This function is called when the user clicks on the ParrelSyncSettings element in the Settings window.
            _settings = ParrelSyncProjectSettings.GetSerializedSettings();
        }

        public override void OnGUI(string searchContext)
        {
            if (ClonesManager.IsClone())
            {
                EditorGUILayout.HelpBox(
                    "This is a clone project. Please use the original project editor to change preferences.",
                    MessageType.Info);

                return;
            }

            GUILayout.BeginVertical("HelpBox");
            GUILayout.BeginVertical("GroupBox");

            _settings.AssetModPref = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "(recommended) Disable asset saving in clone editors- require re-open clone editors",
                    "Disable asset saving in clone editors so all assets can only be modified from the original project editor"
                ),
                _settings.AssetModPref);

            _settings.CopyPackagesFolders = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "CopyPackagesFolders",
                    "CopyPackagesFolders"
                ),
                _settings.CopyPackagesFolders);

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                _settings.AlsoCheckUnityLockFileStaPref = EditorGUILayout.ToggleLeft(
                    new GUIContent(
                        "Also check UnityLockFile lock status while checking clone projects running status",
                        "Disable this can slightly increase Clones Manager window performance, but will lead to in-correct clone project running status"
                        + "(the Clones Manager window show the clone project is still running even it's not) if the clone editor crashed"
                    ),
                    _settings.AlsoCheckUnityLockFileStaPref);
            }

            GUILayout.EndVertical();

            var symbolicLinkFolders = _settings.OptionalSymbolicLinkFolders;
            var optionalFolders = new List<string>(symbolicLinkFolders) { string.Empty };

            GUILayout.BeginVertical("GroupBox");
            GUILayout.Label(new GUIContent("Optional Folders to Symbolically Link"));
            GUILayout.Space(5);

            var projectPath = ClonesManager.GetCurrentProjectPath();
            var isDirty = false;
            for (var i = 0; i < optionalFolders.Count; ++i)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(optionalFolders[i], EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    var result = EditorUtility.OpenFolderPanel("Select Folder to Symbolically Link...", "", "");
                    if (result.Contains(projectPath))
                    {
                        optionalFolders[i] = result.Replace(projectPath, "");
                        isDirty = true;
                    }
                    else if (result != "")
                    {
                        Debug.LogWarning("Symbolic Link folder must be within the project directory");
                    }
                }

                if (GUILayout.Button("Clear", GUILayout.Width(60)))
                {
                    optionalFolders[i] = "";
                    isDirty = true;
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();

            if (isDirty)
            {
                optionalFolders.RemoveAll(string.IsNullOrEmpty);
                symbolicLinkFolders.Clear();
                symbolicLinkFolders.AddRange(optionalFolders);
            }

            if (GUILayout.Button("Reset to default"))
            {
                _settings.AssetModPref = true;
                _settings.CopyPackagesFolders = false;
                _settings.AlsoCheckUnityLockFileStaPref = true;
                _settings.OptionalSymbolicLinkFolders.Clear();

                Debug.Log("Editor preferences cleared");
            }

            GUILayout.EndVertical();

            if (isDirty)
            {
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssets();
            }
        }

        // Register the SettingsProvider
        [SettingsProvider]
        public static SettingsProvider CreateParrelSyncSettingsProvider()
            => new ParrelSyncSettingsProvider(MenuLocationInProjectSettings, SettingsScope.Project);
    }
}
