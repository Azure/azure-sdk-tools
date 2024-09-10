using System.Threading.Tasks;
using APIViewWeb.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

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

        /// <summary>
        /// Endpoint Consumed by Client SPA
        /// </summary>
        /// <returns></returns>
        public async Task PushCommentUpdates(CommentUpdatesDto commentUpdatesDto) 
        {
            await Clients.All.SendAsync("ReceiveCommentUpdates", commentUpdatesDto);
        }
    }   
}
