using System;
using System.Collections.Generic;
using System.Text;

namespace APIView
{
    public class Token
    {
        public string DisplayString { get; set; }
        public TypeReference Type { get; set; }

        public Token()
        {
            this.DisplayString = "";
            this.Type = TypeReference.SpecialType;
        }
        
        public Token(string displayString, TypeReference type)
        {
            this.DisplayString = displayString;
            this.Type = type;
        }
    }
}
