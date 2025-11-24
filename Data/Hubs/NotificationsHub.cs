// RUTA: Sitiowebb/Data/Hubs/NotificationsHub.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Identity;
using Sitiowebb.Models;

namespace Sitiowebb.Data.Hubs
{
    [Authorize] // solo usuarios autenticados
    public class NotificationsHub : Hub
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsHub(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        // Cuando alguien se conecta al hub
        public override async Task OnConnectedAsync()
        {
            var user = await _userManager.GetUserAsync(Context.User);
            if (user != null)
            {
                // Si es manager -> lo metemos en el grupo "managers"
                if (await _userManager.IsInRoleAsync(user, "Manager"))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, "managers");
                }
            }

            await base.OnConnectedAsync();
        }

        // Opcional, por limpieza: sacarlo del grupo al desconectar
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var user = await _userManager.GetUserAsync(Context.User);
            if (user != null && await _userManager.IsInRoleAsync(user, "Manager"))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, "managers");
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}