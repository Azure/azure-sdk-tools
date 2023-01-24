using System;
using System.Threading.Tasks;

namespace ApiView
{
    class Program
    {
        async static Task Main(string[] args)
        {
            try
            {
                await App.RunAsync(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
