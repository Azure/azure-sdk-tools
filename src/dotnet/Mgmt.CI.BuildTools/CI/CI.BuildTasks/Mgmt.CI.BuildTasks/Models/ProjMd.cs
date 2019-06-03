// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

/*
namespace MS.Az.Mgmt.CI.BuildTasks.Models
{
    using Microsoft.Build.Framework;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Utilities;
    using MS.Az.Mgmt.CI.Common.Services;
    using NuGet.Versioning;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using MS.Az.Mgmt.CI.BuildTasks.Common.ExtensionMethods;
    using System.IO;

    public class ProjMd : NetSdkUtilTask
    {
        #region const
        const string prop_NugPkgVersion = "Version";
        const string prop_NugPkgId = "Id";
        const string prop_TargetFx = "TargetFrameworks";
        #endregion

        #region fields

        #endregion

        #region Properties
        #region Public Properties

        public string ProjectFilePath { get; set; }
        NuGetVersion NugetPackageVersion { get; set; }

        SdkNugetPackageSvc SdkNugetPackage { get; set; }

        TargetFx ProjTargetFx { get; set; }

        SdkProjectType ProjectType { get; set; }

        SdkProjectCategory ProjectCategory { get; set; }

        List<string> ProjectReferences { get; set; }
        #endregion



        #region private properties
        MsbuildProject BuildProj { get; set; }


        #endregion
        #endregion

        #region Constructor
        public ProjMd(ITaskItem projTaskItem) : this(projTaskItem.ItemSpec)
        {

        }

        public ProjMd(string projectFilePath)
        {
            Check.NotEmptyNotNull(projectFilePath);
            Check.FileExists(projectFilePath);

            ProjectFilePath = projectFilePath;

            BuildProj = new MsbuildProject(ProjectFilePath);

            string nugPkgVer = BuildProj.GetPropertyValue(prop_NugPkgVersion);
            string nugPkgName = BuildProj.GetPropertyValue(prop_NugPkgId);
            string targetFxString = BuildProj.GetPropertyValue(prop_NugPkgId);
            
            if(string.IsNullOrWhiteSpace(targetFxString))
            {
                targetFxString = BuildProj.GetPropertyValue("TargetFramework");
            }

            ProjTargetFx = new TargetFx(targetFxString);
            SdkNugetPackage = new SdkNugetPackageSvc(nugPkgName, nugPkgVer);

        }
        #endregion

        #region Public Functions

        #endregion

        #region private functions
        /// <summary>
        /// We detect project type depending upon package references
        /// </summary>
        /// <returns></returns>
        SdkProjectType GetProjectType()
        {
            SdkProjectType projType = SdkProjectType.UnSupported;
            if(DetectTestProjectType())
            {
                projType = SdkProjectType.Test;
            }
            else if(GetProjectCategory() != SdkProjectCategory.UnDetermined)
            {
                projType = SdkProjectType.Sdk;
            }

            return projType;
        }

        SdkProjectCategory GetProjectCategory()
        {
            SdkProjectCategory projCategory = SdkProjectCategory.UnDetermined;
            if (DetectMgmtSdkProjectType())
            {
                projCategory = SdkProjectCategory.MgmtPlane;
            }
            else if (DetectDataPlaneSdkProjectCategory())
            {
                projCategory = SdkProjectCategory.DataPlane;
            }
            else if(DetectSdkCommonProjectCategory())
            {
                projCategory = SdkProjectCategory.SdkCommon_Mgmt;
            }

            return projCategory;
        }


        bool DetectTestProjectType()
        {
            bool testProjName = false;
            bool xunitRef = false;

            string projectFileName = Path.GetFileNameWithoutExtension(ProjectFilePath);

            if (!projectFileName.EndsWith(".Test", StringComparison.OrdinalIgnoreCase))
            {
                if (projectFileName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase))
                {
                    testProjName = true;
                }
            }
            else
            {
                testProjName = true;
            }

            List<string> pkgRefList = BuildProj.GetNugetPackageReferences();
            if (pkgRefList.Contains("xunit"))
            {
                xunitRef = true;
            }

            return (testProjName && xunitRef);
        }

        bool DetectMgmtSdkProjectType()
        {
            bool sdkProjName = false;
            bool CRRef = false;

            string projectFileName = Path.GetFileNameWithoutExtension(ProjectFilePath);

            if (projectFileName.StartsWith("Microsoft.Azure.Management", StringComparison.OrdinalIgnoreCase))
            {
                sdkProjName = true;
            }

            //List<string> pkgRefList = BuildProj.GetNugetPackageReferences();
            List<string> pkgRefList = BuildProj.PackageReferenceList;
            if (pkgRefList.Contains("Microsoft.Rest.ClientRuntime"))
            {
                CRRef = true;
            }

            return (sdkProjName && CRRef);
        }

        bool DetectDataPlaneSdkProjectCategory()
        {
            bool dataPlaneProj = false;

            string projectFileName = Path.GetFileNameWithoutExtension(ProjectFilePath);

            if (!projectFileName.StartsWith("Microsoft.Azure.Management", StringComparison.OrdinalIgnoreCase))
            {
                dataPlaneProj = true;
            }

            return dataPlaneProj;
        }

        bool DetectSdkCommonProjectCategory()
        {
            if(ProjectFilePath.Contains("sdkcommon", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion
    }
}
*/