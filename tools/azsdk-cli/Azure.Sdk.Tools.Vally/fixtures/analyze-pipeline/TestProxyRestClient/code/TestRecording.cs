// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core.Pipeline;
using Azure.Core.TestFramework.Models;
using Azure.Core.Tests.TestFramework;

namespace Azure.Core.TestFramework
{
    public class TestRecording : IAsyncDisposable
    {
        private const string RandomSeedVariableKey = "RandomSeed";
        private const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        // cspell: disable-next-line
        private const string charsLower = "abcdefghijklmnopqrstuvwxyz0123456789";
        private const string Sanitized = "Sanitized";
        internal const string DateTimeOffsetNowVariableKey = "DateTimeOffsetNow";

        public SortedDictionary<string, string> Variables => _variables;
        private SortedDictionary<string, string> _variables = new();

        private TestRecording(RecordedTestMode mode, string sessionFile, TestProxy proxy, RecordedTestBase recordedTestBase)
        {
            Mode = mode;
            _sessionFile = sessionFile;
            _proxy = proxy;
            _recordedTestBase = recordedTestBase;
        }

        public static async Task<TestRecording> CreateAsync(RecordedTestMode mode, string sessionFile, TestProxy proxy, RecordedTestBase recordedTestBase)
        {
            var recording = new TestRecording(mode, sessionFile, proxy, recordedTestBase);
            await recording.InitializeProxySettingsAsync();
            return recording;
        }

        internal async Task InitializeProxySettingsAsync()
        {
            var assetsJson = _recordedTestBase.AssetsJsonPath;

            switch (Mode)
            {
                case RecordedTestMode.Record:
                    var recordResponse = await _proxy.Client.StartRecordAsync(new StartInformation(_sessionFile) { XRecordingAssetsFile = assetsJson });
                    RecordingId = recordResponse.Headers.XRecordingId;
                    await ApplySanitizersAsync();

                    break;
                case RecordedTestMode.Playback:
                    ResponseWithHeaders<IReadOnlyDictionary<string, string>, TestProxyStartPlaybackHeaders> playbackResponse = null;
                    try
                    {
                        playbackResponse = await _proxy.Client.StartPlaybackAsync(new StartInformation(_sessionFile) { XRecordingAssetsFile = assetsJson });
                    }
                    catch (RequestFailedException ex)
                        when (ex.Status == 404)
                    {
                        // We don't throw the exception here because Playback only tests that are testing the
                        // recording infrastructure itself will not have session records.
                        MismatchException = new TestRecordingMismatchException(ex.Message, ex);
                        return;
                    }

                    _variables = new SortedDictionary<string, string>((Dictionary<string, string>)playbackResponse.Value);
                    RecordingId = playbackResponse.Headers.XRecordingId;
                    await ApplySanitizersAsync();

                    // temporary until Azure.Core fix is shipped that makes HttpWebRequestTransport consistent with HttpClientTransport
                    var excludedHeaders = new List<string>(_recordedTestBase.LegacyExcludedHeaders)
                    {
                        "Content-Type",
                        "Content-Length",
                        "Connection"
                    };

                    await _proxy.Client.AddCustomMatcherAsync(new CustomDefaultMatcher
                    {
                        ExcludedHeaders = string.Join(",", excludedHeaders),
                        IgnoredHeaders = _recordedTestBase.IgnoredHeaders.Count > 0 ? string.Join(",", _recordedTestBase.IgnoredHeaders) : null,
                        IgnoredQueryParameters = _recordedTestBase.IgnoredQueryParameters.Count > 0 ? string.Join(",", _recordedTestBase.IgnoredQueryParameters) : null,
                        CompareBodies = _recordedTestBase.CompareBodies,
                        IgnoreQueryOrdering = true
                    });

                    foreach (HeaderTransform transform in _recordedTestBase.HeaderTransforms)
                    {
                        await _proxy.Client.AddHeaderTransformAsync(transform, RecordingId);
                    }
                    break;
            }
        }

        // ... (sanitizer application, TestRandom, Now/UtcNow helpers, and disable-
        //      recording scope elided for fixture brevity; the playback start that
        //      triggers the test-proxy 500 lives in InitializeProxySettingsAsync) ...

        public string RecordingId { get; private set; }
    }
}
