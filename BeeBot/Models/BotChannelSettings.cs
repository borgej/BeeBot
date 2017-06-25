using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using BeeBot.Models;
using TwitchLib.Models.Client;

namespace YTBot.Models
{
    public class BotChannelSettings
    {
        public int Id { get; set; }
        public string StreamTitle { get; set; }
        public string StreamGame { get; set; }
        public string StreamComminuty { get; set; }
        
        public virtual Loyalty Loyalty { get; set; }

        public List<StreamViewer> StreamViewers { get; set; }

        public virtual List<Trigger> Triggers { get; set; }

        public virtual List<Timer> Timers { get; set; }

        public virtual ApplicationUser User { get; set; }
    }

    public class StreamViewer
    {
        public int Id { get; set; }
        public string TwitchUsername { get; set; }
        public string TwitchUserId { get; set; }
        public int CurrentPoints { get; set; }
        public int AllTimePoints { get; set; }
        public bool Follower { get; set; }
        public DateTime? FollowerSince { get; set; }
        public bool Subscriber { get; set; }
        public DateTime? SubscriberSince { get; set; }
        public string Channel { get; set; }
        public DateTime? LastGamble { get; set; }
    }

    public class Loyalty        
    {
        public int Id { get; set; }
        public String LoyaltyName { get; set; }
        /// <summary>
        /// Loyalty interval in minutes
        /// </summary>
        public int LoyaltyInterval { get; set; }
        public int LoyaltyValue { get; set; }
        public bool? Track { get; set; }
    }

    public class Timer
    {
        public int Id { get; set; }
        public string TimerName { get; set; }
        public string TimerResponse { get; set; }
        public int TimerInterval { get; set; }
        public bool? Active { get; set; }
        public DateTime? TimerLastRun { get; set; }
    }

    public class Trigger   
    {
        public int Id { get; set; }
        public string TriggerName { get; set; }
        public TriggerType TriggerType {get; set; }
        public string TriggerResponse { get; set; }
        // Trigger restrictions
        public bool? StreamerCanTrigger { get; set; }
        public bool? ModCanTrigger { get; set; }
        public bool? SubCanTrigger { get; set; }
        public bool? ViewerCanTrigger { get; set; }
        public bool? Active { get; set; }
        public virtual List<Quote> TriggerQoute { get; set; }
    }

    public class Quote
    {
        public int Id { get; set; }
        public string QuoteMsg { get; set; }
        public string QuoteBy { get; set; }
        public DateTime? QuoteAdded { get; set; }
    }

    public enum TriggerType 
    {
        Message = 0,
        Quote = 1,
        Statistic = 2,
        Game = 3,
        Loyalty = 4
    }
}