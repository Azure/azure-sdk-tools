using System.Text.Json.Serialization;

namespace APIViewWeb.DTOs
{
    public class SiteNotificationDto
    {
        [JsonPropertyName("reviewId")]
        public string ReviewId { get; set; }

        [JsonPropertyName("revisionId")]
        public string RevisionId { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("type")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public SiteNotificationType Type { get; set; }

        [JsonPropertyName("toastNotification")]
        public ToastNotificationDto ToastNotification { get; set; }


        private string _status;

        [JsonPropertyName("status")]
        public string Status
        {
            get => _status;
            set => _status = NormalizeStatus(value);
        }

        private static string NormalizeStatus(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Trim().ToLowerInvariant();

            // Return known values as constants, allow unknown values to pass through
            return normalized switch
            {
                "success" => SiteNotificationStatus.Success,
                "info" => SiteNotificationStatus.Info,
                "error" => SiteNotificationStatus.Error,
                "warning" => SiteNotificationStatus.Warning,
                _ => value.Trim() // Keep original casing for unknown values
            };
        }
    }

    public class ToastNotificationDto
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }
        [JsonPropertyName("message")]
        public string Message { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("action")]
        public SiteNotificationAction Action { get; set; }
    }

    public static class SiteNotificationStatus
    {
        public const string Success = "success";
        public const string Info = "info";
        public const string Warning = "warning";
        public const string Error = "error";
    }

    public enum SiteNotificationType
    {
        [JsonPropertyName("CopilotReviewCompleted")]
        CopilotReviewCompleted
    }

    public enum SiteNotificationAction
    {

        [JsonPropertyName("None")]
        None,

        [JsonPropertyName("RefreshPage")]
        RefreshPage
    }
}
