using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using BeeBot.Models;
using BeeBot.Signalr;
using Microsoft.AspNet.SignalR;
using TwitchLib;
using TwitchLib.Client;
using TwitchLib.Client.Models;


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

    }
}