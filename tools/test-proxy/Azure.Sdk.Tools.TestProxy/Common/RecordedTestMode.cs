// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public enum RecordedTestMode
    {
        Live,
        Record,
        Playback,
        RemotePlayback,
        RemoteRecord,

        // Backcompat with Track 1
        None = Live
    }
}
