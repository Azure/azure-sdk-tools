// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
namespace MS.Az.Mgmt.CI.BuildTasks.Common.Models
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Extracting Version info from file name
    /// 
    /// TODO:
    /// Test and making sure -preview, -alpha, -pre works (this might need to adjust the regex pattern)
    /// </summary>
    public class NugetPackageFileName
    {
        #region CONST
        //Ms.Az.NetSdkBuildTools.Package.0.9.0.nupkg
        const string VERSION_PATTERN = @".*?(<major>\\d+).*?(<minor>\\d+).*?(<build>\\d+).*?(nupkg)";
        const string NUGET_FILE_EXT = ".nupkg";
        #endregion

        #region Fields
        string _fileNameWithoutVersion;
        #endregion
        public string FileName { get; set; }    
        
        public string FullFilePath { get; set; }

        public string FileExtension { get; set; }

        public string FileNameWithoutVersion
        {
            get
            {
                string withoutVersion = string.Empty;
                if (string.IsNullOrWhiteSpace(_fileNameWithoutVersion))
                {
                    Regex rg = new Regex(VERSION_PATTERN);
                    Match m = rg.Match(FileName);

                    if(m.Success)
                    {
                        GroupCollection gc = m.Groups;
                        VersionString = string.Format("{0}.{1}.{2}", gc["major"], gc["minor"], gc["build"]);
                        VersionNumber = new Version(VersionString);
                    }

                    withoutVersion = FileName.Replace(VersionString, string.Empty);
                }

                return withoutVersion;
            }
        }

        public string VersionString { get; set; }

        public Version VersionNumber { get; set; }

        public NugetPackageFileName(string directoryPath, string fileName)
        {
        }

        public NugetPackageFileName(string fullFilePath)
        {
            Init(null, fullFilePath);
        }

        void Init(string directoryPath, string nupkgFileName)
        {
            DateTime latestTimeStamp = new DateTime(1899, 12, 30);
            DateTime currentItemTimeStamp;
            string latestVersionFile = string.Empty;
            string fileMatchPattern = string.Empty;
            string flName = string.Empty;
            _fileNameWithoutVersion = string.Empty;

            flName = Path.GetFileName(nupkgFileName);

            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                directoryPath = Path.GetDirectoryName(nupkgFileName);
                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    directoryPath = Environment.CurrentDirectory;
                }
            }

            if (!flName.Contains(NUGET_FILE_EXT))
            {
                fileMatchPattern = string.Concat(flName, "*", NUGET_FILE_EXT);
            }
            else
            {
                fileMatchPattern = nupkgFileName;
            }

            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                IEnumerable<string> files = Directory.EnumerateFiles(directoryPath, fileMatchPattern, SearchOption.TopDirectoryOnly);

                foreach(string file in files)
                {
                    currentItemTimeStamp = File.GetLastWriteTime(file);
                    if(currentItemTimeStamp > latestTimeStamp)
                    {
                        latestTimeStamp = currentItemTimeStamp;
                        latestVersionFile = file;
                    }
                }
            }

            FullFilePath = Path.GetFullPath(latestVersionFile);
            FileName = Path.GetFileName(FullFilePath);
            FileExtension = Path.GetFileNameWithoutExtension(FileName);
        }
    }
}
