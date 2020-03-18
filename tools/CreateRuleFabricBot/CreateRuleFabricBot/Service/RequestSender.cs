using OutputColorizer;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CreateRuleFabricBot.Service
{
    public class FabricBotClient
    {
        private readonly string _authHeaderValue;
        private readonly string _owner;
        private readonly string _repo;

        public FabricBotClient(string owner, string repo, string authToken)
        {
            _owner = owner;
            _repo = repo;
            _authHeaderValue = authToken;
        }

        public void CreateTask(string jsonPayload)
        {
            string requestUri = $"https://fabric-cp.azurewebsites.net/api/bot/task/{_owner}/{_repo}";
            var response = SendRequestAsync(HttpMethod.Post, requestUri, CreateContent(jsonPayload)).GetAwaiter().GetResult();
        }

        public void UpdateTask(string taskId, string jsonPayload)
        {
            string requestUri = $"https://fabric-cp.azurewebsites.net/api/bot/task/{_owner}/{_repo}/{taskId}";

            var response = SendRequestAsync(new HttpMethod("PATCH"), requestUri, CreateContent(jsonPayload)).GetAwaiter().GetResult();
        }

        public void DeleteAll()
        {
            Colorizer.Write("Retrieving list of taskIds for [Yellow!{0}]...", $"{_owner}\\{_repo}");
            List<string> taskIds = GetTaskIds();
            Colorizer.WriteLine("[Green!done].");

            //start deleting
            foreach (string taskId in taskIds)
            {
                int retryCount = 0;
            retry:
                Colorizer.WriteLine("Deleting task [Yellow!{0}]...", taskId);
                try
                {
                    DeleteTask(taskId);
                    Colorizer.WriteLine("[Green!done].");
                }
                catch
                {
                    retryCount++;
                    Colorizer.WriteLine("[Red!failed]. Retrying...");
                    Thread.Sleep(2000); // wait 2 seconds for the server
                    if (retryCount > 3)
                    {
                        Colorizer.WriteLine("[DarkYellow!Skipping task] [Yellow!{0}]", taskId);
                    }
                    else
                    {
                        goto retry;
                    }
                }
            }
        }

        public void DeleteTask(string taskId)
        {
            string requestUri = $"https://fabric-cp.azurewebsites.net/api/bot/task/{_owner}/{_repo}/{taskId}";

            var response = SendRequestAsync(HttpMethod.Delete, requestUri).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
        }

        public List<string> GetTaskIds()
        {
            string requestUri = $"https://fabric-cp.azurewebsites.net/api/bot/getBotConfig?githubKey={_owner}/{_repo}";
            List<string> elements = new List<string>();
            var response = SendRequestAsync(HttpMethod.Get, requestUri).GetAwaiter().GetResult();

            // retrieve the information from the response.
            var configJson = JsonDocument.Parse(response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult());
            // get the config
            var configArray = configJson.RootElement.GetProperty("config");
            foreach (JsonElement item in configArray.EnumerateArray())
            {
                JsonElement property = item.GetProperty("id");
                elements.Add(property.GetString());
            }

            return elements;
        }

        private HttpContent CreateContent(string jsonPayload)
        {
            StringContent sc = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            return sc;
        }

        private async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string uri, HttpContent content = null)
        {
            Cookie authCookie = new Cookie("AppServiceAuthSession", _authHeaderValue, "/", "fabric-cp.azurewebsites.net")
            {
                HttpOnly = true,
                Secure = true
            };

            HttpResponseMessage response = null;
            CookieContainer cookies = new CookieContainer();
            using (HttpClientHandler handler = new HttpClientHandler() { CookieContainer = cookies })
            using (HttpClient client = new HttpClient(handler))
            {
                cookies.Add(authCookie);

                switch (method.Method.ToUpperInvariant())
                {
                    case "DELETE":
                        response = await client.DeleteAsync(uri);
                        break;
                    case "POST":
                        response = await client.PostAsync(uri, content);
                        break;
                    case "PATCH":
                        response = await client.SendAsync(new HttpRequestMessage
                        {
                            Method = new HttpMethod("PATCH"),
                            RequestUri = new Uri(uri),
                            Content = content
                        });
                        break;
                    case "GET":
                        response = await client.GetAsync(uri);
                        break;
                    default:
                        throw new NotSupportedException($"{method.Method} not supported");
                }

                if (response != null)
                {
                    response.EnsureSuccessStatusCode();
                }
            }
            return response;
        }
    }
}

