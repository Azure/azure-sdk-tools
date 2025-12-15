using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace APIViewWeb.MiddleWare
{
    public class SwaggerAuthMiddleware
    {
        private readonly RequestDelegate _next;

        public SwaggerAuthMiddleware(RequestDelegate next)
        {
            _next = next;
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
