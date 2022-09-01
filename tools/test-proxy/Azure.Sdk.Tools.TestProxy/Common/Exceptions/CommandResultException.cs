using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Common.Exceptions
{
    public class HttpException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public HttpException()
        {
            StatusCode = HttpStatusCode.InternalServerError;
        }

        public HttpException(HttpStatusCode statusCode)
        {
            StatusCode = statusCode;
        }

        public HttpException(int httpStatusCode)
            : this((HttpStatusCode)httpStatusCode)
        {
        }

        public HttpException(HttpStatusCode statusCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
        }

        public HttpException(HttpStatusCode statusCode, string message, Exception innerException) : base(message, innerException)
        {
            StatusCode = statusCode;
        }

        protected HttpException(HttpStatusCode statusCode, SerializationInfo info, StreamingContext context) : base(info, context)
        {
            StatusCode = statusCode;
        }

    }
}
