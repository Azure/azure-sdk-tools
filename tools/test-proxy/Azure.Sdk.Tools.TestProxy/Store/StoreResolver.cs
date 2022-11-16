using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using Azure.Sdk.Tools.TestProxy.Common;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    public class StoreResolver
    {
        public StoreResolver()
        {
        }

        private Assembly LoadAssembly(string path)
        {
            throw new NotImplementedException();
        }

        private Type GetTypeFromAssembly(string storeName, Assembly inputAssembly)
        {
            var allTypes = inputAssembly.GetTypes().ToList();

            foreach (var type in allTypes) {
                if (type.FullName.ToLowerInvariant().Contains(storeName.ToLowerInvariant()))
                {
                    return type;
                }
            }

            return null;
        }

        /// <summary>
        /// Used to resolve the store given a name and an additional assembly directory.
        /// </summary>
        /// <param name="storeName"></param>
        /// <returns></returns>
        public IAssetsStore ResolveStore(string storeName)
        {
            if (String.IsNullOrWhiteSpace(storeName))
            {
                throw new HttpException(System.Net.HttpStatusCode.BadRequest, $"Unable to load a backing store without a valid input value. Test-Proxy saw value \"{(storeName == null ? "null" : storeName)}\"");
            }

            // first check if the type is present in executing assembly
            var invokingAssembly = Assembly.GetExecutingAssembly();
            var storeType = GetTypeFromAssembly(storeName, invokingAssembly);

            if (storeType == null)
            {
                throw new HttpException(
                    System.Net.HttpStatusCode.BadRequest,
                    $"Unable to load the specified IAssetStore class {storeName}."
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
                    $"Unable to create an instance of type {storeType.Name}. This name was generated from input {storeName}."
                    + $"Visible Exception is \"{e.Message}\"."
                );
            }
        }

        public static string ParseAssetsJsonBody(IDictionary<string, object> options)
        {
            if (!options.ContainsKey("x-recording-assets-file"))
            {
                throw new HttpException(HttpStatusCode.BadRequest, "Users provide the key AssetsJsonLocation within the JSON body sent to this endpoint.");
            }

            var value = options["x-recording-assets-file"].ToString();

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"A valid value to key \"AssetsJsonLocation\" is required. Received null, whitespace, or nothing.");
            }

            if (System.IO.File.Exists(value))
            {
                throw new HttpException(HttpStatusCode.BadRequest, $"When providing a path to the assets json, it must be locally resolvable. Input Value: \"{value}\".");
            }

            return value;
        }
    }
}
