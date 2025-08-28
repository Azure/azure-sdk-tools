using System;
using System.Collections.Generic;

namespace APIViewWeb.Exceptions
{
    public class DuplicateLineIdException : Exception
    {
        public string Language { get; }
        public List<string> DuplicateLineIds { get; }

        public DuplicateLineIdException(string language, List<string> duplicateLineIds) 
            : base($"APIView unexpectedly received duplicate line identifiers (IDs: '{string.Join(", ", duplicateLineIds)}')." +
                   $"Please contact the developer of the {language} APIView parser.")
        {
            Language = language;
            DuplicateLineIds = duplicateLineIds;
        }
    }
}
