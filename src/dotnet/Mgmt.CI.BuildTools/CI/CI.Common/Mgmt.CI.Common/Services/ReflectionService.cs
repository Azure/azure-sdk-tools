// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.Common.Services
{
    using Microsoft.Azure.KeyVault;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading.Tasks;
    public class ReflectionService : NetSdkUtilTask
    {
        #region const

        #endregion

        #region fields
        Assembly _asmToReflect;
        #endregion

        #region Properties
        Assembly AsmToReflect
        {
            get
            {
                if (_asmToReflect == null)
                {
                    byte[] asmData = File.ReadAllBytes(AsmFilePath);
                    if (asmData != null)
                    {
                        _asmToReflect = Assembly.Load(asmData);
                    }
                }

                return _asmToReflect;
            }

            set { _asmToReflect = value; }
        }

        string AsmFilePath { get; set; }
        #endregion

        #region Constructor
        public ReflectionService(string AssemblyFilePath)
        {
            AsmFilePath = AssemblyFilePath;
        }
        #endregion

        #region Public Functions

        public void MetadataLoad()
        {
            //System.Reflection.MetadataLoadContext 
            //MetadataAssemblyResolver foo = new 
            //MetadataLoadContext mdlc = new MetadataLoadContext()
        }

        public List<PropertyInfo> GetPropertiesContainingName(string propertyName)
        {
            List<PropertyInfo> propertyList = new List<PropertyInfo>();
            Type[] availableTypes = AsmToReflect.GetTypes();

            foreach (Type t in availableTypes)
            {
                UtilLogger.LogInfo("Querying Type '{0}' for propertyName '{1}'", t.Name, propertyName);
                PropertyInfo[] memInfos = t.GetProperties(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                foreach (PropertyInfo mInfo in memInfos)
                {
                    UtilLogger.LogInfo("Found:'{0}'", mInfo.Name);
                    if (mInfo.Name.Contains(propertyName))
                    {
                        UtilLogger.LogInfo("Added:'{0}' to list", mInfo.Name);
                        propertyList.Add(mInfo);
                    }
                }
            }

            return propertyList;
        }
        #endregion

        #region private functions
        void GetConstructorInfo()
        {
            Assembly asm = typeof(KeyVaultClient).Assembly;
            FileVersionInfo fver = FileVersionInfo.GetVersionInfo(asm.Location);
            UtilLogger.LogInfo("{0}:{1}:::{2} => {3}", asm.FullName, asm.GetName().Version.ToString(), fver.FileVersion, asm.Location);
            ConstructorInfo[] conArray = typeof(KeyVaultClient).GetConstructors();
            StringBuilder strb = new StringBuilder();

            foreach (ConstructorInfo cInfo in conArray)
            {
                ParameterInfo[] paramArray = cInfo.GetParameters();
                foreach (ParameterInfo pInfo in paramArray)
                {
                    string args = string.Format("{0} {1}, ", pInfo.ParameterType.FullName, pInfo.Name);
                    strb.Append(args);
                }

                string con = string.Format("{0}({1})", cInfo.Name, strb.ToString());
                UtilLogger.LogInfo(con);

                strb.Clear();
            }
        }
        #endregion

        public override void Dispose()
        {
            AsmToReflect = null;
            IsDisposed = true;
        }
    }


    internal class AssemblyLoader : AssemblyLoadContext
    {
        private string folderPath;

        internal AssemblyLoader(string folderPath)
        {
            this.folderPath = Path.GetDirectoryName(folderPath);
        }

        internal Assembly Load(string filePath)
        {
            FileInfo fileInfo = new FileInfo(filePath);
            AssemblyName assemblyName = new AssemblyName(fileInfo.Name.Replace(fileInfo.Extension, string.Empty));

            return this.Load(assemblyName);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            //var dependencyContext = DependencyContext.Default;
            //var ressource = dependencyContext.CompileLibraries.FirstOrDefault(r => r.Name.Contains(assemblyName.Name));

            //if (ressource != null)
            //{
            //    return Assembly.Load(new AssemblyName(ressource.Name));
            //}

            var fileInfo = this.LoadFileInfo(assemblyName.Name);
            if (File.Exists(fileInfo.FullName))
            {
                Assembly assembly = null;
                if (this.TryGetAssemblyFromAssemblyName(assemblyName, out assembly))
                {
                    return assembly;
                }
                return this.LoadFromAssemblyPath(fileInfo.FullName);
            }

            return Assembly.Load(assemblyName);
        }

        private FileInfo LoadFileInfo(string assemblyName)
        {
            string fullPath = Path.Combine(this.folderPath, $"{assemblyName}.dll");

            return new FileInfo(fullPath);
        }

        private bool TryGetAssemblyFromAssemblyName(AssemblyName assemblyName, out Assembly assembly)
        {
            try
            {
                assembly = Default.LoadFromAssemblyName(assemblyName);
                return true;
            }
            catch
            {
                assembly = null;
                return false;
            }
        }
    }
}
