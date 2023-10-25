using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CodeownersUtils.Errors
{
    /// <summary>
    /// The base error class, used by SingleLineError and BlockFormatting, with the common methods and
    /// members. Note that the ToString must be overridden by the concrete classes.
    /// </summary>
    public abstract class BaseError
    {
        protected const string Indent = "  ";
        protected const string IndentWithDash = $"{Indent}-";

        public List<string> Errors { get; private set; }

        // For single line errors this will be the line number with the error
        // but for multi-line errors this is the start line. This is done this
        // way so errors can be processed independenly and sorted by line/start line.
        public int LineNumber { get; private set; }

        public BaseError(int lineNumber, string error)
        {
            LineNumber = lineNumber;
            List<string> errors = new List<string>
            {
                error
            };
            Errors = errors;
        }
        public BaseError(int lineNumber, List<string> errors)
        {
            LineNumber = lineNumber;
            Errors = errors;
        }

        // Create a single error string separated by NewLine characters with indents and dashes
        public string ErrorString 
        { 
            get
            {
                return string.Join(Environment.NewLine, Errors.Select(s => $"{IndentWithDash}{s}"));
            } 
        }
        public abstract override string ToString();
    }
}
