using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace APIViewWeb.LeanModels
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AICommentChangeAction
    {
        Created = 0,
        Deleted,
        Modified
    }

    public class AICommentChangeHistoryModel
    {
        public AICommentChangeAction ChangeAction { get; set; }
        public string User { get; set; }
        public DateTime ChangeDateTime { get; set; }
    }
}
