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

    public class NotificationModel
    {
        public string Message { get; set; }

        public NotificatonLevel Level { get; set; }
    }
}
