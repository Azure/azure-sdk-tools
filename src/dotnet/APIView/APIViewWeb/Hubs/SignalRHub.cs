using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.Hubs
{
    [Authorize]
    public class SignalRHub : Hub
    {
        public override Task OnConnectedAsync()
        {
            string name = Context.User.GetGitHubLogin();
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
                await Clients.OthersInGroup(Context.User.GetGitHubLogin()).SendAsync("ReceiveCommentSelf", reviewId, elementId, partialViewResult); 
                await Clients.Others.SendAsync("ReceiveComment", reviewId, elementId, partialViewResult);
            }
        }
    }
}
