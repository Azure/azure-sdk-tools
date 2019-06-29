// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.BuildTasks.PreBuild
{
    using Microsoft.Build.Framework;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.ExtensionMethods;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Utilities;
    using MS.Az.Mgmt.CI.BuildTasks.Models;
    using MS.Az.Mgmt.CI.Common.ExtensionMethods;
    using MS.Az.Mgmt.CI.Common.Services;
    using System;
    using System.Collections.Generic;
    using System.Dynamic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    public class UpdateNetSdkInfoTask : NetSdkUtilTask
    {
        #region const
        public const string API_TAG_PROPERTYNAME = "AzureApiTag";
        public const string PROPS_APITAG_FILE_NAME = "AzSdk.RP.props";
        public const string PROPS_MULTIAPITAG_FILE_NAME = "AzSdk.MultiApi.RP.props";
        public const string APIMAPTYPENAMETOSEARCH = "SdkInfo";
        public const string PROPERTYNAMEPREFIX = "ApiInfo_";
        #endregion

        #region fields
        string _apiMapTag;
        FileSystemUtility _fileSysUtil;
        #endregion

        #region Properties        

        #region Input Properties

        [Required]
        public SDKMSBTaskItem[] SdkProjectFilePaths { get; set; }
        #endregion

        #region other properties

        //List<Tuple<string, string, string>> AssemblyInfoList { get; set; }
        //List<SDKMSBTaskItem> AssemblyInfoList { get; set; }
        List<ExpandoObject> AssemblyInfoList { get; set; }
        FileSystemUtility FileSysUtil
        {
            get
            {
                if (_fileSysUtil == null)
                {
                    _fileSysUtil = new FileSystemUtility();
                }

                return _fileSysUtil;
            }
        }

        bool AzPropFileExists { get; set; }
        bool AssemblyFilePathExists { get; set; }
        //public string ApiMapTag
        //{
        //    get
        //    {
        //        if(string.IsNullOrWhiteSpace(_apiMapTag))
        //        {
        //            _apiMapTag = GetApiMapTag();
        //        }

        //        return _apiMapTag;
        //    }

        //    private set
        //    {
        //        _apiMapTag = value;
        //    }
        //}

        public override string NetSdkTaskName => "UpdateNetSdkInfoTask";
        #endregion
        #endregion

        #region Constructor
        public UpdateNetSdkInfoTask()
        {
            //AssemblyInfoList = new List<Tuple<string, string, string>>();            
            //AssemblyInfoList = new List<SDKMSBTaskItem>();
            AssemblyInfoList = new List<ExpandoObject>();
            AzPropFileExists = true;
            AssemblyFilePathExists = true;
        }

        #endregion

        #region Public Functions

        public bool ExecuteTask()
        {
            ParseInput();
            UpdateApiTag();

            return UtilLogger.TaskSucceededWithNoErrorsLogged;
        }

        public void UpdateApiTag()
        {
            foreach(dynamic asmInfoItem in AssemblyInfoList)
            {
                string apiTag = GetApiFromSdkInfo(asmInfoItem.AssemblyFilePath);

                if(!File.Exists(asmInfoItem.AzPropFilePath))
                {
                    MsbuildProject buildProj = new MsbuildProject();
                    buildProj.CreateAzPropsfile(asmInfoItem.AzPropFilePath);
                }

                MsbuildProject msbProj = new MsbuildProject(asmInfoItem.AzPropFilePath);
                msbProj.AddUpdateProperty(API_TAG_PROPERTYNAME, apiTag);
            }
        }
        #endregion        

        private void ParseInput()
        {
            foreach(SDKMSBTaskItem sdkProjTaskItem in SdkProjectFilePaths)
            {
                string azPropFilePath = string.Empty;
                string asmPath = string.Empty;
                string projectName = Path.GetFileNameWithoutExtension(sdkProjTaskItem.ItemSpec);
                string dllName = string.Format("{0}.dll", projectName);

                //e.g. <root>\artifacts\bin\Microsoft.Azure.Management.Billing\Debug\
                string outputDirRootPath = sdkProjTaskItem.OutputPath;
                string azSdkPropDirPath = FileSysUtil.TraverseUptoRootWithFileToken(PROPS_APITAG_FILE_NAME, Path.GetDirectoryName(sdkProjTaskItem.ItemSpec));

                if(string.IsNullOrWhiteSpace(azSdkPropDirPath))
                {
                    azSdkPropDirPath = FileSysUtil.TraverUptoRootWithFileExtension(Path.GetDirectoryName(sdkProjTaskItem.ItemSpec));
                }
                
                if(Directory.Exists(azSdkPropDirPath))
                {
                    azPropFilePath = Path.Combine(azSdkPropDirPath, PROPS_APITAG_FILE_NAME);
                }

                var files = FileSysUtil.FindFilePaths(outputDirRootPath, dllName);

                if(files.NotNullOrAny<string>())
                {
                    var asms = files.Where<string>((item) => item.Contains("netstandard2.0", StringComparison.OrdinalIgnoreCase));

                    if(asms.NotNullOrAny<string>())
                    {
                        asmPath = asms.FirstOrDefault<string>();
                    }
                }

                if(!File.Exists(azPropFilePath))
                {
                    AzPropFileExists = false;
                }

                if (!File.Exists(asmPath))
                {
                    AssemblyFilePathExists = false;
                    UtilLogger.LogWarning("'{0}' does not exist. Build project '{1}'", asmPath, sdkProjTaskItem.ItemSpec);
                }

                if(File.Exists(sdkProjTaskItem.ItemSpec) &&
                    //AzPropFileExists == true &&
                    AssemblyFilePathExists == true)
                {
                    dynamic newObj = new ExpandoObject();
                    //dynamic newObj = new SDKMSBTaskItem(sdkProjTaskItem);
                    newObj.AzPropFilePath = azPropFilePath;
                    newObj.AssemblyFilePath = asmPath;
                    newObj.ProjectFilePath = sdkProjTaskItem.ItemSpec;

                    AssemblyInfoList.Add(newObj);

                    //Tuple<string, string, string> asmTuple = new Tuple<string, string, string>(sdkProjTaskItem.ItemSpec, azPropFilePath, asmPath);
                    //AssemblyInfoList.Add(asmTuple);
                }
            }
        }

        //private string GetApiMapTag()
        //{
        //    List<string> sdkInfoFileToBeBuilt = new List<string>();
        //    string apiMapTag = string.Empty;

        //    //string dirToSearch = Path.Combine(GitWorkingDir, RPRelativePath);
        //    string dirToSearch = string.Empty;
        //    if(Directory.Exists(dirToSearch))
        //    {
        //        var sdkInfoFiles = Directory.EnumerateFiles(dirToSearch, "SdkInfo*.cs", SearchOption.AllDirectories);

        //        if (sdkInfoFiles.Any<string>())
        //        {
        //            foreach (string sdkFile in sdkInfoFiles)
        //            {
        //                if (sdkFile.Contains("management", StringComparison.OrdinalIgnoreCase))
        //                {
        //                    sdkInfoFileToBeBuilt.Add(sdkFile);
        //                    break;
        //                }
        //            }
        //        }
        //    }
        //    else
        //    {
        //        UtilLogger.LogException<DirectoryNotFoundException>("'{0}' directory for searching SDKInfo does not exists", dirToSearch);
        //    }

        //    if(sdkInfoFileToBeBuilt.Any<string>())
        //    {
        //        string sdkInfoBinaryPath = BuildBinary(sdkInfoFileToBeBuilt, SdkInfoBinaryName);
        //        if (!File.Exists(sdkInfoBinaryPath))
        //        {
        //            UtilLogger.LogWarning("Unable to build SdkInfo binary to retrieve API Version.");
        //        }

        //        apiMapTag = GetApiFromSdkInfo(sdkInfoBinaryPath);

        //        //update ApiMapTag
        //        ApiMapTag = apiMapTag;
        //    }

        //    return apiMapTag;

        //}

        //private string GetApiMapTag(string rpRelativePath)
        //{

        //    //List<string> findFiles = NetSdkDirStructure.FindFiles(RPName, "SdkInfo*.cs");
        //    List<string> findFiles = new List<string>();
        //    if (!findFiles.Any<string>())
        //    {
        //        UtilLogger.LogException<ApplicationException>("Unable to find SdkInfo after SDK generation. Exiting.....");
        //    }

        //    string sdkInfoBinaryPath = BuildBinary(findFiles, SdkInfoBinaryName);
        //    if (!File.Exists(sdkInfoBinaryPath))
        //    {
        //        UtilLogger.LogException<ApplicationException>("Unable to build SdkInfo binary to retrieve API Version. Exiting....");
        //    }

        //    string apiMapTag = GetApiFromSdkInfo(sdkInfoBinaryPath);

        //    //update ApiMapTag
        //    ApiMapTag = apiMapTag;

        //    return apiMapTag;
        //}


        //private void UpdateSdkRpPropsFile(string newApiTag)
        //{
        //    string apiPropsFile = string.Empty;
        //    if (AzPropFileExists)
        //    {
        //        UtilLogger.LogInfo("Updating Api Tag");
        //        //apiPropsFile = NetSdkDirStructure.FindFile(PROPS_APITAG_FILE_NAME);
        //        //MsBuildProj.MsBuildFilePath = apiPropsFile;
        //        //MsBuildProj.UpdatePropertyValue(API_TAG_PROPERTYNAME, newApiTag);
        //    }
        //    else
        //    {
        //        UtilLogger.LogInfo("Creating SdK API props file");
        //        //CreateSdkRpPropsFile();
        //        //apiPropsFile = NetSdkDirStructure.FindFile(PROPS_APITAG_FILE_NAME);
        //        //MsBuildProj.MsBuildFilePath = apiPropsFile;
        //        UtilLogger.LogInfo("Updating Api Tag");
        //        //MsBuildProj.UpdatePropertyValue(API_TAG_PROPERTYNAME, newApiTag);
        //    }
        //}

        //private void CreateSdkRpPropsFile(string azPropFilePath)
        //{
        //    string sdkRpPropsFilePath = string.Empty;
        //    //string slnFile = NetSdkDirStructure.FindSlnFilePath();
        //    string slnfile = string.Empty;
        //    //if(File.Exists(slnFile))
        //    //{
        //    //    string slnDir = Path.GetDirectoryName(slnFile);
        //    //    sdkRpPropsFilePath = Path.Combine(slnDir, PROPS_APITAG_FILE_NAME);

        //    //    //MsBuildFile msbuildPropsFile = new MsBuildFile(sdkRpPropsFilePath);
        //    //    sdkRpPropsFilePath = msbuildPropsFile.CreateXmlDocWithProps();
        //    //}
        //}

        #region ApiMap
        /// <summary>
        /// Extracts api from the built assembly for sdkinfo
        /// Iterates on api version, normalizes and creates api tag string
        /// </summary>
        /// <param name="binaryPath"></param>
        /// <returns></returns>
        private string GetApiFromSdkInfo(string binaryPath)
        {
            ReflectionService refSvc = new ReflectionService(binaryPath, false);
            List<PropertyInfo> props = refSvc.GetProperties(APIMAPTYPENAMETOSEARCH, PROPERTYNAMEPREFIX);
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
        #endregion

    }
}
