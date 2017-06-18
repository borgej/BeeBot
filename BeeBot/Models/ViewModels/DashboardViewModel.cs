using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using BeeBot.Models;

namespace YTBot.Models.ViewModels
{
    public class DashboardViewModel
    {
        public BotUserSettings BotUserSettings { get; set; }
        public Loyalty LoyaltySettings { get; set; }

        public List<Timer> Timers { get; set; }

        public List<Trigger> Triggers { get; set; }
    }
}