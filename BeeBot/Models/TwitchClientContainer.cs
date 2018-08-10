using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using BeeBot.Models;
using TwitchLib;
using TwitchLib.Client;
using TwitchLib.Client.Models;
using TwitchLib.PubSub;
using YTBot.Services;

namespace YTBot.Models
{
    public class TwitchClientContainer
    {
        public string Id { get; set; }
        public string Channel { get; set; }
        public ApplicationUser User { get; set; }
        public TwitchClient Client { get; set; }
        public TwitchPubSub PubSubClient { get; set; }
        public Thread WorkerThread { get; set; }
        public string ConnectionId { get; set; }

        public List<string> ConsoleLog { get; set; }
        public List<ChatMessage> ChatLog { get; set; }
        public List<Giveaway> Giveaways { get; set; }
        public Dictionary<string, DateTime> LinkPermits { get; set; }
        public List<string> Channelmods { get; set; }
        public Dictionary<string, int> CommandsUsed { get; set; }
        public Dictionary<string, int> ChattersCount { get; set; }
        public List<int> Polls { get; set; }

        public bool ModsControlSongrequest { get; set; }
        public bool LinksInChatAllowed { get; set; }
        public RussianRoulette RRulette { get; set; }
        public DateTime LastRussian { get; set; }
        public List<PlayListItem> SongRequests { get; set; }
        public bool LogOutInProgress { get; set; }

        public TwitchClientContainer()
        {
            ChatLog = new List<ChatMessage>();
            ConsoleLog = new List<string>();
            Giveaways = new List<Giveaway>();
            CommandsUsed = new Dictionary<string, int>();
            ChattersCount = new Dictionary<string, int>();
            Channelmods = new List<string>();
            LinkPermits = new Dictionary<string, DateTime>();
            Polls = new List<int>();
            ModsControlSongrequest = false;
            LinksInChatAllowed = true;
            RRulette = null;

            // initialize last russian run
            LastRussian = DateTime.Now.AddMinutes(-10);
            SongRequests = new List<PlayListItem>();
            LogOutInProgress = false;
        }




    }
}