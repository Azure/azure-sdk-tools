using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using APIViewWebApp.Models;

namespace APIViewWebApp.Pages.Assemblies
{
    public class CreateModel : PageModel
    {
        private readonly APIViewWebApp.Models.APIViewWebAppContext _context;

        public CreateModel(APIViewWebApp.Models.APIViewWebAppContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            return Page();
        }

        public DLL DLL { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            DLL = new DLL(Request.Form["DLL.DllPath"]);
            _context.DLL.Add(DLL);
            await _context.SaveChangesAsync(true);

            return RedirectToPage("./Index");
        }
    }
}