using System;
using System.Collections.Generic;
using System.IO;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.CodeownersMigration.Verification
{
    /// <summary>
    /// Thrown when a CODEOWNERS file cannot be parsed into a trustworthy list of entries.
    /// </summary>
    public class CodeownersLoadException : Exception
    {
        public CodeownersLoadException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Loads and parses a CODEOWNERS file, refusing to return entries when the parser rejected any block.
    /// </summary>
    public static class CodeownersFileLoader
    {
        /// <summary>
        /// Parses a CODEOWNERS file into entries.
        ///
        /// <see cref="CodeownersParser.ParseCodeownersEntries"/> reports malformed blocks on stderr and then
        /// <em>drops</em> them. A dropped block is fatal to an ordinal comparison twice over: it shifts the
        /// position of every entry that follows it, and if the same block is dropped from both files the
        /// comparison passes while having silently checked nothing about it. Parser output is therefore
        /// captured and treated as a load failure rather than as a difference.
        /// </summary>
        /// <exception cref="CodeownersLoadException">The file is missing or a block was rejected.</exception>
        public static List<CodeownersEntry> Load(string path, string teamStorageUri)
        {
            if (!File.Exists(path))
            {
                throw new CodeownersLoadException($"CODEOWNERS file not found: {path}");
            }

            List<string> lines = new List<string>(File.ReadAllLines(path));

            TextWriter originalError = Console.Error;
            var captured = new StringWriter();
            List<CodeownersEntry> entries;
            try
            {
                Console.SetError(captured);
                entries = CodeownersParser.ParseCodeownersEntries(lines, teamStorageUri);
            }
            finally
            {
                Console.SetError(originalError);
            }

            string parserOutput = captured.ToString().Trim();
            if (parserOutput.Length > 0)
            {
                throw new CodeownersLoadException(
                    $"The parser rejected one or more blocks in {path}, so its entries cannot be compared:{Environment.NewLine}{parserOutput}");
            }

            return entries;
        }
    }
}
