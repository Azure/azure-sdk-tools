// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.NetSdk.Build.Models
{
    using MS.Az.Mgmt.CI.BuildTasks.Common.Utilities;
    using MS.Az.Mgmt.CI.BuildTasks.Models;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// This is an internally used type that is created for every project we inspect
    /// There is no need to expose this type publicly
    /// </summary>
    internal class SdkProjectMetadata
    {
        #region const
        const string EXCLUDE_FROM_BUILD = @"ExcludeFromBuild";
        const string EXCLUDE_FROM_TEST = @"ExcludeFromTest";
        const string OUTPUTPATH = @"OutputPath";
        #endregion

        #region fields
        List<string> _sdkPkgRefList;
        #endregion

        #region Properties

        public string ProjectFilePath { get; private set; }
        public bool IsTargetFxInCompliance
        {
            get
            {
                return Fx.IsTargetFxMatch;
            }
        }

        public TargetFx Fx { get; set; }

        public SdkProjectType ProjectType { get; set; }

        public SdkProjectCategory ProjectCategory { get; set; }

        public bool ExcludeFromBuild { get; private set; }

        public bool ExcludeFromTest { get; private set; }

        public List<string> SdkPkgRefList
        {
            get
            {
                if(_sdkPkgRefList == null)
                {
                    _sdkPkgRefList = msbProject.GetSdkPkgReference();
                }

                return _sdkPkgRefList;
            }
        }

        public string OutputPath { get; set; }


        public string BaseLineSdkTargetFxMonikerString { get; private set; }
        public string BaseLineTestTargetFxMonikerString { get; private set; }

        #region private properties
        MsbuildProject msbProject { get; set; }
        
        #endregion

        #endregion

        #region Constructor
        SdkProjectMetadata()
        {
            ExcludeFromBuild = false;
            ExcludeFromTest = false;
        }

        public SdkProjectMetadata(string projectFilePath) : this(projectFilePath, string.Empty, string.Empty) { }

        public SdkProjectMetadata(string projectFilePath, string baselineSdkFxString, string baselineTestFxString) : this()
        {
            BaseLineSdkTargetFxMonikerString = baselineSdkFxString;
            BaseLineTestTargetFxMonikerString = baselineTestFxString;

            Check.FileExists(projectFilePath);
            ProjectFilePath = projectFilePath;
            msbProject = new MsbuildProject(projectFilePath);
            Init();
        }

        #endregion

        #region Public Functions
        void Init()
        {
            if (msbProject.IsProjectTestType)
            {
                ProjectType = SdkProjectType.Test;
                Fx = new TargetFx(ProjectFilePath, msbProject.GetTargetFxMoniker(), BaseLineTestTargetFxMonikerString, ProjectType, Convert.ToBoolean(msbProject.GetSkipBaselineTargetFxMatching()));
            }
            else if (msbProject.IsProjectSdkType)
            {
                ProjectType = SdkProjectType.Sdk;
                Fx = new TargetFx(ProjectFilePath, msbProject.GetTargetFxMoniker(), BaseLineSdkTargetFxMonikerString, ProjectType, Convert.ToBoolean(msbProject.GetSkipBaselineTargetFxMatching()));
            }
            else if (msbProject.IsSdkCommonCategory)
            {
                ProjectType = SdkProjectType.Sdk;
                Fx = new TargetFx(ProjectFilePath, msbProject.GetTargetFxMoniker(), BaseLineSdkTargetFxMonikerString, ProjectType, Convert.ToBoolean(msbProject.GetSkipBaselineTargetFxMatching()));
            }
            else
            {
                ProjectType = SdkProjectType.NotSupported;
                Fx = new TargetFx(ProjectFilePath, msbProject.GetTargetFxMoniker(), BaseLineSdkTargetFxMonikerString, ProjectType, Convert.ToBoolean(msbProject.GetSkipBaselineTargetFxMatching()));
            }

            // There is an edge case where data plane will be marked as 'other'
            // but as of now data plane will not be opting into these tools, if they do, this needs to be changed
            if (msbProject.IsMgmtProjectCategory)
                ProjectCategory = SdkProjectCategory.MgmtPlane;
            else if (msbProject.IsSdkCommonCategory)
                ProjectCategory = SdkProjectCategory.SdkCommon_Mgmt;
            else
                ProjectCategory = SdkProjectCategory.UnDetermined;


            string buildPropValue = msbProject.GetPropertyValue(EXCLUDE_FROM_BUILD);
            string testPropValue = msbProject.GetPropertyValue(EXCLUDE_FROM_TEST);

            if(bool.TryParse(buildPropValue, out bool parsedBuildPropValue))
            {
                ExcludeFromBuild = parsedBuildPropValue;
            }

            if (bool.TryParse(testPropValue, out bool parsedTestPropValue))
            {
                ExcludeFromTest = parsedTestPropValue;
            }

            OutputPath = msbProject.GetPropertyValue(OUTPUTPATH);
        }
        #endregion

        #region private functions
        #endregion
    }
}
