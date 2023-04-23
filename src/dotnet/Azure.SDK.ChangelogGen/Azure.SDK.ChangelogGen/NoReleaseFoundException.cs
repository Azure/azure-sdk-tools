// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.SDK.ChangelogGen
{
    internal class NoReleaseFoundException : Exception
    {
        public NoReleaseFoundException(string message)
            : base(message)
        {
        }
    }
}
