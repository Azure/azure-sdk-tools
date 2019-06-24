using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace APIViewWebApp.Models
{
    public class APIViewWebAppContext : DbContext
    {
        public APIViewWebAppContext (DbContextOptions<APIViewWebAppContext> options)
            : base(options)
        {
        }

        public DbSet<APIViewWebApp.Models.DLL> DLL { get; set; }
    }
}
