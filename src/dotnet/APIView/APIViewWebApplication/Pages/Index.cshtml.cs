using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using APIView;
using TestLibrary;
using Microsoft.CodeAnalysis;

namespace APIViewWebApplication.Pages
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public AssemblyAPIV Assembly { get; set; }
        public void OnGet()
        {
            IAssemblySymbol assemblySymbol = GetAssemblySymbol();
            AssemblyAPIV assembly = new AssemblyAPIV(assemblySymbol);
            Assembly = assembly;
        }

        public static IAssemblySymbol GetAssemblySymbol()
        {
            var compilation = AssemblyAPIV.GetCompilation("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\TestLibrary\\Debug\\netcoreapp2.1\\TestLibrary.dll");
            foreach (var assemblySymbol in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                if (assemblySymbol.Name.Equals("TestLibrary"))
                    return assemblySymbol;
            }

            return null;
        }
    }
}