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
    public class DeleteModel : PageModel
    {
        private readonly APIViewWebApp.Models.APIViewWebAppContext _context;

        public DeleteModel(APIViewWebApp.Models.APIViewWebAppContext context)
        {
            _context = context;
        }

        [BindProperty]
        public DLL DLL { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            DLL = await _context.DLL.FirstOrDefaultAsync(m => m.ID == id);

            if (DLL == null)
            {
                return NotFound();
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            DLL = await _context.DLL.FindAsync(id);

            if (DLL != null)
            {
                _context.DLL.Remove(DLL);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
