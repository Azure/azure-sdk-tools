// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Tasks
{
    using Microsoft.Build.Framework;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.ExecProcess;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class CleanNugetPackagesTask : NetSdkBuildTask
    {
        #region fields
        List<string> packagesToBeCleaned;
        //int testCount;
        #endregion

        #region properties
        public override string NetSdkTaskName => "CleanPackagesTask";

        [Required]
        public string[] PackageReferences { get; set; }

        public string[] RestoreCacheLocations { get; set; }

        //public string NupkgOutputDir { get; set; }

        public string PackageDirSearchPattern { get; set; }

        //public bool WhatIf { get; set; }
        #endregion

        #region constrcutor

        public CleanNugetPackagesTask()
        {
            packagesToBeCleaned = new List<string>();
            //testCount = 1;
        }
        #endregion

        #region public functions
        /// <summary>
        /// Deletes packages from known nuget cache location
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            base.Execute();

            if (WhatIf)
            {
                WhatIfAction();
            }
            else
            {
                packagesToBeCleaned?.AddRange(PackageReferences);

                TaskLogger.LogInfo(MessageImportance.Low, packagesToBeCleaned, "Pacakges to be deleted....");

                List<string> localCacheLocations = new NugetExec().GetRestoreCacheLocation();

                if (RestoreCacheLocations != null)
                {
                    localCacheLocations.AddRange(RestoreCacheLocations);
                }

                if (!string.IsNullOrEmpty(PackageDirSearchPattern))
                {
                    packagesToBeCleaned.Add(PackageDirSearchPattern);
                }

                if (localCacheLocations.Any<string>())
                {
                    Task[] delTsks = new Task[localCacheLocations.Count];
                    int tskCount = 0;

                    localCacheLocations.ForEach((cl) =>
                    {
                    //TaskLogger.LogDebugInfo("Checking {0}", cl);
                    delTsks[tskCount] = Task.Run(async () => await CleanRestoredPackagesAsync(cl));
                        tskCount++;
                    });

                    Task.WaitAll(delTsks);

                    TaskLogger.LogInfo("Cleaning of Packages completed.....");
                }

                //DeleteNupkgOutputDir();
            }
            return TaskLogger.TaskSucceededWithNoErrorsLogged;
        }

        protected override void WhatIfAction()
        {
            TaskLogger.LogWarning("WhatIf Action not implemented");
        }
        #endregion

        #region private functions
        private async Task CleanRestoredPackagesAsync(string cacheLocationDirPath)
        {
            if (Directory.Exists(cacheLocationDirPath))
            {
                TaskLogger.LogInfo("Checking {0}", cacheLocationDirPath);

                foreach (string pkgName in packagesToBeCleaned)
                {   
                    try
                    {
                        if (pkgName.Contains("*") || pkgName.Contains("?"))
                        {
                            var pkgSearchDirs = Directory.EnumerateDirectories(cacheLocationDirPath, pkgName, SearchOption.TopDirectoryOnly);

                            if (pkgSearchDirs.Any<string>())
                            {
                                TaskLogger.LogInfo("Found {0} package(s) under {1}", pkgSearchDirs.Count<string>().ToString(), cacheLocationDirPath);
                            }

                            foreach (string dirWithPkg in pkgSearchDirs)
                            {
                                await DeleteDirAsync(dirWithPkg);
                            }
                        }
                        else
                        {
                            await DeleteDirAsync(Path.Combine(cacheLocationDirPath, pkgName));
                        }
                    }
                    catch (Exception ex)
                    {
                        TaskLogger.LogInfo(ex.ToString());
                    }
                }
            }
        }

        private async Task DeleteDirAsync(string dirToBeDeletedFullPath)
        {
            if(WhatIf)
            {
                if (Directory.Exists(dirToBeDeletedFullPath))
                {
                    TaskLogger.LogInfo("** Would be deleted {0}", dirToBeDeletedFullPath);
                }
            }
            else
            {   
                if (Directory.Exists(dirToBeDeletedFullPath))
                {
                    TaskLogger.LogInfo("Cleaning {0}", dirToBeDeletedFullPath);
                    await Task.Run(() => Directory.Delete(dirToBeDeletedFullPath, true));
                }
            }
        }

        //private void DeleteNupkgOutputDir()
        //{
        //    if(Directory.Exists(NupkgOutputDir))
        //    {
        //        try
        //        {
        //            Directory.Delete(NupkgOutputDir, recursive: true);
        //        }
        //        catch(Exception ex)
        //        {
        //            TaskLogger.LogInfo(ex.ToString());
        //        }
                
        //    }
        //}

        #endregion
    }
}
