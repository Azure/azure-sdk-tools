using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
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
            }
            return base.OnConnectedAsync();
        }

        // create a callback that receives what you push (3 param) 
        public async Task ReceiveComment(string reviewId, string revisionId, string elementId, string partialViewResult)
        {
            if (!string.IsNullOrEmpty(reviewId) && !string.IsNullOrEmpty(elementId)) { 
                await Clients.Others.SendAsync("ReceiveComment", reviewId, revisionId, elementId, partialViewResult);
            }
        }
    }
}
