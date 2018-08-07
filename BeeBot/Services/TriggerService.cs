using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using BeeBot.Models;
using BeeBot.Signalr;
using StrawpollNET;
using StrawpollNET.Data;
using TwitchLib.Api;
using TwitchLib.Api.Exceptions;
using TwitchLib.Client;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;
using YoutubeSearch;
using YTBot.Context;
using YTBot.Models;
using SysRandom = System.Random;

namespace YTBot.Services
{
    public class TriggerService : IDisposable
    {
        private ContextService ContextService { get; set; }
        private ApplicationUser User { get; set; }
        private BotChannelSettings BotChannelSettings { get; set; }

        private TwitchClientContainer TcContainer { get; set; }
        private TwitchClient TwitchClient { get; set; }
        private BotUserSettings BotUserSettings { get; set; }
        private TwitchHub hub { get; set; }

        public TwitchAPI Api { get; set; }
        public List<Trigger> Triggers { get; set; }
        private bool LoyaltyEnabled { get; set; }

        private const int ROULETTETIMEOUT = 1800;

        public TriggerService(ApplicationUser _user, TwitchClientContainer _tcContainer, TwitchHub _hub, TwitchAPI _api)
        {
            hub = _hub;
            Api = _api;
            ContextService = new ContextService();
            BotUserSettings = ContextService.GetBotUserSettingsForUser(User);
            TcContainer = _tcContainer;
            TwitchClient = _tcContainer.Client;
            User = _user;
            BotUserSettings = ContextService.GetBotUserSettingsForUser(User);
            BotChannelSettings = ContextService.GetBotChannelSettings(User);
            LoyaltyEnabled = BotChannelSettings.Loyalty.Track != null;
            Triggers = BotChannelSettings.Triggers;
        }

        /// <summary>
        /// Check if trigger is called
        /// </summary>
        /// <param name="command"></param>
        /// <returns>Trigger called</returns>
        public IEnumerable<Trigger> TriggerCheck(ChatCommand command)
        {
            return Triggers.Where(t => t.Active == true && t.TriggerName.ToLower().Equals(command.CommandText.ToLower()));
        }

        /// <summary>
        /// Check if trigger is called
        /// </summary>
        /// <param name="command"></param>
        /// <returns>Trigger called</returns>
        public bool LoyaltyCheck(ChatCommand command)
        {
            return BotChannelSettings.Loyalty.Track != null && BotChannelSettings.Loyalty.Track == true && (command.CommandText.ToLower().Equals(BotChannelSettings.Loyalty.LoyaltyName.ToLower()) || command.CommandText.ToLower().Equals("burn" + BotChannelSettings.Loyalty.LoyaltyName.ToLower()));
        }

        /// <summary>
        /// Check if giveaways trigger
        /// </summary>
        /// <param name="command"></param>
        /// <returns>Giveaway available</returns>
        public IEnumerable<Giveaway> GiveAwayCheck(ChatCommand command)
        {
            return TcContainer.Giveaways.Where(g => g.Trigger.ToLower().Equals(command.CommandText) && g.EndsAt >= DateTime.Now);
        }

        public async void Run(Trigger trigger, ChatCommand command)
        {
            if (trigger != null)
            {
                switch (trigger.TriggerType)
                {
                    case TriggerType.Message:
                        TcContainer.Client.SendMessage(TcContainer.Channel, trigger.TriggerName);
                        break;
                    case TriggerType.BuiltIn:
                        // !addpoll
                        if (trigger.TriggerName.Equals("addpoll"))
                        {
                            // Establish the poll settins
                            var match = Regex.Match(command.ChatMessage.Message, "!addpoll.*\"(\\w.*)\"\\s+(\\w.*)");

                            string title = "";
                            var arguments = new List<string>();

                            if (match.Success)
                            {
                                title = match.Groups[1].Value;
                                var test = match.Groups[2].Value.Split('|');
                                foreach (var option in test)
                                {
                                    arguments.Add(option.Trim());
                                }
                            }

                            CreateStrawPoll(title, arguments);

                        }

                        // !ban
                        else if (trigger.TriggerName.Equals("ban"))
                        {
                            TwitchClient
                                .BanUser(TcContainer.Channel, command.ArgumentsAsList.FirstOrDefault().ToString(), "Banned!");
                        }

                        // !clip
                        else if (trigger.TriggerName.Equals("clip"))
                        {
                            try
                            {
                                var channelData = await Api.Channels.v5.GetChannelAsync(BotUserSettings.ChannelToken);
                                var clip = await Api.Clips.helix.CreateClipAsync(channelData.Id, BotUserSettings.ChannelToken);
                                var id = clip.CreatedClips.First().Id;
                                var clipUrl = clip.CreatedClips.Last().EditUrl.Replace("/edit", "");
                                await Task.Delay(new TimeSpan(0, 0, 0, 30)); // wait 20 seconds to let the clip generate
                                TwitchClient.SendMessage(TcContainer.Channel, $"/me Created clip: {clipUrl}");
                            }
                            catch (Exception e)
                            {
                                hub.ConsoleLog("Error on !clip: " + e.Message);
                            }
                        }

                        // !commands
                        else if (trigger.TriggerName.Equals("commands"))
                        {
                            TwitchClient
                                .BanUser(TcContainer.Channel, command.ArgumentsAsList.FirstOrDefault().ToString(), "Banned!");
                        }

                        // !giveaway
                        else if (trigger.TriggerName.Equals("giveaway"))
                        {
                            var giveaway = TcContainer.Giveaways.FirstOrDefault(g =>
                                g.Trigger.ToLower().Equals(command.CommandText.ToLower()) && g.EndsAt >= DateTime.Now);

                            var closingIn = giveaway.EndsAt - DateTime.Now;
                            var closingInMinutes = closingIn.Minutes.ToString();

                            TwitchClient.SendMessage(TcContainer.Channel, "/me Giveaway !" + giveaway.Trigger + " for \"" + giveaway.Prize + "\" closing in " + closingInMinutes + " minutes.");
                        }


                        // !help

                        // !multilink
                        else if (trigger.TriggerName.Equals("multilink"))
                        {
                            var baseurl = $"https://multistre.am/{TcContainer.Channel}/";

                            var restOfString = String.Join("/", command.ArgumentsAsList.ToList());

                            var url = baseurl + restOfString;

                            TwitchClient.SendMessage(TcContainer.Channel, "/me " + "See a multistream at " + url);
                        }

                        // !poll
                        else if (trigger.TriggerName.Equals("poll"))
                        {
                            if (TcContainer.Polls.Count == 0)
                            {
                                TwitchClient.SendMessage(TcContainer.Channel, "/me " + $"No polls created yet...");
                            }
                            else
                            {
                                // Get the last Strawpoll ever made
                                int pollId = TcContainer.Polls.Last();
                                var poll = new StrawPoll();
                                var pollFetch = await poll.GetPollAsync(pollId);

                                // Show results
                                TwitchClient.SendMessage(TcContainer.Channel,
                                    "/me " + $"The last poll results for {pollFetch.Title} {pollFetch.PollUrl} are:");
                                var results = pollFetch.Options.Zip(pollFetch.Votes, (a, b) => new { Option = a, Vote = b });
                                var totalVotes = pollFetch.Votes.Sum();
                                foreach (var result in results)
                                {
                                    var percentage = result.Vote == 0
                                        ? "0"
                                        : (((double)result.Vote / (double)totalVotes) * 100).ToString();
                                    TwitchClient.SendMessage(TcContainer.Channel,
                                        "/me " + $"{result.Option} => {result.Vote} votes ({percentage}%)");
                                }
                            }
                        }

                        // !streamer

                        else if (trigger.TriggerName.Equals("streamer"))
                        {
                            await hub.InitializeAPI();
                            var streamerName = command.ArgumentsAsList.First();
                            var twitchUrl = "http://www.Twitch.tv/" + streamerName;
                            var lastStreamed = "";

                            try
                            {
                                var twitchUser = await Api.Users.v5.GetUserByNameAsync(streamerName);
                                var channelData = await Api.Channels.v5.GetChannelByIDAsync(twitchUser.Matches[0].Id);

                                lastStreamed = " - Last streamed '" + channelData.Game + "'";
                                streamerName = twitchUser.Matches[0].DisplayName;

                                TwitchClient
                                    .SendMessage(TcContainer.Channel, "" +
                                                              $"Please go give our friend " + streamerName + " a follow at " +
                                                              twitchUrl +
                                                              " " + lastStreamed);
                            }
                            catch (Exception e)
                            {
                                hub.ConsoleLog("Error on !streamer: " + e.Message);
                            }

                        }

                        // !timeout
                        else if (trigger.TriggerName.Equals("timeout"))
                        {

                            var timeout = new TimeSpan(0, 0, 1, 0);

                            if (command.ArgumentsAsList.Count == 2)
                            {
                                timeout = new TimeSpan(0, 0, Convert.ToInt32(command.ArgumentsAsList.Last().ToString()),
                                    0);
                            }

                            var message = "Timed out for " + Convert.ToString(timeout.Minutes) + " minutes";
                            var joinedChannel = TwitchClient.GetJoinedChannel(TcContainer.Channel);
                            TwitchClient.TimeoutUser(joinedChannel, command.ArgumentsAsList.FirstOrDefault().ToString(),
                                timeout, message);

                        }

                        // !unban
                        else if (trigger.TriggerName.Equals("unban"))
                        {
                            TwitchClient.UnbanUser(TcContainer.Channel, command.ArgumentsAsList.FirstOrDefault().ToString());
                        }

                        break;
                    case TriggerType.Stat:
                        // !follower
                        if (trigger.TriggerName.Equals("follower"))
                        {
                            var twitchUser = await Api.Users.v5.GetUserByNameAsync(command.ChatMessage.Username);
                            var channelData = await Api.Users.v5.GetUserByNameAsync(TcContainer.Channel);
                            try
                            {
                                var follower = await Api.Users.v5.CheckUserFollowsByChannelAsync(twitchUser.Matches[0].Id,
                                    channelData.Matches[0].Id);
                                TwitchClient.SendMessage(TcContainer.Channel,
                                    $@"{twitchUser.Matches[0].DisplayName} has followed {TcContainer.Channel} since {
                                            follower.CreatedAt.ToString("dd.MM.yyyy")
                                        }");
                            }
                            // will throw exception on not following
                            catch (BadResourceException e)
                            {
                                TwitchClient.SendMessage(TcContainer.Channel,
                                    $@"{twitchUser.Matches[0].DisplayName} is not following the channel. :(");
                            }
                        }

                        // !stats
                        else if (trigger.TriggerName.Equals("stats"))
                        {
                            var channelData = await Api.Channels.v5.GetChannelAsync(BotUserSettings.ChannelToken);
                            var channelSubsData =
                                await Api.Channels.v5.GetChannelSubscribersAsync(channelData.Id, null, null, null,
                                    BotUserSettings.ChannelToken);

                            TwitchClient.SendMessage(TcContainer.Channel, $"{TcContainer.Channel} has {channelData.Followers} followers and {channelSubsData.Total} subscribers.");
                        }

                        // !sub
                        else if (trigger.TriggerName.Equals("sub"))
                        {
                            try
                            {
                                var channelData = await Api.Channels.v5.GetChannelAsync(BotUserSettings.ChannelToken);
                                var channelId = channelData.Id;
                                var twitchUser = await Api.Users.v5.GetUserByNameAsync(command.ChatMessage.Username);
                                var twitchUserId = twitchUser.Matches.First().Id;
                                var sub = await Api.Users.v5.CheckUserSubscriptionByChannelAsync(twitchUserId, channelId);

                                TwitchClient.SendMessage(TcContainer.Channel,
                                    $@"{twitchUser.Matches[0].DisplayName} subscribed to the channel at {sub.CreatedAt.ToShortDateString()}");
                            }
                            // will throw exception on not following
                            catch (BadResourceException e)
                            {
                                TwitchClient.SendMessage(TcContainer.Channel,
                                    $@"{command.ChatMessage.DisplayName} is not a subscriber yet. :(");
                            }
                        }

                        // !top
                        else if (trigger.TriggerName.Equals("top"))
                        {
                            var regEx = Regex.Match(command.ArgumentsAsString.ToLower(), "!top\\s+(\\d+)");

                            var number = Convert.ToInt32(regEx.Groups[1].Value);
                            if (number > 10)
                            {
                                number = 10;
                            }

                            var thisUser = User;
                            var topLoyalty = ContextService.TopLoyalty(thisUser, number);

                            var message = "Top" + number.ToString() + ": ";

                            var counter = 1;
                            foreach (var loyalty in topLoyalty)
                            {
                                message += counter + ". " + loyalty.TwitchUsername + " (" + loyalty.CurrentPoints + ") \n";
                                counter++;
                            }

                            TwitchClient.SendMessage(this.TcContainer.Channel, "/me " + message);
                        }


                        // !uptime
                        else if (trigger.TriggerName.Equals("uptime"))
                        {
                            var channel = Api.Channels.v5.GetChannelAsync(BotUserSettings.ChannelToken).Result;
                            var uptime = Api.Streams.v5.GetUptimeAsync(channel.Id);


                            if (uptime.Result == null)
                            {
                                TwitchClient
                                    .SendMessage(this.TcContainer.Channel, "/me " + $"Channel is offline.");
                            }
                            else
                            {
                                if (uptime.Result.Value.Hours == 0)
                                {
                                    TwitchClient
                                        .SendMessage(this.TcContainer.Channel,
                                            "/me " +
                                            $"{channel.DisplayName} has been live for {uptime.Result.Value.Minutes} minutes.");
                                }
                                else
                                {
                                    TwitchClient
                                        .SendMessage(this.TcContainer.Channel,
                                            "/me " +
                                            $"{channel.DisplayName} has been live for {uptime.Result.Value.Hours} hours, {uptime.Result.Value.Minutes} minutes.");
                                }
                            }
                        }

                        break;
                    case TriggerType.Loyalty:
                        // !bonus
                        if (trigger.TriggerName.Equals("bonus"))
                        {
                            try
                            {
                                var verb = "";

                                var loyaltyAmount = Convert.ToInt32(command.ChatMessage.Message.Split(' ')[2]);
                                verb = loyaltyAmount > 0 ? "has been given" : "has been deprived of";
                                string destinationViewerName = command.ChatMessage.Message.Split(' ')[1];

                                var destinationViewerLoyalty = ContextService.GetLoyaltyForUser(
                                    command.ChatMessage.Username.ToLower(), BotUserSettings.BotChannel, null,
                                    destinationViewerName);

                                if (loyaltyAmount != null && (destinationViewerLoyalty != null))
                                {
                                    ContextService.AddLoyalty(User,
                                        command.ChatMessage.Channel.ToLower(), destinationViewerLoyalty, loyaltyAmount);

                                    TwitchClient
                                        .SendMessage(BotUserSettings.BotChannel, "/me " +
                                                                                 $"@{destinationViewerName} was {verb} {loyaltyAmount} {BotChannelSettings.Loyalty.LoyaltyName}");
                                }
                            }
                            catch (Exception e)
                            {
                                hub.ConsoleLog("Error on !bonus: " + e.Message, true);
                            }
                        }

                        // !bonusall
                        else if (trigger.TriggerName.Equals("bonusall"))
                        {
                            try
                            {
                                var verb = "";
                                var bonusValue = Convert.ToInt32(Regex.Match(command.ArgumentsAsString, @"-?\d+").Value);

                                ContextService.AddLoyalty(User,
                                    this.TcContainer.Channel, hub.GetUsersInChannel(BotUserSettings.BotChannel.ToLower()),
                                    bonusValue);

                                verb = bonusValue > 0 ? "has been given" : "has been deprived of";

                                TwitchClient
                                    .SendMessage(BotUserSettings.BotChannel, "/me " +
                                                                             $"Everyone {verb} {bonusValue} {BotChannelSettings.Loyalty.LoyaltyName}");
                            }
                            catch (Exception e)
                            {
                                hub.ConsoleLog("Error on !bonusall: " + e.Message, true);
                            }
                        }

                        
                        // !give
                        else if (trigger.TriggerName.Equals("give"))
                        {
                            try
                            {
                                // get who to give it to
                                var loyaltyAmount = Math.Abs(Convert.ToInt32(command.ChatMessage.Message.Split(' ')[2]));
                                string destinationViewerName = command.ChatMessage.Message.Split(' ')[1];
                                string sourceViewerId = command.ChatMessage.UserId;
                                string sourceViewerName = command.ChatMessage.Username;

                                var sourceViewerLoyalty = ContextService.GetLoyaltyForUser(sourceViewerName,
                                    BotUserSettings.BotChannel,
                                    sourceViewerId,
                                    sourceViewerName);
                                var destinationViewerLoyalty = ContextService.GetLoyaltyForUser(
                                    sourceViewerName,
                                    BotUserSettings.BotChannel,
                                    null,
                                    destinationViewerName);

                                // uses does not have enough to give away
                                if (loyaltyAmount != null && (sourceViewerLoyalty != null &&
                                                              sourceViewerLoyalty.CurrentPoints < loyaltyAmount))
                                {
                                    TwitchClient
                                        .SendMessage(BotUserSettings.BotChannel, "/me " +
                                                                                 $"Stop wasting my time @{command.ChatMessage.DisplayName}, you ain't got that much {BotChannelSettings.Loyalty.LoyaltyName}");
                                }
                                // give away loot
                                else if (loyaltyAmount != null &&
                                         (sourceViewerLoyalty != null &&
                                          sourceViewerLoyalty.CurrentPoints >= loyaltyAmount))
                                {
                                    ContextService.AddLoyalty(ContextService.GetUser(sourceViewerName),
                                        command.ChatMessage.Channel.ToLower(), sourceViewerLoyalty, -loyaltyAmount);
                                    ContextService.AddLoyalty(ContextService.GetUser(sourceViewerName),
                                        command.ChatMessage.Channel.ToLower(), destinationViewerLoyalty, loyaltyAmount);

                                    TwitchClient
                                        .SendMessage(BotUserSettings.BotChannel, "/me " +
                                                                                 $"@{command.ChatMessage.DisplayName} gave {destinationViewerLoyalty.TwitchUsername} {loyaltyAmount} {BotChannelSettings.Loyalty.LoyaltyName}");
                                }
                            }
                            catch (Exception e)
                            {
                                hub.ConsoleLog("Error on !give: " + e.Message, true);
                            }
                        }

                        break;
                    case TriggerType.Game:
                        // !gamble
                        if (trigger.TriggerName.Equals("gamble"))
                        {
                            if (command.ChatMessage.Message.Equals("!gamble"))
                            {
                                TwitchClient
                                    .SendMessage(this.TcContainer.Channel,
                                        "/me " +
                                        $" Type !gamble <amount> or !gamble allin. I use the glorious random number generator web-service from RANDOM.ORG that generates randomness via atmospheric noise.");
                            }
                            else if (command.ChatMessage.Message.ToLower().StartsWith("!gamble"))
                            {
                                // get 
                                var loyalty = ContextService.GetLoyaltyForUser(User.UserName, this.TcContainer.Channel,
                                    command.ChatMessage.UserId, command.ChatMessage.DisplayName.ToLower());

                                // timeout for 5 minutes if user has gamble before
                                if (loyalty != null && (loyalty.LastGamble == null ||
                                                        (loyalty.LastGamble.HasValue &&
                                                         loyalty.LastGamble.Value.AddMinutes(6) <= DateTime.Now)))
                                {
                                    try
                                    {
                                        var r = new Random.Org.Random { UseLocalMode = true };

                                        // get who to give it to
                                        var gambleAmount = command.ChatMessage.Message.Split(' ')[1].ToLower().Equals("allin")
                                            ? loyalty.CurrentPoints
                                            : Math.Abs(Convert.ToInt32(command.ChatMessage.Message.Split(' ')[1]));

                                        string sourceViewerId = command.ChatMessage.UserId;
                                        string sourceViewerName = command.ChatMessage.Username;

                                        var sourceViewerLoyalty = ContextService.GetLoyaltyForUser(
                                            User.UserName,
                                            TcContainer.Channel,
                                            sourceViewerId,
                                            sourceViewerName);

                                        // user has enough loyalty to gamble
                                        if (sourceViewerLoyalty != null &&
                                            sourceViewerLoyalty.CurrentPoints >= gambleAmount)
                                        {
                                            int rolledNumber = 50;
                                            try
                                            {
                                                rolledNumber = r.Next(1, 100);
                                            }
                                            catch (Exception e)
                                            {
                                                rolledNumber = new SysRandom().Next(1, 100);
                                            }


                                            // rolled 1-49
                                            if (rolledNumber < 50)
                                            {
                                                var newAmount = sourceViewerLoyalty.CurrentPoints - (gambleAmount);
                                                TwitchClient
                                                    .SendMessage(TcContainer.Channel,
                                                        "/me " +
                                                        $"@{command.ChatMessage.DisplayName} rolled a sad {rolledNumber}, lost {gambleAmount} and now has {newAmount} {BotChannelSettings.Loyalty.LoyaltyName}!! #theSaltIsReal #rigged");
                                                ContextService.AddLoyalty(
                                                    ContextService.GetUser(User.UserName),
                                                    command.ChatMessage.Channel.ToLower(), sourceViewerLoyalty, -gambleAmount);
                                            }
                                            // rolled 50-99
                                            else if (rolledNumber >= 50 && rolledNumber < 100)
                                            {
                                                var newAmount = (sourceViewerLoyalty.CurrentPoints - gambleAmount) +
                                                                (gambleAmount * 2);

                                                TwitchClient
                                                    .SendMessage(TcContainer.Channel, "/me " +
                                                                              $"@{command.ChatMessage.DisplayName} rolled {rolledNumber}, won {gambleAmount * 2} and now has {newAmount} {BotChannelSettings.Loyalty.LoyaltyName}!");

                                                ContextService.AddLoyalty(
                                                    ContextService.GetUser(User.UserName),
                                                    command.ChatMessage.Channel.ToLower(), sourceViewerLoyalty, gambleAmount);
                                            }
                                            // rolled 100 win * 10
                                            else
                                            {
                                                var newAmount = (sourceViewerLoyalty.CurrentPoints - gambleAmount) +
                                                                (gambleAmount * 10);

                                                TwitchClient
                                                    .SendMessage(TcContainer.Channel, "/me " +
                                                                              $"@{command.ChatMessage.DisplayName} did an epic roll, threw {rolledNumber}, won {gambleAmount * 10} and now has {newAmount} {BotChannelSettings.Loyalty.LoyaltyName}!! #houseCries");

                                                ContextService.AddLoyalty(
                                                    ContextService.GetUser(User.UserName),
                                                    command.ChatMessage.Channel.ToLower(), sourceViewerLoyalty, gambleAmount * 3);
                                            }

                                            ContextService.StampLastGamble(
                                                ContextService.GetUser(User.UserName),
                                                command.ChatMessage.Channel.ToLower(), sourceViewerLoyalty);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        hub.ConsoleLog("Error on !gamble: " + e.Message, true);
                                    }
                                }
                                else if (loyalty == null)
                                {
                                    TwitchClient
                                        .SendMessage(TcContainer.Channel, "/me " +
                                                                  $"@{command.ChatMessage.DisplayName}, you haven't earned any {BotChannelSettings.Loyalty.LoyaltyName} to gamble yet. Stay and the channel and you will recieve {BotChannelSettings.Loyalty.LoyaltyValue.ToString()} every {BotChannelSettings.Loyalty.LoyaltyInterval.ToString()} minute.");
                                }
                                else
                                {
                                    TwitchClient
                                        .SendMessage(TcContainer.Channel, "/me " +
                                                                  $"Chill out @{command.ChatMessage.DisplayName}, you gotta wait 5 minutes from your last gamble to roll the dice again!");
                                }
                            }

                        }

                        // !roulette
                        else if (trigger.TriggerName.Equals("roulette"))
                        {

                            var client = TwitchClient;
                            var joinedChannel = client.GetJoinedChannel(this.TcContainer.Channel);

                            var chatter = ContextService.GetStreamViewer(User.Email, this.TcContainer.Channel, command.ChatMessage.UserId, command.ChatMessage.DisplayName.ToLower());

                            if (chatter.LastRoulette != null && chatter.LastRoulette.Value.AddSeconds(ROULETTETIMEOUT) >= DateTime.Now)
                            {
                                var sleeptime = chatter.LastRoulette.Value.AddSeconds(ROULETTETIMEOUT) - DateTime.Now;
                                client.SendMessage(joinedChannel, $"@{command.ChatMessage.DisplayName}, please wait {sleeptime.Minutes} more minutes before playing roulette.");
                            }
                            else
                            {
                                client.SendMessage(joinedChannel, $"@{command.ChatMessage.DisplayName} places the gun to their head!");
                                var rnd = new SysRandom(Guid.NewGuid().GetHashCode());
                                var theNumberIs = rnd.Next(1, 6);
                                var timeout = new TimeSpan(0, 0, 1, 0);
                                var message = "Timed out for " + Convert.ToString(timeout.Minutes) + " minutes";

                                // User dies(timeout) if 1 is drawn
                                if (theNumberIs == 1)
                                {
                                    Task.Delay(1000);
                                    client.SendMessage(joinedChannel, $"@{command.ChatMessage.DisplayName} pulls the trigger...... brain goes everywhere!! Who knew @{command.ChatMessage.DisplayName} had that much in there?");
                                    //Timeout user
                                    client.TimeoutUser(joinedChannel, command.ChatMessage.DisplayName, timeout, message);
                                    client.SendMessage(joinedChannel, $"@{command.ChatMessage.DisplayName} is now chilling on the floor and sort of all over the place for a minute!");
                                }
                                // Gets away with it!
                                else
                                {
                                    Task.Delay(1000);
                                    client.SendMessage(joinedChannel, $"@{command.ChatMessage.DisplayName} pulls the trigger...... CLICK!....... and survives!!");
                                }
                                // Update last time roulette was played
                                ContextService.SetRouletteTime(User.Email, this.TcContainer.Channel, command.ChatMessage.UserId, command.ChatMessage.DisplayName.ToLower());
                            }
                        }

                        // !russian
                        else if (trigger.TriggerName.ToLower().Equals("russian"))
                        {
                            var roulette = TcContainer.RRulette;
                            var ccontainer = TcContainer;
                            Regex regex = new Regex(@"!russian\s(\d.*)");
                            Match match = regex.Match(command.ChatMessage.Message.ToLower());
                            int bet = 0;

                            // get 
                            string sourceViewerId = command.ChatMessage.UserId;
                            string sourceViewerName = command.ChatMessage.Username;
                            var player = ContextService.GetLoyaltyForUser(
                                HttpContext.Current.User.Identity.Name,
                                TcContainer.Channel,
                                sourceViewerId,
                                sourceViewerName);
                            // start new roulette
                            if (roulette == null)
                            {
                                if (ccontainer.LastRussian != null &&
                                    ((DateTime.Now - ccontainer.LastRussian.AddMinutes(6)).Minutes < 0))
                                {
                                    var minFor = DateTime.Now - ccontainer.LastRussian.AddMinutes(6);
                                    TwitchClient
                                        .SendMessage(TcContainer.Channel,
                                            "/me " +
                                            $"There is a 5 minute sleep time between Russian roulettes, please wait {Math.Abs(minFor.Minutes)} minutes and try again.");
                                    return;
                                }

                                if (match.Success)
                                {
                                    bet = Convert.ToInt32(match.Groups[1].Value);

                                    if (player == null || player.CurrentPoints < bet)
                                    {
                                        TwitchClient
                                            .SendMessage(TcContainer.Channel,
                                                "/me " +
                                                $"@{command.ChatMessage.DisplayName}, you need to have {bet} {BotChannelSettings.Loyalty.LoyaltyName} to enter the big boys Russian Roulette");
                                        return;
                                    }
                                }
                                else
                                {
                                    TwitchClient
                                        .SendMessage(TcContainer.Channel,
                                            "/me " +
                                            $"@{command.ChatMessage.DisplayName}, you need set how much {BotChannelSettings.Loyalty.LoyaltyName} you want to set as \"buy in\".");
                                    return;
                                }


                                var newRoulette = new RussianRoulette { BuyIn = bet };
                                newRoulette.TotalBet += newRoulette.BuyIn;
                                newRoulette.Players.Add(player);
                                this.TcContainer.RRulette = newRoulette;

                                // remove loot from player
                                ContextService.AddLoyalty(User,
                                    command.ChatMessage.Channel.ToLower(), player, -bet);

                                TwitchClient
                                    .SendMessage(TcContainer.Channel,
                                        "/me " +
                                        $"@{player.TwitchUsername} just started a Russian roulette with a buy in at {bet} {BotChannelSettings.Loyalty.LoyaltyName}. Type !russian to join the roulette, starting in 2 minutes!");
                            }
                            // ongoing roulette
                            else
                            {
                                var rroulette = this.TcContainer.RRulette;

                                if (rroulette.Started == true)
                                {
                                    return;
                                }

                                if (player == null || player.CurrentPoints < rroulette.BuyIn)
                                {
                                    TwitchClient
                                        .SendMessage(TcContainer.Channel,
                                            "/me " +
                                            $"@{command.ChatMessage.DisplayName}, you need to have {rroulette.BuyIn} {BotChannelSettings.Loyalty.LoyaltyName} to enter the big boys Russian Roulette");
                                    return;
                                }
                                else
                                {
                                    rroulette.TotalBet += rroulette.BuyIn;
                                    // remove loot from player
                                    ContextService.AddLoyalty(
                                        ContextService.GetUser(HttpContext.Current.User.Identity.Name),
                                        command.ChatMessage.Channel.ToLower(), player, -rroulette.BuyIn);

                                    rroulette.Players.Add(player);

                                    TwitchClient
                                        .SendMessage(TcContainer.Channel,
                                            "/me " +
                                            $"@{command.ChatMessage.DisplayName} just joined the Russian roulette. The total payout is now at {rroulette.TotalBet} {BotChannelSettings.Loyalty.LoyaltyName}, with {rroulette.Players.Count} contestants.");
                                }
                            }
                        }

                        // !give
                        break;
                    case TriggerType.PlayList:
                        // !next
                        if (trigger.TriggerName.ToLower().Equals("next"))
                        {
                            if (command.ChatMessage.IsBroadcaster)
                            {
                                hub.NextSong();
                            }
                            else if (command.ChatMessage.IsModerator && this.TcContainer.ModsControlSongrequest)
                            {
                                hub.NextSong();
                            }
                        }

                        // !pause
                        else if (trigger.TriggerName.ToLower().Equals("pause"))
                        {
                            if (command.ChatMessage.IsBroadcaster)
                            {
                                hub.Pause();
                            }
                            else if (command.ChatMessage.IsModerator && this.TcContainer.ModsControlSongrequest)
                            {
                                hub.Pause();
                            }
                        }

                        // !play
                        else if (trigger.TriggerName.ToLower().Equals("play"))
                        {
                            if (command.ChatMessage.IsBroadcaster)
                            {
                                hub.Play();
                            }
                            else if (command.ChatMessage.IsModerator && this.TcContainer.ModsControlSongrequest)
                            {
                                hub.Play();
                            }
                        }

                        // !prev
                        else if (trigger.TriggerName.ToLower().Equals("prev"))
                        {
                            if (command.ChatMessage.IsBroadcaster)
                            {
                                hub.PrevSong();
                            }
                            else if (command.ChatMessage.IsModerator && this.TcContainer.ModsControlSongrequest)
                            {
                                hub.PrevSong();
                            }
                        }

                        // !sr
                        else if (trigger.TriggerName.ToLower().Equals("sr"))
                        {
                            try
                            {

                                var commandArguments = command.ArgumentsAsString;
                                var userName = command.ChatMessage.DisplayName;



                                // video link fron youtube
                                if (commandArguments.ToLower().Contains("www.youtube.com"))
                                {
                                    var uri = new Uri(commandArguments);

                                    var query = HttpUtility.ParseQueryString(uri.Query);

                                    var videoId = String.Empty;

                                    videoId = query.AllKeys.Contains("v") ? query["v"] : uri.Segments.Last();

                                    var title = hub.GetVideoTitleByHttp(commandArguments, videoId);

                                    // Try again if title cannot be found
                                    if (title == "N/A")
                                    {
                                        title = hub.GetVideoTitleByHttp(commandArguments, videoId);
                                    }
                                    if (this.TcContainer.SongRequests.Any(a => a.VideoId == videoId))
                                    {
                                        TwitchClient.SendMessage(this.TcContainer.Channel,
                                            $"\"{title}\" is already in the playlist.");
                                    }
                                    else
                                    {
                                        var song = hub.UpdatePlaylistFromCommand(commandArguments, title, userName, videoId);
                                        TwitchClient.SendMessage(this.TcContainer.Channel,
                                            $"\"{song.Title}\" was added to the playlist by @{song.RequestedBy}.");
                                    }

                                }
                                // search for the song on youtube
                                else
                                {
                                    // Keyword
                                    string querystring = command.ArgumentsAsString;

                                    var youtubeSearch = new VideoSearch();
                                    var youtubeSearchResult = youtubeSearch.SearchQuery(querystring, 1);

                                    if (youtubeSearchResult != null && youtubeSearchResult.Count > 0)
                                    {
                                        var firstHit = youtubeSearchResult.FirstOrDefault();
                                        var firstVideoUrl = new Uri(firstHit.Url);
                                        var videoId = String.Empty;
                                        var query = HttpUtility.ParseQueryString(firstVideoUrl.Query);
                                        if (query.AllKeys.Contains("v"))
                                        {
                                            videoId = query["v"];
                                        }
                                        else
                                        {
                                            videoId = firstVideoUrl.Segments.Last();
                                        }
                                        if (this.TcContainer.SongRequests.Any(a => a.VideoId == videoId))
                                        {
                                            TwitchClient.SendMessage(this.TcContainer.Channel,
                                                $"\"{firstHit.Title}\" is already in the playlist.");
                                        }
                                        else
                                        {
                                            var song = hub.UpdatePlaylistFromCommand(firstHit.Url, firstHit.Title, userName, videoId);
                                            TwitchClient.SendMessage(this.TcContainer.Channel,
                                                $"\"{song.Title}\" was added to the playlist by @{song.RequestedBy}.");
                                        }

                                    }

                                }


                            }
                            catch (Exception e)
                            {
                            }
                        }

                        // !stop
                        else if (trigger.TriggerName.Equals("stop"))
                        {
                            if (command.ChatMessage.IsBroadcaster)
                            {
                                hub.Stop();
                            }
                            else if (command.ChatMessage.IsModerator && this.TcContainer.ModsControlSongrequest)
                            {
                                hub.Stop();
                            }
                        }

                        // !volume
                        else if (trigger.TriggerName.Equals("volume"))
                        {
                            var volume = Math.Abs(Convert.ToInt32(command.ChatMessage.Message.Split(' ')[1]));
                            if (command.ChatMessage.IsBroadcaster)
                            {
                                hub.Volume(volume);
                            }
                            else if (command.ChatMessage.IsModerator && this.TcContainer.ModsControlSongrequest)
                            {
                                hub.Volume(volume);
                            }
                        }

                        break;
                }
            }
            else
            {
                // giveaways enrollment
                if (this.TcContainer.Giveaways.Any(g =>
                        g.Trigger.ToLower().Equals(command.CommandText.ToLower()) && g.EndsAt >= DateTime.Now))
                {
                    var giveaway = this.TcContainer.Giveaways.FirstOrDefault(g =>
                        g.Trigger.ToLower().Equals(command.CommandText.ToLower()) && g.EndsAt >= DateTime.Now);

                    var twitchUser = await Api.Users.v5.GetUserByNameAsync(command.ChatMessage.Username);

                    var channel = await Api.Users.v5.GetUserByNameAsync(TcContainer.Channel);
                    var isFollower = false;
                    try
                    {
                        await Api.Users.v5.CheckUserFollowsByChannelAsync(twitchUser.Matches[0].Id, channel.Matches[0].Id);
                        isFollower = true;
                    }
                    // will throw exception on not following
                    catch (BadResourceException e)
                    {
                        isFollower = false;
                    }
                    StreamViewer viewer = new StreamViewer()
                    {
                        Channel = TcContainer.Channel,
                        Follower = isFollower,
                        Subscriber = command.ChatMessage.IsSubscriber,
                        TwitchUsername = command.ChatMessage.DisplayName,
                        Mod = command.ChatMessage.IsModerator
                    };

//#if DEBUG
//                    viewer.TwitchUsername = GenerateName(12);
//                    viewer.Follower = NextBool(90);
//                    viewer.Subscriber = NextBool(40);
//                    viewer.Mod = NextBool(20);
//#endif
                    // enter giveaway
                    if (giveaway.CanEnroll(viewer))
                    {
                        giveaway.Enroll(viewer);
                    }
                    // is broadcaster
                    else if (command.ChatMessage.IsBroadcaster)
                    {
                        giveaway.Enroll(viewer);
                    }

                    // Call update on client
                    hub.UpdateGiveaway(giveaway);
                }

                // !loot
                else if (command.CommandText.ToLower().Equals(BotChannelSettings.Loyalty.LoyaltyName) && BotChannelSettings.Loyalty.Track == true)
                {
                    var userLoyalty = ContextService.GetLoyaltyForUser(command.ChatMessage.Username,
                        this.TcContainer.Channel,
                        command.ChatMessage.UserId,
                        command.ChatMessage.Username);

                    if (userLoyalty != null)
                    {
                        TwitchClient
                            .SendMessage(BotUserSettings.BotChannel, "/me " +
                                                                     $"@{command.ChatMessage.DisplayName} has {userLoyalty.CurrentPoints.ToString()} {BotChannelSettings.Loyalty.LoyaltyName}");
                    }
                    else
                    {
                        TwitchClient
                            .SendMessage(BotUserSettings.BotChannel, "/me " +
                                                                     $"@{command.ChatMessage.Username}, you haven't earned any {BotChannelSettings.Loyalty.LoyaltyName} yet. Hang out in the channel and you will recieve {BotChannelSettings.Loyalty.LoyaltyValue.ToString()} every {BotChannelSettings.Loyalty.LoyaltyInterval.ToString()} minute.");
                    }
                }
                
                // !burn
                else if (command.CommandText.ToLower().Equals("burn" + BotChannelSettings.Loyalty.LoyaltyName.ToLower()) && BotChannelSettings.Loyalty.Track == true)
                {
                    var rnd = new SysRandom(Guid.NewGuid().GetHashCode());
                    var userLoyalty = ContextService.GetLoyaltyForUser(command.ChatMessage.Username,
                        this.TcContainer.Channel,
                        command.ChatMessage.UserId,
                        command.ChatMessage.Username);

                    if (userLoyalty != null)
                    {
                        var ripLoyaltySentences = new List<string>
                            {
                                $"@{command.ChatMessage.DisplayName}'s {BotChannelSettings.Loyalty.LoyaltyName} spontaneously combusted!",
                                $"@{command.ChatMessage.DisplayName}'s {BotChannelSettings.Loyalty.LoyaltyName} turned to ashes!",
                                $"@{command.ChatMessage.DisplayName} just scorched his loot.",
                                $"@{command.ChatMessage.DisplayName} just cooked his loot... it's not fit to eat!",
                                $"@{command.ChatMessage.DisplayName} witnessed all their {BotChannelSettings.Loyalty.LoyaltyName} ignite... #rip{BotChannelSettings.Loyalty.LoyaltyName}"
                            };
                        int randonIndex = rnd.Next(ripLoyaltySentences.Count);

                        TwitchClient
                            .SendMessage(BotUserSettings.BotChannel,
                                "/me " + (string)ripLoyaltySentences[randonIndex]);

                        ContextService.AddLoyalty(User,
                            BotUserSettings.BotChannel, userLoyalty, -userLoyalty.CurrentPoints);
                    }
                    else
                    {
                        TwitchClient
                            .SendMessage(BotUserSettings.BotChannel, "/me " +
                                                                     $"@{command.ChatMessage.DisplayName}, you haven't earned any {BotChannelSettings.Loyalty.LoyaltyName} yet. Stay and the channel and you will recieve {BotChannelSettings.Loyalty.LoyaltyValue.ToString()} every {BotChannelSettings.Loyalty.LoyaltyInterval.ToString()} minute.");
                    }
                }

            }

        }


        /// <summary>
        /// Create a strawpoll
        /// </summary>
        /// <param name="title">Title</param>
        /// <param name="options">List<string>()</param>
        private async void CreateStrawPoll(string title, List<string> options)
        {
            // Establish the poll settins
            string pollTitle = title;
            List<string> allOptions = options;
            bool multipleChoice = true;

            // Create the poll
            var poll = new StrawPoll();
            var pollResult = await poll.CreatePollAsync(title, options, true, DupCheck.NORMAL, false);

            TcContainer.Polls.Add(pollResult.Id);

            // Show poll link
            TwitchClient.SendMessage(TcContainer.Channel, $"/me Vote for \"{pollTitle}\" here => {pollResult.PollUrl}");

            hub.ConsoleLog("Created poll '" + title + "' => " + pollResult.PollUrl);
            hub.Clients.Caller.CreatedPoll(title, pollResult.Id);
            hub.Clients.Caller.CreatePoll(title, allOptions);
        }

        /// <summary>
        /// Generate random name of x characters
        /// </summary>
        /// <param name="len"></param>
        /// <returns></returns>
        private static string GenerateName(int len)
        {
            SysRandom r = new SysRandom();
            string[] consonants = { "b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "l", "n", "p", "q", "r", "s", "sh", "zh", "t", "v", "w", "x" };
            string[] vowels = { "a", "e", "i", "o", "u", "ae", "y" };
            string Name = "";
            Name += consonants[r.Next(consonants.Length)].ToUpper();
            Name += vowels[r.Next(vowels.Length)];
            int b = 2; //b tells how many times a new letter has been added. It's 2 right now because the first two letters are already in the name.
            while (b < len)
            {
                Name += consonants[r.Next(consonants.Length)];
                b++;
                Name += vowels[r.Next(vowels.Length)];
                b++;
            }

            return Name;


        }

        private static bool NextBool(int truePercentage = 50)
        {
            SysRandom r = new SysRandom();
            return r.NextDouble() < truePercentage / 100.0;
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