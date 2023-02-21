// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;
using Newtonsoft.Json;

namespace APIViewWeb.Models
{
    public class OpenSourceUserInfo
    {
        public GitHubInfo github;
        public AadInfo aad;
    }

    public class GitHubInfo
    {
        [JsonProperty("login")]
        public string Login { get; set; }

        [JsonProperty("organizations")]
        public string[] Orgs; 
    }

    public class AadInfo
    {
        [JsonProperty("alias")]
        public string Alias { get; set; }

        [JsonProperty("preferredName")]
        public string PrefferedName { get; set; }

        [JsonProperty("userPrncipalName")]
        public string UserPrnciPalName { get; set; }

        [JsonProperty("emailAddress")]
        public string EmailAddress { get; set; }
    }
}
