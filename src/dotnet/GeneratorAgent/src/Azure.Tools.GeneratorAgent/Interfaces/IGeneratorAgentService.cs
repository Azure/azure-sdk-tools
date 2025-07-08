using System.Threading;
using System.Threading.Tasks;

namespace Azure.Tools.GeneratorAgent.Interfaces
{
    public interface IGeneratorAgentService
    {
        Task CreateAgentAsync(CancellationToken ct);
        Task DeleteAgentsAsync(CancellationToken ct);
    }
}
