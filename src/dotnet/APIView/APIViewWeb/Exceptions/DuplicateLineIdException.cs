using System;

namespace APIViewWeb.Exceptions
{
    public class DuplicateLineIdException : Exception
    {
        public string Language { get; }
        public string DuplicateLineId { get; }

        public DuplicateLineIdException(string language, string duplicateLineId) 
            : base($"API review generation failed due to a language parser error. " +
                   $"The parser generated duplicate line identifiers (IDs: '{duplicateLineId}'), which indicates " +
                   $"an issue in the language-specific parser for {language}.")
        {
            Language = language;
            DuplicateLineId = duplicateLineId;
        }
    }
}
