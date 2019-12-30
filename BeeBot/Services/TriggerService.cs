using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using BeeBot.Models;
using BeeBot.Signalr;
using Google.Apis.Logging;
using StrawpollNET;
using StrawpollNET.Data;
using TwitchLib.Api;
using TwitchLib.Api.Core.Exceptions;
using TwitchLib.Api.Models.v5.Channels;
using TwitchLib.Client;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;
using YoutubeSearch;
using YTBot.Models;
using YTBot.Models.ViewModels;
using ChannelSubscribers = TwitchLib.Api.V5.Models.Channels.ChannelSubscribers;
using SysRandom = System.Random;

namespace YTBot.Services
{
    public class TriggerService : IDisposable
    {
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

        private ContextService ContextService { get; }
        private ApplicationUser User { get; }
        private BotChannelSettings BotChannelSettings { get; }

        private TwitchClientContainer TcContainer { get; }
        private TwitchClient TwitchClient { get; }
        private BotUserSettings BotUserSettings { get; }
        private TwitchHub hub { get; }

        public TwitchAPI Api { get; set; }
        public List<Trigger> Triggers { get; set; }
        private bool LoyaltyEnabled { get; }

        public void Dispose()
        {
            var disposableServiceProvider = ContextService as IDisposable;

            if (disposableServiceProvider != null) disposableServiceProvider.Dispose();
        }

        /// <summary>
        ///     Check if trigger is called
        /// </summary>
        /// <param name="command"></param>
        /// <returns>Trigger called</returns>
        public IEnumerable<Trigger> TriggerCheck(ChatCommand command)
        {
            return Triggers.Where(
                t => t.Active == true && t.TriggerName.ToLower().Equals(command.CommandText.ToLower()));
        }

        /// <summary>
        ///     Check if trigger is called
        /// </summary>
        /// <param name="command"></param>
        /// <returns>Trigger called</returns>
        public bool LoyaltyCheck(ChatCommand command)
        {
            return BotChannelSettings.Loyalty.Track != null && BotChannelSettings.Loyalty.Track == true &&
                   (command.CommandText.ToLower().Equals(BotChannelSettings.Loyalty.LoyaltyName.ToLower()) || command
                        .CommandText.ToLower().Equals("burn" + BotChannelSettings.Loyalty.LoyaltyName.ToLower()));
        }

        /// <summary>
        ///     Check if giveaways trigger
        /// </summary>
        /// <param name="command"></param>
        /// <returns>Giveaway available</returns>
        public IEnumerable<Giveaway> GiveAwayCheck(ChatCommand command)
        {
            return TcContainer.Giveaways.Where(g =>
                g.Trigger.ToLower().Equals(command.CommandText) && g.EndsAt >= DateTime.Now);
        }

        public bool KillStatCheck(ChatCommand command)
        {
            if (command.ChatMessage.IsBroadcaster || command.ChatMessage.IsModerator ||
                command.ChatMessage.IsSubscriber)
            {
                if (command.CommandText.ToLower().Equals("kill") || command.CommandText.ToLower().Equals("death") ||
                    command.CommandText.ToLower().Equals("squad") || command.CommandText.ToLower().Equals("reset"))
                {
                    return true;
                }

                return false;
            }

            return false;

        }

        public bool TitleAndGameCheck(ChatCommand command)
        {
            if (command.ChatMessage.IsBroadcaster || command.ChatMessage.IsModerator)
            {
                if (command.CommandText.ToLower().Equals("title") || command.CommandText.ToLower().Equals("game"))
                {
                    return true;
                }

                return false;
            }

            return false;
        }

        public void WhichServerCheck(ChatMessage message)
        {
            if (((message.Message.ToLower().Contains("which") || message.Message.ToLower().Contains("what")) && (message.Message.ToLower().Contains("server")) || message.Message.ToLower().Contains("server?")))
            {
                try
                {
                    if (Triggers.Any(t => t.TriggerName == "server" && t.Active != null && t.Active.Value == true && t.TriggerResponse != string.Empty))
                    {
                        var serverTrigger = Triggers.FirstOrDefault(t => t.TriggerName == "server" && t.TriggerResponse != string.Empty);
                        TwitchClient.SendMessage(BotUserSettings.BotChannel, "/me " + $"{message.Channel} is playing on {serverTrigger.TriggerResponse}");
                    }
                }
                catch (Exception e)
                {
                    hub.ConsoleLog("Error on WichServerCheck" + e.Message, false);
                }
            }
        }

        public async void Run(Trigger trigger, StreamViewer viewer, ChatCommand command)
        {
            if (trigger != null)
            {
                switch (trigger.TriggerType)
                {
                    case TriggerType.Message:
                        TcContainer.Client.SendMessage(TcContainer.Channel, trigger.TriggerResponse);
                        break;
                    case TriggerType.BuiltIn:
                        // !addpoll
                        if (trigger.TriggerName.Equals("addpoll"))
                        {
                            // Establish the poll settins
                            var match = Regex.Match(command.ChatMessage.Message, "!addpoll.*\"(\\w.*)\"\\s+(\\w.*)");

                            var title = "";
                            var arguments = new List<string>();

                            if (match.Success)
                            {
                                title = match.Groups[1].Value;
                                var test = match.Groups[2].Value.Split('|');
                                foreach (var option in test) arguments.Add(option.Trim());
                            }

                            CreateStrawPoll(title, arguments);
                        }
                        else if (trigger.TriggerName.Equals("addcommand"))
                        {
                            if (ContextService.GetTrigger(command.ArgumentsAsList.FirstOrDefault(), User.UserName) != null)
                            {
                                TwitchClient.SendMessage(TcContainer.Channel, "/me !" + command.ArgumentsAsList.FirstOrDefault() + " already exists.");
                                return;
                            }

                            var triggerName = command.ArgumentsAsList.FirstOrDefault();
                            var tmp = command.ArgumentsAsList;
                            tmp.RemoveAt(0);
                            var truiggerMsg = string.Join("  ", tmp);

                            var newTrigger = new Trigger
                            {
                                Id = 0,
                                TriggerName = triggerName,
                                TriggerResponse = truiggerMsg
                            };
                            ContextService.ModAddedTriggerMessage(newTrigger, User.UserName);
                            TwitchClient.SendMessage(TcContainer.Channel, "/me !" + newTrigger.TriggerName + " added.");

                        }
                        else if (trigger.TriggerName.Equals("removecommand"))
                        {
                            var dbTrigger = ContextService.GetTrigger(command.ArgumentsAsList.FirstOrDefault(),
                                User.UserName);
                            if (dbTrigger == null)
                            {
                                TwitchClient.SendMessage(TcContainer.Channel, "/me !" + command.ArgumentsAsList.FirstOrDefault() + " no such command.");
                                return;
                            }

                            var removedTrigger = ContextService.ModRemovedTriggerMessage(dbTrigger, User.UserName);

                            if (removedTrigger == null)
                            {
                                hub.ConsoleLog("Error on !removecommand: Mod tried to remove non-message trigger", true);
                            }

                            TwitchClient.SendMessage(TcContainer.Channel, "/me !" + dbTrigger.TriggerName + " removed.");
                        }
                        else if (trigger.TriggerName.Equals("changecommand"))
                        {
                            var dbTrigger = ContextService.GetTrigger(command.ArgumentsAsList.FirstOrDefault(),
                                User.UserName);
                            if (dbTrigger == null)
                            {
                                TwitchClient.SendMessage(TcContainer.Channel, "/me !" + command.ArgumentsAsList.FirstOrDefault() + " no such command.");
                                return;
                            }
                            var tmp = command.ArgumentsAsList;
                            tmp.RemoveAt(0);
                            var triggerMsg = string.Join("  ", tmp);
                            dbTrigger.TriggerResponse = triggerMsg;

                            var returnTrigger = ContextService.ModChangedTriggerMessage(dbTrigger, User.UserName);
                            if (returnTrigger == null)
                            {
                                hub.ConsoleLog("Error on !changecommand: Mod tried to change non-message trigger text", true);
                            }
                            TwitchClient.SendMessage(TcContainer.Channel, "/me !" + dbTrigger.TriggerName + " changed.");
                        }

                        // !ban
                        else if (trigger.TriggerName.Equals("ban"))
                        {
                            TwitchClient
                                .BanUser(TcContainer.Channel, command.ArgumentsAsList.FirstOrDefault(), "Banned!");
                        }

                        // !clip
                        else if (trigger.TriggerName.Equals("clip"))
                        {
                            try
                            {
                                var channelData = await Api.V5.Channels.GetChannelAsync(BotUserSettings.ChannelToken);
                                var clip = await Api.Helix.Clips.CreateClipAsync(channelData.Id,
                                    BotUserSettings.ChannelToken);
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

                        // !commands || !help
                        else if (trigger.TriggerName.Equals("commands") || trigger.TriggerName.Equals("help"))
                        {
                            var availableTriggers = ContextService.GetAllTriggers(User, viewer, command);
                            //TwitchClient.WhisperThrottler = new MessageThrottler(TwitchClient, 100, new TimeSpan(0, 0, 1) );
                            foreach (var availableTrigger in availableTriggers.Where(t => t.TriggerType != TriggerType.Message))
                            {
                                TwitchClient.SendWhisper(command.ChatMessage.Username, $"!{availableTrigger.TriggerName.ToLower()} - {availableTrigger.TriggerResponse}");
                            }
                        }

                        // !giveaway
                        else if (trigger.TriggerName.Equals("giveaway"))
                        {
                            var giveaway = TcContainer.Giveaways.FirstOrDefault(g =>
                                g.Trigger.ToLower().Equals(command.CommandText.ToLower()) && g.EndsAt >= DateTime.Now);

                            var closingIn = giveaway.EndsAt - DateTime.Now;
                            var closingInMinutes = closingIn.Minutes.ToString();

                            TwitchClient.SendMessage(TcContainer.Channel,
                                "/me Giveaway !" + giveaway.Trigger + " for \"" + giveaway.Prize + "\" closing in " +
                                closingInMinutes + " minutes.");
                        }

                        // !multilink
                        else if (trigger.TriggerName.Equals("multilink"))
                        {
                            var baseurl = $"https://multistre.am/{TcContainer.Channel}/";

                            var restOfString = string.Join("/", command.ArgumentsAsList.ToList());

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
                                var pollId = TcContainer.Polls.Last();
                                var poll = new StrawPoll();
                                var pollFetch = await poll.GetPollAsync(pollId);

                                // Show results
                                TwitchClient.SendMessage(TcContainer.Channel,
                                    "/me " + $"The last poll results for {pollFetch.Title} {pollFetch.PollUrl} are:");
                                var results = pollFetch.Options.Zip(pollFetch.Votes,
                                    (a, b) => new { Option = a, Vote = b });
                                var totalVotes = pollFetch.Votes.Sum();
                                foreach (var result in results)
                                {
                                    var percentage = result.Vote == 0
                                        ? "0"
                                        : (result.Vote / (double)totalVotes * 100).ToString();
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
                                var twitchUser = await Api.V5.Users.GetUserByNameAsync(streamerName);
                                var channelData = await Api.V5.Channels.GetChannelByIDAsync(twitchUser.Matches[0].Id);

                                lastStreamed = " - Last streamed '" + channelData.Game + "'";
                                streamerName = twitchUser.Matches[0].DisplayName;

                                TwitchClient
                                    .SendMessage(TcContainer.Channel, "" +
                                                                      $"Please go give our friend " + streamerName +
                                                                      " a follow at " +
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
                                timeout = new TimeSpan(0, 0, Convert.ToInt32(command.ArgumentsAsList.Last()),
                                    0);

                            var message = "Timed out for " + Convert.ToString(timeout.Minutes) + " minutes";
                            var joinedChannel = TwitchClient.GetJoinedChannel(TcContainer.Channel);
                            TwitchClient.TimeoutUser(joinedChannel, command.ArgumentsAsList.FirstOrDefault(),
                                timeout, message);
                        }

                        // !unban
                        else if (trigger.TriggerName.Equals("unban"))
                        {
                            TwitchClient.UnbanUser(TcContainer.Channel, command.ArgumentsAsList.FirstOrDefault());
                        }

                        // !permit
                        else if (trigger.TriggerName.Equals("permit"))
                        {
                            hub.AddLinkPermit(command.ChatMessage, null);
                        }



                        break;
                    case TriggerType.Stat:
                        // !follower
                        if (trigger.TriggerName.Equals("follower"))
                        {
                            var twitchUser = await Api.V5.Users.GetUserByNameAsync(command.ChatMessage.Username);
                            var channelData = await Api.V5.Users.GetUserByNameAsync(TcContainer.Channel);
                            try
                            {
                                var follower = await Api.V5.Users.CheckUserFollowsByChannelAsync(
                                    twitchUser.Matches[0].Id,
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
                            var channelData = await Api.V5.Channels.GetChannelAsync(BotUserSettings.ChannelToken);
                            ChannelSubscribers channelSubsData = null;
                            try
                            {
                                channelSubsData = await Api.V5.Channels.GetChannelSubscribersAsync(channelData.Id, null, null, null,
                                        BotUserSettings.ChannelToken);
                            }
                            catch (Exception e)
                            {
                                TwitchClient.SendMessage(TcContainer.Channel,
                                    $"{TcContainer.Channel} has {channelData.Followers} followers.");
                            }
                            TwitchClient.SendMessage(TcContainer.Channel,
                                $"{TcContainer.Channel} has {channelData.Followers} followers and {channelSubsData.Total} subscribers.");
                        }

                        // !sub
                        else if (trigger.TriggerName.Equals("sub"))
                        {
                            try
                            {
                                var channelData = await Api.V5.Channels.GetChannelAsync(BotUserSettings.ChannelToken);
                                var channelId = channelData.Id;
                                var twitchUser = await Api.V5.Users.GetUserByNameAsync(command.ChatMessage.Username);
                                var twitchUserId = twitchUser.Matches.First().Id;
                                var sub = await Api.V5.Users.CheckUserSubscriptionByChannelAsync(twitchUserId,
                                    channelId);

                                TwitchClient.SendMessage(TcContainer.Channel,
                                    $@"{twitchUser.Matches[0].DisplayName} subscribed to the channel at {
                                            sub.CreatedAt.ToShortDateString()
                                        }");
                            }
                            // will throw exception on not following
                            catch (BadResourceException e)
                            {
                                TwitchClient.SendMessage(TcContainer.Channel,
                                    $@"{command.ChatMessage.DisplayName} is not a subscriber yet :(");
                            }
                            catch (BadScopeException e)
                            {
                                hub.ConsoleLog("Error on !sub: " + e.Message, true);
                            }
                        }

                        // !top
                        else if (trigger.TriggerName.Equals("top"))
                        {
                            int number = 20;
                            try
                            {
                                var regEx = Regex.Match(command.ArgumentsAsString.ToLower(), "(\\d+)");
                                number = Convert.ToInt32(regEx.Groups[1].Value);
                                if (number > 20)
                                {
                                    number = 20;
                                }
                            }
                            catch (Exception e)
                            {
                                hub.ConsoleLog("Error on !top: " + e.Message, true);
                            }

                            var thisUser = User;
                            var topLoyalty = ContextService.TopLoyalty(thisUser, number);

                            var message = "Top" + number + ": ";

                            var counter = 1;
                            foreach (var loyalty in topLoyalty)
                            {
                                message += counter + ". " + loyalty.TwitchUsername + " (" + loyalty.CurrentPoints +
                                           ") \n";
                                counter++;
                            }

                            TwitchClient.SendMessage(TcContainer.Channel, "/me " + message);
                        }

                        // !uptime
                        else if (trigger.TriggerName.Equals("uptime"))
                        {
                            var channel = Api.V5.Channels.GetChannelAsync(BotUserSettings.ChannelToken).Result;
                            var uptime = Api.V5.Streams.GetUptimeAsync(channel.Id);


                            if (uptime.Result == null)
                            {
                                TwitchClient
                                    .SendMessage(TcContainer.Channel, "/me " + $"Channel is offline.");
                            }
                            else
                            {
                                if (uptime.Result.Value.Hours == 0)
                                    TwitchClient
                                        .SendMessage(TcContainer.Channel,
                                            "/me " +
                                            $"{channel.DisplayName} has been live for {uptime.Result.Value.Minutes} minutes.");
                                else
                                    TwitchClient
                                        .SendMessage(TcContainer.Channel,
                                            "/me " +
                                            $"{channel.DisplayName} has been live for {uptime.Result.Value.Hours} hours, {uptime.Result.Value.Minutes} minutes.");
                            }
                        }

                        break;
                    case TriggerType.Loyalty:
                        // !bonus
                        if (trigger.TriggerName.Equals("bonus"))
                            try
                            {
                                var verb = "";

                                var loyaltyAmount = Convert.ToInt32(command.ChatMessage.Message.Split(' ')[2]);
                                verb = loyaltyAmount > 0 ? "has been given" : "has been deprived of";
                                var destinationViewerName = command.ChatMessage.Message.Split(' ')[1];

                                var destinationViewerLoyalty = ContextService.GetLoyaltyForUser(
                                    command.ChatMessage.Username.ToLower(), BotUserSettings.BotChannel, null,
                                    destinationViewerName);

                                if (loyaltyAmount != null && destinationViewerLoyalty != null)
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

                        // !bonusall
                        else if (trigger.TriggerName.Equals("bonusall"))
                            try
                            {
                                var verb = "";
                                var bonusValue =
                                    Convert.ToInt32(Regex.Match(command.ArgumentsAsString, @"-?\d+").Value);

                                ContextService.AddLoyalty(User,
                                    TcContainer.Channel, hub.GetUsersInChannel(BotUserSettings.BotChannel.ToLower()),
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

                        // !give
                        else if (trigger.TriggerName.Equals("give"))
                            try
                            {
                                // get who to give it to
                                var loyaltyAmount =
                                    Math.Abs(Convert.ToInt32(command.ChatMessage.Message.Split(' ')[2]));
                                var destinationViewerName = command.ChatMessage.Message.Split(' ')[1];
                                var sourceViewerId = command.ChatMessage.UserId;
                                var sourceViewerName = command.ChatMessage.Username;

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
                                if (loyaltyAmount != null && sourceViewerLoyalty != null &&
                                    sourceViewerLoyalty.CurrentPoints < loyaltyAmount)
                                {
                                    TwitchClient
                                        .SendMessage(BotUserSettings.BotChannel, "/me " +
                                                                                 $"Stop wasting my time @{command.ChatMessage.DisplayName}, you ain't got that much {BotChannelSettings.Loyalty.LoyaltyName}");
                                }
                                // give away loot
                                else if (loyaltyAmount != null && sourceViewerLoyalty != null &&
                                         sourceViewerLoyalty.CurrentPoints >= loyaltyAmount)
                                {
                                    ContextService.AddLoyalty(User,
                                        command.ChatMessage.Channel.ToLower(), sourceViewerLoyalty, -loyaltyAmount);
                                    ContextService.AddLoyalty(User,
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

                        break;
                    case TriggerType.Game:
                        // !gamble
                        if (trigger.TriggerName.Equals("gamble"))
                        {
                            if (command.ChatMessage.Message.Equals("!gamble"))
                            {
                                TwitchClient
                                    .SendMessage(TcContainer.Channel,
                                        "/me " +
                                        $" Type !gamble <amount> or !gamble allin. I use the glorious random number generator web-service from RANDOM.ORG that generates randomness via atmospheric noise.");
                            }
                            else if (command.ChatMessage.Message.ToLower().StartsWith("!gamble"))
                            {
                                // get 
                                var loyalty = ContextService.GetLoyaltyForUser(User.UserName, TcContainer.Channel,
                                    command.ChatMessage.UserId, command.ChatMessage.DisplayName.ToLower());

                                // timeout for 5 minutes if user has gamble before
                                if (loyalty != null && (loyalty.LastGamble == null ||
                                                        loyalty.LastGamble.HasValue &&
                                                        loyalty.LastGamble.Value.AddMinutes(6) <= DateTime.Now))
                                    try
                                    {
                                        var r = new Random.Org.Random();

                                        // get who to give it to
                                        var gambleAmount = command.ChatMessage.Message.Split(' ')[1].ToLower()
                                            .Equals("allin")
                                            ? loyalty.CurrentPoints
                                            : Math.Abs(Convert.ToInt32(command.ChatMessage.Message.Split(' ')[1]));

                                        var sourceViewerId = command.ChatMessage.UserId;
                                        var sourceViewerName = command.ChatMessage.Username;

                                        var sourceViewerLoyalty = ContextService.GetLoyaltyForUser(
                                            User.UserName,
                                            TcContainer.Channel,
                                            sourceViewerId,
                                            sourceViewerName);

                                        // user has enough loyalty to gamble
                                        if (sourceViewerLoyalty != null &&
                                            sourceViewerLoyalty.CurrentPoints >= gambleAmount)
                                        {
                                            var rolledNumber = 50;
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
                                                var newAmount = sourceViewerLoyalty.CurrentPoints - gambleAmount;
                                                TwitchClient
                                                    .SendMessage(TcContainer.Channel,
                                                        "/me " +
                                                        $"@{command.ChatMessage.DisplayName} rolled a sad {rolledNumber}, lost {gambleAmount} and now has {newAmount} {BotChannelSettings.Loyalty.LoyaltyName}!! #theSaltIsReal #rigged");
                                                ContextService.AddLoyalty(User,
                                                    command.ChatMessage.Channel.ToLower(), sourceViewerLoyalty,
                                                    -gambleAmount);
                                            }
                                            // rolled 50-99
                                            else if (rolledNumber >= 50 && rolledNumber < 100)
                                            {
                                                var newAmount = sourceViewerLoyalty.CurrentPoints - gambleAmount +
                                                                gambleAmount * 2;

                                                TwitchClient
                                                    .SendMessage(TcContainer.Channel, "/me " +
                                                                                      $"@{command.ChatMessage.DisplayName} rolled {rolledNumber}, won {gambleAmount * 2} and now has {newAmount} {BotChannelSettings.Loyalty.LoyaltyName}!");

                                                ContextService.AddLoyalty(User,
                                                    command.ChatMessage.Channel.ToLower(), sourceViewerLoyalty,
                                                    gambleAmount);
                                            }
                                            // rolled 100 win * 10
                                            else
                                            {
                                                var newAmount = sourceViewerLoyalty.CurrentPoints - gambleAmount +
                                                                (gambleAmount * 10);

                                                TwitchClient
                                                    .SendMessage(TcContainer.Channel, "/me " +
                                                                                      $"@{command.ChatMessage.DisplayName} did an epic roll, threw {rolledNumber}, won {gambleAmount * 10} and now has {newAmount} {BotChannelSettings.Loyalty.LoyaltyName}!! #houseCries");

                                                ContextService.AddLoyalty(User,
                                                    command.ChatMessage.Channel.ToLower(), sourceViewerLoyalty,
                                                    gambleAmount * 3);
                                            }

                                            ContextService.StampLastGamble(User,
                                                command.ChatMessage.Channel.ToLower(), sourceViewerLoyalty);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        hub.ConsoleLog("Error on !gamble: " + e.Message, true);
                                    }
                                else if (loyalty == null)
                                    TwitchClient
                                        .SendMessage(TcContainer.Channel, "/me " +
                                                                          $"@{command.ChatMessage.DisplayName}, you haven't earned any {BotChannelSettings.Loyalty.LoyaltyName} to gamble yet. Stay and the channel and you will recieve {BotChannelSettings.Loyalty.LoyaltyValue} every {BotChannelSettings.Loyalty.LoyaltyInterval} minute.");
                                else
                                    TwitchClient
                                        .SendMessage(TcContainer.Channel, "/me " +
                                                                          $"Chill out @{command.ChatMessage.DisplayName}, you gotta wait 5 minutes from your last gamble to roll the dice again!");
                            }
                        }

                        // !roulette
                        else if (trigger.TriggerName.Equals("roulette"))
                        {
                            var client = TwitchClient;
                            var joinedChannel = client.GetJoinedChannel(TcContainer.Channel);

                            var chatter = ContextService.GetStreamViewer(User.Email, TcContainer.Channel,
                                command.ChatMessage.UserId, command.ChatMessage.DisplayName.ToLower());

                            if (chatter.LastRoulette != null &&
                                chatter.LastRoulette.Value.AddSeconds(ROULETTETIMEOUT) >= DateTime.Now)
                            {
                                var sleeptime = chatter.LastRoulette.Value.AddSeconds(ROULETTETIMEOUT) - DateTime.Now;
                                client.SendMessage(joinedChannel,
                                    $"@{command.ChatMessage.DisplayName}, please wait {sleeptime.Minutes} more minutes before playing roulette.");
                            }
                            else
                            {
                                client.SendMessage(joinedChannel,
                                    $"@{command.ChatMessage.DisplayName} places the gun to their head!");
                                var rnd = new SysRandom(Guid.NewGuid().GetHashCode());
                                var theNumberIs = rnd.Next(1, 6);
                                var timeout = new TimeSpan(0, 0, 1, 0);
                                var message = "Timed out for " + Convert.ToString(timeout.Minutes) + " minutes";

                                // User dies(timeout) if 1 is drawn
                                if (theNumberIs == 1)
                                {
                                    Task.Delay(1000);
                                    client.SendMessage(joinedChannel,
                                        $"@{command.ChatMessage.DisplayName} pulls the trigger...... brain goes everywhere!! Who knew @{command.ChatMessage.DisplayName} had that much in there?");
                                    //Timeout user
                                    client.TimeoutUser(joinedChannel, command.ChatMessage.DisplayName, timeout,
                                        message);
                                    client.SendMessage(joinedChannel,
                                        $"@{command.ChatMessage.DisplayName} is now chilling on the floor and sort of all over the place for a minute!");
                                }
                                // Gets away with it!
                                else
                                {
                                    Task.Delay(1000);
                                    client.SendMessage(joinedChannel,
                                        $"@{command.ChatMessage.DisplayName} pulls the trigger...... CLICK!....... and survives!!");
                                }

                                // Update last time roulette was played
                                ContextService.SetRouletteTime(User.Email, TcContainer.Channel,
                                    command.ChatMessage.UserId, command.ChatMessage.DisplayName.ToLower());
                            }
                        }

                        // !russian
                        else if (trigger.TriggerName.ToLower().Equals("russian"))
                        {
                            var roulette = TcContainer.RRulette;
                            var ccontainer = TcContainer;
                            var regex = new Regex(@"!russian\s(\d.*)");
                            var match = regex.Match(command.ChatMessage.Message.ToLower());
                            var bet = 0;

                            // get 
                            var sourceViewerId = command.ChatMessage.UserId;
                            var sourceViewerName = command.ChatMessage.Username;
                            var player = ContextService.GetLoyaltyForUser(
                                HttpContext.Current.User.Identity.Name,
                                TcContainer.Channel,
                                sourceViewerId,
                                sourceViewerName);
                            // start new roulette
                            if (roulette == null)
                            {
                                if (ccontainer.LastRussian != null &&
                                    (DateTime.Now - ccontainer.LastRussian.AddMinutes(6)).Minutes < 0)
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
                                TcContainer.RRulette = newRoulette;

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
                                var rroulette = TcContainer.RRulette;

                                if (rroulette.Started) return;

                                if (player == null || player.CurrentPoints < rroulette.BuyIn)
                                {
                                    TwitchClient
                                        .SendMessage(TcContainer.Channel,
                                            "/me " +
                                            $"@{command.ChatMessage.DisplayName}, you need to have {rroulette.BuyIn} {BotChannelSettings.Loyalty.LoyaltyName} to enter the big boys Russian Roulette");
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
                                hub.NextSong();
                            else if (command.ChatMessage.IsModerator && TcContainer.ModsControlSongrequest)
                                hub.NextSong();
                        }

                        // !pause
                        else if (trigger.TriggerName.ToLower().Equals("pause"))
                        {
                            if (command.ChatMessage.IsBroadcaster)
                                hub.Pause();
                            else if (command.ChatMessage.IsModerator && TcContainer.ModsControlSongrequest) hub.Pause();
                        }

                        // !play
                        else if (trigger.TriggerName.ToLower().Equals("play"))
                        {
                            if (command.ChatMessage.IsBroadcaster && TcContainer.ModsControlSongrequest)
                                hub.Play();
                            else if (command.ChatMessage.IsModerator && TcContainer.ModsControlSongrequest) hub.Play();
                        }

                        // !prev
                        else if (trigger.TriggerName.ToLower().Equals("prev"))
                        {
                            if (command.ChatMessage.IsBroadcaster)
                                hub.PrevSong();
                            else if (command.ChatMessage.IsModerator && TcContainer.ModsControlSongrequest)
                                hub.PrevSong();
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

                                    var video = new VideoVm();

                                    video.Id = query.AllKeys.Contains("v") ? query["v"] : uri.Segments.Last();

                                    video = await hub.GetVideoInfoByHttp(commandArguments, video.Id);

                                    if (TcContainer.SongRequests.Any(a => a.VideoId == video.Id))
                                    {
                                        TwitchClient.SendMessage(TcContainer.Channel,
                                            $"\"{video.Title}\" is already in the playlist.");
                                    }
                                    else if (SongDurationCheck(video) == false)
                                    {
                                        TwitchClient.SendMessage(TcContainer.Channel, $"\"{video.Title}\" is over 10 minutes long, please request a song that is shorter!");
                                    }
                                    else if (video.NumViews < 500)
                                    {
                                        TwitchClient.SendMessage(TcContainer.Channel, $"Sorry, {userName}, \"{video.Title}\" has too few views on YouTube, the song will not be added :(.");
                                    }
                                    else
                                    {
                                        var song = hub.UpdatePlaylistFromCommand(commandArguments, video.Title, userName, video.Id, video.Length);
                                        TwitchClient.SendMessage(TcContainer.Channel,
                                            $"\"{song.Title}\" added to playlist by @{song.RequestedBy}.");
                                    }
                                }
                                // search for the song on youtube
                                else
                                {
                                    // Keyword
                                    var querystring = command.ArgumentsAsString;

                                    var youtubeSearch = new VideoSearch();
                                    var youtubeSearchResult = youtubeSearch.SearchQuery(querystring, 1);

                                    if (youtubeSearchResult != null && youtubeSearchResult.Count > 0)
                                    {
                                        var firstHit = youtubeSearchResult.FirstOrDefault();
                                        var firstVideoUrl = new Uri(firstHit.Url);
                                        var video = new VideoVm();
                                        video.Id = string.Empty;
                                        video.Title = firstHit.Title;
                                        video.Url = firstHit.Url;
                                        try
                                        {
                                            video.Length = StringToTimeSpan(firstHit.Duration);
                                        }
                                        catch (Exception e)
                                        {
                                            // Video is potentially over 1 hour long
                                            video.Length = new TimeSpan(1, 0, 0);
                                        }


                                        var query = HttpUtility.ParseQueryString(firstVideoUrl.Query);
                                        if (query.AllKeys.Contains("v"))
                                            video.Id = query["v"];
                                        else
                                            video.Id = firstVideoUrl.Segments.Last();

                                        video = await hub.GetVideoInfoByHttp(commandArguments, video.Id);

                                        if (TcContainer.SongRequests.Any(a => a.VideoId == video.Id))
                                        {
                                            TwitchClient.SendMessage(TcContainer.Channel, $"\"{firstHit.Title}\" is already in the playlist.");
                                        }
                                        else if (SongDurationCheck(video) == false)
                                        {
                                            TwitchClient.SendMessage(TcContainer.Channel, $"\"{firstHit.Title}\" is over 10 minutes long, please request a song that is shorter!");
                                        }
                                        else if (video.NumViews < 500)
                                        {
                                            TwitchClient.SendMessage(TcContainer.Channel, $"\"{firstHit.Title}\" has too few views on YouTube, the song will not be added :(.");
                                        }
                                        else
                                        {
                                            var song = hub.UpdatePlaylistFromCommand(firstHit.Url, firstHit.Title, userName, video.Id, video.Length);
                                            TwitchClient.SendMessage(TcContainer.Channel,
                                                $"\"{song.Title}\" added to playlist by @{song.RequestedBy}.");
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
                                hub.Stop();
                            else if (command.ChatMessage.IsModerator && TcContainer.ModsControlSongrequest) hub.Stop();
                        }

                        // !volume
                        else if (trigger.TriggerName.Equals("volume"))
                        {
                            var volume = Math.Abs(Convert.ToInt32(command.ChatMessage.Message.Split(' ')[1]));
                            if (command.ChatMessage.IsBroadcaster)
                                hub.Volume(volume);
                            else if (command.ChatMessage.IsModerator && TcContainer.ModsControlSongrequest)
                                hub.Volume(volume);
                        }

                        break;
                }
            }
            else
            {
                if (command.CommandText.ToLower().Equals("game"))
                {
                    try
                    {
                        if (command.ChatMessage.IsBroadcaster || command.ChatMessage.IsModerator)
                        {
                            var streamInfo = hub.RetrieveStreamInfo();
                            await hub.SaveStreamInfo(streamInfo.Title, command.ArgumentsAsString, null, null, "0");
                        }
                    }
                    catch (Exception e)
                    {
                        hub.ConsoleLog("Error on !game: " + e.Message, true);
                    }
                }
                else if (command.CommandText.ToLower().Equals("title"))
                {
                    try
                    {
                        if (command.ChatMessage.IsBroadcaster || command.ChatMessage.IsModerator)
                        {
                            var streamInfo = hub.RetrieveStreamInfo();
                            await hub.SaveStreamInfo(command.ArgumentsAsString, streamInfo.Game, null, null, "0");
                        }
                    }
                    catch (Exception e)
                    {
                        hub.ConsoleLog("Error on !title: " + e.Message, true);
                    }
                }
                // !kill !death !squad
                if (command.CommandText.ToLower().Equals("kill") || command.CommandText.ToLower().Equals("death") ||
                    command.CommandText.ToLower().Equals("squad") || command.CommandText.ToLower().Equals("reset"))
                {
                    try
                    {
                        var user = ContextService.GetUserFromChannelname(command.ChatMessage.Channel);
                        var bcs = ContextService.GetBotChannelSettings(user);
                        var killStats = bcs.KillStats;
                        if (command.CommandText.ToLower().Equals("kill"))
                        {
                            if (command.ArgumentsAsList.Count == 0)
                            {
                                // no argument, increment
                                killStats.IncrementKills();

                            }
                            else
                            {
                                killStats.Kills = Convert.ToInt32(command.ArgumentsAsList[0]);
                            }
                        }
                        else if (command.CommandText.ToLower().Equals("death"))
                        {
                            if (command.ArgumentsAsList.Count == 0)
                            {
                                // no argument, increment
                                killStats.IncrementDeaths();

                            }
                            else
                            {
                                killStats.Deaths = Convert.ToInt32(command.ArgumentsAsList[0]);
                            }

                        }
                        else if (command.CommandText.ToLower().Equals("squad"))
                        {
                            if (command.ArgumentsAsList.Count == 0)
                            {
                                // no argument, increment
                                killStats.IncrementSquad();

                            }
                            else
                            {
                                killStats.SquadKills = Convert.ToInt32(command.ArgumentsAsList[0]);
                            }
                        }
                        else if (command.CommandText.ToLower().Equals("reset"))
                        {
                            killStats.Kills = 0;
                            killStats.SquadKills = 0;
                            killStats.Deaths = 0;
                        }

                        ContextService.SaveKillStats(BotChannelSettings, killStats);
                    }
                    catch (Exception e)
                    {
                        hub.ConsoleLog("Error on !" + command.CommandText.ToLower() + ": " + e.Message, false);
                    }
                }
                // giveaways enrollment
                if (TcContainer.Giveaways.Any(g =>
                    g.Trigger.ToLower().Equals(command.CommandText.ToLower()) && g.EndsAt >= DateTime.Now))
                {
                    var giveaway = TcContainer.Giveaways.FirstOrDefault(g =>
                        g.Trigger.ToLower().Equals(command.CommandText.ToLower()) && g.EndsAt >= DateTime.Now);

                    var twitchUser = await Api.V5.Users.GetUserByNameAsync(command.ChatMessage.Username);

                    var channel = await Api.V5.Users.GetUserByNameAsync(TcContainer.Channel);
                    var isFollower = false;
                    try
                    {
                        await Api.V5.Users.CheckUserFollowsByChannelAsync(twitchUser.Matches[0].Id,
                            channel.Matches[0].Id);
                        isFollower = true;
                    }
                    // will throw exception on not following
                    catch (BadResourceException e)
                    {
                        isFollower = false;
                    }

                    var thisViewer = new StreamViewer
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
                    if (giveaway.CanEnroll(thisViewer))
                        giveaway.Enroll(thisViewer);
                    // is broadcaster
                    else if (command.ChatMessage.IsBroadcaster) giveaway.Enroll(thisViewer);

                    // Call update on client
                    hub.UpdateGiveaway(giveaway);
                }

                // !loot
                else if (command.CommandText.ToLower().Equals(BotChannelSettings.Loyalty.LoyaltyName) &&
                         BotChannelSettings.Loyalty.Track == true)
                {
                    var userLoyalty = ContextService.GetLoyaltyForUser(command.ChatMessage.Username,
                        TcContainer.Channel,
                        command.ChatMessage.UserId,
                        command.ChatMessage.Username);

                    if (userLoyalty != null)
                        TwitchClient
                            .SendMessage(BotUserSettings.BotChannel, "/me " +
                                                                     $"@{command.ChatMessage.DisplayName} has {userLoyalty.CurrentPoints} {BotChannelSettings.Loyalty.LoyaltyName}");
                    else
                        TwitchClient
                            .SendMessage(BotUserSettings.BotChannel, "/me " +
                                                                     $"@{command.ChatMessage.Username}, you haven't earned any {BotChannelSettings.Loyalty.LoyaltyName} yet. Hang out in the channel and you will recieve {BotChannelSettings.Loyalty.LoyaltyValue} every {BotChannelSettings.Loyalty.LoyaltyInterval} minute.");
                }

                // !burn
                else if (command.CommandText.ToLower()
                             .Equals("burn" + BotChannelSettings.Loyalty.LoyaltyName.ToLower()) &&
                         BotChannelSettings.Loyalty.Track == true)
                {
                    var rnd = new SysRandom(Guid.NewGuid().GetHashCode());
                    var userLoyalty = ContextService.GetLoyaltyForUser(command.ChatMessage.Username,
                        TcContainer.Channel,
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
                        var randonIndex = rnd.Next(ripLoyaltySentences.Count);

                        TwitchClient
                            .SendMessage(BotUserSettings.BotChannel,
                                "/me " + ripLoyaltySentences[randonIndex]);

                        ContextService.AddLoyalty(User,
                            BotUserSettings.BotChannel, userLoyalty, -userLoyalty.CurrentPoints);
                    }
                    else
                    {
                        TwitchClient
                            .SendMessage(BotUserSettings.BotChannel, "/me " +
                                                                     $"@{command.ChatMessage.DisplayName}, you haven't earned any {BotChannelSettings.Loyalty.LoyaltyName} yet. Stay and the channel and you will recieve {BotChannelSettings.Loyalty.LoyaltyValue} every {BotChannelSettings.Loyalty.LoyaltyInterval} minute.");
                    }
                }
            }
        }


        /// <summary>
        ///     Create a strawpoll
        /// </summary>
        /// <param name="title">Title</param>
        /// <param name="options">List<string>()</param>
        private async void CreateStrawPoll(string title, List<string> options)
        {
            // Establish the poll settins
            var pollTitle = title;
            var allOptions = options;
            var multipleChoice = true;

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
        ///     Generate random name of x characters
        /// </summary>
        /// <param name="len"></param>
        /// <returns></returns>
        private static string GenerateName(int len)
        {
            var r = new SysRandom();
            string[] consonants =
            {
                "b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "l", "n", "p", "q", "r", "s", "sh", "zh", "t", "v",
                "w", "x"
            };
            string[] vowels = { "a", "e", "i", "o", "u", "ae", "y" };
            var Name = "";
            Name += consonants[r.Next(consonants.Length)].ToUpper();
            Name += vowels[r.Next(vowels.Length)];
            var
                b = 2; //b tells how many times a new letter has been added. It's 2 right now because the first two letters are already in the name.
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
            var r = new SysRandom();
            return r.NextDouble() < truePercentage / 100.0;
        }


        /// <summary>
        /// Check if song request is under 10 minutes long
        /// </summary>
        /// <param name="songRequest">VideoVm with youtube video and metadata</param>
        /// <returns>True if song length is under 10 mins</returns>
        private bool SongDurationCheck(VideoVm songRequest)
        {
            if (songRequest.Length < new TimeSpan(0, 10, 0))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Get TimeSpan from string e.g.(10:13) 10 minutes and 13 seconds
        /// </summary>
        /// <param name="time">Time in string e.g.(10:13) 10 minutes and 13 seconds</param>
        /// <returns>Timespan representation of input</returns>
        private TimeSpan StringToTimeSpan(string time)
        {
            var format = "m\\:ss";
            var culture = CultureInfo.CurrentCulture;

            return TimeSpan.ParseExact(time, format, culture);
        }
    }
}