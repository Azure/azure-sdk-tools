using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PipelineGenerator
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                cancellationTokenSource.Cancel();
            };

            try
            {
                var program = new Program();
                var exitCondition = program.RunAsync(args, cancellationTokenSource.Token).Result;

                return (int)exitCondition;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return (int)ExitCondition.Exception;
            }

        }

        public async Task<ExitCondition> RunAsync(string[] args, CancellationToken cancellationToken)
        {
            var scanDirectory = new DirectoryInfo(args[0]);
            var scanner = new SdkComponentScanner();
            var components = scanner.Scan(scanDirectory);

            if (components.Count() == 0)
            {
                return ExitCondition.NoComponentsFound;
            }



            return ExitCondition.Success;
        }
    }
}
