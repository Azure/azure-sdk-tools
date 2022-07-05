namespace Azure.Sdk.Tools.TestProxy.Store
{
    /// <summary>
    /// This class is used to represent any assets.json configuration. An assets.json configuration contains all the necessary configuration needed to restore an asset to the local storage directory of the test-proxy.
    /// </summary>
    public class AssetsConfiguration
    {
        /// <summary>
        /// 
        /// </summary>
        public virtual string AssetsJsonRelativeLocation { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public virtual string AssetsJsonLocation { get; set; }
    }
}
