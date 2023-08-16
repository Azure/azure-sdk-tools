using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using MongoDB.Bson.Serialization.Serializers;
using Azure.AI.OpenAI;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace APIViewWeb.LeanModels
{
    public class ReviewsListModel
    {
        public int TotalNumberOfReviews { get; set; }
        public List<ReviewsListItemModel> Reviews { get; set; }
    }

    public class ReviewsListItemModel
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        public string Name { get; set; }
        public string LastRevisionName { get; set; }
        public string Label { get; set; }
        public string Author { get; set; }
        public string Language { get; set; }
        public int NoOfRevisions { get; set; }
        public bool IsClosed { get; set; }
        public string State
        {
            get => (this.IsClosed) ? "Closed" : "Open";
        }
        
        public string Type
        {
            get => (this.IsAutomatic) ? "Approved" : this.FilterType.ToString();
        }
        public bool IsApproved { get; set; }
        public bool IsApprovedForFirstRelease { get; set; }
        public string Status
        {
            get {
                if (this.IsApproved)
                    return "Approved";
                else if (this.IsApprovedForFirstRelease)
                    return "1stRelease";
                else
                    return "Pending";
                }
        }

        public bool IsAutomatic { get; set; }

        public string DisplayName
        {
            get
            {
                var name = LastRevisionName ?? Name;
                return Label != null && !IsAutomatic ?
                    $"{name} - {Label}" : name;
            }
        }

        public ReviewType FilterType { get; set; }
        public string ServiceName { get; set; }
        public string PackageDisplayName { get; set; }
        public DateTime LastUpdated { get; set; }
    }

}
