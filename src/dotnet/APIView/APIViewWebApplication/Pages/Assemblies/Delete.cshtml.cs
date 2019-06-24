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
    public class DeleteModel : PageModel
    {
        private readonly APIViewWebApplication.Models.APIViewWebApplicationContext _context;

        public DeleteModel(APIViewWebApplication.Models.APIViewWebApplicationContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Assembly Assembly { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Assembly = await _context.Assembly.FirstOrDefaultAsync(m => m.ID == id);

            if (Assembly == null)
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

            Assembly = await _context.Assembly.FindAsync(id);

            if (Assembly != null)
            {
                _context.Assembly.Remove(Assembly);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
