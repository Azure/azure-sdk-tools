using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.Hubs
{
    [Authorize]
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;
        public NotificationHub(ILogger<NotificationHub> logger) {
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

    }
}
