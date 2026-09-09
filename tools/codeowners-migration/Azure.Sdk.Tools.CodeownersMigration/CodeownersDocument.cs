using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Azure.Sdk.Tools.CodeownersMigration.Sections;
using Azure.Sdk.Tools.CodeownersMigration.Verification;
using Azure.Sdk.Tools.CodeownersUtils.Parsing;

namespace Azure.Sdk.Tools.CodeownersMigration
{
    /// <summary>
    /// A CODEOWNERS file loaded, parsed and projected into semantic facts.
    /// </summary>
    public class CodeownersDocument
    {
        public string Path { get; private set; }
        public List<string> Lines { get; private set; }
        public List<CodeownersSection> Sections { get; private set; }

        /// <summary>Raw parser entries, in file order. Required for last-match-wins path resolution.</summary>
        public List<CodeownersEntry> Entries { get; private set; }

        /// <summary>Semantic projection of <see cref="Entries"/>, in file order.</summary>
        public List<EntryFacts> Facts { get; private set; }

        /// <summary>Blocks the parser rejected and skipped. These are silent data loss and must be surfaced.</summary>
        public List<string> ParseDiagnostics { get; private set; } = new List<string>();

        /// <summary>Facts grouped by the section they were declared in. Keyed by section name.</summary>
        public Dictionary<string, List<EntryFacts>> FactsBySection { get; private set; }

        public static CodeownersDocument Load(string path, string teamStorageUri = null, string sectionName = null)
        {
            var document = new CodeownersDocument
            {
                Path = path,
                Lines = File.ReadAllLines(path).ToList()
            };

            document.Sections = SectionScanner.FindSections(document.Lines);

            int startLine = -1;
            int endLine = -1;
            if (!string.IsNullOrEmpty(sectionName))
            {
                CodeownersSection section = document.Sections
                    .FirstOrDefault(s => string.Equals(s.Name, sectionName, StringComparison.OrdinalIgnoreCase));
                if (section == null)
                {
                    throw new ArgumentException($"Section '{sectionName}' was not found in {path}.");
                }
                startLine = section.ContentStart;
                endLine = section.SectionEnd;
            }

            document.Entries = ParseCapturingDiagnostics(document.Lines, teamStorageUri, startLine, endLine, document.ParseDiagnostics);
            document.Facts = document.Entries.Select(EntryFacts.FromEntry).ToList();
            document.FactsBySection = document.GroupFactsBySection();

            return document;
        }

        /// <summary>
        /// The parser reports malformed blocks to stderr and then drops the entry. Capture that output so a
        /// dropped block is reported as a hard error rather than silently appearing as a missing entry.
        /// </summary>
        private static List<CodeownersEntry> ParseCapturingDiagnostics(List<string> lines,
                                                                       string teamStorageUri,
                                                                       int startLine,
                                                                       int endLine,
                                                                       List<string> diagnostics)
        {
            TextWriter originalError = Console.Error;
            var captured = new StringWriter();
            try
            {
                Console.SetError(captured);
                return CodeownersParser.ParseCodeownersEntries(lines, teamStorageUri, startLine, endLine);
            }
            finally
            {
                Console.SetError(originalError);
                string text = captured.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    diagnostics.AddRange(text.Split('\n')
                        .Select(l => l.TrimEnd('\r'))
                        .Where(l => !string.IsNullOrWhiteSpace(l)));
                }
            }
        }

        private Dictionary<string, List<EntryFacts>> GroupFactsBySection()
        {
            var bySection = new Dictionary<string, List<EntryFacts>>(StringComparer.OrdinalIgnoreCase);
            foreach (CodeownersSection section in Sections)
            {
                bySection[section.Name] = Facts
                    .Where(f => f.StartLine >= section.ContentStart && f.StartLine < section.SectionEnd)
                    .ToList();
            }
            return bySection;
        }

        /// <summary>
        /// Returns the name of the section an entry was declared in, or null when the entry sits outside any
        /// section banner.
        /// </summary>
        public string SectionNameForLine(int line)
        {
            foreach (CodeownersSection section in Sections)
            {
                if (line >= section.ContentStart && line < section.SectionEnd)
                {
                    return section.Name;
                }
            }
            return null;
        }
    }
}
