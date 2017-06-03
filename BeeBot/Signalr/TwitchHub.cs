using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using BeeBot.Models;
using BeeBot.Services;
using Microsoft.Ajax.Utilities;
using Microsoft.AspNet.SignalR;
using Newtonsoft.Json;
using TwitchLib;
using TwitchLib.Events.Client;
using TwitchLib.Models.Client;

namespace BeeBot.Signalr
{
    public class TwitchHub : Hub
    {
        public static string Username { get; set; }
        public static string Password { get; set; }
        public static string Channel { get; set; }

        private static ConnectionCredentials ConnCred { get; set; }
        public static TwitchClient Client { get; set; }

        public TwitchHub()
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


        /// <summary>
        /// Client sent connect to channel
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="channel"></param>
        public void ConnectBot(string username, string password, string channel)
        {

            try
            {
                if (Client != null)
                {
                    if (Client.IsConnected)
                    {
                        ConsoleLog("Already connected...");
                    }
                    else
                    {
                        Client.Connect();
                        ConsoleLog("Connected");
                    }   
                }
                else
                {
                    if ((string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
                         string.IsNullOrWhiteSpace(channel)))
                    {
                        throw new Exception("No user/pass/channel given");
                    }
                    Username = username;
                    Password = password;
                    Channel = channel;
                    ConnCred = new ConnectionCredentials(Username, Password);

                    Client = new TwitchClient(ConnCred, Channel, logging: false);

                    // Throttle bot
                    Client.ChatThrottler = new TwitchLib.Services.MessageThrottler(20, TimeSpan.FromSeconds(30));



                    Client.OnLog += ConsoleLog;
                    Client.OnConnectionError += ConsoleLogConnectionError;
                    Client.OnMessageReceived += ChatShowMessage;
                    Client.OnUserJoined += ShowUserJoined;
                    Client.OnUserLeft += ShowUserLeft;
                    Client.OnModeratorsReceived += ChannelModerators;

                    Client.Connect();
                    ConsoleLog("Connected to channel " + Channel);
                }
                
                Client.GetChannelModerators(channel);
                Client.WillReplaceEmotes = true;


                var botStatus = new BotStatusVM()
                {
                    info = Client.IsConnected ? "Bot is connected" : "Bot is not connected",
                    message = "",
                    warning = ""
                };

                Clients.All.BotStatus(botStatus);

            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
            }
        }

        public void Reconnect(string username, string password, string channel)
        {
            if (Client.IsConnected)
            {
                DisconnectBot();
                ConsoleLog("Reconnecting to channel " + Channel);
                Client.Connect();
                ConsoleLog("Connected to channel " + Channel);
            }
            else
            {
                ConsoleLog("Reconnecting to channel " + Channel);
                Client.Connect();
                ConsoleLog("Connected to channel " + Channel);
            }
        }

        private void ChannelModerators(object sender, OnModeratorsReceivedArgs e)
        {
            bool botIsMod = e.Moderators.Contains(Username);
            var botStatus = new BotStatusVM()
            {
                info = Client.IsConnected ? "Bot is connected" : "Bot is not connected",
                message = "",
                warning = botIsMod == false ? "Bot is not moderator in channel" : ""
            };

            Clients.All.BotStatus(botStatus);
        }

        /// <summary>
        /// Send user left event to Client
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowUserLeft(object sender, OnUserLeftArgs e)
        {
            string userConnected = DateTime.Now.ToString("HH:mm:ss").ToString() + " - " + e.Username + " (disconnected)";
            var msg = "<div class='userLeft'>" + userConnected + "</div>";

            Clients.All.UsersConnLog(msg);
        }

        /// <summary>
        /// Send user joined channel to Client
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ShowUserJoined(object sender, OnUserJoinedArgs e)
        {
            
            string userConnected = DateTime.Now.ToString("HH:mm:ss").ToString() + " - " + e.Username + " (connected)";
            var msg = "<div class='userConnected'>" + userConnected + "</div>";

            Clients.All.UsersConnLog(msg);
        }

        /// <summary>
        /// Disconnect bot from channel
        /// </summary>
        public void DisconnectBot()
        {
            try
            {
                Client.Disconnect();
                ConsoleLog("Disconnected channel " + Channel);
            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
            }
        }

        public void GiveLoggedInUsers(string users)
        {

        }


        /// <summary>
        /// Check if Client is still connected
        /// </summary>
        public void IsConnected()
        {
            if (Client.IsConnected)
            {

                Clients.All.ConsoleLog(DateTime.Now.ToString("HH:mm:ss").ToString() + " - " +
                                       "Bot is still connected!");

            }
            else
            {
                Clients.All.ConsoleLog(DateTime.Now.ToString("HH:mm:ss").ToString() + " - " +
                                       "Bot is no longer connected!");
            }
        }

        /// <summary>
        /// Update channel topic and game
        /// </summary>
        /// <param name="topic">Topic of the channel</param>
        /// <param name="game">Current game</param>
        /// <returns></returns>
        public bool UpdateChannel(string topic, string game)
        {
            return TwitchAPI.Channels.v5.UpdateChannel(Channel, topic, game, null, null, Password).IsCompleted;
        }


        /// <summary>
        /// Log to Client console
        /// </summary>
        /// <param name="msg"></param>
        public void ConsoleLog(string msg)
        {
            Clients.All.ConsoleLog(DateTime.Now.ToString("HH:mm:ss").ToString() + " - " + msg);
        }

        public void ConsoleLog(object sender, OnLogArgs e)
        {
            Clients.All.ConsoleLog(e.DateTime.ToString("HH:mm:ss").ToString() + " - " + e.Data);
        }

        public void ConsoleLogConnectionError(object sender, OnConnectionErrorArgs e)
        {
            Clients.All.ConsoleLog(DateTime.Now.ToString("HH:mm:ss").ToString() + " - " + e.Error.Message);
        }

        /// <summary>
        /// Send chat message to Client
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ChatShowMessage(object sender, OnMessageReceivedArgs e)
        {
            Regex r = new Regex(@"(https?://[^\s]+)");

            var msg = DateTime.Now.ToString("HH:mm:ss").ToString() + " - " +
                      FormatUsername(e.ChatMessage) + ": " +
                      e.ChatMessage.Message;
            //r.Replace(e.ChatMessage.Message, "<a href=\"$1\" target=\"_blank\">$1</a>");

            // TODO: if links are allowed
            if (e.ChatMessage.IsBroadcaster)
            {
                msg = "<b>" + msg + "</b>";
            }
            if (e.ChatMessage.Message.ToLower().Contains("@"+e.ChatMessage.Channel.ToLower()))
            {
                msg = "<div class=\"chatMsg chatMsgToBroadcaster\">" + msg + "</div>";
            }
            else
            {
                msg = "<div class=\"chatMsg\">" + msg + "</div>";
            }

            // badges
            Clients.All.ChatShow(msg);

        }


        /// <summary>
        /// Add html color to username
        /// </summary>
        /// <param name="msg"></param>
        /// <returns>String formatted span</returns>
        private string FormatUsername(ChatMessage msg)
        {
            var color = msg.Color;
            string badges = "";
            string username = "<span style=\"color:rgb("+color.R+","+ color.B + "," + color.G+");\">" + msg.DisplayName + "</span>";
            foreach (var badge in msg.Badges)
            {
                var key = badge.Key;
                var value = badge.Value;

                if (key.ToLower().Contains("moderator") && Convert.ToInt32(value) == 1)
                {
                    username = "<img='~/Content/moderatorBadge.png' style='padding-right: 3px;' />" + username;
                }
            }
            return username;
        }
    }
}