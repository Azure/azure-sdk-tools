using System;
using System.Collections.Generic;

namespace Azure.Sdk.Tools.HttpFaultInjector
{
    public static class Utils
    {
        public static readonly IDictionary<string, string> FaultModes = new Dictionary<string, string>()
        {
            { "f", "Full response" },
            { "p", "Partial Response (full headers, 50% of body), then wait indefinitely" },
            {"pc", "Partial Response (full headers, 50% of body), then close (TCP FIN)" },
            {"pa", "Partial Response (full headers, 50% of body), then abort (TCP RST)" },
            {"pn", "Partial Response (full headers, 50% of body), then finish normally" },
            {"n", "No response, then wait indefinitely"},
            {"nc", "No response, then close (TCP FIN)" },
            {"na", "No response, then abort (TCP RST)" }
        };

        public static readonly string[] ExcludedRequestHeaders = new string[] {
            // Only applies to request between client and proxy
            "Proxy-Connection",

            // "X-Upstream-Base-Uri" in original request is used as the Base URI in the upstream request
            UpstreamBaseUriHeader,
            "Host",

            ResponseSelectionHeader
        };

        // Headers which must be set on HttpContent instead of HttpRequestMessage
        public static readonly string[] ContentRequestHeaders = new string[] {
            "Content-Length",
            "Content-Type",
        };

        public const string ResponseSelectionHeader = "x-ms-faultinjector-response-option";
        public const string UpstreamBaseUriHeader = "X-Upstream-Base-Uri";

        public static string ReadSelectionFromConsole()
        {
            string fault;
            do
            {
                Console.WriteLine();

                Console.WriteLine("Select a response fault mode then press ENTER:");
                foreach (var kvp in FaultModes)
                {
                    Console.WriteLine($"{kvp.Key}: {kvp.Value}");
                }

                Console.WriteLine();

                fault = Console.ReadLine();
            } while (fault == null || !FaultModes.ContainsKey(fault));

            return fault;
        }
    }
}
