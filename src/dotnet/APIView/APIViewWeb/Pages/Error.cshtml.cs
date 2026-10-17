using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class ErrorModel : PageModel
    {
        public string TraceId { get; set; }

        public bool ShowTraceId => !string.IsNullOrEmpty(TraceId);

        public void OnGet()
        {
            TraceId = Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier;
        }
    }
}
