// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Tools.Samples
{
    /// <summary>
    /// Shared constants for sample generation and translation tools.
    /// </summary>
    public static class SampleConstants
    {
        /// <summary>
        /// Maximum number of characters to load when reading source code context.
        /// </summary>
        public const int MaxContextCharacters = 4000000;

        /// <summary>
        /// Maximum number of characters per file when loading source code context.
        /// </summary>
        public const int MaxCharactersPerFile = 50000;

        /// <summary>
        /// Default batch size for processing samples to avoid overwhelming AI models.
        /// </summary>
        public const int DefaultBatchSize = 5;
    }
}