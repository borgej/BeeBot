using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using BeeBot.Models;
using TwitchLib;
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

        public ContextService ContextService { get; set; }

        

        
    }
}