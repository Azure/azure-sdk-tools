// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.BuildTasks.PreBuild
{
    using Microsoft.Build.Framework;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.ExtensionMethods;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Utilities;
    using MS.Az.Mgmt.CI.Common.ExtensionMethods;
    using MS.Az.Mgmt.CI.Common.Services;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    public class UpdateNetSdkInfoTask : NetSdkBuildTask
    {
        #region const
        public const string API_TAG_PROPERTYNAME = "AzureApiTag";
        public const string PROPS_APITAG_FILE_NAME = "AzSdk.RP.props";
        public const string PROPS_MULTIAPITAG_FILE_NAME = "AzSdk.MultiApi.RP.props";
        public const string APIMAPTYPENAMETOSEARCH = "SdkInfo";
        public const string PROPERTYNAMEPREFIX = "ApiInfo_";
        #endregion

        #region fields
        //SdkForNetRepoDirectoryStructure _netSdkDirStructure;
        string _apiMapTag;
        #endregion

        #region Properties        

        #region Input Properties
        
        /// <summary>
        /// This will be ArtifactsBinDir path in the current scheme of things
        /// </summary>
        [Required]
        public string GeneratedAssemblyDirPath { get; set; }

        //[Required]
        public string GeneratedAssemblyName { get; set; }
        [Required]
        public ITaskItem[] SdkProjectFilePaths { get; set; }
        #endregion

        #region other properties
        List<string> SdkProjectFilePathList { get; set; }

        string SdkInfoBinaryName
        {
            get
            {
                return string.Format("{0}-SdkInfo.dll", "RPName");
            }
        }

        bool IsSdkRPPropsFileExists
        {
            get
            {
                bool fileExists = false;
                //string sdkRpPropFile = NetSdkDirStructure.FindFile("AzSdk.RP.props");
                string sdkRpPropFile = string.Empty;
                if (!string.IsNullOrWhiteSpace(sdkRpPropFile))
                {
                    if (File.Exists(sdkRpPropFile))
                    {
                        fileExists = true;
                    }
                }

                return fileExists;
            }
        }

        public string ApiMapTag
        {
            get
            {
                if(string.IsNullOrWhiteSpace(_apiMapTag))
                {
                    _apiMapTag = GetApiMapTag();
                }

                return _apiMapTag;
            }

            private set
            {
                _apiMapTag = value;
            }
        }

        public override string NetSdkTaskName => "UpdateNetSdkInfoTask";
        #endregion
        #endregion

        #region Constructor
        public UpdateNetSdkInfoTask()
        {
            SdkProjectFilePathList = new List<string>();
        }
        public UpdateNetSdkInfoTask(ITaskItem[] sdkProjectFilePaths) : this()
        {
            Init();
        }

        void Init()
        {
            if (SdkProjectFilePaths != null)
            {
                if (SdkProjectFilePaths.Length > 0)
                {
                    SdkProjectFilePathList = SdkProjectFilePaths.Select<ITaskItem, string>((item) => item.ItemSpec).ToList<string>();
                }
            }
        }

        #endregion

        #region Public Functions

        public bool ExecuteTask()
        {
            UpdateApiTag();

            return TaskLogger.TaskSucceededWithNoErrorsLogged;
        }

        public void UpdateApiTag()
        {
            string apiTag = GetApiMapTag();
            if(string.IsNullOrWhiteSpace(apiTag))
            {
                TaskLogger.LogWarning("Unable to update ApiTag");
            }
            else
            {
                UpdateSdkRpPropsFile(apiTag);
            }
        }
        #endregion

        #region private functions
        private string GetApiMapTag()
        {
            List<string> sdkInfoFileToBeBuilt = new List<string>();
            string apiMapTag = string.Empty;

            //string dirToSearch = Path.Combine(GitWorkingDir, RPRelativePath);
            string dirToSearch = string.Empty;
            if(Directory.Exists(dirToSearch))
            {
                var sdkInfoFiles = Directory.EnumerateFiles(dirToSearch, "SdkInfo*.cs", SearchOption.AllDirectories);

                if (sdkInfoFiles.Any<string>())
                {
                    foreach (string sdkFile in sdkInfoFiles)
                    {
                        if (sdkFile.Contains("management", StringComparison.OrdinalIgnoreCase))
                        {
                            sdkInfoFileToBeBuilt.Add(sdkFile);
                            break;
                        }
                    }
                }
            }
            else
            {
                TaskLogger.LogException<DirectoryNotFoundException>("'{0}' directory for searching SDKInfo does not exists", dirToSearch);
            }

            if(sdkInfoFileToBeBuilt.Any<string>())
            {
                string sdkInfoBinaryPath = BuildBinary(sdkInfoFileToBeBuilt, SdkInfoBinaryName);
                if (!File.Exists(sdkInfoBinaryPath))
                {
                    TaskLogger.LogWarning("Unable to build SdkInfo binary to retrieve API Version.");
                }

                apiMapTag = GetApiFromSdkInfo(sdkInfoBinaryPath);

                //update ApiMapTag
                ApiMapTag = apiMapTag;
            }

            return apiMapTag;

        }

        private string GetApiMapTag(string rpRelativePath)
        {

            //List<string> findFiles = NetSdkDirStructure.FindFiles(RPName, "SdkInfo*.cs");
            List<string> findFiles = new List<string>();
            if (!findFiles.Any<string>())
            {
                TaskLogger.LogException<ApplicationException>("Unable to find SdkInfo after SDK generation. Exiting.....");
            }

            string sdkInfoBinaryPath = BuildBinary(findFiles, SdkInfoBinaryName);
            if (!File.Exists(sdkInfoBinaryPath))
            {
                TaskLogger.LogException<ApplicationException>("Unable to build SdkInfo binary to retrieve API Version. Exiting....");
            }

            string apiMapTag = GetApiFromSdkInfo(sdkInfoBinaryPath);

            //update ApiMapTag
            ApiMapTag = apiMapTag;

            return apiMapTag;
        }


        private void UpdateSdkRpPropsFile(string newApiTag)
        {
            string apiPropsFile = string.Empty;
            if (IsSdkRPPropsFileExists)
            {
                TaskLogger.LogInfo("Updating Api Tag");
                //apiPropsFile = NetSdkDirStructure.FindFile(PROPS_APITAG_FILE_NAME);
                //MsBuildProj.MsBuildFilePath = apiPropsFile;
                //MsBuildProj.UpdatePropertyValue(API_TAG_PROPERTYNAME, newApiTag);
            }
            else
            {
                TaskLogger.LogInfo("Creating SdK API props file");
                CreateSdkRpPropsFile();
                //apiPropsFile = NetSdkDirStructure.FindFile(PROPS_APITAG_FILE_NAME);
                //MsBuildProj.MsBuildFilePath = apiPropsFile;
                TaskLogger.LogInfo("Updating Api Tag");
                //MsBuildProj.UpdatePropertyValue(API_TAG_PROPERTYNAME, newApiTag);
            }
        }

        private void CreateSdkRpPropsFile()
        {
            string sdkRpPropsFilePath = string.Empty;
            //string slnFile = NetSdkDirStructure.FindSlnFilePath();
            string slnfile = string.Empty;
            //if(File.Exists(slnFile))
            //{
            //    string slnDir = Path.GetDirectoryName(slnFile);
            //    sdkRpPropsFilePath = Path.Combine(slnDir, PROPS_APITAG_FILE_NAME);

            //    //MsBuildFile msbuildPropsFile = new MsBuildFile(sdkRpPropsFilePath);
            //    sdkRpPropsFilePath = msbuildPropsFile.CreateXmlDocWithProps();
            //}
        }

        #region ApiMap
        /// <summary>
        /// Finds SDKInfo
        /// Builds assembly, extracts api information
        /// Finally constructs api version tag string
        /// </summary>
        

        /// <summary>
        /// Extracts api from the built assembly for sdkinfo
        /// Iterates on api version, normalizes and creates api tag string
        /// </summary>
        /// <param name="binaryPath"></param>
        /// <returns></returns>
        private string GetApiFromSdkInfo(string binaryPath)
        {
            ReflectionService refSvc = new ReflectionService(binaryPath);
            List<PropertyInfo> props = refSvc.GetPropertiesContainingName(PROPERTYNAMEPREFIX);
            List<Tuple<string, string, string>> combinedApiMap = new List<Tuple<string, string, string>>();
            StringBuilder sb = new StringBuilder();

            foreach (PropertyInfo pInfo in props)
            {
                IEnumerable<Tuple<string, string, string>> apiMap = (IEnumerable<Tuple<string, string, string>>)pInfo.GetValue(null, null);
                if (apiMap.Any())
                {
                    combinedApiMap.AddRange(apiMap);
                }
            }

            Dictionary<string, string> na = new Dictionary<string, string>(new ObjectComparer<string>((l, r) => l.Equals(r, StringComparison.OrdinalIgnoreCase)));

            foreach (var api in combinedApiMap)
            {
                string nsApi = string.Format("{0}_{1}", api.Item1, api.Item3);
                if (!na.ContainsKey(nsApi))
                {
                    na.Add(nsApi, nsApi);
                    sb.Append(string.Format("{0};", nsApi));
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Builds assembly from SDKInfo*.cs
        /// </summary>
        /// <param name="filesToBuild"></param>
        /// <param name="binaryToBeBuiltFullPath"></param>
        /// <returns></returns>
        private string BuildBinary(List<string> filesToBuild, string binaryToBeBuiltFullPath)
        {
            //CSCExec csc = new CSCExec();
            //csc.CsFileList = filesToBuild;
            //csc.DllName = binaryToBeBuiltFullPath;
            //csc.ExecuteCommand();           

            //return csc.GeneratedAssemblyFullPath;

            return "";
        }
        #endregion

        #endregion

    }
}
