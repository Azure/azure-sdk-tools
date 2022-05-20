using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Azure.Sdk.Tools.TestProxy.Common;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    public class StoreResolver
    {
        public string[] AssemblyDirectories { get; set; }

        public StoreResolver(string[] assemblyDirectories)
        {
            foreach(var directory in assemblyDirectories)
            {
                if (!File.Exists(directory))
                {
                    throw new HttpException(System.Net.HttpStatusCode.BadRequest, $"Provided directory {directory} does not exist.");
                }
            }

            AssemblyDirectories = assemblyDirectories;
        }

        public StoreResolver(string assemblyDirectory)
        {
            if (!File.Exists(assemblyDirectory))
            {
                throw new HttpException(System.Net.HttpStatusCode.BadRequest, $"Provided directory {assemblyDirectory} does not exist.");
            }

            AssemblyDirectories = new string[] { assemblyDirectory };
        }

        private Assembly LoadAssembly(string path)
        {
            throw new NotImplementedException();
        }

        private Type GetTypeFromAssembly(string storeName, Assembly inputAssembly)
        {
            foreach(var type in inputAssembly.GetTypes()) {
                if (type.Name.ToLowerInvariant().Contains(storeName.ToLowerInvariant()))
                {
                    return type;
                }
            }

            return null;
        }

        private string[] GetAssembliesFromFolder(string folder)
        {
            return Directory.GetFiles(folder, "*.dll");
        }

        /// <summary>
        /// Used to resolve the store given a name and an additional assembly directory. The provided path will be added to the existing set present within AssemblyDirectories property.
        /// This function will take the FIRST matched assembly type by NAME.
        /// </summary>
        /// <param name="storeName"></param>
        /// <param name="additionalAssemblyDirectory"></param>
        /// <returns></returns>
        public IAssetsStore ResolveStore(string storeName, string additionalAssemblyDirectory)
        {
            if (!File.Exists(additionalAssemblyDirectory))
            {
                throw new HttpException(System.Net.HttpStatusCode.BadRequest, $"Provided directory {additionalAssemblyDirectory} does not exist.");
            }

            var assemblyDirectories = new string[AssemblyDirectories.Length + 1];
            AssemblyDirectories.CopyTo(assemblyDirectories, 0);
            assemblyDirectories[assemblyDirectories.Length] = additionalAssemblyDirectory;

            return ResolveStore(storeName, assemblyDirectories);
        }


        /// <summary>
        /// Used to resolve the store given a name and an additional assembly directory.
        /// </summary>
        /// <param name="storeName"></param>
        /// <returns></returns>
        public IAssetsStore ResolveStore(string storeName)
        {
            return ResolveStore(storeName, AssemblyDirectories);
        }

        private IAssetsStore ResolveStore(string storeName, string[] assemblyDirectories)
        {
            if (String.IsNullOrWhiteSpace(storeName))
            {
                throw new HttpException(System.Net.HttpStatusCode.BadRequest, $"Unable to load a backing store without a valid input value. Test-Proxy saw value \"{(storeName == null ? "null" : storeName)}\"");
            }

            // first check if the type is present in executing assembly
            var invokingAssembly = Assembly.GetExecutingAssembly();
            var storeType = GetTypeFromAssembly(storeName, invokingAssembly);

            // then we will only do the heavy lifting of multiple assemblies if we HAVE to
            if (storeType == null)
            {
                foreach (var assemblyDirectory in assemblyDirectories)
                {
                    var assemblyFiles = GetAssembliesFromFolder(assemblyDirectory);

                    foreach (var assemblyFile in assemblyFiles)
                    {
                        storeType = GetTypeFromAssembly(storeName, LoadAssembly(assemblyFile));

                        if (storeType != null)
                        {
                            break;
                        }
                    }

                    if (storeType != null)
                    {
                        break;
                    }
                }
            }

            if (storeType == null)
            {
                throw new HttpException(
                    System.Net.HttpStatusCode.BadRequest,
                    $"Unable to load the specified IAssetStore class {storeName}. Looked in invoking assembly as well as additional directories: [{String.Join(",", assemblyDirectories)}]"
                );
            }

            try
            {
                var generatedStore = Activator.CreateInstance(storeType);

                return (IAssetsStore)generatedStore;
            }
            catch (Exception e)
            {
                throw new HttpException(
                    System.Net.HttpStatusCode.BadRequest,
                    $"Unable to create an instance of type {storeType.Name}. This name was generated from input {storeName}. The invoking assembly and directories [{String.Join(",", assemblyDirectories)}] were queried for this type."
                    + $"Visible Exception is \"{e.Message}\"."
                );
            }
        }
    }
}
