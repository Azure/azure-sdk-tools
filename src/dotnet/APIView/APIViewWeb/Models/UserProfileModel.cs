// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace APIViewWeb.Models
{
    public class UserProfileModel
    {
        public UserProfileModel(string username)
        {
            Preferences = new UserPreferenceModel();
            UserName = username;
        }

        [JsonPropertyName("id")]
        public string UserName { get; set; }
        public string Email { get; set; }
        public UserPreferenceModel Preferences { get; set; }

    }
}
