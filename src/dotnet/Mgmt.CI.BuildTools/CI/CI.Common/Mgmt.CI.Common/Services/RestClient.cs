// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace MS.Az.Mgmt.CI.BuildTasks.Services
{
    using MS.Az.Mgmt.CI.BuildTasks.Common.Base;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Logger;
    using MS.Az.Mgmt.CI.BuildTasks.Common.Models.REST;
    using Newtonsoft.Json;
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    public class RestClient : NetSdkUtilTask
    {
        #region fields
        HttpClient _httpClient;
        bool _disposed;
        #endregion
        protected Uri BaseUri { get; }
        protected HttpClient Client
        {
            get
            {
                if (_httpClient == null)
                {
                    _httpClient = new HttpClient();
                }

                return _httpClient;
            }

            private set
            {
                _httpClient = value;
            }
        }

        #region Constructor
        public RestClient() : base() { }

        public RestClient(Uri baseUri): this()
        {
            BaseUri = baseUri;
        }

        #endregion

        internal async Task<RestClientResponse<T>> ExecuteRequest<T>(string endpoint, HttpMethod method) where T: class
        {   
            RestClientResponse<T> rcResponse = null;
            string responseContent = string.Empty;
            HttpResponseMessage response = null;
            HttpRequestMessage request = new HttpRequestMessage(method, endpoint);

            response = await Client.SendAsync(request, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);

            HttpStatusCode statusCode = response.StatusCode;

            if(statusCode != HttpStatusCode.OK)
            {
                responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                HttpRequestException ex = JsonConvert.DeserializeObject<HttpRequestException>(responseContent);
                throw ex;
            }
            else
            {
                responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                T body = JsonConvert.DeserializeObject<T>(responseContent);

                rcResponse = new RestClientResponse<T>();
                rcResponse.Body = body;
                rcResponse.Request = request;
                rcResponse.Response = response;
            }

            return rcResponse;
        }

        public override void Dispose()
        {
            Dispose(true);
            base.Dispose();
            //GC.SuppressFinalize(this);            
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                Client.Dispose();
                Client = null;
            }
        }
    }
}
