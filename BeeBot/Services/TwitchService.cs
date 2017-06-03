using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
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

        private ConnectionCredentials connCred { get; set; }
        public TwitchClient client { get; set; }
        public TwitchHub hub { get; set; }

        public TwitchService(string username, string password, string channel, TwitchHub hub_)
        {
            hub = hub_;
            Username = username;
            Password = password;
            Channel = channel;

            connCred = new ConnectionCredentials(Username, Password);
        }


        public bool Connect(bool loggingOn = true)
        {
            client = new TwitchClient(connCred, Channel, logging: loggingOn);

            // Throttle bot
            client.ChatThrottler = new TwitchLib.Services.MessageThrottler(20, TimeSpan.FromSeconds(30));

            

            client.OnLog += hub.ConsoleLog;
            client.OnConnectionError += hub.ConsoleLogConnectionError;
            client.OnMessageReceived += hub.ChatShowMessage;


            client.Connect();
            return true;
        }

        public void Disconnect()
        {
            client.Disconnect();
        }

        public bool IsConnected()
        {
            return client.IsConnected;
        }

        public void GetLoggedInUsers()
        {
            throw new NotImplementedException();
        }

    }
}