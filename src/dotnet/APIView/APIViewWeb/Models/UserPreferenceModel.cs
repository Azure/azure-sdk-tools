using CsvHelper.Configuration.Attributes;

namespace APIViewWeb.Models
{
    public class UserPreferenceModel
    {
        [Name("UserName")]
        public string UserName { get; set; }
        [Name("Language")]
        public string Language { get; set; }
        [Name("FilterType")]
        public ReviewType FilterType { get; set; }
    }
}
