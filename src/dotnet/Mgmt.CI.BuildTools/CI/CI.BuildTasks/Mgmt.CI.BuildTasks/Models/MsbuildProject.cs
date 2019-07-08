// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Models
{
    using Microsoft.Build.Evaluation;
    using Microsoft.Build.Framework;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.ExtensionMethods;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Utilities;
    using NuGet.Versioning;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Xml;

    public class MsbuildProject : NetSdkUtilTask
    {
        #region const
        const string ITEMGROUP_PKGREF = "PackageReference";
        const string ITEMGROUP_PROJREF = "ProjectReference";
        #endregion

        #region fields
        //bool _hasXunitPackageReference;
        List<string> _packageReferenceList;
        string _targetFxMoniker;
        //SdkProjectType _sdkProjType;
        //SdkProjectCategory _sdkProjCategory;

        #endregion

        #region Properties

        /// <summary>
        /// Detects if it's a test project
        /// </summary>
        public bool IsProjectTestType
        {
            get
            {
                bool ifXunit = DoesParticularPkgRefExists("xunit");
                bool ifTestFileName = false;
                string projDirPath = Path.GetDirectoryName(ProjectFilePath);

                if (ProjectFileName.Contains("test", StringComparison.OrdinalIgnoreCase))
                {
                    if (projDirPath.Contains("test", StringComparison.OrdinalIgnoreCase))
                    {
                        ifTestFileName = true;
                    }
                }

                return (ifXunit && ifTestFileName);
            }
        }

        /// <summary>
        /// Detects if it's a SDK project
        /// </summary>
        public bool IsProjectSdkType
        {
            get
            {
                //There is an edge case where this might give false positive, any non-Sdk project that
                //has clientruntime as reference
                if (
                    (DoesParticularPkgRefExists("microsoft.rest.clientruntime"))
                    && (!IsProjectTestType)
                    && (!IsSdkCommonCategory)
                    )
                {
                    return true;
                }
                else
                {
                    return false;
                }   
            }
        }

        public bool IsMgmtProjectCategory
        {
            get
            {
                bool ismgmtCategory = false;
                if(IsProjectTestType)
                {
                    if (
                        (ProjectFileName.Contains("management", StringComparison.OrdinalIgnoreCase))
                        ||
                        (DoesParticularProjectReferenceContains("management"))
                       )
                    {
                        ismgmtCategory = true;
                    }
                }
                else if (ProjectFileName.Contains("management", StringComparison.OrdinalIgnoreCase))
                    ismgmtCategory = true;

                return ismgmtCategory;
            }
        }

        public bool IsSdkCommonCategory
        {
            get
            {
                if(ProjectFilePath.Contains("mgmtcommon", StringComparison.OrdinalIgnoreCase))
                    return true;
                else
                    return false;
            }
        }

        public List<string> PackageReferenceList
        {
            get
            {
                if(_packageReferenceList == null)
                {
                    _packageReferenceList = GetNugetPackageReferences();
                }

                return _packageReferenceList;
            }
        }

        public string TargetFxMoniker
        {
            get
            {
                if(string.IsNullOrWhiteSpace(_targetFxMoniker))
                {
                    _targetFxMoniker = GetTargetFxMoniker();
                }

                return _targetFxMoniker;
            }

            private set
            {
                _targetFxMoniker = value;
            }
        }
        Project LoadedProj { get; set; }
        string ProjectFilePath { get; set; }
        string ProjectFileName { get; set; }
        #endregion

        #region Constructor
        public MsbuildProject() { }

        public MsbuildProject(string projectFullPath) : this()
        {
            Check.FileExists(projectFullPath);
            ProjectFilePath = projectFullPath;
            ProjectFileName = Path.GetFileName(ProjectFilePath);
            Object lockObj = new object();
            lock (lockObj)
            {
                if (ProjectCollection.GlobalProjectCollection.GetLoadedProjects(projectFullPath).Count != 0)
                {
                    LoadedProj = ProjectCollection.GlobalProjectCollection.GetLoadedProjects(projectFullPath).FirstOrDefault<Project>();
                }
                else
                {
                    LoadedProj = new Project(projectFullPath);
                }
            }
            UtilLogger.LogInfo(MessageImportance.Low, "Loading Project '{0}'", projectFullPath);
        }

        #endregion

        #region Public Functions

        #region Get Properties
        public string GetPropertyName(string propertyName)
        {
            ProjectProperty prop = LoadedProj.GetProperty(propertyName);
            UtilLogger.LogInfo(MessageImportance.Low, "Retrieved Property - '{0}'", prop?.Name);

            if (prop == null)
                return string.Empty;
            else
                return prop.Name;
        }

        public string GetPropertyValue(string propertyName)
        {
            var propValue = LoadedProj.GetPropertyValue(propertyName);            
            UtilLogger.LogInfo(MessageImportance.Low, "'{0}' - '{1}'", propertyName, propValue);

            if (propValue == null)
                return string.Empty;
            else
                return propValue;
        }

        public List<string> GetMatchingProperties(string propertyName, StringComparison comparisonType)
        {
            Check.NotEmptyNotNull(propertyName);
            List<string> matchingProperties = null;

            ICollection<ProjectProperty> propCollection = LoadedProj.Properties;

            if(propCollection.Any<ProjectProperty>())
            {
                var matchProps = from p in propCollection where p.Name.Contains(propertyName, comparisonType) select p;

                if(matchProps != null)
                {
                    matchingProperties = new List<string>();
                    foreach (ProjectProperty pp in matchProps)
                    {
                        matchingProperties.Add(pp.Name);
                    }

                    UtilLogger.LogInfo(matchingProperties);
                }
            }
            else
            {
                UtilLogger.LogInfo(MessageImportance.Low, "Unable to find any properties by the name '{0}'", propertyName);
            }

            return matchingProperties;
        }

        public Dictionary<string, string> GetMatchingPropertyAndValues(string propertyName, StringComparison comparisonType)
        {
            Dictionary<string, string> propValue = null;
            List<string> matchProps = GetMatchingProperties(propertyName, comparisonType);

            if(matchProps.Any<string>())
            {
                propValue = new Dictionary<string, string>();

                foreach(string pName in matchProps)
                {
                    string pValue = GetPropertyValue(pName);
                    if(string.IsNullOrWhiteSpace(pValue))
                    {
                        propValue.Add(pName, string.Empty);
                    }
                    else
                    {
                        propValue.Add(pName, pValue);
                    }
                }

                UtilLogger.LogInfo(propValue, "Matching Property names and it's associated Values");
            }
            else
            {
                UtilLogger.LogInfo("Unable to find any properties by the name '{0}'", propertyName);
            }

            return propValue;
        }

        public string GetTargetFxMoniker()
        {
            string tfx = GetPropertyValue("TargetFramework");
            if(string.IsNullOrWhiteSpace(tfx))
            {
                tfx = GetPropertyValue("TargetFrameworks");
            }

            if(string.IsNullOrWhiteSpace(tfx))
            {
                UtilLogger.LogError("Missing Targetframework moniker");
            }
            else
            {
                UtilLogger.LogInfo(MessageImportance.Low, "Targetframewok retrieved as '{0}'", tfx);
            }

            return tfx;
        }

        public string GetSkipBaselineTargetFxMatching()
        {
            bool skipTargetFxMatching = false;
            string skipBaselineTargetFxMatchingValue = GetPropertyValue("SkipBaselineTargetFxMatching");

            if(!bool.TryParse(skipBaselineTargetFxMatchingValue, out skipTargetFxMatching))
            {
                skipTargetFxMatching = false;
            }

            return skipTargetFxMatching.ToString();
        }

        #endregion

        #region Get Items
        public List<string> GetSdkPkgReference()
        {
            //List<string> pkgRefList = GetNugetPackageReferences();
            List<string> pkgRefList = PackageReferenceList;

            var filtered = pkgRefList.Where<string>((item) =>
            {
                if((item.StartsWith("Microsoft.Rest", StringComparison.OrdinalIgnoreCase))
                ||
                (item.StartsWith("Microsoft.Azure.Management", StringComparison.OrdinalIgnoreCase))
                ||
                (item.StartsWith("Microsoft.Azure", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            });

            if(filtered.Any<string>())
            {
                pkgRefList = filtered.ToList<string>();
            }
            else
            {
                pkgRefList.Clear();
            }

            return pkgRefList;
        }

        public List<string> GetProjectReference()
        {
            List<string> projRefs = new List<string>();
            ICollection<ProjectItem> pRefs = LoadedProj.GetItemsIgnoringCondition(ITEMGROUP_PROJREF);

            if(pRefs.Any<ProjectItem>())
            {
                projRefs = pRefs.Select<ProjectItem, string>((item) => item.EvaluatedInclude).ToList<string>();
            }

            return projRefs;
        }

        public Dictionary<string, string> GetNugetPkgRefsAndVersionInfo(bool skipVersionInfo = false)
        {
            Dictionary<string, string> pkgRefVer = null;
            List<Tuple<string, string, string>> pkgRefTup = new List<Tuple<string, string, string>>();
            ICollection<ProjectItem> pkgRefItems = LoadedProj.GetItemsIgnoringCondition(ITEMGROUP_PKGREF);
            //ICollection<ProjectItem> pkgRefItems = LoadedProj.GetItemsByEvaluatedInclude(ITEMGROUP_PKGREF);
            //ICollection<ProjectItem> allItems = LoadedProj.ItemsIgnoringCondition;
            //ICollection<ProjectItem> pkgRefItems = allItems.Where<ProjectItem>((item) => item.ItemType.Equals(ITEMGROUP_PKGREF)).ToList<ProjectItem>();

            string piPkgRef = string.Empty;
            string pkgRefVerStr = string.Empty;

            if (pkgRefItems.Any<ProjectItem>())
            {
                pkgRefVer = new Dictionary<string, string>();
                foreach (ProjectItem pi in pkgRefItems)
                {
                    piPkgRef = pi.EvaluatedInclude;

                    if (!pkgRefVer.ContainsKey(piPkgRef))
                    {
                        if (skipVersionInfo == false)
                        {
                            ICollection<ProjectMetadata> mdCol = pi.Metadata;
                            foreach (ProjectMetadata pimd in mdCol)
                            {
                                if (pimd.Name.Equals("Version", StringComparison.OrdinalIgnoreCase))
                                {
                                    string verStr = pimd.EvaluatedValue;
                                    if (!string.IsNullOrWhiteSpace(verStr))
                                    {
                                        NuGetVersion ver = GetMinimumNuGetVersion(verStr);
                                        if (ver != null)
                                        {
                                            pkgRefVerStr = ver.ToString();
                                            pkgRefVer.Add(piPkgRef, pkgRefVerStr);
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            pkgRefVer.Add(piPkgRef, pkgRefVerStr);
                        }
                    }
                }
            }

            UtilLogger.LogInfo(MessageImportance.Low, pkgRefVer, "Retrieved PackageReferences and it's version");
            return pkgRefVer;
        }
        #endregion

        #region Add/Update Properties
        public void AddUpdateProperty(string propertyName, string newPropertyValue)
        {
            if(!string.IsNullOrWhiteSpace(newPropertyValue))
            {
                ProjectProperty prop = LoadedProj.GetProperty(propertyName);
                UtilLogger.LogInfo(MessageImportance.Low, "Processing Project '{0}'", this.ProjectFilePath);
                if (prop == null)
                {
                    UtilLogger.LogInfo(MessageImportance.Low, "'{0}' property not found. Adding/setting it's value to '{1}'", propertyName, newPropertyValue);
                    LoadedProj.SetProperty(propertyName, newPropertyValue);
                    LoadedProj.Save(this.ProjectFilePath);
                }
                else
                {
                    string currentValue = prop.EvaluatedValue;
                    if (string.IsNullOrWhiteSpace(currentValue))
                    {
                        UtilLogger.LogInfo(MessageImportance.Low, "Setting '{0}' value to '{1}'", propertyName, newPropertyValue);
                        LoadedProj.SetProperty(propertyName, newPropertyValue);
                        LoadedProj.Save(this.ProjectFilePath);
                    }
                    else
                    {
                        UtilLogger.LogInfo(MessageImportance.Low, "Current value of '{0}' is '{1}'", propertyName, newPropertyValue);
                        if (!currentValue.Equals(newPropertyValue, StringComparison.OrdinalIgnoreCase))
                        {
                            UtilLogger.LogInfo(MessageImportance.Low, "Setting '{0}' value to '{1}'", propertyName, newPropertyValue);
                            LoadedProj.SetProperty(propertyName, newPropertyValue);
                            LoadedProj.Save(this.ProjectFilePath);
                        }
                    }
                }
            }
        }
        #endregion

        public override void Dispose()
        {
            LoadedProj = null;
        }

        internal string CreateAzPropsfile(string fullFilePathToCreate)
        {
            if (!File.Exists(fullFilePathToCreate))
            {
                XmlDocument doc = new XmlDocument();
                XmlElement root = doc.DocumentElement;

                XmlComment comment = doc.CreateComment("This file and it's contents are updated at build time moving or editing might result in build failure. Take due deligence while editing this file");

                XmlElement projNode = doc.CreateElement("Project");
                projNode.SetAttribute("ToolsVersion", "15.0");
                projNode.SetAttribute("xmlns", "http://schemas.microsoft.com/developer/msbuild/2003");
                doc.AppendChild(projNode);

                projNode.AppendChild(comment);

                XmlElement propGroup = doc.CreateElement("PropertyGroup");
                projNode.AppendChild(propGroup);

                XmlElement apiTagProp = doc.CreateElement("AzureApiTag");
                propGroup.AppendChild(apiTagProp);

                XmlElement pkgTag = doc.CreateElement("PackageTags");
                XmlText pkgTagValue = doc.CreateTextNode("$(PackageTags);$(CommonTags);$(AzureApiTag);");
                pkgTag.AppendChild(pkgTagValue);

                propGroup.AppendChild(pkgTag);

                doc.Save(fullFilePathToCreate);
            }

            return fullFilePathToCreate;
        }

        #endregion

        #region private functions
        List<string> GetNugetPackageReferences()
        {
            List<string> pkgRefList = new List<string>();
            Dictionary<string, string> pkgRefVer = GetNugetPkgRefsAndVersionInfo(skipVersionInfo: true);

            if (pkgRefVer.Any<KeyValuePair<string, string>>())
            {
                pkgRefList = (from pi in pkgRefVer select pi.Key).ToList<string>();
            }

            return pkgRefList;
        }

        /// <summary>
        /// Does a wild search on the particular package name
        /// Uses string.Contains to search for provided packagename
        /// </summary>
        /// <param name="packageName"></param>
        /// <returns></returns>
        bool DoesParticularPkgRefExists(string packageName)
        {
            var pkgRefs = PackageReferenceList.Where<string>((item) => item.Contains(packageName, StringComparison.OrdinalIgnoreCase));
            if (pkgRefs.Any<string>())
                return true;
            else
                return false;
        }

        bool DoesParticularProjectReferenceContains(string projReferenceName)
        {
            var projRefs = GetProjectReference();
            var filtered = projRefs.Where<string>((item) => item.Contains(projReferenceName, StringComparison.OrdinalIgnoreCase));

            if (filtered.Any<string>())
                return true;
            else
                return false;
        }

        NuGetVersion GetMinimumNuGetVersion(string versionString)
        {
            //Check.NotEmptyNotNull(versionString);
            VersionRange verRange;
            NuGetVersion ver = null;
            if(VersionRange.TryParse(versionString, out verRange))
            {
                if(verRange.MinVersion != null)
                {
                    UtilLogger.LogInfo(MessageImportance.Low, "Minimum version detected '{0}'", verRange.MinVersion.ToString());
                    ver = verRange.MinVersion;
                }

                if (verRange.MaxVersion != null)
                {
                    UtilLogger.LogInfo(MessageImportance.Low, "Maximum version detected '{0}'", verRange.MaxVersion.ToString());
                }
            }
            else
            {
                UtilLogger.LogWarning("Unable to parse version string '{0}'", versionString);
            }

            return ver;
        }
        #endregion
    }
}