using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AzureSDKDSpecTools.Models
{
    public class GenericResponse
    {
        public string Status {get; set; } = string.Empty;
        public List<string> Details { get; set; } = [];
        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
