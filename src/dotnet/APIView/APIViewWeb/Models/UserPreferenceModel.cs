using System.Collections;
using System.Collections.Generic;
using CsvHelper.Configuration.Attributes;

namespace APIViewWeb.Models
{
    public class UserPreferenceModel
    {
        [Name("UserName")]
        public string UserName { get; set; }
        [Name("Language")]
        public IEnumerable<string> Language { get; set; }
        [Name("FilterType")]
        public IEnumerable<ReviewType> FilterType { get; set; }
    }
}
