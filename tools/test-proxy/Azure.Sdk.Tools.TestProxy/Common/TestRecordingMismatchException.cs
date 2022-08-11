// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Runtime.Serialization;
using Azure.Sdk.Tools.TestProxy.Common.Exceptions;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    [Serializable]
    public class TestRecordingMismatchException : HttpException
    {
        public TestRecordingMismatchException() : base(HttpStatusCode.NotFound)
        {
        }

        public TestRecordingMismatchException(string message) : base(HttpStatusCode.NotFound, message)
        {
        }

        public TestRecordingMismatchException(string message, Exception innerException) : base(HttpStatusCode.NotFound, message, innerException)
        {
        }

        protected TestRecordingMismatchException(SerializationInfo info, StreamingContext context) : base(HttpStatusCode.NotFound, info, context)
        {
        }
    }
}
