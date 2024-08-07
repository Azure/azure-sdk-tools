using System.Text.Json.Serialization;
using System.Text.Json;
using APIViewWeb.Helpers;
using Microsoft.AspNetCore.Http;

namespace APIViewWeb.Extensions
{
    public static class HttpExtensions
    {
        public static void AddPaginationHeader(this HttpResponse response, PaginationHeader header)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            response.Headers.Append("Pagination", JsonSerializer.Serialize(header, options));
            response.Headers.Append("Access-Control-Expose-Headers", "Pagination");
        }
    }
}
