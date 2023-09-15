using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace APIViewWeb.Helpers
{ 
    public class LeanJsonResult : JsonResult
    {
        private readonly int _statusCode;
        public LeanJsonResult(object value, int statusCode) : base(value)
        {
            _statusCode = statusCode;
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var response = context.HttpContext.Response;

            response.ContentType = !string.IsNullOrEmpty(ContentType) ? ContentType : "application/json";
            response.StatusCode = _statusCode;

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };

            await JsonSerializer.SerializeAsync(response.Body, Value, options);
        }
    }
}
