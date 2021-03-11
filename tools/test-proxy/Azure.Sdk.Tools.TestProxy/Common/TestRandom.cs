// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public class TestRandom : Random
    {
        private readonly RecordedTestMode _mode;

        public TestRandom(RecordedTestMode mode, int seed) :
            base(seed)
        {
            _mode = mode;
        }

        public TestRandom(RecordedTestMode mode) :
            base()
        {
            _mode = mode;
        }

        public Guid NewGuid()
        {
            if (_mode == RecordedTestMode.Live)
            {
                return Guid.NewGuid();
            }
            else
            {
                var bytes = new byte[16];
                NextBytes(bytes);
                return new Guid(bytes);
            }
        }
    }
}
