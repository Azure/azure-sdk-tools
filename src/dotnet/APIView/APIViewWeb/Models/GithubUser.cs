// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Newtonsoft.Json;

namespace APIViewWeb.Models
{
    public class GithubUser
    {
        [JsonProperty(PropertyName = "id")]
        public int Id { get; set; }
        [JsonProperty(PropertyName = "login")]
        public string Login { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonProperty(PropertyName = "html_url")]
        public string HtmlUrl { get; set; }

        public override bool Equals(object obj)
        {
            // users cannot have the same username, so only login needs to be compared
            GithubUser g = obj as GithubUser;
            return g != null && g.Login == this.Login;
        }

        public override int GetHashCode()
        {
            return this.Login.GetHashCode();
        }
    }

    public class GitHubOrganization
    {
        public string Login { get; set; } = "";
    }
}
