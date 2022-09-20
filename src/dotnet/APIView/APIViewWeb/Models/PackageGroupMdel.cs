using System.Collections.Generic;

namespace APIViewWeb.Models
{
    public class PackageGroupModel
    {

        public string PackageDisplayName { get; set; }

        public List<ReviewDisplayModel> reviews { get; set; } = new ();
    }
}
