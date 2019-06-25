// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Common.Utilities
{
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Logger;
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// File System IO Utilities
    /// </summary>
    public class FileSystemUtility : NetSdkUtilTask
    {
        #region const
        const int TEMP_DIR_COUNT = 1000;
        #endregion

        #region fields

        #endregion

        #region Properties

        #endregion

        #region Constructor
        public FileSystemUtility() { }
        #endregion

        #region Public Functions

        /// <summary>
        /// Given a directory path, traverses one directory
        /// </summary>
        /// <param name="directoryNameToken"></param>
        /// <returns></returns>
        public string TraverseUptoRootWithDirToken(string directoryTokenToFind)
        {
            return TraverseUptoRootWithDirToken(directoryTokenToFind, string.Empty);
        }

        /// <summary>
        /// Starts at a location and traverses to root depending upon the token it's searching for
        /// </summary>
        /// <param name="directoryTokenToFind"></param>
        /// <param name="startingDir"></param>
        /// <returns></returns>
        public string TraverseUptoRootWithDirToken(string directoryTokenToFind, string startingDir)
        {
            string srcRootDir = string.Empty;
            string seedDirPath = string.Empty;

            if (!string.IsNullOrWhiteSpace(startingDir))
            {
                if (Directory.Exists(startingDir))
                {
                    seedDirPath = startingDir;
                }
            }

            if (string.IsNullOrWhiteSpace(directoryTokenToFind))
            {
                directoryTokenToFind = ".git";
            }

            if (string.IsNullOrWhiteSpace(seedDirPath))
            {
                seedDirPath = Directory.GetCurrentDirectory();
            }

            if (!Directory.Exists(seedDirPath))
            {
                seedDirPath = Path.GetDirectoryName(this.GetType().GetTypeInfo().Assembly.Location);
            }

            string dirRoot = Directory.GetDirectoryRoot(seedDirPath);

            var tokenDirs = Directory.EnumerateDirectories(seedDirPath, directoryTokenToFind, SearchOption.TopDirectoryOnly);

            while (seedDirPath != dirRoot)
            {
                if (tokenDirs.Any<string>())
                {
                    srcRootDir = Path.GetDirectoryName(tokenDirs.First<string>());
                    break;
                }

                seedDirPath = Directory.GetParent(seedDirPath).FullName;
                tokenDirs = Directory.EnumerateDirectories(seedDirPath, directoryTokenToFind, SearchOption.TopDirectoryOnly);
            }

            return srcRootDir;
        }

        /// <summary>
        /// Starts at the given location traverses to root of directory depending upon the token its earching for
        /// </summary>
        /// <param name="fileTokenToFind"></param>
        /// <param name="startingDir"></param>
        /// <returns></returns>
        public string TraverseUptoRootWithFileToken(string fileTokenToFind, string startingDir)
        {
            string srcRootDir = string.Empty;
            string seedDirPath = string.Empty;

            if (!string.IsNullOrWhiteSpace(startingDir))
            {
                if (Directory.Exists(startingDir))
                {
                    seedDirPath = startingDir;
                }
            }

            if (string.IsNullOrWhiteSpace(fileTokenToFind))
            {
                fileTokenToFind = "build.proj";
            }

            if (string.IsNullOrWhiteSpace(seedDirPath))
            {
                seedDirPath = Directory.GetCurrentDirectory();
            }

            if (!Directory.Exists(seedDirPath))
            {
                seedDirPath = Path.GetDirectoryName(this.GetType().GetTypeInfo().Assembly.Location);
            }

            string dirRoot = Directory.GetDirectoryRoot(seedDirPath);

            var buildProjFile = Directory.EnumerateFiles(seedDirPath, fileTokenToFind, SearchOption.TopDirectoryOnly);

            while (seedDirPath != dirRoot)
            {
                if (buildProjFile.Any<string>())
                {
                    srcRootDir = Path.GetDirectoryName(buildProjFile.First<string>());
                    break;
                }

                seedDirPath = Directory.GetParent(seedDirPath).FullName;
                buildProjFile = Directory.EnumerateFiles(seedDirPath, fileTokenToFind, SearchOption.TopDirectoryOnly);
            }

            return srcRootDir;
        }

        /// <summary>
        /// Traverses to root of directory depending upon the token it's searching for
        /// </summary>
        /// <param name="startingDir"></param>
        /// <param name="fileExtensionToFind"></param>
        /// <returns></returns>
        public string TraverUptoRootWithFileExtension(string startingDir, string fileExtensionToFind = ".sln")
        {
            string srcRootDir = string.Empty;
            string seedDirPath = string.Empty;
            string extPattern = string.Format("*{0}", fileExtensionToFind);


            if (!string.IsNullOrWhiteSpace(startingDir))
            {
                if (Directory.Exists(startingDir))
                {
                    seedDirPath = startingDir;
                }
            }

            if (string.IsNullOrWhiteSpace(seedDirPath))
            {
                seedDirPath = Directory.GetCurrentDirectory();
            }

            if (!Directory.Exists(seedDirPath))
            {
                seedDirPath = Path.GetDirectoryName(this.GetType().GetTypeInfo().Assembly.Location);
            }

            string dirRoot = Directory.GetDirectoryRoot(seedDirPath);

            var filesWithExt = Directory.EnumerateFiles(seedDirPath, extPattern, SearchOption.TopDirectoryOnly);

            while (seedDirPath != dirRoot)
            {
                if (filesWithExt.Any<string>())
                {
                    srcRootDir = Path.GetDirectoryName(filesWithExt.First<string>());
                    break;
                }

                seedDirPath = Directory.GetParent(seedDirPath).FullName;
                filesWithExt = Directory.EnumerateFiles(seedDirPath, extPattern, SearchOption.TopDirectoryOnly);
            }

            return srcRootDir;
        }


        public void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                UtilLogger.LogInfo("Creating directory '{0}'", destDirName);
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                UtilLogger.LogInfo("Copying: Source: '{0}', Desitination: '{1}'", file.FullName, temppath);
                file.CopyTo(temppath, overwrite: true);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }

        public string FindFilePath(string rootDirPathToSearchIn, string fileNameToSearch)
        {
            Check.DirectoryExists(rootDirPathToSearchIn);
            Check.NotEmptyNotNull(fileNameToSearch);
            string fileFound = string.Empty;


            var files = Directory.EnumerateFiles(rootDirPathToSearchIn, fileNameToSearch, SearchOption.AllDirectories);

            if (files.Any<string>())
            {
                fileFound = files.FirstOrDefault<string>();
            }

            return fileFound;
        }

        public string GetTempDirPath(string seedDirPath = "", string GIT_DIR_POSTFIX = "")
        {
            string newDir = "FSUtil";
            int tempDirCount = 0;
            string initialTempDirPath = string.Empty;
            if (!string.IsNullOrWhiteSpace(seedDirPath))
            {
                if (Directory.Exists(seedDirPath))
                {
                    initialTempDirPath = seedDirPath;
                }
            }

            if (string.IsNullOrWhiteSpace(initialTempDirPath))
            {
                initialTempDirPath = Path.GetTempPath();
                initialTempDirPath = Path.Combine(initialTempDirPath, newDir);
            }

            string tempFileName = Path.GetFileNameWithoutExtension(Path.GetTempFileName());
            tempFileName = string.Concat(tempFileName, GIT_DIR_POSTFIX);

            string tempDir = Path.Combine(initialTempDirPath, tempFileName);

            while (DirFileExists(tempDir) && tempDirCount < TEMP_DIR_COUNT)
            {
                tempFileName = string.Concat(Path.GetFileNameWithoutExtension(Path.GetTempFileName()), GIT_DIR_POSTFIX);
                tempDir = Path.Combine(Path.GetTempFileName(), tempFileName);
                tempDirCount++;
            }

            if (tempDirCount >= TEMP_DIR_COUNT)
            {
                ApplicationException appEx = new ApplicationException(string.Format("Cleanup temp directory. More than '{0}' directories detected with similar naming pattern: '{1}", TEMP_DIR_COUNT.ToString(), tempDir));
                UtilLogger.LogException(appEx);
            }

            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            return tempDir;
        }
        #endregion

        #region private functions
        private bool DirFileExists(string path)
        {
            bool dirExists = true;
            bool fileExists = true;
            bool dirFileExists = true;

            dirExists = Directory.Exists(path);
            fileExists = File.Exists(path);
            dirFileExists = (dirExists && fileExists);
            return dirFileExists;
        }
        #endregion
    }
}
