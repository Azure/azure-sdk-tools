// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using Newtonsoft.Json;

namespace APIViewWeb.Models
{
    public class UserProfileModel
    {
        public UserProfileModel(string username)
        {
            Preferences = new UserPreferenceModel();
            UserName = username;
        }

        [JsonProperty("id")]
        public string UserName { get; set; }
        public string Email { get; set; }
        public UserPreferenceModel Preferences { get; set; }

    }
}
