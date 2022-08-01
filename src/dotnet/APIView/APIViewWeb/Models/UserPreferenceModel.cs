using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        [Name("State")]
        public IEnumerable<string> State { get; set; }
        [Name("Status")]
        public IEnumerable<string> Status { get; set; }
        [Name("HideLineNumbers")]
        public bool? HideLineNumbers { get; set; }
        [Name("HideLeftNavigation")]
        public bool? HideLeftNavigation { get; set; }
    }
}
