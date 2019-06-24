using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using APIViewWebApplication.Models;

namespace APIViewWebApplication.Pages.Assemblies
{
    public class CreateModel : PageModel
    {
        private readonly APIViewWebApplication.Models.APIViewWebApplicationContext _context;

        public CreateModel(APIViewWebApplication.Models.APIViewWebApplicationContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            return Page();
        }

        [BindProperty]
        public Assembly Assembly { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            _context.Assembly.Add(Assembly);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}