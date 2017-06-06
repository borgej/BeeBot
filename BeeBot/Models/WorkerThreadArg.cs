using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using TwitchLib;

namespace YTBot.Models
{
    public class WorkerThreadArg
    {
        public string Username { get; set; }
        public string Channel { get; set; }
        public TwitchClient Client { get; set; }
    }
}