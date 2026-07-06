// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.Cli.Prompts.Templates
{
    public abstract class ClassificationBaseTemplate<T, I> : BasePromptTemplate
    {
        /// <summary>
        /// Parses the classification result and optionally updates the provided items with the classified data.
        /// </summary>
        /// <param name="result">The raw classification result.</param>
        /// <param name="items">Optional list of items to update with the classified result.</param>
        /// <returns>Parsed classification result.</returns>
        public abstract List<T> ParseClassifyResult(string result, List<I>? items = null);
    }
}
