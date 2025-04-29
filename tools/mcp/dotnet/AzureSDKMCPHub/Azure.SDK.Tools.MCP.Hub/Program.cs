
using System.Reflection;
using Azure.SDK.Tools.MCP.Contract;

namespace Azure.SDK.Tools.MCP.Hub
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var toolTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(MCPHubTool).IsAssignableFrom(t) && !t.IsAbstract);

            foreach (var type in toolTypes)
            {
                var toolInstance = Activator.CreateInstance(type);

                if (toolInstance != null)
                {
                    services.AddSingleton(toolInstance.GetType(), toolInstance);
                }
                else
                {
                    System.Console.WriteLine($"Unable to get tool instance for {type.FullName}. Aborting adding as service.");
                }
            }
        }
    }
}
