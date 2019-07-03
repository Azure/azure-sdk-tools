// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.Common.Services
{
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    public class ReflectionService : NetSdkUtilTask
    {
        #region fields
        Assembly _asmToReflect;
        MetadataLoadContext _mlc;
        #endregion

        #region Properties
        Assembly AssemblyToReflect
        {
            get
            {
                if(_asmToReflect == null)
                {
                    _asmToReflect = GetAssembly(UseMetadataLoadContext);
                }

                return _asmToReflect;
            }
            set
            {
                _asmToReflect = value;
            }
        }

        MetadataLoadContext MetaCtx
        {
            get
            {
                if (_mlc == null)
                {
                    _mlc = new MetadataLoadContext(new SimpleAssemblyResolver());
                }

                return _mlc;
            }
            
        }

        string AsmFilePath { get; set; }

        bool UseMetadataLoadContext { get; set; }
        #endregion

        #region Constructor
        public ReflectionService(string AssemblyFilePath) : this(AssemblyFilePath, useMetadataLoadContext: true) { }

        public ReflectionService(string assemblyFilePath, bool useMetadataLoadContext = true)
        {
            AsmFilePath = assemblyFilePath;
            UseMetadataLoadContext = useMetadataLoadContext;
        }
        #endregion

        #region Public Functions
        public Assembly GetAssembly(bool useMetadataLoadContext)
        {
            Assembly loadedAssemly = null;
            if (useMetadataLoadContext)
            {
                loadedAssemly = MetaCtx.LoadFromAssemblyPath(AsmFilePath);
            }
            else
            {
                loadedAssemly = Assembly.LoadFrom(AsmFilePath);
            }

            return loadedAssemly;
        }

        public List<PropertyInfo> GetProperties(string typeNameStartWith, string propertyName)
        {
            List<PropertyInfo> propertyList = new List<PropertyInfo>();
            try
            {
                var exportedNS = GetAssembly(true).ExportedTypes.Select<Type, string>((item) => item.Namespace);
                var distinctNS = exportedNS.Distinct<string>();
                Type sdkInfoType = null;

                foreach(string ns in distinctNS)
                {
                    string fqtype = string.Format("{0}.sdkinfo", ns);
                    sdkInfoType = GetAssembly(false).GetType(fqtype, throwOnError: true, ignoreCase: true);
                    if(sdkInfoType != null)
                    {
                        break;
                    }
                }

                UtilLogger.LogInfo("Querying Type '{0}' for propertyName '{1}'", sdkInfoType.Name, propertyName);
                PropertyInfo[] memInfos = sdkInfoType.GetProperties(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

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
            catch (Exception ex)
            {
                UtilLogger.LogWarning(ex.Message);
            }

            return propertyList;
        }

        #endregion

        public override void Dispose()
        {
            AssemblyToReflect = null;
            IsDisposed = true;
        }
    }

    class SimpleAssemblyResolver : CoreMetadataAssemblyResolver
    {
        private static readonly Version s_Version0000 = new Version(0, 0, 0, 0);

        public SimpleAssemblyResolver() { }

        public override Assembly Resolve(MetadataLoadContext context, AssemblyName assemblyName)
        {
            Assembly core = base.Resolve(context, assemblyName);
            if (core != null)
                return core;

            ReadOnlySpan<byte> pktFromAssemblyName = assemblyName.GetPublicKeyToken();
            foreach (Assembly assembly in context.GetAssemblies())
            {
                AssemblyName assemblyNameFromContext = assembly.GetName();
                if (assemblyName.Name.Equals(assemblyNameFromContext.Name, StringComparison.OrdinalIgnoreCase) &&
                    NormalizeVersion(assemblyName.Version).Equals(assemblyNameFromContext.Version) &&
                    pktFromAssemblyName.SequenceEqual(assemblyNameFromContext.GetPublicKeyToken()) &&
                    NormalizeCultureName(assemblyName.CultureName).Equals(NormalizeCultureName(assemblyNameFromContext.CultureName)))
                    return assembly;
            }

            return null;
        }

        private Version NormalizeVersion(Version version)
        {
            if (version == null)
                return s_Version0000;

            return version;
        }

        private string NormalizeCultureName(string cultureName)
        {
            if (cultureName == null)
                return string.Empty;

            return cultureName;
        }
    }

    class CoreMetadataAssemblyResolver : MetadataAssemblyResolver
    {
        #region fields
        private Assembly _coreAssembly;
        #endregion

        #region Constructor
        public CoreMetadataAssemblyResolver() { }
        #endregion

        #region Public Functions
        public override Assembly Resolve(MetadataLoadContext context, AssemblyName assemblyName)
        {
            string name = assemblyName.Name;

            if (name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("System.Private.CoreLib", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("System.Runtime", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("netstandard", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("System.Reflection.Metadata", StringComparison.OrdinalIgnoreCase) ||
                // For interop attributes such as DllImport and Guid:
                name.Equals("System.Runtime.InteropServices", StringComparison.OrdinalIgnoreCase))
            {
                if (_coreAssembly == null)
                    _coreAssembly = context.LoadFromStream(CreateStreamForCoreAssembly());

                return _coreAssembly;
            }

            return null;
        }
        #endregion

        #region private functions
        Stream CreateStreamForCoreAssembly()
        {
            // We need a core assembly in IL form. Since this version of this code is for Jitted platforms, the System.Private.Corelib
            // of the underlying runtime will do just fine.
            string assumedLocationOfCoreLibrary = typeof(object).Assembly.Location;
            if (assumedLocationOfCoreLibrary == null || assumedLocationOfCoreLibrary == string.Empty)
            {
                throw new Exception("Could not find a core assembly to use for tests as 'typeof(object).Assembly.Location` returned " +
                    "a null or empty value. The most likely cause is that you built the tests for a Jitted runtime but are running them " +
                    "on an AoT runtime.");
            }

            return File.OpenRead(GetPathToCoreAssembly());
        }

        string GetPathToCoreAssembly()
        {
            return typeof(object).Assembly.Location;
        }

        string GetNameOfCoreAssembly()
        {
            return typeof(object).Assembly.GetName().Name;
        }
        #endregion
    }
}
