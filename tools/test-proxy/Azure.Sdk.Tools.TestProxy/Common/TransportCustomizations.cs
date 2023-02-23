
using System.Collections.Generic;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class PemPair
    {
        public string PemValue { get; set; }
        public string PemKey { get; set; }
    }

    public class TransportCustomizations
    {
        /// <summary>
        /// When customizing to allow or disallow 
        /// </summary>
        public bool AllowAutoRedirect { get; set; } = false;

        /// <summary>
        /// Provide a value for this property when you have a custom certificate securing the target resource. The public key contained here-in will be using during validation of the SSL connection (comparing thumbprints).
        /// </summary>
        public string TLSValidationCert { get; set; }

        /// <summary>
        /// When providing a TLS Validation Cert, that validation cert will often only apply to a single hostname. Providing this hostname as an argument ensures
        /// that this TLSValidationCert will only be utilized when attempting to reach out to the value contained in TLSValidationCertHost.
        /// </summary>
        public string TSLValidationCertHost { get; set; }

        /// <summary>
        /// Each certificate pair contained within this list should be added to the clientHandler for the server or an individual recording.
        /// </summary>
        public List<PemPair> Certificates { get; set; }

        /// <summary>
        /// During playback, a response is normally returned all at once. By offering this response time, we can
        /// "stretch" the writing of the response bytes over a time range of milliseconds.
        /// </summary>
        public int PlaybackResponseTime { get; set; } = 0;
    }
}
