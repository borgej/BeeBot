using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNet.SignalR;
using TwitchLib;
using TwitchLib.Models.Client;

namespace BeeBot.Signalr
{
    public class TwitchHubPersistent : PersistentConnection
    {
        public static string Username { get; set; }
        public static string Password { get; set; }
        public static string Channel { get; set; }

        private static ConnectionCredentials ConnCred { get; set; }
        public static TwitchClient Client { get; set; }

        public TwitchHubPersistent()
        {


            // Create a Long running task to do an infinite loop which will keep sending the server time

            // to the clients every 3 seconds.

            //var taskTimer = Task.Factory.StartNew(async () =>
            //    {
            //        while (true)
            //        {
            //            string timeNow = DateTime.Now.ToString();
            //            //Sending the server time to all the connected clients on the Client method SendServerTime()

            //            Clients.All.SendServerTime(timeNow);
            //            //Delaying by 3 seconds.

            //            await Task.Delay(3000);
            //        }
            //    }, TaskCreationOptions.LongRunning
            //);

            //var id = taskTimer.Id;

            //Task.Factory.


        }

        protected override Task OnConnected(IRequest request, string connectionId)
        {
            return Connection.Send(connectionId, "Welcome!");
        }

        protected override Task OnReceived(IRequest request, string connectionId, string data)
        {
            return Connection.Broadcast(data);
        }
    }
}