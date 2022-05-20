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
            return null;
        }

        private Type CheckAssemblyForStore(string storeName, Assembly inputAssembly)
        {
            throw new NotImplementedException();
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
            // first check if the type is present in executing assembly
            var invokingAssembly = Assembly.GetExecutingAssembly();

            var storeType = CheckAssemblyForStore(storeName, invokingAssembly);

            if (storeType == null)
            {
                foreach(var assemblyDirectory in AssemblyDirectories)
                {
                    var assemblyFiles = GetAssembliesFromFolder(assemblyDirectory);

                    foreach(var assemblyFile in assemblyFiles)
                    {
                        storeType = CheckAssemblyForStore(storeName, LoadAssembly(assemblyFile));

                        if(storeType != null)
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
                    $"Unable to load the specified IAssetStore class {storeName}. Looked in invoking assembly as well as additional directories: [{String.Join(",", AssemblyDirectories)}]"
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
                    $"Unable to create an instance of type {storeType.Name}. This name was generated from input {storeName}. The invoking assembly and directories [{String.Join(",", AssemblyDirectories)}] were queried for this type."
                );
            }
        }

        /// <summary>
        /// Used to resolve the store given a name and an additional assembly directory.
        /// </summary>
        /// <param name="storeName"></param>
        /// <returns></returns>
        public IAssetsStore ResolveStore(string storeName)
        {
            // TODO: implement
            return new NullStore();
        }
    }
}
