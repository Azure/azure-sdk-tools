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
    /// <summary>
    /// Review line object corresponds to each line displayed on API review. If an empty line is required then add a review line object without any token.
    /// </summary>
    public class ReviewLine
    {
        /// <summary>
        /// LineId is only required if we need to support commenting on a line that contains this token. 
        /// Usually code line for documentation or just punctuation is not required to have lineId.lineId should be a unique value within
        /// the review token file to use it assign to review comments as well as navigation Id within the review page.        /// for e.g Azure.Core.HttpHeader.Common, azure.template.template_main
        /// </summary>
        public string LineId { get; set; }
        public string CrossLanguageId { get; set; }
        /// <summary>
        /// List of tokens that constructs a line in API review
        /// </summary>
        public List<ReviewToken> Tokens { get; set; } = [];
        /// <summary>
        /// Add any child lines as children. For e.g. all classes and namespace level methods are added as a children of namespace(module) level code line.
        /// Similarly all method level code lines are added as children of it's class code line.
        /// </summary>
        public List<ReviewLine> Children { get; set; } = [];
        /// <summary>
        /// This is set if API is marked as hidden
        /// </summary>
        public bool? IsHidden { get; set; }
        /// <summary>
        /// This is set if a line is end of context. For e.g. end of a class or name space line "}"
        /// </summary>
        public bool? IsContextEndLine { get; set; }
        /// <summary>
        /// This is to set a line as related to another line. So when a related line is hidden in node or tree view then current line will also be hidden
        /// for e.g. an attribute line or notation line will be set as related to that API or class line.
        /// </summary>
        public string RelatedToLine { get; set; }
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

        [JsonIgnore]
        public int Indent {  get; set; }
        [JsonIgnore]
        public ReviewLine parentLine { get; set; }
        public void AddToken(ReviewToken token)
        {
            Tokens.Add(token);
        }

        public void AppendApiTextToBuilder(StringBuilder sb, int indent = 0, bool skipDocs = true, int lineIndentSpaces = 4)
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
                for(int j = 0; j < lineIndentSpaces; j++)
                {
                    sb.Append(" ");
                }
            }
            //Process all tokens
            sb.Append(ToString(true));
            
            sb.Append(Environment.NewLine);
            foreach (var child in Children)
            {
                child.AppendApiTextToBuilder(sb, indent + 1, skipDocs, lineIndentSpaces);
            }
        }

        private string ToString(bool includeAllTokens)
        {
            var filterdTokens = includeAllTokens ? Tokens: Tokens.Where(x => x.SkipDiff != true);
            if (!filterdTokens.Any())
            {
                return string.Empty;
            }
            StringBuilder sb = new();
            bool spaceAdded = false;
            foreach (var token in filterdTokens)
            {
                sb.Append(token.HasPrefixSpace == true && !spaceAdded ? " " : string.Empty);
                sb.Append(token.Value);
                sb.Append(token.HasSuffixSpace == true ? " " : string.Empty);
                spaceAdded = token.HasSuffixSpace == true;
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

        private static string CreateHashFromString(string inputString)
        {
            int hash = inputString.GetHashCode();
            return "nId" + hash.ToString();
        }
    }
}
