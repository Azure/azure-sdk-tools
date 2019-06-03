// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Common.Utilities
{
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Logger;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// File System IO Utilities
    /// </summary>
    public class FileSystemUtility : NetSdkUtilTask //NetSdkUtilBase<NetSdkBuildTaskLogger>
    {
        public FileSystemUtility() { }
        //public FileSystemUtility(NetSdkBuildTaskLogger log) : base(log) { }

        /// <summary>
        /// Given a directory path, traverses one directory
        /// </summary>
        /// <param name="directoryNameToken"></param>
        /// <returns></returns>
        public string TraverseUptoRootWithDirToken(string directoryTokenToFind)
        {
            return TraverseUptoRootWithDirToken(directoryTokenToFind, string.Empty);
        }
        
        public string TraverseUptoRootWithDirToken(string directoryTokenToFind, string startingDir)
        {
            string srcRootDir = string.Empty;
            string seedDirPath = string.Empty;

            if(!string.IsNullOrWhiteSpace(startingDir))
            {
                if(Directory.Exists(startingDir))
                {
                    seedDirPath = startingDir;
                }
            }

            if(string.IsNullOrWhiteSpace(directoryTokenToFind))
            {
                directoryTokenToFind = ".git";
            }

            if(string.IsNullOrWhiteSpace(seedDirPath))
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

            if(string.IsNullOrWhiteSpace(fileTokenToFind))
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
            //List<FileInfo> filteredFiles = files
            foreach (FileInfo file in files)
            {
                //if (file.Name.ToLower().EndsWith("nupkg") ||
                //    file.Name.ToLower().EndsWith("nupkg") ||
                //    file.Name.ToLower().EndsWith("nupkg"))
                //{

                //}
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

            if(files.Any<string>())
            {
                fileFound = files.FirstOrDefault<string>();
            }

            return fileFound;
        }
    }
}
