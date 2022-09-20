using System.Collections.Generic;

namespace APIViewWeb.Models
{
    public class ServiceGroupModel
    {

        public string ServiceName { get; set; }

        public SortedDictionary<string, PackageGroupModel> packages { get; set; } = new();
    }
}
