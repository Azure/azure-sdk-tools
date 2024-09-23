using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Azure.Sdk.Tools.PipelineWitness
{
    public class Program
    {
        public static async Task Main(params string[] args)
        {
            // per queue storage performance docs, set the default connection limit to >= 100
            // https://learn.microsoft.com/en-us/azure/storage/queues/storage-performance-checklist#increase-default-connection-limit
            ServicePointManager.DefaultConnectionLimit = 100;

            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddHealthChecks();
            builder.Services.AddHttpLogging(options => { });

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            Startup.Configure(builder);

            WebApplication app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.UseHealthChecks("/");

            app.UseHttpLogging();

            await app.RunAsync();
        }
    }
}
