using System;

namespace Azure.Sdk.Tools.CodeownersMigration.Verification
{
    /// <summary>
    /// The kind of difference found at a given entry position.
    /// </summary>
    public enum DifferenceKind
    {
        /// <summary>A semantic field differs between the base and compare entry at the same position.</summary>
        FieldChanged,

        /// <summary>The base file has an entry at this position and the compare file has none.</summary>
        MissingFromCompare,

        /// <summary>The compare file has an entry at this position and the base file has none.</summary>
        MissingFromBase
    }

    /// <summary>
    /// A single semantic difference between the base and compare CODEOWNERS files, located by the
    /// zero-based position of the entry in each file.
    /// </summary>
    public class EntryDifference
    {
        /// <summary>Zero-based index of the entry within the parsed entry list.</summary>
        public int Index { get; set; }

        public DifferenceKind Kind { get; set; }

        /// <summary>The semantic field that differs, for <see cref="DifferenceKind.FieldChanged"/>.</summary>
        public string Field { get; set; }

        /// <summary>Path expression of the entry, used to orient the reader.</summary>
        public string Path { get; set; }

        /// <summary>Line number of the block in the base file, or -1 when absent.</summary>
        public int BaseLine { get; set; } = -1;

        /// <summary>Line number of the block in the compare file, or -1 when absent.</summary>
        public int CompareLine { get; set; } = -1;

        public string BaseValue { get; set; }

        public string CompareValue { get; set; }

        public override string ToString()
        {
            switch (Kind)
            {
                case DifferenceKind.MissingFromCompare:
                    return $"[{Index}] entry present in base (line {BaseLine + 1}) but absent from compare: {BaseValue}";
                case DifferenceKind.MissingFromBase:
                    return $"[{Index}] entry present in compare (line {CompareLine + 1}) but absent from base: {CompareValue}";
                default:
                    return string.Join(
                        Environment.NewLine,
                        $"[{Index}] {Path} ({Field} differs; base line {BaseLine + 1}, compare line {CompareLine + 1})",
                        $"      base:    {BaseValue}",
                        $"      compare: {CompareValue}");
            }
        }
    }
}
