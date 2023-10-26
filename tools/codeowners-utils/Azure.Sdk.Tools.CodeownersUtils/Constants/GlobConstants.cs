using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.CodeownersUtils.Constants
{
    /// <summary>
    /// Contains the patterns and characters that are either special or invalid for a CODEOWNERS file as defined by GitHub documentation.
    /// https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-code-owners#codeowners-syntax
    /// There is one additional things that is disallowed and that is the question mark operator. This was disallowed by the original
    /// CodeownersParser.
    /// </summary>
    public class GlobConstants
    {
        // Escaping a pattern starting with # using \ so it is treated as a pattern and not a comment
        public const string EscapedPound = @"\#";
        // While the following are characters, they need to be string to be used in the ErrorMessageConstants
        // Using ! to negate a pattern
        public const string ExclamationMark = "!";
        // Using [ ] to define a character range
        public const string LeftBracket = "[";
        public const string RightBracket = "]";
        public const string QuestionMark = "?";


        // The following are all specical characters/glob patterns that need to be checked when
        // looking for unsupported character sequences.

        // Wildcarding file/directory names with a "*", outside of "/*" is invalid. The globber
        // won't be able to deal with them correctly. For directories, instead of /dirPartial*,
        // /dirPartial*/ should be used.
        public const string SingleAsterisk = "*";
        // A Single slash "/" is unsupported by GitHub
        public const string SingleSlash = "/";
        // Two asterisks is legal if surrounded by single slashes but, if not, it's otherwise
        // equivalent to a single star which is what should be used to avoid confusion.
        public const string TwoAsterisks = "**";
        // The suffix of "/**" is not supported because it is equivalent to "/". For example,
        // "/foo/**" is equivalent to "/foo/". One exception to this rule is if the entire 
        // path is "/**". The reason being is that GitHub doesn't match "/" to anything,
        // if it is the entire path but, instead, expects "/**".
        public const string SingleSlashTwoAsterisks = "/**";
        // "/**/" as a suffix is equivalent to the  suffix "/**" which is equivalent to the suffix "/"
        // which is what should be being used to avoid confusion
        public const string SingleSlashTwoAsterisksSingleSlash = "/**/";
    }
}
