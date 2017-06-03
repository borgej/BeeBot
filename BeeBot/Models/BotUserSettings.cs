using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using BeeBot.Models;

namespace YTBot.Models
{
    public class BotUserSettings
    {
        public string Id { get; set; }
        public string BotUsername { get; set; }
        public string BotPassword { get; set; }
        public string BotChannel { get; set; }

        public virtual ApplicationUser User { get; set; }
    }
}