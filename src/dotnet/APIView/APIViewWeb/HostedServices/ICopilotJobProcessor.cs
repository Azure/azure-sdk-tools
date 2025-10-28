using System.Threading;
using System.Threading.Tasks;
using APIViewWeb.Models;

namespace APIViewWeb.HostedServices;

public interface ICopilotJobProcessor
{
    Task ProcessJobAsync(AIReviewJobInfoModel jobInfo, CancellationToken cancellationToken = default);
}
