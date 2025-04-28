using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureSDKDSpecTools.Models
{
    public class GenericResponse
    {
        public string Status {get; set; } = string.Empty;
        public List<string> Details { get; set; } = [];
    }
}
