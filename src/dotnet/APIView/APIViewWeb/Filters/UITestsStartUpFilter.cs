using APIViewWeb.MiddleWare;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System;

namespace APIViewWeb.Filters
{
    public class UITestsStartUpFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return builder =>
            {
                builder.UseMiddleware<UITestsMiddleWare>();
                next(builder);
            };
        }
    }
}
