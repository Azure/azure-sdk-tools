using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using System;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AllowEmptyBodyAttribute : Attribute
    {
    }

    public class EmptyBodyFormatter : IInputFormatter
    {
        public bool CanRead(InputFormatterContext context)
        {
            var endpoint = context.HttpContext.GetEndpoint();
            return endpoint?.Metadata.GetMetadata<AllowEmptyBodyAttribute>() is not null &&
                context.HttpContext.Request.ContentLength == 0;
        }

        public Task<InputFormatterResult> ReadAsync(InputFormatterContext context)
        {
            return InputFormatterResult.SuccessAsync(null);
        }
    }
}
