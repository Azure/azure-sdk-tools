using Azure.Core;

namespace Hello.Cdk
{
    internal class Program
    {
        static void Main(string[] args)
        {
            HelloCdkInfrastructure infra = new HelloCdkInfrastructure();
            string path = args.Length > 0 ? args[0] : "./infra";
            infra.ToBicep(path);
        }
    }
}