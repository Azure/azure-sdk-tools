using System;
using System.Text.Encodings.Web;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class RecordSessionEncoder : JavaScriptEncoder
    {
        private char[] PreservedSymbols = new char[]
        {
            '+'
        };

        private JavaScriptEncoder defaultEncoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

        public override int MaxOutputCharactersPerInputCharacter => 1;

        public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
        {

            return defaultEncoder.FindFirstCharacterToEncode(text, textLength);
        }

        public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
        {
            // Preserve the '+' character without encoding
            if ((char)unicodeScalar == '+')
            {
                if (bufferLength < 1)
                {
                    numberOfCharactersWritten = 0;
                    return false;
                }

                buffer[0] = '+';
                numberOfCharactersWritten = 1;
                return false;
            }

            // For all other characters, delegate to the default encoder
            return defaultEncoder.TryEncodeUnicodeScalar(unicodeScalar, buffer,bufferLength, out numberOfCharactersWritten);
        }

        public override bool WillEncode(int unicodeScalar)
        {
            return defaultEncoder.WillEncode(unicodeScalar);
        }
    }
}
