using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using BeeBot.Models;
using TwitchLib.Client;
using TwitchLib.Client.Models;
using YTBot.Context;
using YTBot.Models;

namespace YTBot.Services
{
    public class TriggerService : IDisposable
    {
        private ContextService ContextService { get; set; }
        private ApplicationUser User { get; set; }
        private BotChannelSettings Bcs { get; set; }

        private TwitchClientContainer TcContainer { get; set; }
        private TwitchClient TwitchClient { get; set; }
        public List<Trigger> Triggers { get; set; } 
        private bool LoyaltyEnabled { get; set; }

        public TriggerService(ApplicationUser _user, TwitchClientContainer _tcContainer)
        {
            ContextService = new ContextService();
            TcContainer = _tcContainer;
            TwitchClient = _tcContainer.Client;
            User = _user;
            Bcs = ContextService.GetBotChannelSettings(User);
            LoyaltyEnabled = Bcs.Loyalty.Track != null;
            Triggers = ContextService.GetTriggers(User);
        }

        /// <summary>
        /// Check if trigger is called
        /// </summary>
        /// <param name="command"></param>
        /// <returns>Trigger called</returns>
        public Trigger TriggerCheck(ChatCommand command)
        {
            return Triggers.FirstOrDefault(t =>
                t.Active == true && t.TriggerName.ToLower().Equals(command.CommandText.ToLower()));
        }


        public virtual void Dispose()
        {
            var disposableServiceProvider = ContextService as IDisposable;

            if (disposableServiceProvider != null)
            {
                disposableServiceProvider.Dispose();
            }
        }
    }
}