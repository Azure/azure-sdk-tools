using System.Collections.Generic;
using System;
using Newtonsoft.Json;

namespace APIViewWeb.Models
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
        public string Author { get; set; }
        public string Language { get; set; }
        public int NoOfRevisions { get; set; }
        public bool IsClosed { get; set; }
        public bool IsAutomatic { get; set; }
        public ReviewType FilterType { get; set; }
        public string ServiceName { get; set; }
        public string PackageDisplayName { get; set; }
        public HashSet<string> Subscribers { get; set; } = new HashSet<string>();
        public DateTime LastUpdated { get; set; }
    }

}
