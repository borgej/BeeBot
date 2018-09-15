using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using BeeBot.Models;
using TwitchLib.Client.Models;
using YTBot.Context;
using YTBot.Models;

namespace YTBot.Services
{
    public class ContextService : IDisposable
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

            Context = new ApplicationDbContext();

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
            var bcs = GetBotChannelSettings(user);
            if (bcs == null)
            {
                SetInitialBotChannelSettings(user);
            }
            

            return botUserSettings;
        }

        public PlayListItem SaveSongRequest(ApplicationUser user, PlayListItem song)
        {
            var bcs = GetBotChannelSettings(GetUser(user.Email));

            if (bcs.SongRequests == null)
            {
                bcs.SongRequests = new List<PlayListItem>();
            }

            bcs.SongRequests.Add(song);

            Context.SaveChanges();

            return song;
        }

        public void DeleteSongRequest(ApplicationUser user, string id)
        {
            var bcs = GetBotChannelSettings(GetUser(user.Email));

            if (bcs.SongRequests == null)
            {
                return;
            }

            var dbSong = bcs.SongRequests.FirstOrDefault(s => s.Url.Contains(id));
            if (dbSong != null)
            {
                Context.PlaylistItems.Remove(dbSong);
                Context.SaveChanges();
            }
        }

        private BotChannelSettings SetInitialBotChannelSettings(ApplicationUser user)
        {
            var bcs = GetBotChannelSettings(user);
            // Check that the user has BotChannelSettings
            if (bcs == null)
            {
                var newBcs = new BotChannelSettings()
                {
                    User = GetUser(user.UserName),
                    StreamViewers = new List<StreamViewer>(),
                    Timers = new List<Timer>(),
                    Triggers = GetInitialSystemTriggers(),
                    Loyalty = new Loyalty(),
                    StreamGame = "",
                    StreamTitle = ""
                };
                Context.BotChannelSettings.Add(newBcs);
                Context.SaveChanges();
                return newBcs;
            }

            return bcs;
        }

        public bool HasSystemTriggers(string username)
        {
            var bcs = GetBotChannelSettings(GetUser(username));

            if (bcs.Triggers.Where(t => t.TriggerType != TriggerType.Message).Count() == GetInitialSystemTriggers().Count)
            {
                return false;
            }

            return true;
        }

        private static List<Trigger> GetInitialSystemTriggers()
        {
            var triggers = new List<Trigger>();

            // !help
            var help = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = true,
                FollowerCanTrigger = true,
                SubCanTrigger = true,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "help",
                TriggerResponse = "Shows all listed commands for this user.",
                TriggerType = TriggerType.BuiltIn
            };
            triggers.Add(help);

            // !addcommand
            var addcommand = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "addcommand",
                TriggerResponse = "Adds trigger. !addcommand [triggername] [message]",
                TriggerType = TriggerType.BuiltIn
            };
            triggers.Add(addcommand);

            // !removecommand
            var removecommand = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "removecommand",
                TriggerResponse = "Removes trigger. !removecommand [triggername]",
                TriggerType = TriggerType.BuiltIn
            };
            triggers.Add(removecommand);

            // !changecommand
            var changecommand = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "changecommand",
                TriggerResponse = "Changes message for trigger. !changecommand [trigger] [respnse message]",
                TriggerType = TriggerType.BuiltIn
            };
            triggers.Add(changecommand);

            // !commands
            var commands = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = true,
                FollowerCanTrigger = true,
                SubCanTrigger = true,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "commands",
                TriggerResponse = "Shows all listed commands for this user.",
                TriggerType = TriggerType.BuiltIn
            };
            triggers.Add(commands);

            // !uptime
            var uptime = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = true,
                FollowerCanTrigger = true,
                SubCanTrigger = true,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "uptime",
                TriggerResponse = "Shows how long has the channel has been online.",
                TriggerType = TriggerType.Stat
            };
            triggers.Add(uptime);

            // !stats
            var stats = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = true,
                FollowerCanTrigger = true,
                SubCanTrigger = true,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "stats",
                TriggerResponse = "Shows channel statistics (Number of followers and subs).",
                TriggerType = TriggerType.Stat
            };
            triggers.Add(stats);

            // !follower
            var follower = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = true,
                FollowerCanTrigger = true,
                SubCanTrigger = true,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "follower",
                TriggerResponse = "Shows how long user has been following the channel.",
                TriggerType = TriggerType.Stat
            };
            triggers.Add(follower);

            // !sub
            var sub = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = true,
                FollowerCanTrigger = true,
                SubCanTrigger = true,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "sub",
                TriggerResponse = "Shows how many months the user has been subscribed to the channel.",
                TriggerType = TriggerType.Stat
            };
            triggers.Add(sub);

            // !clip
            var clip = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = true,
                FollowerCanTrigger = true,
                SubCanTrigger = true,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "clip",
                TriggerResponse = "Creates a clip from the latest 30 seconds and posts clip-link after 20 seconds to channel.",
                TriggerType = TriggerType.BuiltIn
            };
            triggers.Add(clip);

            // !roulette
            var roulette = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = true,
                FollowerCanTrigger = true,
                SubCanTrigger = true,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "roulette",
                TriggerResponse = "Roulette game, 1 bullet in a revolver. Timed out for 1 minute if you loose.",
                TriggerType = TriggerType.Game
            };
            triggers.Add(roulette);

            // !roulette
            var russian = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = true,
                FollowerCanTrigger = true,
                SubCanTrigger = true,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "russian",
                TriggerResponse = "Russian [amount] Start a Russian roulette.",
                TriggerType = TriggerType.Game
            };
            triggers.Add(russian);

            // !gamble
            var gamble = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = true,
                FollowerCanTrigger = true,
                SubCanTrigger = true,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "gamble",
                TriggerResponse = "Gambles <amount> or 'allin' to gamble with your loyalty credits.",
                TriggerType = TriggerType.Game
            };
            triggers.Add(gamble);

            // !multilink
            var multilink = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "multilink",
                TriggerResponse = "Multilink <TwitchUser> to create multi-stream link and posts to chat.",
                TriggerType = TriggerType.BuiltIn
            };
            triggers.Add(multilink);

            // !streamer
            var streamer = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "streamer",
                TriggerResponse = "Streamer <TwitchUser> to create post a message to channel to please check out and follow this user.",
                TriggerType = TriggerType.BuiltIn
            };
            triggers.Add(streamer);

            // !bonus
            var bonus = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "bonus",
                TriggerResponse = "Bonus <amount> <ChannelUser> to give this user <amount> of loyalty credits.",
                TriggerType = TriggerType.Loyalty
            };
            triggers.Add(bonus);

            // !bonusall
            var bonusall = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "bonusall",
                TriggerResponse = "Bonusall <amount> to give all users in chat <amount> of loyalty credits.",
                TriggerType = TriggerType.Loyalty
            };
            triggers.Add(bonusall);

            // !give
            var give = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = true,
                FollowerCanTrigger = true,
                SubCanTrigger = true,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "give",
                TriggerResponse = "Give <amount> to give <amount> of loot from your own credits.",
                TriggerType = TriggerType.Loyalty
            };
            triggers.Add(give);

            // !topX
            var top = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = true,
                FollowerCanTrigger = true,
                SubCanTrigger = true,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "top",
                TriggerResponse = "Top[number] <amount> shows stream currency top-list.",
                TriggerType = TriggerType.Stat
            };
            triggers.Add(top);

            // !burn
            var burn = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = true,
                FollowerCanTrigger = true,
                SubCanTrigger = true,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "burn",
                TriggerResponse = "Burn all your channel credits!",
                TriggerType = TriggerType.Loyalty
            };
            triggers.Add(burn);

            // !sr SongRequest
            var sr = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = true,
                FollowerCanTrigger = true,
                SubCanTrigger = true,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "sr",
                TriggerResponse = "Sr [YoutubeUrl]|\"Video title.\" adds song to playlist.",
                TriggerType = TriggerType.PlayList
            };
            triggers.Add(sr);

            // !play
            var play = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "play",
                TriggerResponse = "Plays song from the playlist.",
                TriggerType = TriggerType.PlayList
            };
            triggers.Add(play);
            // !prev
            var prev = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "prev",
                TriggerResponse = "Plays previous song from playlist.",
                TriggerType = TriggerType.PlayList
            };
            triggers.Add(prev);
            // !next
            var next = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "next",
                TriggerResponse = "Selects and plays next song in playlist.",
                TriggerType = TriggerType.PlayList
            };
            triggers.Add(next);
            // !stop
            var stop = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "stop",
                TriggerResponse = "Stops song beeing played from playlist.",
                TriggerType = TriggerType.PlayList
            };
            triggers.Add(stop);
            // !pause
            var pause = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "pause",
                TriggerResponse = "Pauses song beeing played from playlist.",
                TriggerType = TriggerType.PlayList
            };
            triggers.Add(pause);
            // !volume
            var volume = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "volume",
                TriggerResponse = "Volume [number] adjusts the volume on song beeing played.",
                TriggerType = TriggerType.PlayList
            };
            triggers.Add(volume);

            // !timeout
            var timeout = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "timeout",
                TriggerResponse = "Timeout [username] for 1 minute..",
                TriggerType = TriggerType.BuiltIn
            };
            triggers.Add(timeout);

            // !ban
            var ban = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "ban",
                TriggerResponse = "Ban [username] bans user from channel.",
                TriggerType = TriggerType.BuiltIn
            };
            triggers.Add(ban);

            // !ban
            var unban = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "unban",
                TriggerResponse = "Unbans [username] user from channel.",
                TriggerType = TriggerType.BuiltIn
            };
            triggers.Add(unban);

            // !poll
            var poll = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "poll",
                TriggerResponse = "Shows Last poll result and url",
                TriggerType = TriggerType.BuiltIn
            };
            triggers.Add(poll);

            // !addpoll
            var addpoll = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "addpoll",
                TriggerResponse = "Addpoll \"[Title]\" [option1]|[option2]|[optionN] Creates a new poll and posts url to channel.",
                TriggerType = TriggerType.BuiltIn
            };
            triggers.Add(addpoll);

            // !permit
            var permit = new Trigger()
            {
                Active = true,
                ViewerCanTrigger = false,
                FollowerCanTrigger = false,
                SubCanTrigger = false,
                ModCanTrigger = true,
                StreamerCanTrigger = true,
                TriggerName = "permit",
                TriggerResponse = "Permit [username] Permits a user to post link to chat for 5 minutes.",
                TriggerType = TriggerType.BuiltIn
            };
            triggers.Add(permit);

            return triggers;
        }

        public void AddSystemTriggers(string identityName)
        {
            var bcs = GetBotChannelSettings(GetUser(identityName));

            var systemTriggers = GetInitialSystemTriggers();

            foreach (var systemTrigger in systemTriggers)
            {
                if (!bcs.Triggers.Any(t => t.TriggerName.ToLower() == systemTrigger.TriggerName.ToLower()))
                {
                    bcs.Triggers.Add(systemTrigger);
                }
            }

            Context.SaveChanges();
        }

        public StreamViewer GetStreamViewer(string username, string channel, string twitchId, string twitchusername = null)
        {
            if (twitchusername == null)
            {
                try
                {
                    return Context.Viewers.SingleOrDefault(u => u.TwitchUserId == twitchId && u.Channel.ToLower() == channel.ToLower());
                }
                catch (Exception e)
                {
                    return null;
                }

            }
            else
            {
                try
                {

                    return Context.Viewers.SingleOrDefault(
                        u => u.TwitchUsername.ToLower() == twitchusername.ToLower() && u.Channel.ToLower() == channel.ToLower());
                }
                catch (Exception e)
                {
                    return null;
                }

            }
        }

        public void SetRouletteTime(string username, string channel, string twitchId,
            string twitchusername = null)
        {
            if (twitchusername == null)
            {
                try
                {
                    var user = Context.Viewers.SingleOrDefault(u => u.TwitchUserId == twitchId && u.Channel.ToLower() == channel.ToLower());
                    user.LastRoulette = DateTime.Now;
                    Context.SaveChanges();
                }
                catch (Exception e)
                {

                }

            }
            else
            {
                try
                {

                    var user = Context.Viewers.SingleOrDefault(u => u.TwitchUsername.ToLower() == twitchusername.ToLower() && u.Channel.ToLower() == channel.ToLower());
                    user.LastRoulette = DateTime.Now;
                    Context.SaveChanges();
                }
                catch (Exception e)
                {

                }

            }
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

        public List<StreamViewer> TopLoyalty(ApplicationUser user, int topNumber) 
        {
            try
            {
                Context = new ApplicationDbContext();

                var botChannelSettings = Context.BotChannelSettings.FirstOrDefault(b => b.User.Id == user.Id);
                var userSettings = GetBotUserSettingsForUser(user);

                List<StreamViewer> topLoyalty = Context.Viewers.Where(s => s.Channel.ToLower().Equals(userSettings.BotChannel.ToLower())).OrderByDescending(p => p.CurrentPoints).Take(topNumber).ToList();

                return topLoyalty;
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public List<Quote> GetQuotes(ApplicationUser user)
        {
            try
            {
                Context = new ApplicationDbContext();

                var botChannelSettings = Context.BotChannelSettings.Include("Quotes").FirstOrDefault(b => b.User.Id == user.Id);

                var qoutes = botChannelSettings.Quotes.ToList();

                return qoutes;
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public Quote GetQuote(ApplicationUser user, int id)
        {
            try
            {
                Context = new ApplicationDbContext();

                var botChannelSettings = Context.BotChannelSettings.Include("Quotes").FirstOrDefault(b => b.User.Id == user.Id);

                var qoute = botChannelSettings.Quotes.SingleOrDefault(q => q.Nr == id);

                return qoute;
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public Quote RemoveQuote(ApplicationUser user, int id)
        {
            try
            {
                Context = new ApplicationDbContext();

                var botChannelSettings = Context.BotChannelSettings.Include("Quotes").FirstOrDefault(b => b.User.Id == user.Id);

                var qoute = botChannelSettings.Quotes.SingleOrDefault(q => q.Nr == id);

                return qoute;
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public Quote SaveQoute(ApplicationUser user, Quote quote)
        {
            try
            {
                Context = new ApplicationDbContext();

                var quotes = GetQuotes(user);

                quote.Nr = quotes.Count + 1;
                Context.Quotes.Add(quote);
                Context.SaveChanges();

                return quote;
            }
            catch (Exception e)
            {
                throw;
            }
        }


        public IEnumerable<StreamViewer> GetStreamViewers(ApplicationUser user, string channel)
        {
            Context = new ApplicationDbContext();
            var botChannelSettings = Context.BotChannelSettings.FirstOrDefault(b => b.User.Id == user.Id);
            var userSettings = GetBotUserSettingsForUser(user);

            return Context.Viewers.Where(s => s.Channel.ToLower().Equals(userSettings.BotChannel.ToLower()));

        }

        /// <summary>
        /// Get BotChannelSettings for user
        /// </summary>
        /// <param name="user"></param>
        /// <returns>BotChannelSettings</returns>
        public BotChannelSettings GetBotChannelSettings(ApplicationUser user)
        {
            Context = new ApplicationDbContext();

            if (user == null)
            {
                return null;
            }

            try
            {
                //var botChannelSettings = Context.BotChannelSettings.Include("Loyalty").Include("Triggers").Include("Timers").Include("Quotes").Include("StreamViewers").FirstOrDefault(b => b.User.Id == user.Id);
                var botChannelSettings = Context.BotChannelSettings.Include("Loyalty").Include("Triggers").Include("Timers").Include("Quotes").Include("SongRequests").FirstOrDefault(b => b.User.Id == user.Id);

                return botChannelSettings;
            }
            catch (Exception e)
            {
                return null;
            }
            

            
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

        [OutputCache(Duration = 5, VaryByParam = "username, twitchusername")]
        public StreamViewer GetLoyaltyForUser(string username, string channel, string twitchId, string twitchusername = null)
        {
            if (twitchusername == null)
            {
                try
                {
                    return Context.Viewers.SingleOrDefault(u => u.TwitchUserId == twitchId && u.Channel.ToLower() == channel.ToLower());
                }
                catch (Exception e)
                {
                    return null;
                }
                
            }
            else
            {
                try
                {
                    
                    return Context.Viewers.SingleOrDefault(
                        u => u.TwitchUsername.ToLower() == twitchusername.ToLower() && u.Channel.ToLower() == channel.ToLower());
                }
                catch (Exception e)
                {
                    return null;
                }

            }

        }

        /// <summary>
        /// Add loyaltypoints to user(s)
        /// </summary>
        /// <param name="user">ApplicationUser</param>
        /// <param name="viewers">Viewers currently in channel</param>
        /// <param name="loyaltyPoints">Points to give to the user(s), or null for periodic channel loyalty</param>
        public void AddLoyalty(ApplicationUser user, string channel, IEnumerable<StreamViewer> viewers, int? loyaltyPoints = null)
        {
            Context = new ApplicationDbContext();

            var botChannelSettings = GetBotChannelSettings(user);
            var streamViewers = GetStreamViewers(user, channel) ?? new List<StreamViewer>();

            if (loyaltyPoints != null)
            {
                foreach (var viewer in viewers)
                {
                    var dbViewer = Context.Viewers.FirstOrDefault(v => v.TwitchUsername.ToLower() == viewer.TwitchUsername.ToLower() && v.Channel.ToLower() == channel.ToLower());


                    if (dbViewer != null)
                    {
                        dbViewer.CurrentPoints += Convert.ToInt32(loyaltyPoints);
                        if (viewer.CurrentPoints < 0)
                        {
                            viewer.CurrentPoints = 0;
                        }
                        if (dbViewer.CurrentPoints >= dbViewer.AllTimePoints)
                        {
                            dbViewer.AllTimePoints = dbViewer.CurrentPoints;
                            dbViewer.Channel = channel;
                        }
                    }
                    else
                    {
                        viewer.CurrentPoints += Convert.ToInt32(loyaltyPoints);
                        if (viewer.CurrentPoints < 0)
                        {
                            viewer.CurrentPoints = 0;
                        }
                        viewer.AllTimePoints = Convert.ToInt32(loyaltyPoints);
                        viewer.Channel = channel;

                        Context.Viewers.Add(viewer);
                    }
                }
            }
            else
            {
                if (botChannelSettings.Loyalty != null && botChannelSettings.Loyalty.Track == true)
                {
                    foreach (var viewer in viewers)
                    {
                        var dbViewer = Context.Viewers.FirstOrDefault(v => v.TwitchUsername.ToLower() == viewer.TwitchUsername.ToLower() && v.Channel.ToLower() == channel.ToLower());

                        if (dbViewer != null)
                        {
                            dbViewer.CurrentPoints += botChannelSettings.Loyalty.LoyaltyValue;
                            if (viewer.CurrentPoints < 0)
                            {
                                viewer.CurrentPoints = 0;
                            }
                            if (dbViewer.CurrentPoints >= dbViewer.AllTimePoints)
                            {
                                dbViewer.AllTimePoints = dbViewer.CurrentPoints;
                                dbViewer.Channel = channel;
                            }
                        }
                        else
                        {
                            viewer.CurrentPoints = botChannelSettings.Loyalty.LoyaltyValue;
                            if (viewer.CurrentPoints < 0)
                            {
                                viewer.CurrentPoints = 0;
                            }
                            viewer.AllTimePoints = botChannelSettings.Loyalty.LoyaltyValue;
                            viewer.Channel = channel;
                            Context.Viewers.Add(viewer);
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
            Context = new ApplicationDbContext();

            var triggers = new List<Trigger>();


            var userTriggers = Context.BotChannelSettings.Include("Triggers").FirstOrDefault(u => u.User.Id == user.Id).Triggers;

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
        [OutputCache(Duration = 10, VaryByParam = "username")]
        public List<Trigger> GetTriggers(string username)
        {
            Context = new ApplicationDbContext();

            var user = Context.Users.FirstOrDefault(u => u.UserName == username);

            return GetTriggers(user);
        }

        public IQueryable<Trigger> GetCallableTriggers(ApplicationUser user, StreamViewer chatter, ChatCommand msg)
        {
            var allTriggers = GetTriggers(user);

            return  (IQueryable<Trigger>) allTriggers.Where(t => t.CanTrigger(chatter, msg) && t.Active == true);
        }

        /// <summary>
        /// Get list of timers defined for the user
        /// </summary>
        /// <param name="user"></param>
        /// <returns>List of timers</returns>
        public List<YTBot.Models.Timer> GetTimers(ApplicationUser user)
        {
            Context = new ApplicationDbContext();

            var timers = new List<Timer>();


            var userTimers = Context.BotChannelSettings.Include("Timers").FirstOrDefault(u => u.User.Id == user.Id).Timers;

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
        [OutputCache(Duration = 10, VaryByParam = "username")]
        public List<YTBot.Models.Timer> GetTimers(string username)
        {
            Context = new ApplicationDbContext();

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
            Context = new ApplicationDbContext();

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
            Context = new ApplicationDbContext();

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
            Context = new ApplicationDbContext();

            var bcs = GetBotChannelSettings(user);

            var dbViewer = Context.Viewers.SingleOrDefault(s => s.Id == streamViewer.Id && s.Channel.ToLower() == channel.ToLower());

            dbViewer.LastGamble = DateTime.Now;
                
            Context.SaveChanges();


        }


        /// <summary>
        /// Save timer
        /// </summary>
        /// <param name="timer">Timer object</param>
        /// <param name="username">Username as string</param>
        /// <returns></returns>
        public Timer SaveTimer(Timer timer, string username)
        {
            Context = new ApplicationDbContext();

            var bcs = GetBotChannelSettings(GetUser(username));

            if (timer.Id == 0)
            {
                timer.TimerLastRun = DateTime.Now.AddSeconds(2);
                bcs.Timers.Add(timer);
            }
            else
            {
                
                var dbTimer = bcs.Timers.SingleOrDefault(t => t.Id == timer.Id);

                dbTimer.Active = timer.Active;
                dbTimer.TimerInterval = timer.TimerInterval;
                dbTimer.TimerName = timer.TimerName;
                dbTimer.TimerResponse = timer.TimerResponse;
                dbTimer.TimerLastRun = DateTime.Now;
            }

            Context.SaveChanges();

            return timer;
        }

        /// <summary>
        /// Delete timer
        /// </summary>
        /// <param name="id">Timer id</param>
        /// <param name="username">Username as string</param>
        public void DeleteTimer(int id, string username)
        {
            Context = new ApplicationDbContext();

            var bcs = GetBotChannelSettings(GetUser(username));

            bcs.Timers.Remove(Context.Timers.SingleOrDefault(t => t.Id == id));

            Context.Timers.Remove(Context.Timers.SingleOrDefault(t => t.Id == id));

            Context.SaveChanges();
        }

        /// <summary>
        /// Stamp Timer last run variable
        /// </summary>
        /// <param name="id">Timer id</param>
        /// <param name="username">Username as string</param>
        /// <returns></returns>
        public DateTime TimerStampLastRun(int id, string username)
        {
            Context = new ApplicationDbContext();

            var bcs = GetBotChannelSettings(GetUser(username));

            var timer = bcs.Timers.SingleOrDefault(t => t.Id == id);
            timer.TimerLastRun = DateTime.Now;

            Context.SaveChanges();

            return Convert.ToDateTime(timer.TimerLastRun);
        }

        /// <summary>
        /// Stamp all Timers with last run DateTime.now
        /// </summary>
        /// <param name="username">Username as string</param>
        public void TimersResetLastRun(string username)
        {
            Context = new ApplicationDbContext();

            var bcs = GetBotChannelSettings(GetUser(username));

            foreach (var timer in bcs.Timers.ToList())
            {
                timer.TimerLastRun = DateTime.Now.AddSeconds(+2);
            }

            Context.SaveChanges();
        }

        public void DeleteTrigger(int id, string username)
        {
            Context = new ApplicationDbContext();

            var bcs = GetBotChannelSettings(GetUser(username));

            bcs.Triggers.Remove(Context.Triggers.SingleOrDefault(t => t.Id == id));

            Context.Triggers.Remove(Context.Triggers.SingleOrDefault(t => t.Id == id));

            Context.SaveChanges();
        }

        public Trigger SaveTrigger(Trigger trigger, BotChannelSettings bcs)
        {

            if (trigger.Id == 0)
            {
                if (bcs.Triggers.Any(t => t.TriggerName.ToLower().Equals(trigger.TriggerName.ToLower())))
                {
                    throw new Exception("Triggername already in triggerlist: " + trigger.TriggerName);
                }
                bcs.Triggers.Add(trigger);
            }
            else
            {

                var dbTrigger = bcs.Triggers.SingleOrDefault(t => t.Id == trigger.Id);

                dbTrigger.Active = trigger.Active;
                dbTrigger.TriggerName = trigger.TriggerName;
                dbTrigger.TriggerResponse = trigger.TriggerResponse;
                dbTrigger.ModCanTrigger = trigger.ModCanTrigger;
                dbTrigger.ViewerCanTrigger = trigger.ViewerCanTrigger;
                dbTrigger.StreamerCanTrigger = trigger.StreamerCanTrigger;
                dbTrigger.SubCanTrigger = trigger.SubCanTrigger;
                dbTrigger.TriggerType = trigger.TriggerType;
            }

            Context.SaveChanges();

            return trigger;
        }

        public Trigger SaveTrigger(Trigger trigger, string username)
        {
            Context = new ApplicationDbContext();

            var bcs = GetBotChannelSettings(GetUser(username));

            if (trigger.Id == 0)
            {
                bcs.Triggers.Add(trigger);
            }
            else
            {

                var dbTrigger = bcs.Triggers.SingleOrDefault(t => t.Id == trigger.Id);

                dbTrigger.Active = trigger.Active;
                dbTrigger.TriggerName = trigger.TriggerName;
                dbTrigger.TriggerResponse = trigger.TriggerResponse;
                dbTrigger.ModCanTrigger = trigger.ModCanTrigger;
                dbTrigger.ViewerCanTrigger = trigger.ViewerCanTrigger;
                dbTrigger.StreamerCanTrigger = trigger.StreamerCanTrigger;
                dbTrigger.SubCanTrigger = trigger.SubCanTrigger;
                dbTrigger.TriggerType = trigger.TriggerType;
            }

            Context.SaveChanges();

            return trigger;
        }

        public Trigger GetTrigger(string triggername, string username)
        {
            Context = new ApplicationDbContext();
            var bcs = GetBotChannelSettings(GetUser(username));
            return bcs.Triggers.FirstOrDefault(t => t.TriggerName.ToLower() == triggername.ToLower());
        }

        public Trigger ModAddedTriggerMessage(Trigger trigger, string username)
        {
            Context = new ApplicationDbContext();
            var bcs = GetBotChannelSettings(GetUser(username));

            if (trigger.Id == 0)
            {
                trigger.Active = true;
                trigger.FollowerCanTrigger = true;
                trigger.ModCanTrigger = true;
                trigger.StreamerCanTrigger = true;
                trigger.SubCanTrigger = true;
                trigger.TriggerType = TriggerType.Message;
                trigger.ViewerCanTrigger = true;
                bcs.Triggers.Add(trigger);
            }

            Context.SaveChanges();

            return trigger;
        }

        public Trigger ModRemovedTriggerMessage(Trigger trigger, string username)
        {
            Context = new ApplicationDbContext();
            var bcs = GetBotChannelSettings(GetUser(username));

            var deleteTrigger =
                bcs.Triggers.FirstOrDefault(t => t.Id == trigger.Id);
            if (deleteTrigger.TriggerType == TriggerType.Message)
            {
                bcs.Triggers.Remove(deleteTrigger);
            }
            else
            {
                return null;
            }
            

            Context.SaveChanges();

            return trigger;
        }

        public Trigger ModChangedTriggerMessage(Trigger trigger, string username)
        {
            Context = new ApplicationDbContext();
            var bcs = GetBotChannelSettings(GetUser(username));

            if (trigger.TriggerType != TriggerType.Message)
            {
                return null;
            }
            var dbTrigger = bcs.Triggers.FirstOrDefault(t => t.Id == trigger.Id);

            dbTrigger.Active = trigger.Active = true;
            dbTrigger.FollowerCanTrigger = trigger.FollowerCanTrigger = true;
            dbTrigger.ModCanTrigger = trigger.ModCanTrigger = true;
            dbTrigger.StreamerCanTrigger = trigger.StreamerCanTrigger = true;
            dbTrigger.SubCanTrigger = trigger.SubCanTrigger = true;
            dbTrigger.TriggerType = trigger.TriggerType = TriggerType.Message;
            dbTrigger.ViewerCanTrigger = trigger.ViewerCanTrigger = true;
            dbTrigger.TriggerResponse = trigger.TriggerResponse;

            Context.SaveChanges();

            return trigger;
        }

        public virtual void Dispose()
        {
            var disposableServiceProvider = Context as IDisposable;

            if (disposableServiceProvider != null)
            {
                disposableServiceProvider.Dispose();
            }
        }


    }
}