using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloneAPIViewDB
{
    public class Program
    {
        async static Task Main(string[] args)
        {
            try
            {
                if (args.Length != 1)
                {
                    throw new ArgumentException("usage: CloneAPIViewDB [Old Review ID]", nameof(args));
                }
                var runner = new MigrationRunner();
                await runner.MigrateDocuments(reviewId: args[0]);

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
