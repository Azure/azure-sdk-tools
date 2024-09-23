using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CodeownersUtils.Errors
{
    /// <summary>
    /// BlockFormattingError is used to report errors for a source block that spans multiple lines. It's used to display the
    /// entire block of from the CODEOWNERS file. For example, if a block contains ServiceLabel and a PRLabel but 
    /// doesn't end in a source path/owners line, the error would display the start/end line numbers with the actual block
    /// in between. For example:
    /// Source block error.
    /// Source block start: 120
    ///   actual source line 120
    ///   actual source line 121
    ///   actual source line 122
    /// Source block end: 122
    ///   - first error
    ///   - second error
    /// </summary>
    public class BlockFormattingError: BaseError
    {
        public int EndLineNumber { get; private set; }
        public List<string> SourceBlock { get; private set; }
        public BlockFormattingError(int startLine, int endLine, List<string> sourceBlock, string error) : base(startLine, error)
        {
            EndLineNumber = endLine;
            SourceBlock = sourceBlock;
        }
        public BlockFormattingError(int startLine, int endLine, List<string> sourceBlock, List<string> errors) : base(startLine, errors)
        {
            EndLineNumber = endLine;
            SourceBlock = sourceBlock;
        }

        public string SourceBlockString
        {
            get
            {
                return string.Join(Environment.NewLine, SourceBlock.Select(s => $"{Indent}{s}"));
            }
        }
        public override string ToString()
        {
            var returnString = string.Join(
                Environment.NewLine,
                $"Source block error.",
                $"Source Block Start: {LineNumber}",
                $"{SourceBlockString}",
                $"Source Block End: {EndLineNumber}",
                ErrorString
                );
            return returnString;
        }
    }
}
