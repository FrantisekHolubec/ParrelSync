using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ParrelSync
{
    /// <summary>
    /// Contains all required methods for creating a linked clone of the Unity project.
    /// </summary>
    public class ClonesManager
    {
        /// <summary>
        /// Name used for an identifying file created in the clone project directory.
        /// </summary>
        /// <remarks>
        /// (!) Do not change this after the clone was created, because then connection will be lost.
        /// </remarks>
        public const string CloneFileName = ".clone";

        /// <summary>
        /// Suffix added to the end of the project clone name when it is created.
        /// </summary>
        /// <remarks>
        /// (!) Do not change this after the clone was created, because then connection will be lost.
        /// </remarks>
        public const string CloneNameSuffix = "_clone";

        public const string ProjectName = "ParrelSync";

        /// <summary>
        /// The maximum number of clones
        /// </summary>
        public const int MaxCloneProjectCount = 10;

        /// <summary>
        /// Name of the file for storing clone's argument.
        /// </summary>
        public const string ArgumentFileName = ".parrelsyncarg";

        /// <summary>
        /// Default argument of the new clone
        /// </summary>
        public const string DefaultArgument = "client";

        #region Managing clones

        /// <summary>
        /// Creates clone from the project currently open in Unity Editor.
        /// </summary>
        /// <returns></returns>
        public static Project CreateCloneFromCurrent()
        {
            if (IsClone())
            {
                Debug.LogError("This project is already a clone. Cannot clone it.");
                return null;
            }

            var currentProjectPath = GetCurrentProjectPath();
            return CreateCloneFromPath(currentProjectPath);
        }

        /// <summary>
        /// Creates clone of the project located at the given path.
        /// </summary>
        /// <param name="sourceProjectPath"></param>
        /// <returns></returns>
        public static Project CreateCloneFromPath(string sourceProjectPath)
        {
            var sourceProject = new Project(sourceProjectPath);

            string cloneProjectPath = null;

            // Find available clone suffix id
            var availableCloneSuffixId = 0;
            for (var i = 0; i < MaxCloneProjectCount; i++)
            {
                var originalProjectPath = GetCurrentProject().projectPath;
                var possibleCloneProjectPath = originalProjectPath + CloneNameSuffix + "_" + i;

                if (!Directory.Exists(possibleCloneProjectPath))
                {
                    cloneProjectPath = possibleCloneProjectPath;
                    availableCloneSuffixId = i;
                    break;
                }
            }

            if (string.IsNullOrEmpty(cloneProjectPath))
            {
                Debug.LogError("The number of cloned projects has reach its limit. Limit: " + MaxCloneProjectCount);
                return null;
            }

            var cloneProject = new Project(cloneProjectPath);

            Debug.Log("Start cloning project, original project: " + sourceProject + ", clone project: " + cloneProject);

            CreateProjectFolder(cloneProject);

            // Link Folders
            LinkFolders(sourceProject.assetPath, cloneProject.assetPath);
            LinkFolders(sourceProject.projectSettingsPath, cloneProject.projectSettingsPath);
            LinkFolders(sourceProject.autoBuildPath, cloneProject.autoBuildPath);
            LinkFolders(sourceProject.localPackages, cloneProject.localPackages);

            // Optional Link Folders
            var projectSettings = ParrelSyncProjectSettings.GetSerializedSettings();
            var optionalLinkPaths = projectSettings.OptionalSymbolicLinkFolders;

            foreach (var path in optionalLinkPaths)
            {
                var sourceOptionalPath = sourceProjectPath + path;
                var cloneOptionalPath = cloneProjectPath + path;
                LinkFolders(sourceOptionalPath, cloneOptionalPath);
            }

            if (projectSettings.CopyPackagesFolders)
            {
                Debug.Log("Packages copy: " + cloneProject.packagesPath);
                CopyDirectoryWithProgressBar(
                    sourceProject.packagesPath, cloneProject.packagesPath,
                    "Cloning Project Packages '" + sourceProject.name + "'. ");
            }
            else
            {
                LinkFolders(sourceProject.packagesPath, cloneProject.packagesPath);
            }

            // Copy Folders
            Debug.Log("Library copy: " + cloneProject.libraryPath);
            CopyDirectoryWithProgressBar(
                sourceProject.libraryPath, cloneProject.libraryPath,
                "Cloning Project Library '" + sourceProject.name + "'. ");

            RegisterClone(cloneProject, availableCloneSuffixId);

            return cloneProject;
        }

        /// <summary>
        /// Registers a clone by placing an identifying ".clone" file in its root directory.
        /// </summary>
        /// <param name="cloneProject"></param>
        private static void RegisterClone(Project cloneProject, int availableCloneSuffixId)
        {
            // Add clone identifier file.
            var identifierFile = Path.Combine(cloneProject.projectPath, CloneFileName);
            File.Create(identifierFile).Dispose();

            //Add argument file with default argument
            var argumentFilePath = Path.Combine(cloneProject.projectPath, ArgumentFileName);
            File.WriteAllText(argumentFilePath, DefaultArgument + "_" + availableCloneSuffixId, Encoding.UTF8);

            // Add collabignore.txt to stop the clone from messing with Unity Collaborate if it's enabled. Just in case.
            var collabignoreFile = Path.Combine(cloneProject.projectPath, "collabignore.txt");
            File.WriteAllText(collabignoreFile, "*"); // Make it ignore ALL files in the clone.
        }

        /// <summary>
        /// Opens a project located at the given path (if one exists).
        /// </summary>
        /// <param name="projectPath"></param>
        public static void OpenProject(string projectPath)
        {
            if (!Directory.Exists(projectPath))
            {
                Debug.LogError("Cannot open the project - provided folder (" + projectPath + ") does not exist.");
                return;
            }

            if (projectPath == GetCurrentProjectPath())
            {
                Debug.LogError("Cannot open the project - it is already open.");
                return;
            }

            var projectSettings = ParrelSyncProjectSettings.GetSerializedSettings();
            if (projectSettings.CopyPackagesFolders)
            {
                // Validate (and update if needed) the "Packages" folder before opening clone project to ensure the clone project will have the
                // same "compiling environment" as the original project
                ValidateCopiedFoldersIntegrity.ValidateFolder(projectPath, GetOriginalProjectPath(), "Packages");
            }

            var fileName = GetApplicationPath();
            var args = "-projectPath \"" + projectPath + "\"";
            Debug.Log("Opening project \"" + fileName + " " + args + "\"");
            StartHiddenConsoleProcess(fileName, args);
        }

        private static string GetApplicationPath()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    return EditorApplication.applicationPath;
                case RuntimePlatform.OSXEditor:
                    return EditorApplication.applicationPath + "/Contents/MacOS/Unity";
                case RuntimePlatform.LinuxEditor:
                    return EditorApplication.applicationPath;
                default:
                    throw new NotImplementedException("Platform has not supported yet ;(");
            }
        }

        /// <summary>
        /// Is this project being opened by an Unity editor?
        /// </summary>
        /// <param name="projectPath"></param>
        /// <returns></returns>
        public static bool IsCloneProjectRunning(string projectPath)
        {
            //Determine whether it is opened in another instance by checking the UnityLockFile
            var unityLockFilePath = new[] { projectPath, "Temp", "UnityLockfile" }.Aggregate(Path.Combine);
            var projectSettings = ParrelSyncProjectSettings.GetSerializedSettings();

            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    //Windows editor will lock "UnityLockfile" file when project is being opened.
                    //Sometime, for instance: windows editor crash, the "UnityLockfile" will not be deleted even the project
                    //isn't being opened, so a check to the "UnityLockfile" lock status may be necessary.
                    if (projectSettings.AlsoCheckUnityLockFileStaPref)
                    {
                        return File.Exists(unityLockFilePath) && FileUtilities.IsFileLocked(unityLockFilePath);
                    }

                    return File.Exists(unityLockFilePath);
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.LinuxEditor:
                    // Mac editor won't lock "UnityLockfile" file when project is being opened
                    return File.Exists(unityLockFilePath);
                default:
                    throw new NotImplementedException("IsCloneProjectRunning: Unsupported Platform: " + Application.platform);
            }
        }

        /// <summary>
        /// Deletes the clone of the currently open project, if such exists.
        /// </summary>
        public static void DeleteClone(string cloneProjectPath)
        {
            // Clone won't be able to delete itself.
            if (IsClone()
                || cloneProjectPath == string.Empty
                || cloneProjectPath == GetOriginalProjectPath())
            {
                return;
            }

            // Check what OS is
            string identifierFile;
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    Debug.Log("Attempting to delete folder \"" + cloneProjectPath + "\"");

                    //The argument file will be deleted first at the beginning of the project deletion process
                    //to prevent any further reading and writing to it(There's a File.Exist() check at the (file)editor windows.)
                    //If there's any file in the directory being write/read during the deletion process, the directory can't be fully removed.
                    identifierFile = Path.Combine(cloneProjectPath, ArgumentFileName);
                    File.Delete(identifierFile);

                    var args = "/c " + @"rmdir /s/q " + string.Format("\"{0}\"", cloneProjectPath);
                    StartHiddenConsoleProcess("cmd.exe", args);

                    break;
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.LinuxEditor:
                    Debug.Log("Attempting to delete folder \"" + cloneProjectPath + "\"");

                    //The argument file will be deleted first at the beginning of the project deletion process
                    //to prevent any further reading and writing to it(There's a File.Exist() check at the (file)editor windows.)
                    //If there's any file in the directory being write/read during the deletion process, the directory can't be fully removed.
                    identifierFile = Path.Combine(cloneProjectPath, ArgumentFileName);
                    File.Delete(identifierFile);

                    FileUtil.DeleteFileOrDirectory(cloneProjectPath);

                    break;
                default:
                    Debug.LogWarning("Not in a known editor. Where are you!?");
                    break;
            }
        }

        public static void SyncPackages(string cloneProjectPath)
        {
            if (string.IsNullOrEmpty(cloneProjectPath))
            {
                return;
            }

            var sourceProjectPath = GetOriginalProjectPath();
            if (cloneProjectPath == sourceProjectPath)
            {
                return;
            }

            var sourceProject = new Project(sourceProjectPath);
            var cloneProject = new Project(cloneProjectPath);

            FileUtil.ReplaceDirectory(sourceProject.packagesPath, cloneProject.packagesPath);
            Debug.Log("Package Folder Synced (" + sourceProject.packagesPath + " => " + cloneProject.packagesPath + ")");
        }

        #endregion

        #region Creating project folders

        /// <summary>
        /// Creates an empty folder using data in the given Project object
        /// </summary>
        /// <param name="project"></param>
        public static void CreateProjectFolder(Project project)
        {
            var path = project.projectPath;
            Debug.Log("Creating new empty folder at: " + path);
            Directory.CreateDirectory(path);
        }

        #endregion

        #region Creating symlinks

        private static void CreateLink(string sourcePath, string destinationPath)
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    var cmd = "/C mklink /J " + string.Format("\"{0}\" \"{1}\"", destinationPath, sourcePath);
                    Debug.Log("Windows junction: " + cmd);
                    StartHiddenConsoleProcess("cmd.exe", cmd);
                    break;
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.LinuxEditor:
                    sourcePath = sourcePath.Replace(" ", "\\ ");
                    destinationPath = destinationPath.Replace(" ", "\\ ");
                    var command = string.Format("ln -s {0} {1}", sourcePath, destinationPath);

                    Debug.Log($"Symlink: {command}");

                    ExecuteBashCommand(command);
                    break;
                default:
                    Debug.LogWarning("Not in a known editor. Application.platform: " + Application.platform);
                    break;
            }
        }

        //TODO(?) avoid terminal calls and use proper api stuff. See below for windows!
        ////https://docs.microsoft.com/en-us/windows/desktop/api/ioapiset/nf-ioapiset-deviceiocontrol
        //[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        //private static extern bool DeviceIoControl(System.IntPtr hDevice, uint dwIoControlCode,
        //	System.IntPtr InBuffer, int nInBufferSize,
        //	System.IntPtr OutBuffer, int nOutBufferSize,
        //	out int pBytesReturned, System.IntPtr lpOverlapped);

        /// <summary>
        /// Create a link / junction from the original project to it's clone.
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destinationPath"></param>
        private static void LinkFolders(string sourcePath, string destinationPath)
        {
            if (!Directory.Exists(sourcePath))
            {
                return;
            }

            if (Directory.Exists(destinationPath))
            {
                Debug.LogWarning("Skipping Asset link, it already exists: " + destinationPath);
                return;
            }

            CreateLink(sourcePath, destinationPath);
        }

        #endregion

        #region Utility methods

        private static bool? _isCloneFileExistCache;

        /// <summary>
        /// Returns true if the project currently open in Unity Editor is a clone.
        /// </summary>
        /// <returns></returns>
        public static bool IsClone()
        {
            if (_isCloneFileExistCache == null)
            {
                // The project is a clone if its root directory contains an empty file named ".clone".
                var cloneFilePath = Path.Combine(GetCurrentProjectPath(), CloneFileName);
                _isCloneFileExistCache = File.Exists(cloneFilePath);
            }

            return (bool)_isCloneFileExistCache;
        }

        /// <summary>
        /// Get the path to the current unityEditor project folder's info
        /// </summary>
        /// <returns></returns>
        public static string GetCurrentProjectPath()
            => Path.GetDirectoryName(Application.dataPath);

        /// <summary>
        /// Return a project object that describes all the paths we need to clone it.
        /// </summary>
        /// <returns></returns>
        public static Project GetCurrentProject()
            => new Project(GetCurrentProjectPath());

        /// <summary>
        /// Get the argument of this clone project.
        /// If this is the original project, will return an empty string.
        /// </summary>
        /// <returns></returns>
        public static string GetArgument()
        {
            if (!IsClone())
            {
                return string.Empty;
            }

            var argumentFilePath = Path.Combine(GetCurrentProjectPath(), ArgumentFileName);

            return File.Exists(argumentFilePath)
                ? File.ReadAllText(argumentFilePath, Encoding.UTF8)
                : string.Empty;
        }

        /// <summary>
        /// Returns the path to the original project.
        /// If currently open project is the original, returns its own path.
        /// If the original project folder cannot be found, retuns an empty string.
        /// </summary>
        /// <returns></returns>
        public static string GetOriginalProjectPath()
        {
            if (!IsClone())
            {
                // If this is the original, we return its own path.
                return GetCurrentProjectPath();
            }

            // If this is a clone...
            // Original project path can be deduced by removing the suffix from the clone's path.
            var cloneProjectPath = GetCurrentProject().projectPath;

            var index = cloneProjectPath.LastIndexOf(CloneNameSuffix, StringComparison.Ordinal);
            if (index > 0)
            {
                var originalProjectPath = cloneProjectPath.Substring(0, index);
                if (Directory.Exists(originalProjectPath))
                {
                    return originalProjectPath;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns all clone projects path.
        /// </summary>
        /// <returns></returns>
        public static List<string> GetCloneProjectsPath()
        {
            var projectsPath = new List<string>(MaxCloneProjectCount);
            for (var i = 0; i < MaxCloneProjectCount; i++)
            {
                var originalProjectPath = GetCurrentProject().projectPath;
                var cloneProjectPath = originalProjectPath + CloneNameSuffix + "_" + i;

                if (Directory.Exists(cloneProjectPath))
                {
                    projectsPath.Add(cloneProjectPath);
                }
            }

            return projectsPath;
        }

        /// <summary>
        /// Copies directory located at sourcePath to destinationPath. Displays a progress bar.
        /// </summary>
        /// <param name="sourcePath">Directory to be copied.</param>
        /// <param name="destinationPath">Destination directory (created automatically if needed).</param>
        /// <param name="progressBarPrefix">Optional string added to the beginning of the progress bar window header.</param>
        private static void CopyDirectoryWithProgressBar(string sourcePath, string destinationPath, string progressBarPrefix = "")
        {
            var source = new DirectoryInfo(sourcePath);
            var destination = new DirectoryInfo(destinationPath);

            long totalBytes = 0;
            long copiedBytes = 0;

            CopyDirectoryWithProgressBarRecursive(
                source, destination, ref totalBytes, ref copiedBytes,
                progressBarPrefix);

            EditorUtility.ClearProgressBar();
        }

        /// <summary>
        /// Copies directory located at sourcePath to destinationPath. Displays a progress bar.
        /// Same as the previous method, but uses recursion to copy all nested folders as well.
        /// </summary>
        /// <param name="source">Directory to be copied.</param>
        /// <param name="destination">Destination directory (created automatically if needed).</param>
        /// <param name="totalBytes">Total bytes to be copied. Calculated automatically, initialize at 0.</param>
        /// <param name="copiedBytes">To track already copied bytes. Calculated automatically, initialize at 0.</param>
        /// <param name="progressBarPrefix">Optional string added to the beginning of the progress bar window header.</param>
        private static void CopyDirectoryWithProgressBarRecursive(DirectoryInfo source, DirectoryInfo destination,
            ref long totalBytes, ref long copiedBytes, string progressBarPrefix = "")
        {
            // Directory cannot be copied into itself.
            if (string.Equals(source.FullName, destination.FullName, StringComparison.CurrentCultureIgnoreCase))
            {
                Debug.LogError("Cannot copy directory into itself.");
                return;
            }

            // Calculate total bytes, if required.
            if (totalBytes == 0)
            {
                totalBytes = GetDirectorySize(source, true, progressBarPrefix);
            }

            // Create destination directory, if required.
            if (!Directory.Exists(destination.FullName))
            {
                Directory.CreateDirectory(destination.FullName);
            }

            var ignoredFolders = new HashSet<string>
            {
                "BuildReports",
                "Demo",
                "Examples",
                "Samples",
                "Tests",
            };

            var ignoredLibraryFolders = new HashSet<string>
            {
                "Artifacts",
                "Bee",
                "BuildPlayerData",
                "BurstCache",
                "PlayerDataCache",
                "Search",
                "ShaderCache",
                "SplashScreenCache",
                "StateCache",
                "TempArtifacts",
                "VP",
            };

            var ignoredFiles = new HashSet<string>
            {
                ".DS_Store",
                "Thumbs.db",
                "Desktop.ini",
                "Samples.meta",
                "Examples.meta",
                "Tests.meta",
            };

            var ignoredExtensions = new HashSet<string>
            {
                ".md",
                ".log",
                ".pdf",
                ".backup",
                ".pid",
                ".zip",
                ".tar",
                ".tar.gz",
                ".csproj",
                ".unitypackage",
                ".buildreport",
            };

            // Copy all files from the source.
            foreach (var file in source.GetFiles())
            {
                // Ensure file exists before continuing.
                var fileName = file.Name;
                if (!file.Exists
                    || ignoredFiles.Contains(fileName)
                    || ignoredExtensions.Contains(file.Extension))
                {
                    continue;
                }

                var ignoredExtension = false;
                foreach (var extension in ignoredExtensions)
                {
                    if (file.FullName.EndsWith($"{extension}.meta", StringComparison.OrdinalIgnoreCase))
                    {
                        ignoredExtension = true;
                        break;
                    }
                }

                if (ignoredExtension)
                {
                    continue;
                }

                try
                {
                    file.CopyTo(Path.Combine(destination.ToString(), fileName), true);
                }
                catch (IOException)
                {
                    // Some files may throw IOException if they are currently open in Unity editor.
                    // Just ignore them in such case.
                }

                // Account the copied file size.
                copiedBytes += file.Length;

                // Display the progress bar.
                var progress = copiedBytes / (float)totalBytes;
                var cancelCopy = EditorUtility.DisplayCancelableProgressBar(
                    progressBarPrefix + "Copying '" + source.FullName + "' to '" + destination.FullName + "'...",
                    "(" + (progress * 100f).ToString("F2") + "%) Copying file '" + fileName + "'...",
                    progress);

                if (cancelCopy)
                {
                    return;
                }
            }

            // TODO sync addressables build folder

            var projectPath = GetCurrentProjectPath();
            // Copy all nested directories from the source.
            foreach (var sourceNestedDir in source.GetDirectories())
            {
                var folderName = sourceNestedDir.Name;
                if (folderName.EndsWith('~')
                    || ignoredFolders.Contains(folderName)
                    || (sourceNestedDir.Parent is { Name: "Library" }
                        && sourceNestedDir.Parent.FullName == $"{projectPath}/Library"
                        && ignoredLibraryFolders.Contains(folderName)))
                {
                    continue;
                }

                var nextDestinationNestedDir = destination.CreateSubdirectory(folderName);
                CopyDirectoryWithProgressBarRecursive(
                    sourceNestedDir, nextDestinationNestedDir,
                    ref totalBytes, ref copiedBytes, progressBarPrefix);
            }
        }

        /// <summary>
        /// Calculates the size of the given directory. Displays a progress bar.
        /// </summary>
        /// <param name="directory">Directory, which size has to be calculated.</param>
        /// <param name="includeNested">If true, size will include all nested directories.</param>
        /// <param name="progressBarPrefix">Optional string added to the beginning of the progress bar window header.</param>
        /// <returns>Size of the directory in bytes.</returns>
        private static long GetDirectorySize(DirectoryInfo directory, bool includeNested = false,
            string progressBarPrefix = "")
        {
            EditorUtility.DisplayProgressBar(
                progressBarPrefix + "Calculating size of directories...",
                "Scanning '" + directory.FullName + "'...", 0f);

            // Calculate size of all files in directory.
            var filesSize = directory.GetFiles().Sum(file => file.Exists ? file.Length : 0);

            // Calculate size of all nested directories.
            long directoriesSize = 0;
            if (includeNested)
            {
                var nestedDirectories = directory.GetDirectories();
                foreach (var nestedDir in nestedDirectories)
                {
                    directoriesSize += GetDirectorySize(nestedDir, true, progressBarPrefix);
                }
            }

            return filesSize + directoriesSize;
        }

        /// <summary>
        /// Starts process in the system console, taking the given fileName and args.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="args"></param>
        private static void StartHiddenConsoleProcess(string fileName, string args)
            => Process.Start(fileName, args);

        /// <summary>
        /// Thanks to https://github.com/karl-/unity-symlink-utility/blob/master/SymlinkUtility.cs
        /// </summary>
        /// <param name="command"></param>
        private static void ExecuteBashCommand(string command)
        {
            command = command.Replace("\"", "\"\"");

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"" + command + "\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            using (proc)
            {
                proc.Start();
                proc.WaitForExit();

                if (!proc.StandardError.EndOfStream)
                {
                    Debug.LogError(proc.StandardError.ReadToEnd());
                }
            }
        }

        public static void OpenProjectInFileExplorer(string path)
        {
            Process.Start(@path);
        }

        #endregion
    }
}
