using System.IO;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    public interface IAssetsStore
    {
        /// <summary>
        /// Given a configuration, push the changes made by the test-proxy into the remote store.
        /// </summary>
        /// <param name="assetsConfig"></param>
        /// <param name="contextPath"></param>
        public void Save(AssetsConfiguration assetsConfig, string contextPath);

        /// <summary>
        /// Given a configuration, pull any remote resources down into the provided contextPath.
        /// </summary>
        /// <param name="assetsConfig"></param>
        /// <param name="contextPath"></param>
        public void Restore(AssetsConfiguration assetsConfig, string contextPath);

        /// <summary>
        /// Given a configuration, determine the state of the resources present under contextPath, reset those resources to their "fresh" state.
        /// </summary>
        /// <param name="assetsConfig"></param>
        /// <param name="contextPath"></param>
        public void Reset(AssetsConfiguration assetsConfig, string contextPath);

        /// <summary>
        /// Parses a configuration file and returns the appropriate Configuration class for further usage.
        /// </summary>
        /// <param name="assetsJsonPath"></param>
        /// <returns>An object representing the configuration class. This means that call-site should cast this to their preferred Configuration type.</returns>
        public object ParseConfigurationFile(string assetsJsonPath);
    }
}
