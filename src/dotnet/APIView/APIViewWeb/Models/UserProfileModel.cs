// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;
using System.Security.Claims;
using Newtonsoft.Json;

namespace APIViewWeb.Models
{
    public class UserProfileModel
    {
        // Default case
        public UserProfileModel() 
        {
            Languages = new HashSet<string>();
            Preferences = new UserPreferenceModel();
        }

        public UserProfileModel(ClaimsPrincipal User, string email, HashSet<string> languages, UserPreferenceModel preferences)
        {
            UserName = User.GetGitHubLogin();
            Email = email;
            Languages = languages ?? new HashSet<string>();
            Preferences = preferences ?? new UserPreferenceModel();
        }

        public UserProfileModel(string username)
        {
            Languages = new HashSet<string>();
            Preferences = new UserPreferenceModel();
            UserName = username;
        }

        [JsonProperty("id")]
        public string UserName { get; set; }
        public string Email { get; set; }

        // Approvers only
        public HashSet<string> Languages { get; set; }

        public UserPreferenceModel Preferences { get; set; }

    }
}
