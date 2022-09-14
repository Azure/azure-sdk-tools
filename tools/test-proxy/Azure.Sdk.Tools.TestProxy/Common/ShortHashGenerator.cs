using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class ShortHashGenerator
    {
        /// <summary>
        /// Given a string, UTF8 encode it and create the hash using SHA1. Convert the resulting string to Base64
        /// and return a string consisting of only letters or digits of the requested length (default is 10). 
        /// This isn't supposed to be perfect, it's supposed to be good enough. The primary usage of this is to 
        /// shorten the local paths of the AssetsRepoLocation
        /// </summary>
        /// <param name="inputString">The string to generate the short hash from.</param>
        /// <param name="returnHashLength">The length of the returned hash, default is 10</param>
        /// <returns></returns>
        public static string GenerateShortHash(string inputString, int returnHashLength=10)
        {
            // 28 is the max length base64 string, chances are it'll be shorter once the non-lettersOrDigits are
            // stripped away but ensure that the user isn't trying to get something longer than what could
            // possibly be produced
            if (returnHashLength < 1 || returnHashLength > 28)
            {
                throw new ArgumentException($"returnHashLength must be > 1 and <= 28");
            }

            byte[] hash = SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(inputString));
            // Converting the hash to Hex will produce 40 character which is quite a bit longer than we actually want to add to
            // the path. Converting the hash to Base64 produces 28 characters, which is still too long, and also has the potential
            // to have non-alphanumeric characters which wouldn't be okay in a directory name. Take the Base64 string, grab only
            // letters and digits and then, only grab the first 10 characters. This should be reasonably unique for the scenarios
            // in which we're using it.
            string returnString = String.Concat(Convert.ToBase64String(hash).ToCharArray().Where(char.IsLetterOrDigit).Take(returnHashLength));

            int strLen = returnString.Length;
            // Take won't throw, if the count is greater than the number of items in the string it'll return the entire string.
            // If an input string is unable to produce a short hash if the requested length ensure the input string and it's
            // resulting value/length are reported clearly and correctly.
            if (strLen < returnHashLength)
            {
                throw new ArgumentException($"GenerateShortHash of {inputString} does not produce a return string of the required return hash length. Return value: {returnString}, returnHashLength: {returnHashLength}");
            }

            return returnString;
        }
    }
}
