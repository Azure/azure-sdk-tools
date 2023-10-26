using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Constants;

namespace Azure.Sdk.Tools.CodeownersUtils.Utils
{
    /// <summary>
    /// Utility class to detect and parse monikers from CODEOWNERS lines.
    /// </summary>
    public static class MonikerUtils
    {
        /// <summary>
        /// Given a CODEOWNERS line, parse the moniker from the line if one exists.
        /// </summary>
        /// <param name="line">The CODEOWNERS line to parse.</param>
        /// <returns>String, the moniker if there was one on the line, null otherwise.</returns>
        public static string ParseMonikerFromLine(string line)
        {
            if (line.StartsWith(SeparatorConstants.Comment))
            {
                // Strip off the starting # and trim the result. Note, replacing tabs with
                // spaces isn't necessary as Trim would trim off any leading or trailing tabs.
                string strippedLine = line.Substring(1).Trim();
                var monikers = typeof(MonikerConstants)
                              .GetFields(BindingFlags.Public | BindingFlags.Static)
                              .Where(field => field.IsLiteral)
                              .Where(field => field.FieldType == typeof(string))
                              .Select(field => field.GetValue(null) as string);
                foreach (string tempMoniker in monikers)
                {
                    // Line starts with "<Moniker>:", unfortunately /<NotInRepo>/ has no colon and needs
                    // to be checked separately
                    if (strippedLine.StartsWith($"{tempMoniker}{SeparatorConstants.Colon}"))
                    {
                        return tempMoniker;
                    }
                }
                // Special case for the /<NotInRepo>/ moniker which has no colon
                if (strippedLine.StartsWith($"{MonikerConstants.MissingFolder}"))
                {
                    return MonikerConstants.MissingFolder;
                }
            }
            // Anything that doesn't match an existing moniker is treated as a comment
            return null;
        }

        /// <summary>
        /// Check whether a line is one of our Monikers.
        /// </summary>
        /// <param name="line">string, the line to check</param>
        /// <returns>true if the line contains a moniker, false otherwise</returns>
        public static bool IsMonikerLine(string line)
        {
            if (line.StartsWith(SeparatorConstants.Comment))
            {
                // Strip off the #
                string strippedLine = line.Substring(1).Replace('\t', ' ').Trim();
                var monikers = typeof(MonikerConstants)
                              .GetFields(BindingFlags.Public | BindingFlags.Static)
                              .Where(field => field.IsLiteral)
                              .Where(field => field.FieldType == typeof(string))
                              .Select(field => field.GetValue(null) as string);
                foreach (string tempMoniker in monikers)
                {
                    // Line starts with "<Moniker>:", unfortunately /<NotInRepo>/ has no colon and needs
                    // to be checked separately
                    if (strippedLine.StartsWith($"{tempMoniker}{SeparatorConstants.Colon}"))
                    {
                        return true;
                    }
                }
                // Special case for the /<NotInRepo>/ moniker which has no colon
                if (strippedLine.StartsWith($"{MonikerConstants.MissingFolder}"))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
