using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace APIViewWebApplication.Models
{
    public class APIViewWebApplicationContext : DbContext
    {
        public APIViewWebApplicationContext (DbContextOptions<APIViewWebApplicationContext> options)
            : base(options)
        {
        }

        public DbSet<APIViewWebApplication.Models.Assembly> Assembly { get; set; }
    }
}
