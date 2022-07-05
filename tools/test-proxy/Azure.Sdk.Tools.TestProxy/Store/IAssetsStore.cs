using System.IO;

namespace Azure.Sdk.Tools.TestProxy.Store
{
    public interface IAssetsStore
    {
        /// <summary>
        /// Given a configuration, push the changes made by the test-proxy into the remote store.
        /// </summary>
        /// <param name="pathToAssetsJson"></param>
        /// <param name="contextPath"></param>
        public abstract void Push(string pathToAssetsJson, string contextPath);

        /// <summary>
        /// Given a configuration, pull any remote resources down into the provided contextPath.
        /// </summary>
        /// <param name="pathToAssetsJson"></param>
        /// <param name="contextPath"></param>
        public abstract void Restore(string pathToAssetsJson, string contextPath);

        /// <summary>
        /// Given a configuration, determine the state of the resources present under contextPath, reset those resources to their "fresh" state.
        /// </summary>
        /// <param name="pathToAssetsJson"></param>
        /// <param name="contextPath"></param>
        public abstract void Reset(string pathToAssetsJson, string contextPath);
    }
}
