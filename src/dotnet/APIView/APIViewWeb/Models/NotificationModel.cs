using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace APIViewWeb.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum NotificatonLevel {
        Info,
        Warning,
        Error
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum AIReviewGenerationStatus
    {
        Generating,
        Succeeded,
        Error
    }

    public class NotificationModel
    {
        public string Message { get; set; }

        public NotificatonLevel Level { get; set; }
    }

    public class AIReviewGenerationNotificationModel : NotificationModel
    {
        public string ReviewId { get; set; }
        public string RevisionId { get; set; }
        public bool IsLatestRevision { get; set; }
        public AIReviewGenerationStatus Status { get; set; }
    }
}
