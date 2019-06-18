// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.


namespace MS.Az.Mgmt.CI.BuildTasks.Models
{
    using Microsoft.Build.Framework;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Utilities;
    using MS.Az.Mgmt.CI.BuildTasks.Utilities;
    using MS.Az.Mgmt.CI.Common.ExtensionMethods;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class TargetFx : NetSdkUtilTask
    {
        #region const
        const string BASELINE_SDK_FX_MONIKER = @"net452;net461;netstandard1.4;netstandard2.0";
        const string BASELINE_TEST_FX_MONIKER = @"netcoreapp2.0";
        const string SKIP_TARGETFX_MATCHING = "Microsoft.Azure.Services.AppAuthentication.csproj;Microsoft.Rest.ClientRuntime.Etw.csproj;Microsoft.Rest.ClientRuntime.Log4Net.csproj;Microsoft.Azure.Test.HttpRecorder.csproj;Microsoft.Rest.ClientRuntime.Azure.TestFramework.csproj";
        #endregion

        #region fields
        bool _isTargetFxMatch;

        string _fxTargetMonikerString;
        string _environmentSpecificTargetFxMonikerString;
        string _fxBaseLineMonikerString;
        List<string> _skipTargetFxMatchingList;
        #endregion

        #region Properties
        internal List<string> SkipTargetFxMatchingList
        {
            get
            {
                if(_skipTargetFxMatchingList == null)
                {
                    _skipTargetFxMatchingList = new List<string>();
                    string[] tokens = SKIP_TARGETFX_MATCHING.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    if(tokens.NotNullOrAny<string>())
                    {
                        _skipTargetFxMatchingList = tokens.ToList<string>();
                    }
                }

                return _skipTargetFxMatchingList;
            }
        }
        public string FxTargetMonikerString
        {
            get
            {
                if(string.IsNullOrWhiteSpace(_fxTargetMonikerString))
                {
                    if(TargetFxList.NotNullOrAny<string>())
                    {
                        _fxTargetMonikerString = string.Join(";", TargetFxList);
                    }
                }

                return _fxTargetMonikerString;
            }

            private set { _fxTargetMonikerString = value; }
        }

        public string EnvironmentSpecificTargetFxMonikerString
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_environmentSpecificTargetFxMonikerString))
                {
                    if (TargetFxList.NotNullOrAny<string>())
                    {
                        _environmentSpecificTargetFxMonikerString = string.Join(";", EnvironmentSpecificTargetFxList);
                    }
                }

                return _environmentSpecificTargetFxMonikerString;
            }

            private set { _environmentSpecificTargetFxMonikerString = value; }
        }

        public string FxBaselineMonikerString
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_fxBaseLineMonikerString))
                {
                    if (TargetFxList.NotNullOrAny<string>())
                    {
                        _fxBaseLineMonikerString = string.Join(";", BaselineTargetFxList);
                    }
                }

                return _fxBaseLineMonikerString;
            }

            private set { _fxBaseLineMonikerString = value; }
        }

        public List<string> TargetFxList { get; set; }

        public List<string> NotMatchingFxList { get; set; }

        public List<string> EnvironmentSpecificTargetFxList { get; set; }

        public bool IsTargetFxMatch
        {
            get
            {
                if (SkipBaselineTargetFxMatching == true)
                {
                    _isTargetFxMatch = true;
                }
                else
                { 
                    if (DetectEnv.IsRunningUnderNonWindows)
                    {
                        UtilLogger.LogInfo(MessageImportance.Low, "Non-Windows Platform Detected");
                        var commonFxList = TargetFxList.Intersect<string>(EnvironmentSpecificTargetFxList, StringComparer.OrdinalIgnoreCase);
                        if (commonFxList.NotNullOrAny<string>())
                        {
                            if (commonFxList.Count<string>().Equals(EnvironmentSpecificTargetFxList.Count))
                            {
                                _isTargetFxMatch = true;
                            }
                        }
                    }
                    else
                    {
                        UtilLogger.LogInfo(MessageImportance.Low, "Windows Platform Detected");
                        if (NotMatchingFxList.Count == 0)
                        {
                            _isTargetFxMatch = true;
                        }
                    }
                }

                return _isTargetFxMatch;
            }

            private set
            {
                _isTargetFxMatch = value;
            }
        }

        public bool IsBeingBuiltOnWindows
        {
            get
            {
                return DetectEnv.IsRunningUnderWindowsOS;
            }
        }

        public bool IsApplicableForCurrentPlatform
        {
            get
            {
                if (EnvironmentSpecificTargetFxList.Count == 0)
                    return false;
                else
                    return true;
            }
        }

        public bool SkipBaselineTargetFxMatching { get; set; }

        public string ProjectFile { get; set; }

        #region private properties
        SdkProjectType ProjectType { get; set; }
        List<string> BaselineTargetFxList { get; set; }
        #endregion

        #endregion

        #region Constructor
        //public TargetFx(string targetFxMoniker, SdkProjectType sdkProjectType = SdkProjectType.Sdk) : this(targetFxMoniker, string.Empty, sdkProjectType) { }

        public TargetFx(string projectFilePath, string targetFxMoniker, string baselineFxMoniker, SdkProjectType projectType) : this(projectFilePath, targetFxMoniker, baselineFxMoniker, projectType, skipBaselineTargetFxMatching: false) { }

        public TargetFx(string projectFilePath, string targetFxMoniker, string baselineFxMoniker, SdkProjectType projectType, bool skipBaselineTargetFxMatching)
        {
            Check.NotEmptyNotNull(projectFilePath);
            Check.NotEmptyNotNull(targetFxMoniker);
            ProjectFile = projectFilePath;
            FxTargetMonikerString = targetFxMoniker;
            ProjectType = projectType;
            SkipBaselineTargetFxMatching = skipBaselineTargetFxMatching;

            if (string.IsNullOrWhiteSpace(baselineFxMoniker))
            {
                if (ProjectType == SdkProjectType.Test)
                {
                    FxBaselineMonikerString = BASELINE_TEST_FX_MONIKER;
                }
                else
                {
                    FxBaselineMonikerString = BASELINE_SDK_FX_MONIKER;
                }
            }
            else
            {
                FxBaselineMonikerString = baselineFxMoniker;
            }

            UtilLogger.LogInfo(MessageImportance.Low, "Processing Project '{0}'", ProjectFile);
            UtilLogger.LogInfo(MessageImportance.Low, "Baseline Fx List '{0}'", FxBaselineMonikerString);
            UtilLogger.LogInfo(MessageImportance.Low, "Target Fx List '{0}'", FxTargetMonikerString);

            Init();
        }

        void Init()
        {
            BaselineTargetFxList = GetFxList(FxBaselineMonikerString);
            TargetFxList = GetFxList(FxTargetMonikerString);
            UtilLogger.LogInfo(MessageImportance.Low, "Target Fx list '{0}' for '{1}' ProjectType. Expected baseline Fx list '{2}'", FxTargetMonikerString, ProjectType.ToString(), FxBaselineMonikerString);

            EnvironmentSpecificTargetFxList = GetPlatformSpecificApplicableFxList();
            NotMatchingFxList = GetNotMatchingFxList(BaselineTargetFxList, EnvironmentSpecificTargetFxList);
        }
        #endregion

        #region Public Functions

        #endregion

        #region private functions       

        /// <summary>
        /// Get applicable target fx list specific to the underlying platform
        /// </summary>
        /// <returns></returns>
        List<string> GetPlatformSpecificApplicableFxList()
        {
            List<string> envTargetFxList = new List<string>();
            if (DetectEnv.IsRunningUnderNonWindows)
            {
                UtilLogger.LogInfo(MessageImportance.Low, "Non-Windows Platform Detected");
                var applicableFx = TargetFxList.Where<string>((item) => !item.StartsWith("net4", StringComparison.OrdinalIgnoreCase));
                if (applicableFx.NotNullOrAny<string>())
                {
                    envTargetFxList.AddRange(applicableFx);
                }
            }
            else
            {
                UtilLogger.LogInfo(MessageImportance.Low, "Windows Platform Detected");
                envTargetFxList.AddRange(TargetFxList);
            }

            UtilLogger.LogInfo(MessageImportance.Low, envTargetFxList, "Platform specific Fx list");

            return envTargetFxList;
        }

        List<string> GetNotMatchingFxList(List<string> baseLineFxList, List<string> targetFxList)
        {
            List<string> notMatchingFx = new List<string>();
            IEnumerable<string> misMatch = null;
            IsTargetFxMatch = true;

            if (baseLineFxList.Count > targetFxList.Count)
            {
                misMatch = baseLineFxList.Except<string>(targetFxList, new ObjectComparer<string>((lhs, rhs) => lhs.Equals(rhs, StringComparison.OrdinalIgnoreCase)));
                if(misMatch.NotNullOrAny<string>())
                {
                    IsTargetFxMatch = false;
                    notMatchingFx = misMatch.ToList<string>();
                    UtilLogger.LogInfo("Target Fx that do not match the baseline", notMatchingFx);
                }
            }
            else if(targetFxList.Count > baseLineFxList.Count)
            {
                misMatch = targetFxList.Except<string>(baseLineFxList, new ObjectComparer<string>((lhs, rhs) => lhs.Equals(rhs, StringComparison.OrdinalIgnoreCase)));
                if (misMatch.NotNullOrAny<string>())
                {
                    IsTargetFxMatch = false;
                    notMatchingFx = misMatch.ToList<string>();
                    UtilLogger.LogInfo("Target Fx that do not match the baseline", notMatchingFx);
                }
            }
            else if(targetFxList.Count == targetFxList.Count)
            {
                misMatch = targetFxList.Except<string>(baseLineFxList, new ObjectComparer<string>((lhs, rhs) => lhs.Equals(rhs, StringComparison.OrdinalIgnoreCase)));
                if (misMatch.NotNullOrAny<string>())
                {
                    IsTargetFxMatch = false;
                    notMatchingFx = misMatch.ToList<string>();
                    UtilLogger.LogInfo("Target Fx that do not match the baseline", notMatchingFx);
                }
            }

            return notMatchingFx;
        }

        List<string> GetFxList(string fxMonikerString)
        {
            List<string> fxList = new List<string>();
            var fxTokens = fxMonikerString.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            if (fxTokens.Any<string>())
            {
                fxList = fxTokens.ToList<string>();
                //UtilLogger.LogInfo("Fx Moniker string:'{0}'", fxMonikerString);
            }
            else
            {
                UtilLogger.LogWarning("No Target Framework found");
            }

            return fxList;
        }
        #endregion
    }

    enum FxMoniker
    {
        net45,
        net452,
        net46,
        net461,
        net462,
        net472,
        netcoreapp11,
        netcoreapp20,
        netstandard13,
        netstandard14,
        netstandard16,
        netstandard20,
        UnSupported
    }

    enum FxMonikerCategory
    {
        FullDesktop,
        NetCore
    }

    public enum SdkProjectType
    {
        All,
        Sdk,
        Test,
        NotSupported,
        UnDetermined
    }

    public enum SdkProjectCategory
    {
        All,
        MgmtPlane,
        DataPlane,
        SdkCommon_Mgmt,  //These are common projects used for management plane projects
        Test,
        NotSupported,
        UnDetermined
    }
}
