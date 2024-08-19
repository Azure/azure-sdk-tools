// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using APIView.TreeToken;

namespace APIView.Model.V2
{
    /*** Review line object corresponds to each line displayed on API review. If an empty line is required then 
     * add a review line object without any token. */
    public class ReviewLine
    {
        /*** LineId is only required if we need to support commenting on a line that contains this token. 
         *  Usually code line for documentation or just punctuation is not required to have lineId. lineId should be a unique value within 
         *  the review token file to use it assign to review comments as well as navigation Id within the review page.
         *  for e.g Azure.Core.HttpHeader.Common, azure.template.template_main
         */
        public string LineId { get; set; }
        public string CrossLanguageId { get; set; }
        /*** list of tokens that constructs a line in API review */
        public List<ReviewToken> Tokens { get; set; } = [];
        /*** Add any child lines as children. For e.g. all classes and namespace level methods are added as a children of namespace(module) level code line. 
         *  Similarly all method level code lines are added as children of it's class code line.*/
        public List<ReviewLine> Children { get; set; } = [];
        /*** This is set if API is marked as hidden */
        public bool? IsHidden { get; set; }

        // Following properties are helper methods that's used to render review lines to UI required format.
        [JsonIgnore]
        public DiffKind DiffKind { get; set; } = DiffKind.NoneDiff;
        [JsonIgnore]
        public bool IsActiveRevisionLine = true;
        [JsonIgnore]
        public bool IsDocumentation => Tokens.Count > 0 && Tokens[0].IsDocumentation == true;
        [JsonIgnore]
        public bool IsEmpty => Tokens.Count == 0 || !Tokens.Any( t => t.SkipDiff != true);
        [JsonIgnore]
        public bool Processed { get; set; } = false;

        public void Add(ReviewToken token)
        {
            Tokens.Add(token);
        }

        public void RemoveSuffixSpace()
        {
            if (Tokens.Count > 0)
            {
                Tokens[Tokens.Count - 1].HasSuffixSpace = false;
            }
        }

        public void AddSuffixSpace()
        {
            if (Tokens.Count > 0)
            {
                Tokens[Tokens.Count - 1].HasSuffixSpace = true;
            }
        }

        public void GetApiText(StringBuilder sb, int indent = 0, bool skipDocs = true)
        {
            if (skipDocs && Tokens.Count > 0 && Tokens[0].IsDocumentation == true)
            {
                return;
            }

            //Add empty line in case of review line without tokens
            if (Tokens.Count == 0)
            {
                sb.Append(Environment.NewLine);
                return;
            }
            //Add spaces for indentation
            for (int i = 0; i < indent; i++)
            {
                sb.Append("    ");
            }
            //Process all tokens
            sb.Append(ToString(true));
            
            sb.Append(Environment.NewLine);
            foreach (var child in Children)
            {
                child.GetApiText(sb, indent + 1, skipDocs);
            }
        }

        private string ToString(bool includeAllTokens)
        {
            var filterdTokens = Tokens.Where(x => includeAllTokens || x.SkipDiff != true);
            if (!filterdTokens.Any())
            {
                return "";
            }
            StringBuilder sb = new();
            foreach (var token in filterdTokens)
            {
                sb.Append(token.Value);
                sb.Append(token.HasSuffixSpace == true ? " " : "");
            }
            return sb.ToString();
        }

        
        public override string ToString()
        {
            return ToString(false);
        }

        public override bool Equals(object obj)
        {
            if(obj is ReviewLine other)
            {
                return ToString() == other.ToString();
            }
            return false;
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public string GetTokenNodeIdHash(string parentNodeIdHash, int lineIndex)
        {
            var idPart = LineId;
            var token = Tokens.FirstOrDefault(t => t.RenderClasses.Count > 0);
            if (token != null)
            {
                idPart = $"{idPart}-{token.RenderClasses.First()}";
            }
            idPart = $"{idPart}-{lineIndex}-{DiffKind}";
            var hash = CreateHashFromString(idPart);
            return hash + parentNodeIdHash.Replace("nId", "").Replace("root", ""); // Append the parent node Id to ensure uniqueness
        }

        private string CreateHashFromString(string inputString)
        {
            int hash = HashCode.Combine(inputString);
            return "nId" + hash.ToString();
        }
    }
}
