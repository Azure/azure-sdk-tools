using APIViewWebApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace RazorPagesMovie.Models
{
    public static class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new APIViewWebAppContext(
                serviceProvider.GetRequiredService<
                    DbContextOptions<APIViewWebAppContext>>()))
            {
                // Look for any movies.
                if (context.DLL.Any())
                {
                    return;   // DB has been seeded
                }

                context.DLL.AddRange(
                    new DLL("C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\TestLibrary\\Debug\\netcoreapp2.1\\TestLibrary.dll")
                    {
                        DllPath = "C:\\Users\\t-mcpat\\Documents\\azure-sdk-tools\\artifacts\\bin\\TestLibrary\\Debug\\netcoreapp2.1\\TestLibrary.dll"
                    }
                );
                context.SaveChanges();
            }
        }
    }
}
