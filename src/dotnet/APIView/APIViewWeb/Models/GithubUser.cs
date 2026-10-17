// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace APIViewWeb.Models
{
    public class GithubUser
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("login")]
        public string Login { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonPropertyName("html_url")]
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
