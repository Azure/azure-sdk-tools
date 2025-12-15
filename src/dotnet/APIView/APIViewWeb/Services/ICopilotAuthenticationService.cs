using System.Threading;
using System.Threading.Tasks;

namespace APIViewWeb.Services
{
    public interface ICopilotAuthenticationService
    {
        Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    }
}
