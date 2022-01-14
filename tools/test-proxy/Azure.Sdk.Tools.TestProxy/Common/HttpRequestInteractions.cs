// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class HttpRequestInteractions
    {

        public static JsonProperty GetProp(string name, JsonElement jsonElement)
        {
            return jsonElement.EnumerateObject()
                        .FirstOrDefault(p => string.Compare(p.Name, name, StringComparison.OrdinalIgnoreCase) == 0);
        }

        public async static Task<string> GetBodyKey(HttpRequest req, string key, bool allowNulls = false)
        {
            string value = null;
            var document = await GetBody(req);

            if(document != null)
            {
                var recordingFile = GetProp(key, document.RootElement);

                if (recordingFile.Value.ValueKind != JsonValueKind.Undefined)
                {
                    value = recordingFile.Value.GetString();
                }
                else
                {
                    if (!allowNulls)
                    {
                        throw new HttpException(HttpStatusCode.BadRequest, $"Failed attempting to retrieve value from request body. Targeted key was: {key}. Raw body value was {document.RootElement.GetRawText()}.");
                    }
                }
            }
            
            return value;
        }

        public async static Task<JsonDocument> GetBody(HttpRequest req)
        {
            if (req.ContentLength > 0)
            {
                try
                {
                    var result = await JsonDocument.ParseAsync(req.Body, options: new JsonDocumentOptions() { AllowTrailingCommas = true });
                    return result;
                }
                catch (Exception e)
                {
                    req.Body.Position = 0;
                    using (StreamReader readstream = new StreamReader(req.Body, Encoding.UTF8))
                    {
                        string bodyContent = readstream.ReadToEnd();
                        throw new HttpException(HttpStatusCode.BadRequest, $"The body of this request is invalid JSON. Content: { bodyContent }. Exception detail: {e.Message}");
                    }
                }
            }

            return null;
        }
    }
}
