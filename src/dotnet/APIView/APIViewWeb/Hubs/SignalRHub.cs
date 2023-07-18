using System.Threading.Tasks;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.Hubs
{
    [Authorize]
    public class SignalRHub : Hub
    {
        private readonly ILogger<SignalRHub> _logger;
        public SignalRHub(ILogger<SignalRHub> logger) {
            _logger = logger;
        }

        public override Task OnConnectedAsync()
        {
            string name = Context.User.Identity.Name;
            if (!string.IsNullOrEmpty(name))
            {
                Groups.AddToGroupAsync(Context.ConnectionId, name);
                Clients.Caller.SendAsync("ReceiveConnectionId", Context.ConnectionId);
            }
            return base.OnConnectedAsync();
        }

        public async Task PushComment(string reviewId, string elementId, string partialViewResult)
        {
            if (!string.IsNullOrEmpty(reviewId) && !string.IsNullOrEmpty(elementId)) { 
                await Clients.Others.SendAsync("ReceiveComment", reviewId, elementId, partialViewResult);
            }
        }
    }
}
