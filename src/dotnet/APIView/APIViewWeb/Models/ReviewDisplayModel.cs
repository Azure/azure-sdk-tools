using System;

namespace APIViewWeb.Models
{
    public class ReviewDisplayModel
    {
        public string Name { get; set; }
        public string ReviewDisplayName { get; set; }
        public string id { get; set; }
        public string Author { get; set; }
        public DateTime LastUpdated { get; set; }
        public string Language { get; set; }
        public bool IsClosed { get; set; }
        public ReviewType FilterType { get; set; } = ReviewType.Manual;
        public bool IsApproved { get; set; }
        public string ServiceName { get; set; }
        public string PackageDisplayName { get; set; }

        public ReviewDisplayModel(ReviewModel review)
        {
            ReviewDisplayName = review.DisplayName;
            Name = review.Name;
            id = review.ReviewId;
            Author = review.Author;
            LastUpdated = review.LastUpdated;
            Language = review.Language;
            IsClosed = review.IsClosed;
            FilterType = review.FilterType;
            IsApproved = review.IsApproved;
            ServiceName = review.ServiceName;
            PackageDisplayName = review.PackageDisplayName;
        }
    }
}
