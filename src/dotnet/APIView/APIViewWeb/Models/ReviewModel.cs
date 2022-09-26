// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Newtonsoft.Json;

namespace APIViewWeb
{
    public class ReviewModel
    {
        private bool _runAnalysis;

        public ReviewModel()
        {
            Revisions = new ReviewRevisionModelList(this);
        }

        [JsonProperty("id")]
        public string ReviewId { get; set; } = IdHelper.GenerateId();

        public string Name { get; set; }
        public string Author { get; set; }
        public DateTime CreationDate { get; set; }
        public ReviewRevisionModelList Revisions { get; set; }

        [Obsolete("Back compat")]
        public List<ReviewCodeFileModel> Files { get; set; } = new List<ReviewCodeFileModel>();

        [JsonIgnore]
        public bool UpdateAvailable { get; set; }

        public bool RunAnalysis
        {
#pragma warning disable 618
            get => _runAnalysis || Revisions.SelectMany(r => r.Files).Any(f => f.RunAnalysis);
#pragma warning restore 618
            set => _runAnalysis = value;
        }

        public bool IsClosed { get; set; }

        public HashSet<string> Subscribers { get; set; } = new HashSet<string>();

        public bool IsUserSubscribed(ClaimsPrincipal user)
        {
            string email = GetUserEmail(user);
            if (email != null)
            {
                return Subscribers.Contains(email);
            }
            return false;
        }

        public string GetUserEmail(ClaimsPrincipal user) =>
            NotificationManager.GetUserEmail(user);

        // gets CSS safe language name - such that css classes based on language name would not need any escaped characters
        public string GetLanguageCssSafeName()
        {
            switch (Language.ToLower())
            {
                case "c#":
                    return "csharp";
                case "c++":
                    return "cplusplus";
                default:
                    return Language.ToLower();
            }
        }

        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                var revision = Revisions.LastOrDefault();
                var label = revision?.Label;
                var name = revision?.Name ?? Name;
                return label != null && !IsAutomatic ?
                    $"{name} - {label}" :
                    name;
            }
        }

        public DateTime LastUpdated => Revisions.LastOrDefault()?.CreationDate ?? CreationDate;

        [JsonIgnore]
        public string Language => Revisions.LastOrDefault()?.Files.LastOrDefault()?.Language;

        [JsonIgnore]
        public string LanguageVariant => Revisions.LastOrDefault()?.Files.LastOrDefault()?.LanguageVariant;

        [JsonIgnore]
        public string PackageName {
            get
            {
                var packageName = Revisions.LastOrDefault()?.Files.LastOrDefault()?.PackageName;
                if (String.IsNullOrWhiteSpace(packageName))
                {
                    return "Other";
                }
                else 
                {
                    return packageName;
                }
            }
        }

        // Master version of review for each package will be auto created
        public bool IsAutomatic { get; set; }

        public ReviewType FilterType { get; set; }

        [JsonIgnore]
        public bool IsApproved => Revisions.LastOrDefault()?.Approvers?.Any() ?? false;

        public string ServiceName { get; set; }

        public string PackageDisplayName { get; set; }

        // Approvers requested for review and when (for hiding older reviews)
        public HashSet<string> requestedReviewers { get; set; } = null;

        public DateTime approvalRequestedOn;

        public DateTime approvalDate;
    }
}
