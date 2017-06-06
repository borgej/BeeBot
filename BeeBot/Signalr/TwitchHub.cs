using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Mapping;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using BeeBot.Models;
using BeeBot.Services;
using Microsoft.Ajax.Utilities;
using Microsoft.AspNet.SignalR;
using Newtonsoft.Json;
using TwitchLib;
using TwitchLib.Events.Client;
using TwitchLib.Models.Client;
using YTBot.Context;
using YTBot.Models;
using YTBot.Services;

namespace BeeBot.Signalr
{
    public class TwitchHub : Hub
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Channel { get; set; }

        private ContextService ContextService { get; set; }

        public static List<TwitchClientContainer> ClientContainers { get; set; }

        private ConnectionCredentials ConnCred { get; set; }
        public TwitchClient Client { get; set; }

        private const int SLEEPSECONDS = 1;



        public TwitchHub()
        {
            ContextService = new ContextService();

            if (ClientContainers == null)
            {
                ClientContainers = new List<TwitchClientContainer>();
            }
        }

        public override Task OnConnected()
        {
            if (ClientContainers.Any(c => c.Id == Context.User.Identity.Name))
            {
                Client = GetClient();
            }
            else
            {
                var tcc = new TwitchClientContainer()
                {
                    Id = Context.User.Identity.Name,
                    Channel = Channel
                };

                ClientContainers.Add(tcc);
            }

            return base.OnConnected();
        }

        /// <summary>
        /// Client sent connect to channel
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="channel"></param>
        public void ConnectBot(string username, string password, string channel)
        {

            try
            {
                if (GetClient() != null)
                {
                    if (GetClient().IsConnected)
                    {
                        ConsoleLog("Already connected...");
                    }
                    else
                    {
                        GetClient().Connect();
                        GetClient().SendMessage(channel, " is now connected and serving its master!");
                        ConsoleLog("Connected");
                    }   
                }
                else
                {
                    if ((string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
                         string.IsNullOrWhiteSpace(channel)))
                    {
                        throw new Exception("No user/pass/channel given");
                    }
                    Username = username;
                    Password = password;
                    Channel = channel;
                    ConnCred = new ConnectionCredentials(Username, Password);


                    var clientContainer = GetClientContainer();
                    clientContainer.Client = new TwitchClient(ConnCred, Channel, logging: false);

                    // Throttle bot
                    clientContainer.Client.ChatThrottler = new TwitchLib.Services.MessageThrottler(20, TimeSpan.FromSeconds(30));



                    clientContainer.Client.OnLog += ConsoleLog;
                    clientContainer.Client.OnConnectionError += ConsoleLogConnectionError;
                    clientContainer.Client.OnMessageReceived += ChatShowMessage;
                    clientContainer.Client.OnMessageSent += ChatShowMessage;
                    clientContainer.Client.OnUserJoined += ShowUserJoined;
                    clientContainer.Client.OnUserLeft += ShowUserLeft;
                    clientContainer.Client.OnModeratorsReceived += ChannelModerators;

                    clientContainer.Client.Connect();
                    ConsoleLog("Connected to channel " + Channel);
                    GetClient().SendMessage(channel, " is now connected and serving its master!");

                    var arg = new WorkerThreadArg()
                    {
                        Channel = Channel,
                        Username = Context.User.Identity.Name, 
                        Client = clientContainer.Client
                    };

                    var parameterizedThreadStart = new ParameterizedThreadStart(TrackLoyaltyAndTimers);
                    var tcc = new TwitchClientContainer();
                    tcc.WorkerThread = new Thread(parameterizedThreadStart);
                    tcc.Client = clientContainer.Client;
                    tcc.WorkerThread.Start(arg);
                }

                GetClientContainer().Client.GetChannelModerators(channel);
                GetClientContainer().Client.WillReplaceEmotes = true;


                var botStatus = new BotStatusVM()
                {
                    info = GetClientContainer().Client.IsConnected ? "Bot is connected" : "Bot is not connected",
                    message = "",
                    warning = ""
                };

                Clients.Caller.BotStatus(botStatus);

            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
            }
        }

        

        public void Reconnect(string username, string password, string channel)
        {
            try
            {
                if (GetClientContainer().Client.IsConnected)
                {
                    DisconnectBot();
                    ConsoleLog("Reconnecting to channel " + Channel);
                    GetClientContainer().Client.Connect();
                    GetClient().SendMessage(channel, " is now connected and serving its master!");
                    ConsoleLog("Connected to channel " + Channel);
                }
                else
                {
                    ConsoleLog("Reconnecting to channel " + Channel);
                    GetClient().Connect();
                    GetClient().SendMessage(channel, " is now connected and serving its master!");

                    ConsoleLog("Connected to channel " + Channel);
                }
            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
            }
        }

        public void ChannelModerators(object sender, OnModeratorsReceivedArgs e)
        {
            bool botIsMod = e.Moderators.Contains(Username);
            var botStatus = new BotStatusVM()
            {
                info = GetClientContainer().Client.IsConnected ? "Bot is connected" : "Bot is not connected",
                message = "",
                warning = botIsMod == false ? "Bot is not moderator in channel" : ""
            };

            Clients.Caller.BotStatus(botStatus);
        }

        /// <summary>
        /// Send user left event to Client
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ShowUserLeft(object sender, OnUserLeftArgs e)
        {
            string userConnected = DateTime.Now.ToString("HH:mm:ss").ToString() + " - " + e.Username + " (disconnected)";
            var msg = "<div class='userLeft'>" + userConnected + "</div>";

            Clients.Caller.UsersConnLog(msg);
        }

        /// <summary>
        /// Send user joined channel to Client
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ShowUserJoined(object sender, OnUserJoinedArgs e)
        {
            
            string userConnected = DateTime.Now.ToString("HH:mm:ss").ToString() + " - " + e.Username + " (connected)";
            var msg = "<div class='userConnected'>" + userConnected + "</div>";

            Clients.Caller.UsersConnLog(msg);
        }

        public void ShowUserTimedOut(object sender, OnUserTimedoutArgs e)
        {
            string userConnected = DateTime.Now.ToString("HH:mm:ss").ToString() + " - " + e.Username + " (timed out)";
            var msg = "<div class='userTimedOut'>" + userConnected + "</div>";

            Clients.Caller.UsersConnLog(msg);
        }

        public void ShowUserBanned(object sender, OnUserBannedArgs e)
        {
            string userConnected = DateTime.Now.ToString("HH:mm:ss").ToString() + " - " + e.Username + " (banned)";
            var msg = "<div class='userBanned'>" + userConnected + "</div>";

            Clients.Caller.UsersConnLog(msg);
        }

        /// <summary>
        /// Disconnect bot from channel
        /// </summary>
        public void DisconnectBot()
        {
            try
            {
                GetClient().SendMessage(Channel, " is now connected and serving its master!");
                GetClientContainer().Client.Disconnect();
                ConsoleLog("Disconnected channel " + Channel);
            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
            }
        }

        public void GiveLoggedInUsers(string users)
        {

        }


        /// <summary>
        /// Check if Client is still connected
        /// </summary>
        public void IsConnected()
        {
            if (GetClientContainer().Client.IsConnected)
            {

                Clients.Caller.ConsoleLog(DateTime.Now.ToString("HH:mm:ss").ToString() + " - " +
                                       "Bot is still connected!");

            }
            else
            {
                Clients.Caller.ConsoleLog(DateTime.Now.ToString("HH:mm:ss").ToString() + " - " +
                                       "Bot is no longer connected!");
            }
        }

        /// <summary>
        /// Update channel topic and game
        /// </summary>
        /// <param name="topic">Topic of the channel</param>
        /// <param name="game">Current game</param>
        /// <returns></returns>
        public bool UpdateChannel(string topic, string game)
        {
            return TwitchAPI.Channels.v5.UpdateChannel(Channel, topic, game, null, null, Password).IsCompleted;
        }


        /// <summary>
        /// Log to Client console
        /// </summary>
        /// <param name="msg"></param>
        public void ConsoleLog(string msg, bool debug = false)
        {
            Clients.Caller.ConsoleLog(DateTime.Now.ToString("HH:mm:ss").ToString() + " - " + msg);
        }

        public void ConsoleLog(object sender, OnLogArgs e)
        {
            Clients.Caller.ConsoleLog(e.DateTime.ToString("HH:mm:ss").ToString() + " - " + e.Data);
        }

        public void ConsoleLogConnectionError(object sender, OnConnectionErrorArgs e)
        {
            Clients.Caller.ConsoleLog(DateTime.Now.ToString("HH:mm:ss").ToString() + " - " + e.Error.Message);
        }

        /// <summary>
        /// Send chat message to Client
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ChatShowMessage(object sender, OnMessageReceivedArgs e)
        {
            Regex r = new Regex(@"(https?://[^\s]+)");

            var msg = DateTime.Now.ToString("HH:mm:ss").ToString() + " - " +
                      FormatUsername(e.ChatMessage) + ": " +
                      e.ChatMessage.Message;
            //r.Replace(e.ChatMessage.Message, "<a href=\"$1\" target=\"_blank\">$1</a>");

            // TODO: check if links are allowed
            if (e.ChatMessage.IsBroadcaster)
            {
                msg = "<b>" + msg + "</b>";
            }
            if (e.ChatMessage.Message.ToLower().Contains("@"+e.ChatMessage.Channel.ToLower()))
            {
                msg = "<div class=\"chatMsg chatMsgToBroadcaster\">" + msg + "</div>";
            }
            else
            {
                msg = "<div class=\"chatMsg\">" + msg + "</div>";
            }

            // 
            Clients.Caller.ChatShow(msg);

            // check for triggers
            ChatMessageTriggerCheck(e.ChatMessage);

        }

        /// <summary>
        /// Send chat message to Client
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChatShowMessage(object sender, OnMessageSentArgs e)
        {
            if (e.SentMessage.Message.StartsWith("/"))
            {
                return;
            }

            Regex r = new Regex(@"(https?://[^\s]+)");

            var msg = DateTime.Now.ToString("HH:mm:ss").ToString() + " - " +
                      FormatUsername(e.SentMessage) + ": " +
                      e.SentMessage.Message;
            //r.Replace(e.ChatMessage.Message, "<a href=\"$1\" target=\"_blank\">$1</a>");

            // TODO: check if links are allowed
            //if (e.SentMessage.)
            //{
            //    msg = "<b>" + msg + "</b>";
            //}
            if (e.SentMessage.Message.ToLower().Contains("@" + e.SentMessage.Channel.ToLower()))
            {
                msg = "<div class=\"chatMsg chatMsgToBroadcaster\">" + msg + "</div>";
            }
            else
            {
                msg = "<div class=\"chatMsg\">" + msg + "</div>";
            }

            // 
            Clients.Caller.ChatShow(msg);

        }

        /// <summary>
        /// Check for chat triggers
        /// </summary>
        /// <param name="chatmessage"></param>
        public void ChatMessageTriggerCheck(ChatMessage chatmessage)
        {
            var triggers = ContextService.GetTriggers(Context.User.Identity.Name).Where(t => t.Active == true);
            // loot name
            var bcs = ContextService.GetBotChannelSettings(ContextService.GetUser(Context.User.Identity.Name));
            if (bcs.Loyalty != null && bcs.Loyalty.LoyaltyName != null && !string.IsNullOrWhiteSpace(bcs.Loyalty.LoyaltyName))
            {
                // !<loyaltyName>
                if (chatmessage.Message.StartsWith("!"+bcs.Loyalty.LoyaltyName))
                {
                    var userLoyalty = ContextService.GetLoyaltyForUser(Context.User.Identity.Name, chatmessage.UserId,
                        chatmessage.Username);

                    if (userLoyalty != null)
                    {
                        GetClient().SendMessage(
                            $"@{chatmessage.DisplayName} has {userLoyalty.CurrentPoints.ToString()} {bcs.Loyalty.LoyaltyName}");
                    }
                }

                // !burn<loyaltyName>
                if (chatmessage.Message.StartsWith("!burn" + bcs.Loyalty.LoyaltyName.ToLower()))
                {
                    Random rnd = new Random();
                    var userLoyalty = ContextService.GetLoyaltyForUser(Context.User.Identity.Name, chatmessage.UserId,
                        chatmessage.Username);

                    if (userLoyalty != null)
                    {
                        var ripLoyaltySentences = new List<string>
                        {
                            $"@{chatmessage.DisplayName}'s {bcs.Loyalty.LoyaltyName} spontaneously combusted!",
                            $"@{chatmessage.DisplayName}'s {bcs.Loyalty.LoyaltyName} turned to ashes!",
                            $"@{chatmessage.DisplayName} just scorched his loot.",
                            $"@{chatmessage.DisplayName} just cooked his loot... it's not fit to eat!",
                            $"@{chatmessage.DisplayName} witnessed all their {bcs.Loyalty.LoyaltyName} ignite... #rip{bcs.Loyalty.LoyaltyName}"
                        };
                        int randonIndex = rnd.Next(ripLoyaltySentences.Count);


                        ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name), chatmessage.Channel.ToLower(), userLoyalty, -userLoyalty.CurrentPoints);
                        GetClient().SendMessage((string)ripLoyaltySentences[randonIndex]);
                    }
                }

                if (chatmessage.Message.StartsWith("!bonusall") || chatmessage.Message.StartsWith("!give") || chatmessage.Message.StartsWith("!bonus") || chatmessage.Message.StartsWith("!gamble"))
                {
                    // PS: only mods and streamer can use these
                    if (chatmessage.IsBroadcaster || chatmessage.IsModerator)
                    {
                        // !bonusall
                        if (chatmessage.Message.StartsWith("!bonusall"))
                        {
                            try
                            {
                                var verb = "";
                                var bonusValue = Math.Abs(Convert.ToInt32(Regex.Match(chatmessage.Message, @"-?\d+").Value));

                                ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name), chatmessage.Channel.ToLower(), GetUsersInChannel(chatmessage.Channel.ToLower()), bonusValue);

                                if (bonusValue > 0)
                                {
                                    verb = "has been given";

                                }
                                else
                                {
                                    verb = "has been deprived of";
                                }

                                GetClient()
                                    .SendMessage(
                                        $"Everyone {verb} {bonusValue} {bcs.Loyalty.LoyaltyName}");
                            }
                            catch (Exception e)
                            {
                                ConsoleLog("Error on !bonusall: " + e.Message, true);
                            }
                        } 
                        // !bonus
                        else if (chatmessage.Message.StartsWith("!bonus"))
                        {
                            try
                            {
                                var loyaltyAmount = Math.Abs(Convert.ToInt32(chatmessage.Message.Split(' ')[2]));
                                string destinationViewerName = chatmessage.Message.Split(' ')[1];

                                var destinationViewerLoyalty = ContextService.GetLoyaltyForUser(Context.User.Identity.Name, null,
                                    destinationViewerName);

                                if (loyaltyAmount != null && (destinationViewerLoyalty != null))
                                {
                                    ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name), chatmessage.Channel.ToLower(), destinationViewerLoyalty, loyaltyAmount);

                                    GetClient().SendMessage($"@{chatmessage.DisplayName} was given {loyaltyAmount} {bcs.Loyalty.LoyaltyName}");
                                }
                            }
                            catch (Exception e)
                            {
                                ConsoleLog("Error on !bonus: " + e.Message, true);
                            }
                        }
                    }

                    //  !give
                    if (chatmessage.Message.StartsWith("!give"))
                    {
                        try
                        {
                            // get who to give it to
                            var loyaltyAmount = Math.Abs(Convert.ToInt32(chatmessage.Message.Split(' ')[2]));
                            string destinationViewerName = chatmessage.Message.Split(' ')[1];
                            string sourceViewerId = chatmessage.UserId;
                            string sourceViewerName = chatmessage.Username;

                            var sourceViewerLoyalty = ContextService.GetLoyaltyForUser(Context.User.Identity.Name, sourceViewerId,
                                sourceViewerName);
                            var destinationViewerLoyalty = ContextService.GetLoyaltyForUser(Context.User.Identity.Name, null,
                                destinationViewerName);

                            // uses does not have enough to give away
                            if (loyaltyAmount != null && (sourceViewerLoyalty != null && sourceViewerLoyalty.CurrentPoints < loyaltyAmount))
                            {
                                GetClient().SendMessage($"Stop wasting my time @{chatmessage.DisplayName}, you ain't got that much {bcs.Loyalty.LoyaltyName}");
                            }
                            // give away loot
                            else if (loyaltyAmount != null && (sourceViewerLoyalty != null && sourceViewerLoyalty.CurrentPoints >= loyaltyAmount))
                            {
                                ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name), chatmessage.Channel.ToLower(), sourceViewerLoyalty, -loyaltyAmount);
                                ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name), chatmessage.Channel.ToLower(), destinationViewerLoyalty, loyaltyAmount);

                                GetClient().SendMessage($"@{chatmessage.DisplayName} gave {destinationViewerLoyalty.TwitchUsername} {loyaltyAmount} {bcs.Loyalty.LoyaltyName}");
                            }
                        }
                        catch (Exception e)
                        {
                            ConsoleLog("Error on !give: " + e.Message, true);
                        }


                    }

                    // !gamble
                    if (chatmessage.Message.StartsWith("!gamble"))
                    {
                        // get 
                        var loyalty = ContextService.GetLoyaltyForUser(Context.User.Identity.Name, chatmessage.UserId);

                        // timeout for 5 minutes if user has gamble before
                        if(loyalty != null && (loyalty.LastGamble == null || (loyalty.LastGamble.HasValue && loyalty.LastGamble.Value.AddMinutes(5) <= DateTime.Now)))
                        {
                            try
                            {
                                Random rnd = new Random();

                                int gambleAmount;
                                bool allIn = false;
                                // get who to give it to
                                if (chatmessage.Message.Split(' ')[1].ToLower().Equals("allin"))
                                {
                                    allIn = true;
                                    gambleAmount = loyalty.CurrentPoints;
                                }
                                else
                                {
                                    gambleAmount = Math.Abs(Convert.ToInt32(chatmessage.Message.Split(' ')[1]));
                                }
                                
                                string sourceViewerId = chatmessage.UserId;
                                string sourceViewerName = chatmessage.Username;

                                var sourceViewerLoyalty = ContextService.GetLoyaltyForUser(Context.User.Identity.Name, sourceViewerId,
                                    sourceViewerName);

                                // user has enough loyalty to gamble
                                if (sourceViewerLoyalty != null && sourceViewerLoyalty.CurrentPoints >= gambleAmount)
                                {
                                    var rolledNumber = rnd.Next(1, 100);

                                    // rolled 1-49
                                    if (rolledNumber < 50)
                                    {
                                        var newAmount = sourceViewerLoyalty.CurrentPoints - (gambleAmount);
                                        ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name), chatmessage.Channel.ToLower(), sourceViewerLoyalty, -gambleAmount);
                                        GetClient().SendMessage($"@{chatmessage.DisplayName} rolled a sad {rolledNumber}, lost {gambleAmount} and now got {newAmount} {bcs.Loyalty.LoyaltyName}!! #house\"Always\"Wins");
                                    }
                                    // rolled 50-99
                                    else if (rolledNumber >= 50 && rolledNumber < 100)
                                    {
                                        var newAmount = sourceViewerLoyalty.CurrentPoints + (gambleAmount * 2);
                                        ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name), chatmessage.Channel.ToLower(), sourceViewerLoyalty, gambleAmount * 2);
                                        GetClient().SendMessage($"@{chatmessage.DisplayName} rolled {rolledNumber}, won {gambleAmount * 2} and now got {newAmount} {bcs.Loyalty.LoyaltyName}!");
                                    }
                                    // rolled 100
                                    else
                                    {
                                        ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name), chatmessage.Channel.ToLower(), sourceViewerLoyalty, gambleAmount * 3);
                                        var newAmount = sourceViewerLoyalty.CurrentPoints + (gambleAmount * 3);
                                        GetClient().SendMessage($"@{chatmessage.DisplayName} did an epic roll, threw {rolledNumber}, won {gambleAmount * 3} and now got {newAmount} {bcs.Loyalty.LoyaltyName}!! #houseCries");
                                    }

                                    ContextService.StampLastGamble(ContextService.GetUser(Context.User.Identity.Name), chatmessage.Channel.ToLower(), sourceViewerLoyalty);
                                }
                            }
                            catch (Exception e)
                            {
                                ConsoleLog("Error on !gamble: " + e.Message, true);
                            }
                        }
                        
                        


                    }

                }
            }
            

            

            if (triggers.Any(t => t.TriggerName.Equals(chatmessage.Message.Split(' ').FirstOrDefault())))
            {
                var trigger =
                    triggers.FirstOrDefault(t => t.TriggerName.Equals(chatmessage.Message.Split(' ').FirstOrDefault()));
                switch (trigger.TriggerType)
                {
                    // Chat response
                    case TriggerType.Message:
                        // TODO: something here
                        break;
                    // Game response
                    case TriggerType.Game:
                        // TODO: something here
                        break;
                    // Quote response
                    case TriggerType.Quote:
                        // TODO: something here
                        break;
                    // Statistic response
                    case TriggerType.Statistic:
                        // TODO: something here
                        break;
                    case TriggerType.Loyalty:
                        // TODO: something here
                        break;
                    default:
                        // TODO: something here
                        break;
                }
                
            }
        }

        private void LoyaltyAdmin(string message)
        {
            
        }


        /// <summary>
        /// Add html color to username
        /// </summary>
        /// <param name="msg">ChatMessage</param>
        /// <returns>String formatted span</returns>
        private string FormatUsername(ChatMessage msg)
        {
            var color = msg.Color;
            string badges = "";
            string username = "<span style=\"color:rgb("+color.R+","+ color.B + "," + color.G+");\">" + msg.DisplayName + "</span>";
            foreach (var badge in msg.Badges)
            {
                var key = badge.Key;
                var value = badge.Value;

                if (key.ToLower().Contains("moderator") && Convert.ToInt32(value) == 1)
                {
                    username = "<img='~/Content/moderatorBadge.png' style='padding-right: 3px;' />" + username;
                }
            }
            return username;
        }

        /// <summary>
        /// Add html color to username in SentMessage
        /// </summary>
        /// <param name="msg">SentMessage</param>
        /// <returns>String formatted span</returns>
        private string FormatUsername(SentMessage msg)
        {
            var color = msg.ColorHex.ToString();
            if (!color.StartsWith("#"))
            {
                color = "#" + color;
            }
            if (color.Length < 1)
            {
                // red color for bot
                color += "b30000";
            }
            string badges = "";
            string username = "<span style=\"color:"+ color + ";\">" + msg.DisplayName + "</span>";
            foreach (var badge in msg.Badges)
            {
                var key = badge.Key;
                var value = badge.Value;

                if (key.ToLower().Contains("moderator") && Convert.ToInt32(value) == 1)
                {
                    username = "<img='~/Content/moderatorBadge.png' style='padding-right: 3px;' />" + username;
                }
            }
            return username;
        }

        /// <summary>
        /// Gets the current TwitchClient for the current web-connection
        /// </summary>
        /// <returns></returns>
        private TwitchClient GetClient()
        {
            return ClientContainers.FirstOrDefault(t => t.Id == Context.User.Identity.Name).Client;
        }

        /// <summary>
        /// Gets the current TwitchClientContainer for the current web-connection
        /// </summary>
        /// <returns></returns>
        private TwitchClientContainer GetClientContainer()
        {
            return ClientContainers.FirstOrDefault(t => t.Id == Context.User.Identity.Name);
        }


        public void TrackLoyaltyAndTimers(object arg)
        {
            var wtarg = (WorkerThreadArg)arg;
            ApplicationUser User = ContextService.GetUser(wtarg.Username);

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var lastLoyaltyElapsedMinutes = stopWatch.Elapsed.Minutes;



            while (true)
            {

                while (wtarg.Client == null)
                {
                    Thread.Sleep(SLEEPSECONDS);
                }
                while (wtarg.Client != null && wtarg.Client.IsConnected == false)
                {
                    Thread.Sleep(SLEEPSECONDS);
                }

                // Update database connector
                ContextService = new ContextService();

                // Timers
                // TODO


                // Loyalty 
                var botChannelSettings = ContextService.GetBotChannelSettings(User);
                if (botChannelSettings != null && botChannelSettings.Loyalty != null && botChannelSettings.Loyalty.Track == true)
                {

                    if (stopWatch.Elapsed.Minutes % botChannelSettings.Loyalty.LoyaltyInterval == 0 && lastLoyaltyElapsedMinutes != stopWatch.Elapsed.Minutes)
                    {
                        var channelUsers = GetUsersInChannel(wtarg.Channel);

                        ContextService.AddLoyalty(User, Channel, channelUsers);

                        lastLoyaltyElapsedMinutes = stopWatch.Elapsed.Minutes;
                    }
                }

                // chill for a second
                Thread.Sleep(SLEEPSECONDS);
            }

        }


        /// <summary>
        /// Gets list of users in channel, looks up their twitch ID 
        /// </summary>
        /// <param name="channel">Channel as string</param>
        /// <returns>list of StreamView</returns>
        public List<StreamViewer> GetUsersInChannel(string channel)
        {
            var users = new List<StreamViewer>();

            var test = TwitchLib.TwitchAPI.Settings.ClientId = "gokkk5ean0yksozv0ctvljwqpceuin";
            var streamUsers = TwitchAPI.Undocumented.GetChatters(channel).Result;

            foreach (var user in streamUsers)
            {
                var tmpUser = TwitchAPI.Users.v3.GetUserFromUsername(user.Username);

                var t = new StreamViewer();

                t.TwitchUsername = user.Username;
                t.TwitchUserId = tmpUser.Result.Id;

                if (!users.Any(u => u.TwitchUserId == t.TwitchUserId))
                {
                    users.Add(t);
                }
                    
            }

            return users;
        }



    }


}