using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CodeownersLinter.Constants
{
    /// <summary>
    /// Contains the patterns and characters that are invalid for a CODEOWNERS file as defined by GitHub documentation.
    /// https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners#codeowners-syntax
    /// </summary>
    public class InvalidGlobPatterns
    {
        // Escaping a pattern starting with # using \ so it is treated as a pattern and not a comment
        public const string EscapedPound = @"\#";
        // While the following are characters, they need to be string to be used in the ErrorMessageConstants
        // Using ! to negate a pattern
        public const string ExclamationMark = "!";
        // Using [ ] to define a character range
        public const string LeftBracket = "[";
        public const string RightBracket = "]";
    }
}
