using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using APIViewWebApplication.Models;

namespace APIViewWebApplication.Pages.Assemblies
{
    public class EditModel : PageModel
    {
        private readonly APIViewWebApplication.Models.APIViewWebApplicationContext _context;

        public EditModel(APIViewWebApplication.Models.APIViewWebApplicationContext context)
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

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(Assembly).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AssemblyExists(Assembly.ID))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool AssemblyExists(int id)
        {
            return _context.Assembly.Any(e => e.ID == id);
        }
    }
}
