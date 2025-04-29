namespace Azure.SDK.Tools.MCP.Contract
{
    public interface MCToolInterface
    {
        void RegisterServices(IServiceCollection services, IConfiguration config);
        void MapEndpoints(IEndpointRouteBuilder endpoints);
    }
}
