
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
        /// When populated, this value will be used to contact retrieve and retrieve a ledger TLS Certificate. This should be just the hostname of the targeted confidential ledger.
        /// </summary>
        public string LedgerId { get; set; }

        /// <summary>
        /// When grabbing a TLS cert from the confidential ledger identity service, what api version are we using?
        /// </summary>
        public string LedgerApiVersion { get; set; } = "2022-05-13";

        public string ConfidentialLedgerIdentityUri { get; set; } = "https://identity.confidential-ledger.core.azure.com";

        /// <summary>
        /// Each certificate pair contained within this list should be added to the clientHandler for the server or an individual recording.
        /// </summary>
        public List<PemPair> Certificates { get; set; }
    }
}
