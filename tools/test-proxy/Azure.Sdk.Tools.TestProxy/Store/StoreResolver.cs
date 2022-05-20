namespace Azure.Sdk.Tools.TestProxy.Store
{
    public class StoreResolver
    {
        public string[] AssemblyDirectories { get; set; }

        public StoreResolver(string[] assemblyDirectories)
        {
            // TODO: handle null/empty/invalid paths here.
            AssemblyDirectories = assemblyDirectories;
        }

        public StoreResolver(string assemblyDirectory)
        {
            // TODO: handle null/empty/invalid paths here.
            AssemblyDirectories = new string[] { assemblyDirectory };
        }

        /// <summary>
        /// Used to resolve the store given a name and an additional assembly directory. The provided path will be added to the existing set present within AssemblyDirectories property.
        /// </summary>
        /// <param name="storeName"></param>
        /// <param name="additionalAssemblyDirectory"></param>
        /// <returns></returns>
        public IAssetsStore ResolveStore(string storeName, string additionalAssemblyDirectory)
        {
            // TODO: implement
            return new NullStore();
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
