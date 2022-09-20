using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class ApplyCondition
    {
        public string UriRegex
        {
            get => _uriRegex;
            set
            {
                StringSanitizer.ConfirmValidRegex(value);
                _uriRegex = value;
            }
        }

        private string _uriRegex;

        public HeaderCondition ResponseHeader { get; set; }

        public ApplyCondition() { }

        /// <summary>
        /// This constructor is used to abstract the creation of an ApplyCondition from API input.
        /// This is a separate function to allow context-sensitive setting. EG, if setting a condition,
        /// one of the trigger properties should be populated! This is a bit immaterial when only dealing with 
        /// a single property, but we might as well start this way.
        /// </summary>
        /// <param name="jsonElement">The contents of "condition" key with the body of a request to /Admin/AddSanitizer or /Admin/AddTransform.</param>
        public ApplyCondition(JsonElement jsonElement)
        {
            var conditionDefined = false;
            try
            {
                // URI condition
                var uriProp = HttpRequestInteractions.GetProp(nameof(UriRegex), jsonElement);

                if(uriProp.Value.ValueKind != JsonValueKind.Undefined)
                {
                    UriRegex = uriProp.Value.GetString();
                    conditionDefined = true;
                }

                // Response header condition
                var headerProp = HttpRequestInteractions.GetProp(nameof(ResponseHeader), jsonElement);
                if(headerProp.Value.ValueKind != JsonValueKind.Undefined)
                {
                    
                    ResponseHeader = JsonSerializer.Deserialize<HeaderCondition>(headerProp.Value.ToString(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (string.IsNullOrWhiteSpace(ResponseHeader.Key))
                    {
                        throw new ArgumentException("Key is required for response header conditions.");
                    }
                    conditionDefined = true;
                }


                // ... body condition support goes here.
            }
            catch(Exception e)
            {
                throw new HttpException(
                    HttpStatusCode.BadRequest,
                    $"An unexpected error occured during parse of condition body. Condition Definition: {jsonElement.GetRawText()}. Exception detail: {e.Message}."
                );
            }

            if (!conditionDefined)
            {
                throw new HttpException(
                    HttpStatusCode.BadRequest, 
                    $"This request defined a condition. The definition of said condition is invalid. At least one trigger regex must be present. Condition Definition: {jsonElement.GetRawText()}."
                );
            }
        }

        public bool IsApplicable(RecordEntry entry)
        {
            if (UriRegex != null)
            {
                if (!Regex.IsMatch(entry.RequestUri, UriRegex))
                {
                    return false;
                }
            }

            if (ResponseHeader != null)
            {
                if (!entry.Response.Headers.ContainsKey(ResponseHeader.Key))
                {
                    return false;
                }

                if (ResponseHeader.ValueRegex != null)
                {
                    foreach (string header in entry.Response.Headers[ResponseHeader.Key])
                    {
                        // if at least one header value matches, then the condition passes
                        if (Regex.IsMatch(header, ResponseHeader.ValueRegex))
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }

            return true;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<pre>{");

            sb.Append($"\n   \"UriRegex\": \"{UriRegex}\",");
            // ... body/header condition support goes here.

            sb.Append("\n}</pre>");
            return sb.ToString();
        }
    }
}
