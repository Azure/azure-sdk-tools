using System.Collections.Generic;
using Azure.Sdk.Tools.CodeownersUtils.Errors;

namespace Azure.Sdk.Tools.CodeownersUtils.Parsing
{
    /// <summary>
    /// Structured parse result for CODEOWNERS parsing that allows callers to handle
    /// malformed blocks without relying on console output.
    /// </summary>
    public class CodeownersParseResult
    {
        public List<CodeownersEntry> Entries { get; } = new List<CodeownersEntry>();

        public List<BlockFormattingError> BlockErrors { get; } = new List<BlockFormattingError>();
    }
}
