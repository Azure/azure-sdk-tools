using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Sdk.Tools.CodeownersUtils.Errors;

namespace Azure.Sdk.Tools.CodeownersUtils.Utils
{
    /// <summary>
    /// Utilities for processing the CODEOWNERS baseline error file as well as processing errors
    /// based upon the contents of the file. The baseline error file is simply a text file that
    /// contains every error, on its own line. The errors have been deduped so any given error
    /// will only exist in the file once.
    /// </summary>
    public class BaselineUtils
    {
        private string _baselineFile = null;
        public BaselineUtils()
        {
        }
        public BaselineUtils(string baselineFile)
        {
            _baselineFile = Path.GetFullPath(baselineFile);
        }

        /// <summary>
        /// Given a list of errors for the repository, generate the baseline file. Note that the baseline only consists of
        /// single line errors, in other words errors that are for owners and labels, not blocks. The reason being is that
        /// these errors are deduped and block errors are thrown out while parsing. This means that if someone screws up
        /// a block and that error ends up in the deduped list, that someone else could add a new block with the same error
        /// that wouldn't get flagged. This would result in someone adding an incorrect block which would get tossed out in
        /// parsing and possibly prevent processing somewhere else because the entry isn't there. (for example adding labels
        /// to a PR based upon files paths wouldn't work if the block that the PRLabel was in was thrown out because it was
        /// malformed).
        /// </summary>
        /// <param name="errors">The list of errors</param>
        /// <returns>HashSet&lt;string&gt; containing the unique errors. Used in testing for verification.</returns>
        public void GenerateBaseline(List<BaseError> errors)
        {
            HashSet<string> uniqueErrors = new HashSet<string>();

            // Filter out block errors.
            var lineErrors = errors.OfType<SingleLineError>().ToList();

            // For each error get the error string and add it to the hash if
            // isn't already in there.
            foreach (var error in lineErrors)
            {
                foreach (string errorString in error.Errors)
                {
                    if (!uniqueErrors.Contains(errorString))
                    {
                        uniqueErrors.Add(errorString);
                    }
                }
            }

            // The HashSet will contain the unique list of errors and those
            // will be written out to the baseline file. If there are no errors
            // then this will cause an empty file to be written out. 
            using (var sw = new StreamWriter(_baselineFile))
            {
                foreach (string errorString in uniqueErrors)
                {
                    sw.WriteLine(errorString);
                }
            }
        }

        /// <summary>
        /// Given a list of errors from parsing the CODEOWNERS file, trim it down to only the
        /// errors that don't already exist in the baseline.
        /// </summary>
        /// <param name="errors">List&lt;BaseError&gt; from parsing the CODEOWNERS file.</param>
        /// <returns>List&lt;BaseError&gt; representing the list of errors not in the baseline.</returns>
        public List<BaseError> FilterErrorsUsingBaseline(List<BaseError> errors)
        {
            List<BaseError> remainingErrors = new List<BaseError>();
            HashSet<string> uniqueErrors = new HashSet<string>();
            using (var sr = new StreamReader(_baselineFile))
            {
                while (sr.ReadLine() is { } line)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        uniqueErrors.Add(line);
                    }
                }
            }

            // For each error look at the error strings and see if they're already in
            // the list of errors, if so remove them. The reason why ToList is being
            // used here is to ensure that the original list of errors isn't modified.
            foreach (var error in errors.ToList())
            {
                // Block formatting errors will not be filtered out
                if (error is BlockFormattingError)
                {
                    remainingErrors.Add(error);
                    continue;
                }

                // This might look odd, to use ToList on something that's already a
                // list but doing this causes the compiler to create a copy of the list
                // so the original can be modified without the "Collection was modified;
                // Enumeration operation might not execute."
                foreach (var errorString in error.Errors.ToList())
                {
                    if (uniqueErrors.Contains(errorString))
                    {
                        error.Errors.Remove(errorString);
                    }
                }
                // If there are any errors left then add this error to the remainingErrors list
                if (error.Errors.Count > 0)
                {
                    remainingErrors.Add(error);
                }
            }
            return remainingErrors;
        }
    }
}
