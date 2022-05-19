using System.IO;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    public abstract class AssetsStore
    {
        /// <summary>
        /// Given a configuration, push the changes made by the test-proxy into the remote store.
        /// </summary>
        /// <param name="assetsConfig"></param>
        /// <param name="contextPath"></param>
        public abstract void Save<T>(T assetsConfig, string contextPath);

        /// <summary>
        /// Given a configuration, pull any remote resources down into the provided contextPath.
        /// </summary>
        /// <param name="assetsConfig"></param>
        /// <param name="contextPath"></param>
        public abstract void Restore<T>(T assetsConfig, string contextPath);

        /// <summary>
        /// Given a configuration, determine the state of the resources present under contextPath, reset those resources to their "fresh" state.
        /// </summary>
        /// <param name="assetsConfig"></param>
        /// <param name="contextPath"></param>
        public abstract void Reset<T>(T assetsConfig, string contextPath);

        /// <summary>
        /// Parses a configuration file and returns the appropriate Configuration class for further usage.
        /// </summary>
        /// <param name="assetsJsonPath"></param>
        /// <returns>An object representing the configuration class. This means that call-site should cast this to their preferred Configuration type.</returns>
        public abstract AssetsConfiguration ParseConfigurationFile(string assetsJsonPath);
    }
}
