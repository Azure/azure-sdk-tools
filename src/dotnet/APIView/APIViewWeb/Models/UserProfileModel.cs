// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Text.Json.Serialization;

namespace APIViewWeb.Models
{
    public class UserProfileModel
    {
        public UserProfileModel()
        {
            Preferences = new UserPreferenceModel();
        }
        
        public UserProfileModel(string username)
        {
            Preferences = new UserPreferenceModel();
            UserName = username;
        }

        [JsonPropertyName("id")]
        public string UserName { get; set; }
        
        // Alias for UserName to support frontend expecting "userName"
        [JsonPropertyName("userName")]
        public string UserNameAlias => UserName;
        
        public string Email { get; set; }
        public UserPreferenceModel Preferences { get; set; }

    }
}
