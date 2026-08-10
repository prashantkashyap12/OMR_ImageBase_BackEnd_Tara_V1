using System.Net.WebSockets;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using Syncfusion.EJ2.Notifications;
using TesseractOCR.Pix;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using Newtonsoft.Json.Linq;
namespace SQCScanner.websoketManager
{
    public class WebSoketHandler
    {
        private readonly WebSocketConnectionManager _connectionManager;
        public WebSoketHandler(WebSocketConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        // send Massage based userId sapcefic and // check if socket is open or not
        public async Task UserMessageAsync(string userId, string message)
        {
            var socket = _connectionManager.GetSocketByUserId(userId);

            if (socket == null)
            {
                Console.WriteLine($"No socket found for userId: {userId}");
                return;
            }

            if (socket.State != WebSocketState.Open)
            {
                Console.WriteLine($" Socket for {userId} is not open. State: {socket.State}");
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(message);
            try
            {
                if (message != "")
                {
                    Console.WriteLine($" Sending message to {userId}: {message}");
                    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    _connectionManager.RemoveSocket(userId);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending to {userId}: {ex.Message}");
                _connectionManager.RemoveSocket(userId);
            }
        }

        // BroadCast Massage 
        public async Task BroadcastTestAsync(string message)
        {
            // to find connected Allsoket
            foreach (var socket in _connectionManager.GetAllSockets())
            {
                // those are ture and open
                if (socket.State == WebSocketState.Open)
                {
                    // Message 
                    var bytes = Encoding.UTF8.GetBytes(message);

                    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }

    }
}
