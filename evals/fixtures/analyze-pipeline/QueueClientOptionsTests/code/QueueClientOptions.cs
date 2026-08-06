// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Storage.Queues.Models;
using Azure.Storage.Shared;
using Microsoft.Extensions.Configuration;

namespace Azure.Storage.Queues
{
    /// <summary>
    /// Provides the client configuration options for connecting to Azure Queue
    /// Storage
    /// </summary>
    public partial class QueueClientOptions : ClientOptions, ISupportsTenantIdChallenges
    {
        /// <summary>
        /// The Latest service version supported by this client library.
        /// </summary>
        internal const ServiceVersion LatestVersion = StorageVersionExtensions.LatestVersion;

        /// <summary>
        /// The versions of Azure Queue Storage supported by this client
        /// library.
        ///
        /// For more information, see
        /// <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/versioning-for-the-azure-storage-services">
        /// Versioning for the Azure Storage services</see>.
        /// </summary>
        public enum ServiceVersion
        {
#pragma warning disable CA1707 // Identifiers should not contain underscores
            /// <summary>
            /// The 2019-02-02 service version described at
            /// <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/version-2019-02-02">
            /// Version 2019-02-02</see>.
            /// </summary>
            V2019_02_02 = 1,

            /// <summary>
            /// The 2019-07-07 service version described at
            /// <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/version-2019-07-07">
            /// Version 2019-07-07</see>.
            /// </summary>
            V2019_07_07 = 2,

            /// <summary>
            /// The 2019-12-12 service version.
            /// </summary>
            V2019_12_12 = 3,

            /// <summary>
            /// The 2020-02-10 service version.
            /// </summary>
            V2020_02_10 = 4,

            /// <summary>
            /// The 2020-04-08 service version.
            /// </summary>
            V2020_04_08 = 5,

            /// <summary>
            /// The 2020-06-12 service version.
            /// </summary>
            V2020_06_12 = 6,

            /// <summary>
            /// The 2020-08-14 service version.
            /// </summary>
            V2020_08_04 = 7,

            /// <summary>
            /// The 2020-10-02 service version.
            /// </summary>
            V2020_10_02 = 8,

            /// <summary>
            /// The 2020-12-06 service version.
            /// </summary>
            V2020_12_06 = 9,

            /// <summary>
            /// The 2021-02-12 service version.
            /// </summary>
            V2021_02_12 = 10,

            /// <summary>
            /// The 2021-04-10 service version.
            /// </summary>
            V2021_04_10 = 11,

            /// <summary>
            /// The 2021-06-08 service version.
            /// </summary>
            V2021_06_08 = 12,

            /// <summary>
            /// The 2021-08-06 service version.
            /// </summary>
            V2021_08_06 = 13,

            /// <summary>
            /// The 2021-10-04 service version.
            /// </summary>
            V2021_10_04 = 14,

            /// <summary>
            /// The 2021-12-02 service version.
            /// </summary>
            V2021_12_02 = 15,

            /// <summary>
            /// The 2022-11-02 service version.
            /// </summary>
            V2022_11_02 = 16,

            /// <summary>
            /// The 2023-01-03 service version.
            /// </summary>
            V2023_01_03 = 17,

            /// <summary>
            /// The 2023-05-03 service version.
            /// </summary>
            V2023_05_03 = 18,

            /// <summary>
            /// The 2023-08-03 service version.
            /// </summary>
            V2023_08_03 = 19,

            /// <summary>
            /// The 2023-11-03 service version.
            /// </summary>
            V2023_11_03 = 20,

            /// <summary>
            /// The 2024-02-04 service version.
            /// </summary>
            V2024_02_04 = 21,

            /// <summary>
            /// The 2024-05-04 service version.
            /// </summary>
            V2024_05_04 = 22,

            /// <summary>
            /// The 2024-08-04 service version.
            /// </summary>
            V2024_08_04 = 23,

            /// <summary>
            /// The 2024-11-04 service version.
            /// </summary>
            V2024_11_04 = 24,

            /// <summary>
            /// The 2025-01-05 service version.
            /// </summary>
            V2025_01_05 = 25,

            /// <summary>
            /// The 2025-05-05 service version.
            /// </summary>
            V2025_05_05 = 26,

            /// <summary>
            /// The 2025-07-05 service version.
            /// </summary>
            V2025_07_05 = 27,

            /// <summary>
            /// The 2025-11-05 service version.
            /// </summary>
            V2025_11_05 = 28,

            /// <summary>
            /// The 2026-02-06 service version.
            /// </summary>
            V2026_02_06 = 29,

            /// <summary>
            /// The 2026-04-06 service version.
            /// </summary>
            V2026_04_06 = 30,

            /// <summary>
            /// The 2026-06-06 service version.
            /// </summary>
            V2026_06_06 = 31,

            /// <summary>
            /// The 2026-10-06 service version.
            /// </summary>
            V2026_10_06 = 32,

            /// <summary>
            /// The 2026-12-06 service version.
            /// </summary>
            V2026_12_06 = 33
#pragma warning restore CA1707 // Identifiers should not contain underscores
        }

        /// <summary>
        /// Gets the <see cref="ServiceVersion"/> of the service API used when
        /// making requests.  For more, see
        /// For more information, see
        /// <see href="https://docs.microsoft.com/en-us/rest/api/storageservices/versioning-for-the-azure-storage-services">
        /// Versioning for the Azure Storage services</see>.
        /// </summary>
        public ServiceVersion Version { get; }


        // ... (unrelated QueueClientOptions properties, constructors, and Build/
        //      authentication members elided for fixture brevity; the version-parse
        //      bug under test lives entirely in the ServiceVersion enum above and
        //      the TryGetServiceVersion switch below) ...

        internal static bool TryGetServiceVersion(string version, out ServiceVersion serviceVersion)
        {
            serviceVersion = ServiceVersion.V2019_02_02;
            switch (version)
            {
                case "2019-02-02":
                    serviceVersion = ServiceVersion.V2019_02_02;
                    return true;
                case "2019-07-07":
                    serviceVersion = ServiceVersion.V2019_07_07;
                    return true;
                case "2019-12-12":
                    serviceVersion = ServiceVersion.V2019_12_12;
                    return true;
                case "2020-02-10":
                    serviceVersion = ServiceVersion.V2020_02_10;
                    return true;
                case "2020-04-08":
                    serviceVersion = ServiceVersion.V2020_04_08;
                    return true;
                case "2020-06-12":
                    serviceVersion = ServiceVersion.V2020_06_12;
                    return true;
                case "2020-08-04":
                    serviceVersion = ServiceVersion.V2020_08_04;
                    return true;
                case "2020-10-02":
                    serviceVersion = ServiceVersion.V2020_10_02;
                    return true;
                case "2020-12-06":
                    serviceVersion = ServiceVersion.V2020_12_06;
                    return true;
                case "2021-02-12":
                    serviceVersion = ServiceVersion.V2021_02_12;
                    return true;
                case "2021-04-10":
                    serviceVersion = ServiceVersion.V2021_04_10;
                    return true;
                case "2021-06-08":
                    serviceVersion = ServiceVersion.V2021_06_08;
                    return true;
                case "2021-08-06":
                    serviceVersion = ServiceVersion.V2021_08_06;
                    return true;
                case "2021-10-04":
                    serviceVersion = ServiceVersion.V2021_10_04;
                    return true;
                case "2021-12-02":
                    serviceVersion = ServiceVersion.V2021_12_02;
                    return true;
                case "2022-11-02":
                    serviceVersion = ServiceVersion.V2022_11_02;
                    return true;
                case "2023-01-03":
                    serviceVersion = ServiceVersion.V2023_01_03;
                    return true;
                case "2023-05-03":
                    serviceVersion = ServiceVersion.V2023_05_03;
                    return true;
                case "2023-08-03":
                    serviceVersion = ServiceVersion.V2023_08_03;
                    return true;
                case "2023-11-03":
                    serviceVersion = ServiceVersion.V2023_11_03;
                    return true;
                case "2024-02-04":
                    serviceVersion = ServiceVersion.V2024_02_04;
                    return true;
                case "2024-05-04":
                    serviceVersion = ServiceVersion.V2024_05_04;
                    return true;
                case "2024-08-04":
                    serviceVersion = ServiceVersion.V2024_08_04;
                    return true;
                case "2024-11-04":
                    serviceVersion = ServiceVersion.V2024_11_04;
                    return true;
                case "2025-01-05":
                    serviceVersion = ServiceVersion.V2025_01_05;
                    return true;
                case "2025-05-05":
                    serviceVersion = ServiceVersion.V2025_05_05;
                    return true;
                case "2025-07-05":
                    serviceVersion = ServiceVersion.V2025_07_05;
                    return true;
                case "2025-11-05":
                    serviceVersion = ServiceVersion.V2025_11_05;
                    return true;
                case "2026-02-06":
                    serviceVersion = ServiceVersion.V2026_02_06;
                    return true;
                case "2026-04-06":
                    serviceVersion = ServiceVersion.V2026_04_06;
                    return true;
                case "2026-06-06":
                    serviceVersion = ServiceVersion.V2026_06_06;
                    return true;
                default:
                    return false;
            }
        }
    }
}
