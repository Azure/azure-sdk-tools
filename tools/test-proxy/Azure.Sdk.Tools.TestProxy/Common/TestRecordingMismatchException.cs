// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Runtime.Serialization;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    [Serializable]
    public class TestRecordingMismatchException : Exception
    {
        public TestRecordingMismatchException()
        {
        }

        public TestRecordingMismatchException(string message) : base(message)
        {
        }

        public TestRecordingMismatchException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected TestRecordingMismatchException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
