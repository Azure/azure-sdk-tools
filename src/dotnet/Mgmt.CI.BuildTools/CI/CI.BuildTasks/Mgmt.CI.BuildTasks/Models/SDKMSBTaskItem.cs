// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Models
{
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using MS.Az.Mgmt.CI.Common.ExtensionMethods;
    using MS.Az.NetSdk.Build.Models;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;


    /// <summary>
    /// This type is what will be used in the build system
    /// The purpose of this type is expose required meta data that will be used throught the build system
    /// </summary>
    public class SDKMSBTaskItem : ITaskItem
    {
        #region const
        //META DATA STRINGS

        const string TARGET_FX = "TargetFx";
        const string PLATFORM_SPECIFIC_TARGET_FX = "PlatformSpecificFx";
        const string PROJECT_TYPE = "ProjectType";
        const string PROJECT_CATEGORY = "ProjectCategory";
        const string PKG_REF_LIST = "PkgRefList";
        const string OUTPUT_PATH = "OutputPath";

        #endregion

        #region fields
        Dictionary<string, string> _metaDataCollection;
        //List<string> _mdNames;
        #endregion

        #region Properties

        #region custom properties
        public SdkProjectType SdkProjType { get; private set; }

        public SdkProjectCategory SdkProjCategory { get; private set; }

        public string TargetFxMonikerString { get; private set; }

        public string PlatformSpecificTargetFxMonikerString { get; internal set; }

        public List<string> PackageRefList { get; set; }

        public string OutputPath { get; set; }
        #endregion

        public string ItemSpec { get; set; }

        SdkProjectMetadata InternalSdkProjMD { get; set; }

        public ICollection MetadataNames
        {
            get
            {
                List<string> mdNames = new List<string>();
                if(_metaDataCollection.NotNullOrAny<KeyValuePair<string, string>>())
                {
                   mdNames = _metaDataCollection.Select<KeyValuePair<string, string>, string>((item) => item.Key).ToList<string>();
                }

                return mdNames;
            }
        }

        public int MetadataCount => _metaDataCollection.Count;

        #endregion

        #region Constructor
        SDKMSBTaskItem()
        {
            _metaDataCollection = new Dictionary<string, string>();
        }
        internal SDKMSBTaskItem(string itemSpecFullPath) : this()
        {
            InternalSdkProjMD = new SdkProjectMetadata(itemSpecFullPath);
            Init();
        }

        internal SDKMSBTaskItem(SdkProjectMetadata sdkProjMetadata) : this()
        {
            InternalSdkProjMD = sdkProjMetadata;
            Init();
        }



        internal SDKMSBTaskItem(SDKMSBTaskItem ti) : this()
        {
            SdkProjCategory = ti.SdkProjCategory;
            SdkProjType = ti.SdkProjType;
            TargetFxMonikerString = ti.TargetFxMonikerString;
            PlatformSpecificTargetFxMonikerString = ti.PlatformSpecificTargetFxMonikerString;
            PackageRefList = ti.PackageRefList;
            ItemSpec = ti.ItemSpec;
            OutputPath = ti.OutputPath;
            InternalSdkProjMD = ti.InternalSdkProjMD;
            //_metaDataCollection = ti._metaDataCollection;
        }

        public void UpdateMetadata()
        {
            this.SetMetadata(TARGET_FX, TargetFxMonikerString);
            this.SetMetadata(PLATFORM_SPECIFIC_TARGET_FX, PlatformSpecificTargetFxMonikerString);
            this.SetMetadata(PROJECT_TYPE, SdkProjType.ToString());
            this.SetMetadata(PROJECT_CATEGORY, SdkProjCategory.ToString());
            this.SetMetadata(OUTPUT_PATH, OutputPath);

            if (PackageRefList.Any<string>())
            {
                string pkgStr = string.Join(";", PackageRefList);
                this.SetMetadata(PKG_REF_LIST, pkgStr);
            }
        }

        void Init()
        {
            //_metaDataCollection = new Dictionary<string, string>();

            ItemSpec = InternalSdkProjMD.ProjectFilePath;

            //TargetFx
            if (!string.IsNullOrWhiteSpace(InternalSdkProjMD.Fx.FxTargetMonikerString))
            {
                TargetFxMonikerString = InternalSdkProjMD.Fx.FxTargetMonikerString;
                this.SetMetadata(TARGET_FX, TargetFxMonikerString);

            }

            //Platform specific Fx
            if (!string.IsNullOrWhiteSpace(InternalSdkProjMD.Fx.EnvironmentSpecificTargetFxMonikerString))
            {
                PlatformSpecificTargetFxMonikerString = InternalSdkProjMD.Fx.EnvironmentSpecificTargetFxMonikerString;
                this.SetMetadata(PLATFORM_SPECIFIC_TARGET_FX, PlatformSpecificTargetFxMonikerString);
            }

            // Project Type
            this.SetMetadata(PROJECT_TYPE, InternalSdkProjMD.ProjectType.ToString());

            // Project Category
            this.SetMetadata(PROJECT_CATEGORY, InternalSdkProjMD.ProjectCategory.ToString());

            // Package Reference list
            PackageRefList = InternalSdkProjMD.SdkPkgRefList;

            // Output Path
            OutputPath = InternalSdkProjMD.OutputPath;
            this.SetMetadata(OUTPUT_PATH, OutputPath);

            if (PackageRefList.Any<string>())
            {
                string pkgStr = string.Join(";", PackageRefList);
                this.SetMetadata(PKG_REF_LIST, pkgStr);
            }
        }
        #endregion

        #region Public Functions
        public void SetMetadata(string metadataName, string metadataValue)
        {
            string mdVal = string.Empty;
            if (!string.IsNullOrWhiteSpace(metadataValue))
            {
                mdVal = metadataValue;
            }

            if (!string.IsNullOrWhiteSpace(metadataName))
            {
                if (_metaDataCollection.ContainsKey(metadataName))
                {
                    _metaDataCollection[metadataName] = metadataValue;
                }
                else
                {
                    _metaDataCollection.Add(metadataName, metadataValue);
                }
            }
        }

        public void RemoveMetadata(string metadataName)
        {
            if(!string.IsNullOrWhiteSpace(metadataName))
            {
                if(_metaDataCollection.ContainsKey(metadataName))
                {
                    _metaDataCollection.Remove(metadataName);
                }
            }
        }

        public string GetMetadata(string metadataName)
        {
            string mdVal = string.Empty;
            if (!string.IsNullOrWhiteSpace(metadataName))
            {
                if (_metaDataCollection.ContainsKey(metadataName))
                {
                    mdVal = _metaDataCollection[metadataName];
                }
            }

            return mdVal;
        }

        public IDictionary CloneCustomMetadata()
        {
            return _metaDataCollection.ToDictionary(entry => entry.Key,
                                                    entry => entry.Value);
        }

        public void CopyMetadataTo(ITaskItem destinationItem)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region private functions

        #endregion
    }
}
