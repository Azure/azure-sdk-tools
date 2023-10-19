using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Azure.Sdk.Tools.CodeownersUtils.Errors
{
    /// <summary>
    /// SingleLineError is the error class to hold any/all issues with a single line being parsed.
    /// For example, a source/path @owner1...@ownerX line could have different issues with several
    /// owners. The error string would look like:
    /// Error on line 500.
    /// Source line: source/path @owner1...@ownerX
    ///   - first error
    ///   - second error
    /// </summary>
    public class SingleLineError : BaseError
    {
        public string Source { get; private set; }

        public SingleLineError(int lineNumber, string sourceText, string error) : base(lineNumber, error)
        {
            Source = sourceText;
        }
        public SingleLineError(int lineNumber, string sourceText, List<string> errors) : base(lineNumber, errors)
        {
            Source = sourceText;
        }
        public override string ToString()
        {
            var returnString = string.Join(
                Environment.NewLine,
                $"Error(s) on line {LineNumber}",
                $"Source Line: {Source}",
                ErrorString
                );
            return returnString;
        }
    }
}
