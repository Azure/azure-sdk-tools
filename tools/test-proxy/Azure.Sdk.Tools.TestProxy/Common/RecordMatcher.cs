// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Web;
using Azure.Core;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    /// <summary>
    /// The default matcher. Matches a request against the set of recorded requests during playback by comparing RequestUri, Headers, and Body.
    ///
    /// Excludes the following headers while comparing: Date, x-ms-date, x-ms-client-request-id, User-Agent, Request-Id, and traceparent.
    /// </summary>
    public class RecordMatcher
    {
        // Headers that are normalized by HttpClient
        private HashSet<string> _normalizedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Accept",
            "Content-Type"
        };

        /// <summary>
        /// When true, message bodies are compared during matching. In addition, when true incoming request bodies will be sanitized.
        /// </summary>
        public bool ShouldCompareBodies { get; private set; }

        /// <summary>
        /// When true, query parameter ordering is ignored during URI normalization.
        /// </summary>
        public bool ShouldIgnoreQueryOrdering { get; private set; }

        public RecordMatcher(bool compareBodies = true, bool ignoreQueryOrdering = false)
        {
            ShouldCompareBodies = compareBodies;
            ShouldIgnoreQueryOrdering = ignoreQueryOrdering;
        }

        /// <summary>
        /// Headers that will be entirely ignored during matching.
        /// </summary>
        public HashSet<string> ExcludeHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Request-Id",
            "traceparent",
        };


        /// <summary>
        /// Headers whose CONTENT will be ignored during matching, but whose PRESENCE will still be checked for on both request and record sides.
        /// </summary>
        public HashSet<string> IgnoredHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Date",
            "x-ms-date",
            "x-ms-client-request-id",
            "x-ms-client-id",
            "User-Agent",
            "x-ms-useragent",
            "x-ms-version",
            "If-None-Match",
            "sec-ch-ua",
            "sec-ch-ua-mobile",
            "sec-ch-ua-platform",
            "Referrer",
            "Referer",
            "Origin",
            "Content-Length"
        };

        /// <summary>
        /// Query parameters whose values can change between recording and playback without causing URI matching
        /// to fail. The presence or absence of the query parameter itself is still respected in matching.
        /// </summary>
        public HashSet<string> IgnoredQueryParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
        };

        private const string VolatileValue = "Volatile";

        public virtual RecordEntry FindMatch(RecordEntry request, IList<RecordEntry> entries)
        {
            int bestScore = int.MaxValue;
            RecordEntry bestScoreEntry = null;

            foreach (RecordEntry entry in entries)
            {
                int score = 0;

                var uri = request.RequestUri;
                var recordRequestUri = entry.RequestUri;
                if (entry.IsTrack1Recording)
                {
                    //there's no domain name for request uri in track 1 record, so add it from request uri
                    int len = 8; //length of "https://"
                    int domainEndingIndex = uri.IndexOf('/', len);
                    if (domainEndingIndex > 0)
                    {
                        recordRequestUri = uri.Substring(0, domainEndingIndex) + recordRequestUri;
                    }
                }

                if (!AreUrisSame(recordRequestUri, uri))
                {
                    score++;
                }

                if (entry.RequestMethod != request.RequestMethod)
                {
                    score++;
                }

                //we only check Uri + RequestMethod for track1 record
                if (!entry.IsTrack1Recording)
                {
                    score += CompareHeaderDictionaries(request.Request.Headers, entry.Request.Headers, IgnoredHeaders, ExcludeHeaders);

                    request.Request.TryGetContentType(out var requestContentType);
                    entry.Request.TryGetContentType(out var recordContentType);
                    ContentTypeUtilities.TryGetTextEncoding(requestContentType, out var encoding);
                    score += CompareBodies(request.Request.Body, entry.Request.Body, requestContentType, recordContentType, encoding, descriptionBuilder: null);
                }

                if (score == 0)
                {
                    return entry;
                }

                if (score < bestScore)
                {
                    bestScoreEntry = entry;
                    bestScore = score;
                }
            }

            throw new TestRecordingMismatchException(GenerateException(request, bestScoreEntry, entries));
        }

        public virtual int CompareBodies(byte[] requestBody, byte[] recordBody, string requestContentType, string recordContentType, Encoding encoding, StringBuilder descriptionBuilder = null)
        {
            if (!ShouldCompareBodies)
            {
                return 0;
            }

            if (requestBody == null && recordBody == null)
            {
                return 0;
            }

            if (requestBody == null)
            {
                descriptionBuilder?.AppendLine("Record has body but request doesn't");
                return 1;
            }

            if (recordBody == null)
            {
                descriptionBuilder?.AppendLine("Request has body but record doesn't");
                return 1;
            }

            if (ContentTypeUtilities.IsMultiPart(requestContentType, out var requestBoundary) &&
                ContentTypeUtilities.IsMultiPart(recordContentType, out var recordBoundary) &&
                !requestBoundary.AsSpan().SequenceEqual(recordBoundary.AsSpan()))
            {
                encoding ??= Encoding.UTF8;

                ReadOnlySequence<byte> requestMinusBoundary = MultipartUtilities.RemoveBoundaries(requestBody, encoding.GetBytes(requestBoundary), encoding);
                ReadOnlySequence<byte> recordMinusBoundary = MultipartUtilities.RemoveBoundaries(recordBody, encoding.GetBytes(recordBoundary), encoding);

                if (!requestMinusBoundary.SequenceEqual(recordMinusBoundary, out int index, out int length))
                {
                    descriptionBuilder?.AppendLine("Multipart bodies differ after removing boundaries.");
                    if (length > 0)
                    {
                        descriptionBuilder?.AppendLine($"First difference inside the snippet from {index} to {length}.");
                        descriptionBuilder?.AppendLine($"Request snippet: \"{requestMinusBoundary.SliceToString(index, Math.Min(length, requestMinusBoundary.Length - index), encoding)}\"");
                        descriptionBuilder?.AppendLine($"Record snippet:  \"{recordMinusBoundary.SliceToString(index, Math.Min(length, recordMinusBoundary.Length - index), encoding)}\"");
                    }
                    return 1;
                }
                return 0;
            }

            if (!requestBody.SequenceEqual(recordBody))
            {
                // we just failed sequence equality, before erroring, lets check if we're a json body and check for property equality
                if (!string.IsNullOrWhiteSpace(requestContentType) && requestContentType.Contains("json"))
                {
                    var jsonDifferences = JsonComparer.CompareJson(requestBody, recordBody);

                    if (jsonDifferences.Count > 0)
                    {

                        if (descriptionBuilder != null)
                        {
                            descriptionBuilder.AppendLine($"There are differences between request and recordentry bodies:");
                            foreach (var jsonDifference in jsonDifferences)
                            {
                                descriptionBuilder.AppendLine(jsonDifference);
                            }
                        }

                        return 1;
                    }
                }
                else
                {
                    if (descriptionBuilder != null)
                    {
                        var minLength = Math.Min(requestBody.Length, recordBody.Length);
                        int i;
                        for (i = 0; i < minLength - 1; i++)
                        {
                            if (requestBody[i] != recordBody[i])
                            {
                                break;
                            }
                        }
                        descriptionBuilder.AppendLine($"Request and record bodies do not match at index {i}:");
                        var before = Math.Max(0, i - 10);
                        var afterRequest = Math.Min(i + 20, requestBody.Length);
                        var afterResponse = Math.Min(i + 20, recordBody.Length);
                        descriptionBuilder.AppendLine($"     request: \"{Encoding.UTF8.GetString(requestBody, before, afterRequest - before)}\"");
                        descriptionBuilder.AppendLine($"     record:  \"{Encoding.UTF8.GetString(recordBody, before, afterResponse - before)}\"");
                    }
                    return 1;
                }
            }

            return 0;
        }

        private bool AreUrisSame(string entryUri, string otherEntryUri) =>
            NormalizeUri(entryUri) == NormalizeUri(otherEntryUri);

        private void AddQueriesToUri(RequestUriBuilder req, IEnumerable<string> accessKeySet, NameValueCollection queryParams)
        {
            foreach (string param in accessKeySet)
            {
                req.AppendQuery(
                    param,
                    IgnoredQueryParameters.Contains(param) ? VolatileValue : queryParams[param]);
            }
        }

        private string NormalizeUri(string uriToNormalize)
        {
            var req = new RequestUriBuilder();
            var uri = new Uri(uriToNormalize);
            req.Reset(uri);
            req.Query = "";
            NameValueCollection queryParams = HttpUtility.ParseQueryString(uri.Query);

            if (ShouldIgnoreQueryOrdering)
            {
                AddQueriesToUri(req, queryParams.AllKeys.OrderBy(x => x), queryParams);
            }
            else
            {
                AddQueriesToUri(req, queryParams.AllKeys, queryParams);
            }

            return req.ToUri().ToString();
        }

        private string GenerateException(RecordEntry request, RecordEntry bestScoreEntry, IList<RecordEntry> entries = null)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Unable to find a record for the request {request.RequestMethod} {request.RequestUri}");

            if (bestScoreEntry == null)
            {
                builder.AppendLine("No records to match.");
                return builder.ToString();
            }

            if (request.RequestMethod != bestScoreEntry.RequestMethod)
            {
                builder.AppendLine($"Method doesn't match, request <{request.RequestMethod}> record <{bestScoreEntry.RequestMethod}>");
            }

            if (!AreUrisSame(request.RequestUri, bestScoreEntry.RequestUri))
            {
                builder.AppendLine("Uri doesn't match:");
                builder.AppendLine($"    request <{request.RequestUri}>");
                builder.AppendLine($"    record  <{bestScoreEntry.RequestUri}>");
            }

            builder.AppendLine("Header differences:");

            CompareHeaderDictionaries(request.Request.Headers, bestScoreEntry.Request.Headers, IgnoredHeaders, ExcludeHeaders, builder);

            builder.AppendLine("Body differences:");

            request.Request.TryGetContentType(out var requestContentType);
            bestScoreEntry.Request.TryGetContentType(out var recordContentType);
            ContentTypeUtilities.TryGetTextEncoding(requestContentType, out var encoding);
            CompareBodies(request.Request.Body, bestScoreEntry.Request.Body, requestContentType, recordContentType, encoding, descriptionBuilder: builder);

            if (entries != null && entries.Count >= 1)
            {
                builder.AppendLine("Remaining Entries:");
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    builder.AppendLine($"{i}: {entry.RequestUri}");
                }
            }
            return builder.ToString();
        }

        private string JoinHeaderValues(string[] values)
        {
            return string.Join(",", values);
        }

        private string[] RenormalizeContentHeaders(string[] values)
        {
            return new[] {
                string.Join(", ", values
                    .Select(value =>
                        string.Join(", ", value.Split(',').Select(part => part.Trim())))
                    .Select(value =>
                        string.Join("; ", value.Split(";").Select(part => part.Trim())))
                )
            };
        }

        public virtual int CompareHeaderDictionaries(SortedDictionary<string, string[]> headers, SortedDictionary<string, string[]> entryHeaders, HashSet<string> ignoredHeaders, HashSet<string> excludedHeaders, StringBuilder descriptionBuilder = null)
        {
            int difference = 0;
            var remaining = new SortedDictionary<string, string[]>(entryHeaders, entryHeaders.Comparer);
            foreach (KeyValuePair<string, string[]> header in headers)
            {
                var requestHeaderValues = header.Value;
                var headerName = header.Key;

                if (excludedHeaders.Contains(headerName))
                {
                    continue;
                }

                if (remaining.TryGetValue(headerName, out string[] entryHeaderValues))
                {
                    if (ignoredHeaders.Contains(headerName)) {
                        remaining.Remove(headerName);
                        continue;
                    }

                    // Content-Type, Accept headers are normalized by HttpClient, re-normalize them before comparing
                    if (_normalizedHeaders.Contains(headerName))
                    {
                        requestHeaderValues = RenormalizeContentHeaders(requestHeaderValues);
                        entryHeaderValues = RenormalizeContentHeaders(entryHeaderValues);
                    }

                    if (headerName.Equals("Content-Type", StringComparison.Ordinal))
                    {
                        if (requestHeaderValues.Length == 1 && entryHeaderValues.Length == 1)
                        {
                            var requestContentType = requestHeaderValues[0];
                            var entryContentType = entryHeaderValues[0];
                            if (ContentTypeUtilities.IsMultiPart(requestContentType, out var requestBoundary) &&
                                ContentTypeUtilities.IsMultiPart(entryContentType, out var entryBoundary))
                            {
                                string requestContentTypeWithoutBoundary = requestContentType.Replace(requestBoundary, string.Empty);
                                string entryContentTypeWithoutBoundary = entryContentType.Replace(entryBoundary, string.Empty);
                                if (!requestContentTypeWithoutBoundary.Equals(entryContentTypeWithoutBoundary, StringComparison.Ordinal))
                                {
                                    difference++;
                                    descriptionBuilder?.AppendLine($"    <{headerName}> values differ, request <{requestContentTypeWithoutBoundary}>, record <{entryContentTypeWithoutBoundary}>");
                                }
                                remaining.Remove(headerName);
                                continue;
                            }
                        }
                    }

                    remaining.Remove(headerName);
                    if (!entryHeaderValues.SequenceEqual(requestHeaderValues))
                    {
                        difference++;
                        descriptionBuilder?.AppendLine($"    <{headerName}> values differ, request <{JoinHeaderValues(requestHeaderValues)}>, record <{JoinHeaderValues(entryHeaderValues)}>");
                    }
                }
                else
                {
                    difference++;
                    descriptionBuilder?.AppendLine($"    <{headerName}> is absent in record, value <{JoinHeaderValues(requestHeaderValues)}>");
                }
            }

            foreach (KeyValuePair<string, string[]> header in remaining)
            {
                if (!excludedHeaders.Contains(header.Key))
                {
                    difference++;
                    descriptionBuilder?.AppendLine($"    <{header.Key}> is absent in request, value <{JoinHeaderValues(header.Value)}>");
                }
            }

            return difference;
        }

        private class HeaderComparer : IEqualityComparer<KeyValuePair<string, string[]>>
        {
            public bool Equals(KeyValuePair<string, string[]> x, KeyValuePair<string, string[]> y)
            {
                return x.Key.Equals(y.Key, StringComparison.OrdinalIgnoreCase) &&
                       x.Value.SequenceEqual(y.Value);
            }

            public int GetHashCode(KeyValuePair<string, string[]> obj)
            {
                return obj.GetHashCode();
            }
        }
    }
}
