using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class HttpException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public HttpException()
        {
            this.StatusCode = HttpStatusCode.InternalServerError;
        }

        public HttpException(HttpStatusCode statusCode)
        {
            this.StatusCode = statusCode;
        }

        public HttpException(int httpStatusCode)
            : this((HttpStatusCode)httpStatusCode)
        {
        }

        public HttpException(HttpStatusCode statusCode, string message)
            : base(message)
        {
            this.StatusCode = statusCode;
        }

        public HttpException(HttpStatusCode statusCode, string message, Exception innerException) : base(message, innerException)
        {
            this.StatusCode = statusCode;
        }

        protected HttpException(HttpStatusCode statusCode, SerializationInfo info, StreamingContext context) : base(info, context)
        {
            this.StatusCode = statusCode;
        }

    }
}
