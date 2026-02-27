// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
using System.Collections.Generic;
using System.Text.Json.Serialization;
using APIViewWeb.LeanModels;

namespace APIViewWeb.Models
{
    public class UserPreferenceModel
    {
        public string UserName { get; set; }
        public IEnumerable<string> Language { get; set; } = new List<string>();
        public IEnumerable<APIRevisionType> APIRevisionType { get; set; } = new List<APIRevisionType>();
        public IEnumerable<string> State { get; set; } = new List<string>();
        public IEnumerable<string> Status { get; set; } = new List<string>();
        public bool HideLineNumbers { get; set; }
        public bool HideLeftNavigation { get; set; }
        public bool ShowHiddenApis { get; set; }
        public bool ShowDocumentation { get; set; }
        public bool HideReviewPageOptions { get; set; }
        public bool HideIndexPageOptions { get; set; }
        public bool HideSamplesPageOptions { get; set; }
        public bool HideRevisionsPageOptions { get; set; }
        public bool ShowComments { get; set; }
        public bool ShowSystemComments { get; set; }
        public bool DisableCodeLinesLazyLoading { get; set; }
        public string Theme { get; set; } = "light-theme";
    }
}
