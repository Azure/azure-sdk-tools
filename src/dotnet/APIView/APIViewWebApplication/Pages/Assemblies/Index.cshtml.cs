using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using APIViewWebApplication.Models;

namespace APIViewWebApplication.Pages.Assemblies
{
    public class IndexModel : PageModel
    {
        private readonly APIViewWebApplication.Models.APIViewWebApplicationContext _context;

        public IndexModel(APIViewWebApplication.Models.APIViewWebApplicationContext context)
        {
            _context = context;
        }

        public IList<Assembly> Assembly { get;set; }

        public async Task OnGetAsync()
        {
            Assembly = await _context.Assembly.ToListAsync();
        }
    }
}
