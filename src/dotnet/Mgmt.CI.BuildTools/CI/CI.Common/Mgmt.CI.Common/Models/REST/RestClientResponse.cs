// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Common.Models.REST
{
    using System;
    using System.Net.Http;
    internal class RestClientResponse<T> : IRestClientResponse<T> where T : class
    {
        public HttpRequestMessage Request { get; set; }

        public HttpResponseMessage Response { get; set; }

        public T Body { get; set; }
    }

    internal class RestClientResponse : RestClientResponse<Object> { }


    interface IRestClientResponse<T> where T : class
    {
        HttpRequestMessage Request { get; set; }

        HttpResponseMessage Response { get; set; }

        T Body { get; set; }
    }
}
