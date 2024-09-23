using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb.MiddleWare
{
    public class SwaggerAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _config;

        public SwaggerAuthMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            _config = config;
        }
        public async Task InvokeAsync(HttpContext context)
        {
            // Extends Authentication to the Swagger UI
            if (context.Request.Path.StartsWithSegments("/swagger"))
            {
                if (!context.User.Identity.IsAuthenticated)
                {
                    context.Response.StatusCode = 302;
                    context.Response.Headers["Location"] = "/Login";
                }
                else
                {
                    await _next.Invoke(context);
                }
            }
            else
            {
                await _next.Invoke(context);
            }
            
        }
    }
}
