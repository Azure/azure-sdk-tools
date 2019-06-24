using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using APIViewWebApp.Models;

namespace APIViewWebApp.Pages.Assemblies
{
    public class IndexModel : PageModel
    {
        private readonly APIViewWebApp.Models.APIViewWebAppContext _context;

        public IndexModel(APIViewWebApp.Models.APIViewWebAppContext context)
        {
            _context = context;
        }

        public IList<DLL> DLL { get;set; }

        public async Task OnGetAsync()
        {
            DLL = await _context.DLL.ToListAsync();
        }
    }
}
