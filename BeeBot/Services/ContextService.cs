using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using BeeBot.Models;
using YTBot.Context;
using YTBot.Models;

namespace YTBot.Services
{
    public class ContextService
    {
        public ApplicationDbContext Context{ get; set; }



        /// <summary>
        /// Constructor
        /// </summary>
        public ContextService()
        {
            Context = new ApplicationDbContext();
        }

        /// <summary>
        /// Get a users BotUserSettings
        /// </summary>
        /// <param name="user"></param>
        /// <returns>BotUsersSettings</returns>
        public BotUserSettings GetBotUserSettingsForUser(ApplicationUser user)
        {
            if (user == null)
            {
                //throw new Exception("No user object given");
                return null;
            }


            var botUserSettings = Context.BotUserSettings.FirstOrDefault(b => b.User.Id == user.Id);

            if (botUserSettings == null)
            {
                botUserSettings = new BotUserSettings()
                {
                    BotUsername = "", 
                    BotPassword = "",
                    BotChannel = ""
                };
            }

            // Check if user has botchannelsettings stored in db
            if (GetBotChannelSettings(user) == null)
            {
                SetInitialBotChannelSettings(user);
            }
            

            return botUserSettings;
        }

        public BotChannelSettings SetInitialBotChannelSettings(ApplicationUser user)
        {
            // Check that the user has BotChannelSettings
            if (GetBotChannelSettings(user) == null)
            {
                var bcs = new BotChannelSettings()
                {
                    User = user,
                    StreamViewers = new List<StreamViewer>(),
                    Timers = new List<Timer>(),
                    Triggers = new List<Trigger>(),
                    Loyalty = new Loyalty(),
                    StreamGame = "",
                    StreamTitle = ""
                };
                Context.BotChannelSettings.Add(bcs);
                Context.SaveChanges();
                return bcs;
            }

            return null;
        }

        /// <summary>
        /// Set users bot settings
        /// </summary>
        /// <param name="botUserSettings"></param>
        /// <returns>BotUserSettings</returns>
        public BotUserSettings SetBotUserSettingsForUser(BotUserSettings botUserSettings)
        {
            if (botUserSettings?.User == null)
            {
                return null;
            }

            try
            {
                var dbSettings = Context.BotUserSettings.FirstOrDefault(u => u.User.Id == botUserSettings.User.Id);

                if (dbSettings != null)
                {
                    dbSettings.BotChannel = botUserSettings.BotChannel;
                    dbSettings.BotUsername = botUserSettings.BotUsername;
                    dbSettings.BotPassword = botUserSettings.BotPassword;
                    dbSettings.ChannelToken = botUserSettings.ChannelToken;
                }
                else
                {
                    Context.BotUserSettings.Add(botUserSettings);
                }

                Context.SaveChanges();
            }
            catch (Exception e)
            {
                throw;
            }

            return botUserSettings;
        }

        public BotChannelSettings SetBotChannelSettings(BotChannelSettings botChannelSettings, ApplicationUser user)
        {
            var settings = GetBotChannelSettings(user);
            if (settings == null)
            {
                // create new
                Context.BotChannelSettings.Add(botChannelSettings);
            }
            else
            {
                Context.BotChannelSettings.AddOrUpdate(b => b.Id == botChannelSettings.Id, botChannelSettings);
            }

            Context.SaveChanges();

            return botChannelSettings;
        }

        /// <summary>
        /// Get BotChannelSettings for user
        /// </summary>
        /// <param name="user"></param>
        /// <returns>BotChannelSettings</returns>
        public BotChannelSettings GetBotChannelSettings(ApplicationUser user)
        {
            if (user == null)
            {
                //throw new Exception("No user object given");
                return null;
            }


            var botChannelSettings = Context.BotChannelSettings.FirstOrDefault(b => b.User.Id == user.Id);

            return botChannelSettings;
        }

        /// <summary>
        /// Add loyaltypoints to user
        /// </summary>
        /// <param name="user">ApplicationUser</param>
        /// <param name="viewer">Viewers currently in channel</param>
        /// <param name="loyaltyPoints">Points to give to the user, or null for periodic channel loyalty</param>
        public void AddLoyalty(ApplicationUser user, string channel, StreamViewer viewer, int? loyaltyPoints = null)
        {
            var viewList = new List<StreamViewer> {viewer};

            AddLoyalty(user, channel, viewList, loyaltyPoints);
        }

        public StreamViewer GetLoyaltyForUser(string username, string twitchId, string twitchusername = null)
        {
            if (twitchusername == null)
            {
                return GetBotChannelSettings(GetUser(username))
                    .StreamViewers.SingleOrDefault(u => u.TwitchUserId == twitchId);
            }
            else
            {
                return GetBotChannelSettings(GetUser(username))
                    .StreamViewers.SingleOrDefault(u => u.TwitchUsername.ToLower() == twitchusername.ToLower());
            }

        }

        /// <summary>
        /// Add loyaltypoints to user(s)
        /// </summary>
        /// <param name="user">ApplicationUser</param>
        /// <param name="viewers">Viewers currently in channel</param>
        /// <param name="loyaltyPoints">Points to give to the user(s), or null for periodic channel loyalty</param>
        public void AddLoyalty(ApplicationUser user, string channel, List<StreamViewer> viewers, int? loyaltyPoints = null)
        {
            var botChannelSettings = GetBotChannelSettings(user);

            if (botChannelSettings.StreamViewers == null)
            {
                botChannelSettings.StreamViewers = new List<StreamViewer>();
            }

            if (loyaltyPoints != null)
            {
                foreach (var viewer in viewers)
                {
                    var dbViewer = botChannelSettings.StreamViewers.FirstOrDefault(v => v.TwitchUserId == viewer.TwitchUserId && v.Channel.ToLower() == channel.ToLower());

                    if (dbViewer != null)
                    {
                        dbViewer.CurrentPoints += Convert.ToInt32(loyaltyPoints);
                        if (dbViewer.CurrentPoints >= dbViewer.AllTimePoints)
                        {
                            dbViewer.AllTimePoints = dbViewer.CurrentPoints;
                            dbViewer.Channel = channel;
                        }
                    }
                    else
                    {
                        viewer.CurrentPoints += Convert.ToInt32(loyaltyPoints);
                        viewer.AllTimePoints = Convert.ToInt32(loyaltyPoints);
                        viewer.Channel = channel;

                        Context.BotChannelSettings.FirstOrDefault(b => b.User.Id == user.Id).StreamViewers.Add(viewer);
                    }
                }
            }
            else
            {
                if (botChannelSettings.Loyalty != null && botChannelSettings.Loyalty.Track == true)
                {
                    foreach (var viewer in viewers)
                    {
                        var dbViewer = botChannelSettings.StreamViewers.FirstOrDefault(v => v.TwitchUserId == viewer.TwitchUserId && v.Channel.ToLower() == channel.ToLower());

                        if (dbViewer != null)
                        {
                            dbViewer.CurrentPoints += botChannelSettings.Loyalty.LoyaltyValue;
                            if (dbViewer.CurrentPoints >= dbViewer.AllTimePoints)
                            {
                                dbViewer.AllTimePoints = dbViewer.CurrentPoints;
                                dbViewer.Channel = channel;
                            }
                        }
                        else
                        {
                            viewer.CurrentPoints = botChannelSettings.Loyalty.LoyaltyValue;
                            viewer.AllTimePoints = botChannelSettings.Loyalty.LoyaltyValue;
                            viewer.Channel = channel;
                            Context.BotChannelSettings.FirstOrDefault(b => b.User.Id == user.Id).StreamViewers.Add(viewer);
                        }
                    }
                }
            }

            Context.SaveChanges();

        }


        /// <summary>
        /// Get list of triggers defined for the user
        /// </summary>
        /// <param name="user"></param>
        /// <returns>List of triggers</returns>
        public List<Trigger> GetTriggers(ApplicationUser user)
        {
            var triggers = new List<Trigger>();


            var userTriggers = Context.BotChannelSettings.FirstOrDefault(u => u.User.Id == user.Id).Triggers;

            if (userTriggers != null)
            {
                triggers.AddRange(userTriggers);
            }

            return triggers;
        }

        /// <summary>
        /// Get list of triggers defined for the user
        /// </summary>
        /// <param name="user"></param>
        /// <returns>List of triggers</returns>
        public List<Trigger> GetTriggers(string username)
        {
            var user = Context.Users.FirstOrDefault(u => u.UserName == username);

            return GetTriggers(user);
        }

        /// <summary>
        /// Get list of timers defined for the user
        /// </summary>
        /// <param name="user"></param>
        /// <returns>List of timers</returns>
        public List<YTBot.Models.Timer> GetTimers(ApplicationUser user)
        {
            var timers = new List<Timer>();


            var userTimers = Context.BotChannelSettings.FirstOrDefault(u => u.User.Id == user.Id).Timers;

            if (userTimers != null)
            {
                timers.AddRange(userTimers);
            }

            return timers;
        }

        /// <summary>
        /// Get list of timers defined for the user
        /// </summary>
        /// <param name="user"></param>
        /// <returns>List of timers</returns>
        public List<YTBot.Models.Timer> GetTimers(string username)
        {
            var user = Context.Users.FirstOrDefault(u => u.UserName == username);

            return GetTimers(user);
        }

        /// <summary>
        /// Gets the ApplicationUser for the string username
        /// </summary>
        /// <param name="username"></param>
        /// <returns>ApplicationUser</returns>
        [OutputCache(Duration = 3600, VaryByParam = "username")]
        public ApplicationUser GetUser(string username)
        {
            return Context.Users.SingleOrDefault(u => u.UserName.ToLower().Equals(username.ToLower()));
        }


        /// <summary>
        /// SetLoyalty for channel
        /// </summary>
        /// <param name="user">ApplicationUser</param>
        /// <param name="loyalty">loyalty</param>
        /// <returns>Loyalty</returns>
        public Loyalty SetLoyalty(ApplicationUser user, Loyalty loyalty)
        {
            var channelSettings = GetBotChannelSettings(user);

            if (channelSettings.Loyalty == null)
            {
                channelSettings.Loyalty = loyalty;
            }
            else
            {
                channelSettings.Loyalty.LoyaltyName = loyalty.LoyaltyName;
                channelSettings.Loyalty.LoyaltyValue = loyalty.LoyaltyValue;
                channelSettings.Loyalty.LoyaltyInterval = loyalty.LoyaltyInterval;
                channelSettings.Loyalty.Track = loyalty.Track;
            }

            Context.SaveChanges();

            return channelSettings.Loyalty;
        }

        /// <summary>
        /// Save gamble DateTime for user, timeout for x minutes
        /// </summary>
        /// <param name="user">ApplicationUser</param>
        /// <param name="channel">string</param>
        /// <param name="streamViewer">StreamViewer</param>
        public void StampLastGamble(ApplicationUser user, string channel, StreamViewer streamViewer)
        {
            var bcs = GetBotChannelSettings(user);

            var viewer = bcs.StreamViewers.SingleOrDefault(
                s => s.Id == streamViewer.Id && s.Channel.ToLower() == channel.ToLower());
            viewer.LastGamble = DateTime.Now;

            Context.SaveChanges();


        }
    }
}