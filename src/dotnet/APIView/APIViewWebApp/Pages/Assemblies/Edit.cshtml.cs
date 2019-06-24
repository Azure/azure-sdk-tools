using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using APIViewWebApp.Models;

namespace APIViewWebApp.Pages.Assemblies
{
    public class EditModel : PageModel
    {
        private readonly APIViewWebApp.Models.APIViewWebAppContext _context;

        public EditModel(APIViewWebApp.Models.APIViewWebAppContext context)
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

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Attach(DLL).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DLLExists(DLL.ID))
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

        private bool DLLExists(int id)
        {
            return _context.DLL.Any(e => e.ID == id);
        }
    }
}
