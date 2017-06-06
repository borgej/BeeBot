using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using BeeBot.Models;
using BeeBot.Signalr;
using Microsoft.AspNet.SignalR;
using TwitchLib;
using TwitchLib.Models.Client;

namespace BeeBot.Services
{
    public class TwitchService
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Channel { get; set; }

        public string ClientId { get; set; }

        private ConnectionCredentials ConnCred { get; set; }
        public TwitchClient Client { get; set; }
        public TwitchHub hub { get; set; }

        public TwitchService(string username, string password, string channel, string Id, TwitchHub hub_)
        {
            hub = hub_;
            Username = username;
            Password = password;
            Channel = channel;
            ClientId = Id;

            ConnCred = new ConnectionCredentials(Username, Password);
        }


        public void Connect(bool loggingOn = true)
        {
            try
            {
                if (Client != null)
                {
                    if (Client.IsConnected)
                    {
                        hub.ConsoleLog("Already connected...");
                    }
                    else
                    {
                        Client.Connect();
                        hub.ConsoleLog("Connected");
                    }
                }
                else
                {
                    if ((string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password) ||
                         string.IsNullOrWhiteSpace(Channel)))
                    {
                        throw new Exception("No user/pass/channel given");
                    }

                    ConnCred = new ConnectionCredentials(Username, Password);

                    Client = new TwitchClient(ConnCred, Channel, logging: false);

                    // Throttle bot
                    Client.ChatThrottler = new TwitchLib.Services.MessageThrottler(20, TimeSpan.FromSeconds(30));



                    Client.OnLog += hub.ConsoleLog;
                    Client.OnConnectionError += hub.ConsoleLogConnectionError;
                    Client.OnMessageReceived += hub.ChatShowMessage;
                    Client.OnUserJoined += hub.ShowUserJoined;
                    Client.OnUserLeft += hub.ShowUserLeft;
                    Client.OnUserTimedout += hub.ShowUserTimedOut;
                    Client.OnUserBanned += hub.ShowUserBanned;
                    Client.OnModeratorsReceived += hub.ChannelModerators;

                    Client.Connect();
                    hub.ConsoleLog("Connected to channel " + Channel);
                }

                Client.GetChannelModerators(Channel);
                Client.WillReplaceEmotes = true;


                var botStatus = new BotStatusVM()
                {
                    info = Client.IsConnected ? "Bot is connected" : "Bot is not connected",
                    message = "",
                    warning = ""
                };

                hub.Clients.All.BotStatus(botStatus);
                hub.Clients.Caller.BotStatus(botStatus);

            }
            catch (Exception e)
            {
                hub.ConsoleLog(e.Message);
            }
        }

        public void Disconnect()
        {
            Client.Disconnect();
        }

        public bool IsConnected()
        {
            return Client.IsConnected;
        }

        public void GetLoggedInUsers()
        {
            throw new NotImplementedException();
        }

    }
}