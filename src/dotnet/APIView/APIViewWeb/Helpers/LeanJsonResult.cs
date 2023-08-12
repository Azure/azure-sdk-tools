using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Mvc;

namespace APIViewWeb.Helpers
{
    public class LeanJsonResult : JsonResult
    {
        public LeanJsonResult(object value) : base(value)
        {
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var response = context.HttpContext.Response;

            response.ContentType = !string.IsNullOrEmpty(ContentType) ? ContentType : "application/json";

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };

            await JsonSerializer.SerializeAsync(response.Body, Value, options);
        }
    }
}
