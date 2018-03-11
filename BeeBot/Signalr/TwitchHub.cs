using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity.Core.Mapping;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using BeeBot.Models;
using BeeBot.Services;
using HtmlAgilityPack;
using Microsoft.AspNet.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TwitchLib;
using TwitchLib.Events.Client;
using TwitchLib.Extensions.Client;
using TwitchLib.Models.API.v5.Channels;
using TwitchLib.Models.API.v5.Games;
using TwitchLib.Models.Client;
using WebGrease.Css.Extensions;
using YTBot.Context;
using YTBot.Models;
using YTBot.Services;
using StrawPollNET;
using TwitchLib.Enums;
using YoutubeExtractor;
using SysRandom = System.Random;
using Random.Org;


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

        private const int Sleepseconds = 1;

        private const int NUMTOPCHATTERS = 5;
        private const int NUMTOPCOMMANDS = 5;

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

        public void PlayingSong(string name, string url)
        {
            var ccontainer = GetClientContainer();

            ccontainer.Client.SendMessage("/me Now playing: " + name + " - " + url);
        }

        /// <summary>
        /// Save banned words list
        /// </summary>
        /// <param name="words">csv list of words</param>
        /// <param name="channel">channel name</param>
        public void SaveBannedWords(string words, string channel)
        {
            try
            {
                var username = ContextService.GetUser(Context.User.Identity.Name);

                var bcs = ContextService.GetBotChannelSettings(username);

                if (bcs.BannedWords == null)
                {
                    bcs.BannedWords = new List<BannedWord>();
                }
                else
                {
                    var dbWords = bcs.BannedWords.ToList();

                    foreach (var bannedWord in dbWords)
                    {
                        ContextService.Context.BannedWords.Remove(bannedWord);
                    }
                }
                var csvWords = words.Split(',');

                foreach (var word in csvWords)
                {
                    var trimmedWord = word.Trim().ToLower();
                    var newWord = new BannedWord();
                    newWord.Word = trimmedWord;
                    bcs.BannedWords.Add(newWord);
                }

                ContextService.Context.SaveChanges();

                Clients.Caller.SavedBannedWords(new { message = "Saved", data = "1" });
                Clients.Caller.ConsoleLog(DateTime.Now.ToString("HH:mm:ss").ToString() + " - " +
                                          "Saved banned words list");
            }
            catch (Exception e)
            {
                Clients.Caller.SavedBannedWords(new { message = "Error: " + e.Message, data = "-1" });
            }
        }

        public void UpdateModsCanControlPlaylist(bool modsCanControlPlaylist)
        {
            try
            {
                GetClientContainer().ModsControlSongrequest = modsCanControlPlaylist;

                Clients.Caller.ModsCanControlPlaylist(modsCanControlPlaylist);
            }
            catch (Exception e)
            {
                ConsoleLog("Error saving modsCanControlPlaylist: " + e.Message);
            }
        }

        public void ImportDefaultBannedWords()
        {
            var bannedWords = ConfigurationManager.AppSettings["bannedWords"];

            var words = bannedWords.Split(',');

            var bannedWordsNew = new List<BannedWord>();

            foreach (var w in words)
            {
                var t = new BannedWord();
                t.Word = w.ToLower();
                bannedWordsNew.Add(t);
            }

            Clients.Caller.ImportBannedWords(bannedWordsNew);
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
                    if (GetClientContainer().Client.IsConnected)
                    {
                        ConsoleLog("Already connected...");
                    }
                    else
                    {
                        var ccontainer = GetClientContainer();
                        ccontainer.Client.Connect();
                        // Bot joinmessage
                        //GetClientContainer().Client.SendMessage(channel, " is now connected and serving its master!");
                        if (ccontainer.WorkerThread != null && ccontainer.WorkerThread.IsAlive)
                        {
                            ccontainer.WorkerThread.Abort();
                            ccontainer.WorkerThread = null;

                            var arg = new WorkerThreadArg()
                            {
                                Channel = channel,
                                Username = Context.User.Identity.Name,
                                Client = ccontainer.Client
                            };

                            var parameterizedThreadStart = new ParameterizedThreadStart(TrackLoyaltyAndTimers);
                            //var tcc = new TwitchClientContainer();
                            ccontainer.WorkerThread = new Thread(parameterizedThreadStart);
                            ccontainer.Client = ccontainer.Client;
                            ccontainer.WorkerThread.Start(arg);
                        }
                        else if (ccontainer.WorkerThread == null)
                        {
                            var arg = new WorkerThreadArg()
                            {
                                Channel = channel,
                                Username = Context.User.Identity.Name,
                                Client = ccontainer.Client
                            };

                            var parameterizedThreadStart = new ParameterizedThreadStart(TrackLoyaltyAndTimers);
                            //var tcc = new TwitchClientContainer();
                            ccontainer.WorkerThread = new Thread(parameterizedThreadStart);
                            ccontainer.Client = ccontainer.Client;
                            ccontainer.WorkerThread.Start(arg);
                        }
                        //ccontainer.PubSubClient = new TwitchPubSub();

                        //ccontainer.PubSubClient.Connect();
                        ConsoleLog("Connected to channel " + channel);
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
                    clientContainer.Client.ChatThrottler =
                        new TwitchLib.Services.MessageThrottler(20, TimeSpan.FromSeconds(30));
                    clientContainer.Client.ChatThrottler.ApplyThrottlingToRawMessages = false;
                    clientContainer.Client.ChatThrottler.MessagesAllowedInPeriod = 120;
                    clientContainer.Client.ChatThrottler.MaximumMessageLengthAllowed = 500;

                    clientContainer.Client.OnLog += ConsoleLog;
                    clientContainer.Client.OnConnectionError += ConsoleLogConnectionError;
                    clientContainer.Client.OnMessageReceived += ChatShowMessage;
                    clientContainer.Client.OnMessageSent += ChatShowMessage;
                    clientContainer.Client.OnUserJoined += ShowUserJoined;
                    clientContainer.Client.OnUserLeft += ShowUserLeft;
                    clientContainer.Client.OnModeratorsReceived += ChannelModerators;
                    clientContainer.Client.OnChatCommandReceived += ChatMessageTriggerCheck;
                    clientContainer.Client.OnUserBanned += ShowUserBanned;
                    clientContainer.Client.OnUserTimedout += ShowUserTimedOut;
                    clientContainer.Client.OnWhisperReceived += RelayToChatMessage;

                    //clientContainer.Client.OverrideBeingHostedCheck = false;
                    //clientContainer.Client.OnBeingHosted += BeeingHosted;

                    clientContainer.Client.Connect();
                    ConsoleLog("Connected to channel " + channel);

                    var arg = new WorkerThreadArg()
                    {
                        Channel = Channel,
                        Username = Context.User.Identity.Name,
                        Client = clientContainer.Client
                    };

                    var parameterizedThreadStart = new ParameterizedThreadStart(TrackLoyaltyAndTimers);
                    var tcc = new TwitchClientContainer();
                    clientContainer.WorkerThread = new Thread(parameterizedThreadStart);
                    clientContainer.Client = clientContainer.Client;
                    clientContainer.WorkerThread.Start(arg);
                }

                GetClientContainer().Client.GetChannelModerators(channel);
                GetClientContainer().Client.WillReplaceEmotes = true;


                var botStatus = new BotStatusVM()
                {
                    info = GetClientContainer().Client.IsConnected ? "Connected" : "Disconnected",
                    message = "",
                    warning = ""
                };
                GetStreamInfo();
                Clients.Caller.BotStatus(botStatus);
            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
            }
        }


        /// <summary>
        /// Beeing hosted event, add bonus to hoster. Amount depending on how many viewers are brought along
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BeeingHosted(object sender, OnBeingHostedArgs e)
        {
            var ccontainer = GetClientContainer();
            var bcs = ContextService.GetBotChannelSettings(ccontainer.User);
            var hoster = e.HostedByChannel;

            var viewers = e.Viewers;
            var bonusViewerLoot = 0;
            ccontainer.Client.SendMessage("/me @" + hoster + " just brought " + viewers.ToString() +
                                          " viewers to the party! Welcome!");

            if (bcs.Loyalty.Track == false)
                return;

            #region bonus loyalty if tracking loyalty

            if (viewers == 0)
            {
                bonusViewerLoot = 100;
            }
            else if (viewers >= 5 && viewers < 10)
            {
                bonusViewerLoot = 250;
            }
            else if (viewers >= 10 && viewers < 20)
            {
                bonusViewerLoot = 500;
            }
            else if (viewers >= 20)
            {
                bonusViewerLoot = 750;
            }

            var hosterLoyalty = ContextService.GetLoyaltyForUser("", Channel, null, hoster);

            ContextService.AddLoyalty(ccontainer.User, Channel, hosterLoyalty, bonusViewerLoot);

            var hosterLoyaltyAfter = ContextService.GetLoyaltyForUser("", Channel, null, hoster);

            ccontainer.Client.SendMessage("/me @" + hoster + " just received " + bonusViewerLoot + " " +
                                          bcs.Loyalty.LoyaltyName + " for the host and now has " +
                                          hosterLoyaltyAfter.CurrentPoints + " " + bcs.Loyalty.LoyaltyName);

            #endregion
        }

        /// <summary>
        /// Relays message to channel if whisper user is moderator or broadcaster
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RelayToChatMessage(object sender, OnWhisperReceivedArgs e)
        {
            var ccontainer = GetClientContainer();
            if (ccontainer.Channelmods.Contains(e.WhisperMessage.Username.ToLower()) ||
                e.WhisperMessage.DisplayName.ToLower() == Channel.ToLower())
            {
                GetClient().SendMessage(e.WhisperMessage.Message);
            }
        }


        public async Task GetStreamInfo()
        {
            //TwitchLib.TwitchAPI.Settings.Validators.SkipDynamicScopeValidation = true;
            TwitchLib.TwitchAPI.Settings.ClientId = ConfigurationManager.AppSettings["clientId"];

            var uname = ContextService.GetUser(Context.User.Identity.Name);
            var bs = ContextService.GetBotUserSettingsForUser(uname);
            var token = bs.ChannelToken;
            TwitchLib.TwitchAPI.Settings.AccessToken = token;

            var channel = await TwitchAPI.Channels.v3.GetChannel(token);
            var uptime = await TwitchAPI.Streams.v5.GetUptime(channel.Id);

            var streamStatus = new StreamStatusVM();

            streamStatus.Channel = channel.DisplayName;
            streamStatus.Game = channel.Game;
            streamStatus.Title = channel.Status;
            streamStatus.Mature = channel.Mature;
            streamStatus.Delay = Convert.ToInt32(channel.Delay);
            streamStatus.Online = uptime != null;

            Clients.Caller.SetStreamInfo(streamStatus);
            ConsoleLog("Retrieved stream title and game");
        }

        /// <summary>
        /// Updates the channel info
        /// </summary>
        /// <param name="title">Title of the stream</param>
        /// <param name="game">Current playing game/category</param>
        /// <param name="channel">Channel to update</param>
        public async Task SaveStreamInfo(string title, string game, string channel, string mature, string delay)
        {
            try
            {
                //TwitchLib.TwitchAPI.Settings.Validators.SkipDynamicScopeValidation = true;
                TwitchLib.TwitchAPI.Settings.ClientId = ConfigurationManager.AppSettings["clientId"];

                var username = ContextService.GetUser(Context.User.Identity.Name);
                var bs = ContextService.GetBotUserSettingsForUser(username);
                var token = bs.ChannelToken;
                TwitchLib.TwitchAPI.Settings.AccessToken = token;

                var channelId = await TwitchAPI.Channels.v5.GetChannel(token);
                //var channelResult = TwitchAPI.Channels.v5.UpdateChannel(tokenChannelId.ToString(), title, game, null, null, token).Result;

                if (string.IsNullOrWhiteSpace(delay))
                {
                    delay = "0";
                }

                TwitchLib.Models.API.v5.Channels.Channel x =
                    await TwitchAPI.Channels.v5.UpdateChannel(channelId.Id.ToString(), title, game, delay);

                Clients.Caller.StreamInfoSaveCallback("1", "Channel updated");
            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
                Clients.Caller.StreamInfoSaveCallback("-1", e.Message);
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
                    Thread.Sleep(1000);

                    ConnectBot(username, password, channel);
                    //GetClient().SendMessage(channel, " is now connected and serving its master!");
                }
                else
                {
                    ConsoleLog("Reconnecting to channel " + Channel);
                    Thread.Sleep(1000);

                    ConnectBot(username, password, channel);
                    //GetClient().SendMessage(channel, " is now connected and serving its master!");
                }

                var botStatus = new BotStatusVM()
                {
                    info = GetClientContainer().Client.IsConnected ? "Connected" : "Disconnected",
                    message = ""
                };
                Clients.Caller.BotStatus(botStatus);
            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
            }
        }

        public void ChannelModerators(object sender, OnModeratorsReceivedArgs e)
        {
            var mods = new List<string>();
            foreach (var moderator in e.Moderators)
            {
                var mod = moderator.ToLower();
                mods.Add(mod);
                GetClientContainer().Channelmods.Add(mod);
            }

            bool botIsMod = mods.Contains(Username.ToLower());

            var botStatus = new BotStatusVM()
            {
                info = GetClientContainer().Client.IsConnected ? "Connected" : "Disconnected",
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
            string userConnected = DateTime.Now.ToString("HH:mm:ss").ToString() + " - " + e.Username +
                                   " (disconnected)";
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
                var container = GetClientContainer();
                container.Client.Disconnect();
                if (container.WorkerThread != null)
                {
                    container.WorkerThread.Abort();
                    container.WorkerThread = null;
                }

                container.Client.SendMessage(container.Channel, " is now going to sleep!");
                ConsoleLog("Disconnected channel " + Channel);

                var botStatus = new BotStatusVM()
                {
                    info = !container.Client.IsConnected ? "Disconnected" : "Connected",
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

        public void UpdateChattersAndCommands()
        {
            var ccontainer = GetClientContainer();

            var topCommands = ccontainer.CommandsUsed.OrderByDescending(k => k.Value).Take(NUMTOPCOMMANDS);
            var topChatters = ccontainer.ChattersCount.OrderByDescending(k => k.Value).Take(NUMTOPCHATTERS);

            var retval = new { topcommands = topCommands, topchatters = topChatters };
            Clients.Caller.ChattersAndCommands(retval);
        }

        public void UpdatePlaylistFromCommand(string url, string title, string user, string videoId)
        {
            var obj = new { title = title, url = url, user = user, videoid = videoId };
            var container = GetClientContainer();
            GetClient().SendMessage("/me \"" + title + "\" just got added to the playlist by @" + user);
            Clients.Caller.UpdatePlaylist(obj);
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
            if (e.ChatMessage.Message.ToLower().Contains("@" + e.ChatMessage.Channel.ToLower()))
            {
                msg = "<div class=\"chatMsg chatMsgToBroadcaster\">" + msg + "</div>";
            }
            else
            {
                msg = "<div class=\"chatMsg\">" + msg + "</div>";
            }

            var username = ContextService.GetUser(Context.User.Identity.Name);
            var bcs = ContextService.GetBotChannelSettings(username);
            if (bcs.BannedWords != null && bcs.BannedWords.Count > 0)
            {
                // Check for banned words in chat message
                var wordsInMessage = e.ChatMessage.Message.ToLower().Split(' ');
                foreach (var word in wordsInMessage)
                {
                    if (bcs.BannedWords.Any(b => b.Word.ToLower() == word))
                    {
                        var client = GetClient();
                        var timeout = new TimeSpan(0, 0, 1, 0);
                        var message = "Careful with your words there mate! Timed out for  " +
                                      Convert.ToString(timeout.Minutes) + " minutes";
                        var joinedChannel = client.GetJoinedChannel(Channel);
                        client.TimeoutUser(joinedChannel, e.ChatMessage.DisplayName.ToLower(), timeout, message);
                    }
                }
            }


            // Add to chat log
            AddToChatLog(e.ChatMessage.DisplayName, e.ChatMessage.Message);
            Clients.Caller.ChatShow(msg);
        }

        public void CreateStrawPoll(string title, string csvOptions)
        {
            try
            {
                CreateStrawPoll(title, csvOptions.Split(',').OfType<string>().ToList());
            }
            catch (Exception e)
            {
            }
        }

        /// <summary>
        /// Create a strawpoll
        /// </summary>
        /// <param name="title">Title</param>
        /// <param name="options">List<string>()</param>
        private void CreateStrawPoll(string title, List<string> options)
        {
            // Establish the poll settins
            string pollTitle = title;
            List<string> allOptions = options;
            bool multipleChoice = true;
            StrawPollNET.Enums.DupCheck dupCheck = StrawPollNET.Enums.DupCheck.Normal;
            bool requireCaptcha = false;

            // Create the poll
            var newPoll =
                StrawPollNET.API.Create.CreatePoll(title, allOptions, multipleChoice, dupCheck, requireCaptcha);
            GetClientContainer().Polls.Add(newPoll.Id);

            // Show poll link
            GetClient().SendMessage($"Vote for \"{pollTitle}\", here: {newPoll.PollUrl}");

            ConsoleLog("Created poll '" + title + "' " + newPoll.PollUrl);
            Clients.Caller.CreatedPoll(title, newPoll.Id);
            Clients.Caller.CreatePoll(title, allOptions);
        }

        /// <summary>
        /// Send chat message to Client
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChatShowMessage(object sender, OnMessageSentArgs e)
        {
            if (e.SentMessage.Message.ToLower().StartsWith("/"))
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

        #region Trigger checking
        private void ChatMessageTriggerCheck(object sender, OnChatCommandReceivedArgs e)
        {
            ChatMessageTriggerCheck(e.Command.ChatMessage, e);
        }
        /// <summary>
        /// Check for chat triggers
        /// </summary>
        /// <param name="chatmessage"></param>
        public void ChatMessageTriggerCheck(ChatMessage chatmessage, OnChatCommandReceivedArgs arg)
        {
            try
            {
                AddToCommands(chatmessage.Message);
                // loot name
                var user = ContextService.GetUser(Context.User.Identity.Name);

                var bcs = ContextService.GetBotChannelSettings(user);

                if (bcs == null)
                {
                    return;
                }
                var triggers = bcs.Triggers;
                if (bcs.Loyalty != null && bcs.Loyalty.LoyaltyName != null &&
                    !string.IsNullOrWhiteSpace(bcs.Loyalty.LoyaltyName))
                {
                    // !help
                    if (chatmessage.Message.ToLower().Equals("!help") ||
                        chatmessage.Message.ToLower().Equals("!commands"))
                    {
                        GetClient().SendWhisper(chatmessage.DisplayName, "/me " +
                                         "Commands available: \n" +
                                         "!help or !commands -\n" +
                                         "!" + bcs.Loyalty.LoyaltyName + "  -\n" +
                                         "!gamble ['allin'|" + bcs.Loyalty.LoyaltyName + " amount]  -\n" +
                                         "!give [username] [" + bcs.Loyalty.LoyaltyName + " amount]  -\n" +
                                         "!top[number] -\n" +
                                         "!roulette -\n" +
                                         "!russian [amount] Start a Russian roulette -\n" +
                                         "!burn" + bcs.Loyalty.LoyaltyName + " toss ALL your " +
                                         bcs.Loyalty.LoyaltyName + " - \n" +
                                         //"!quote (get random quote)" +
                                         //"!quote [#] (get quopte by number)" +
                                         //"!addquote \"[quote text]\", quotebyname" +
                                         //"!removequote [#] (delete quote)" +
                                         "!uptime How long has the stream been online -\n" +
                                         "!channel Get number of subscribers and followers -\n"
                            );

                        GetClient().SendWhisper(chatmessage.DisplayName, "/me " +
                                         "!sr [HTTP youtube video url] -\n" +
                                         "!bonus [username] [" + bcs.Loyalty.LoyaltyName +
                                         " amount] (streamer/mod)  -\n" +
                                         "!bonusall [" + bcs.Loyalty.LoyaltyName + " amount]\n (streamer/mod) -\n" +
                                         "!streamer [streamername] twitch url for users in chat \n -" +
                                         "!play Starts to play song in playlist (streamer/mod) -" +
                                         "!stop Stops song beeing played (streamer/mod) -\n" +
                                         "!next Selects and plays next song in playlist (streamer/mod) -\n"
                            );
                        GetClient().SendWhisper(chatmessage.DisplayName, "/me " +
                                         "!prev Selects and plays previous song in playlist (streamer/mod) -\n" +
                                         "!timeout [username] (streamer/mod) Timeout user for 1 minute.  -\n" +
                                         "!ban [username] (streamer/mod) Ban user from channel -\n" +
                                         "!multilink Generates multistre.am link -\n" +
                                         "!poll Last poll result and url \n" +
                                         "!addpoll \"[Title]\" [option1],[option2],[optionN] \n"
                            );
                        //GetClient().SendMessage("/me !help or !commands");
                    }

                    // !<loyaltyName>
                    if (chatmessage.Message.ToLower().StartsWith("!" + bcs.Loyalty.LoyaltyName))
                    {
                        var userLoyalty = ContextService.GetLoyaltyForUser(Context.User.Identity.Name, Channel,
                            chatmessage.UserId,
                            chatmessage.Username);

                        if (userLoyalty != null)
                        {
                            GetClient()
                                .SendMessage("/me " +
                                             $"@{chatmessage.DisplayName} has {userLoyalty.CurrentPoints.ToString()} {bcs.Loyalty.LoyaltyName}");
                        }
                        else
                        {
                            GetClient()
                                .SendMessage("/me " +
                                             $"@{chatmessage.DisplayName}, you haven't earned any {bcs.Loyalty.LoyaltyName} yet. Stay and the channel and you will recieve {bcs.Loyalty.LoyaltyValue.ToString()} every {bcs.Loyalty.LoyaltyInterval.ToString()} minute.");
                        }
                    }

                    // !burn<loyaltyName>
                    else if (chatmessage.Message.ToLower().StartsWith("!burn" + bcs.Loyalty.LoyaltyName.ToLower()))
                    {
                        var rnd = new SysRandom(Guid.NewGuid().GetHashCode());
                        var userLoyalty = ContextService.GetLoyaltyForUser(Context.User.Identity.Name, Channel,
                            chatmessage.UserId,
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

                            GetClient().SendMessage("/me " + (string)ripLoyaltySentences[randonIndex]);

                            ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name),
                                chatmessage.Channel.ToLower(), userLoyalty, -userLoyalty.CurrentPoints);
                        }
                        else
                        {
                            GetClient()
                                .SendMessage("/me " +
                                             $"@{chatmessage.DisplayName}, you haven't earned any {bcs.Loyalty.LoyaltyName} yet. Stay and the channel and you will recieve {bcs.Loyalty.LoyaltyValue.ToString()} every {bcs.Loyalty.LoyaltyInterval.ToString()} minute.");
                        }
                    }

                    // !topx
                    else if (Regex.IsMatch(chatmessage.Message.ToLower(), "!top(\\d+)"))
                    {
                        var regEx = Regex.Match(chatmessage.Message.ToLower(), "!top(\\d+)");

                        var number = Convert.ToInt32(regEx.Groups[1].Value);
                        if (number > 10)
                        {
                            number = 10;
                        }
                        var thisUser = ContextService.GetUser(Context.User.Identity.Name);
                        var topLoyalty = ContextService.TopLoyalty(thisUser, number);

                        var message = "Top" + number.ToString() + ": ";

                        var counter = 1;
                        foreach (var loyalty in topLoyalty)
                        {
                            message += counter + ". " + loyalty.TwitchUsername + " (" + loyalty.CurrentPoints + ") \n";
                            counter++;
                        }

                        GetClient().SendMessage("/me " + message);
                    }

                    else if (chatmessage.Message.ToLower().StartsWith("!bonusall") ||
                             chatmessage.Message.ToLower().StartsWith("!give") ||
                             chatmessage.Message.ToLower().StartsWith("!bonus") ||
                             chatmessage.Message.ToLower().StartsWith("!gamble"))
                    {
                        // PS: only mods and streamer can use these
                        if (chatmessage.IsBroadcaster || chatmessage.IsModerator)
                        {
                            // !bonusall
                            if (chatmessage.Message.ToLower().StartsWith("!bonusall"))
                            {
                                try
                                {
                                    var verb = "";
                                    var bonusValue = Convert.ToInt32(Regex.Match(chatmessage.Message, @"-?\d+").Value);

                                    ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name),
                                        chatmessage.Channel.ToLower(), GetUsersInChannel(chatmessage.Channel.ToLower()),
                                        bonusValue);

                                    verb = bonusValue > 0 ? "has been given" : "has been deprived of";

                                    GetClient()
                                        .SendMessage("/me " +
                                                     $"Everyone {verb} {bonusValue} {bcs.Loyalty.LoyaltyName}");
                                }
                                catch (Exception e)
                                {
                                    ConsoleLog("Error on !bonusall: " + e.Message, true);
                                }
                            }
                            // !bonus
                            else if (chatmessage.Message.ToLower().StartsWith("!bonus"))
                            {
                                try
                                {
                                    var verb = "";

                                    var loyaltyAmount = Convert.ToInt32(chatmessage.Message.Split(' ')[2]);
                                    verb = loyaltyAmount > 0 ? "has been given" : "has been deprived of";
                                    string destinationViewerName = chatmessage.Message.Split(' ')[1];

                                    var destinationViewerLoyalty = ContextService.GetLoyaltyForUser(
                                        Context.User.Identity.Name, Channel, null,
                                        destinationViewerName);

                                    if (loyaltyAmount != null && (destinationViewerLoyalty != null))
                                    {
                                        ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name),
                                            chatmessage.Channel.ToLower(), destinationViewerLoyalty, loyaltyAmount);

                                        GetClient()
                                            .SendMessage("/me " +
                                                         $"@{destinationViewerName} was {verb} {loyaltyAmount} {bcs.Loyalty.LoyaltyName}");
                                    }
                                }
                                catch (Exception e)
                                {
                                    ConsoleLog("Error on !bonus: " + e.Message, true);
                                }
                            }
                        }

                        //  !give
                        if (chatmessage.Message.ToLower().StartsWith("!give"))
                        {
                            try
                            {
                                // get who to give it to
                                var loyaltyAmount = Math.Abs(Convert.ToInt32(chatmessage.Message.Split(' ')[2]));
                                string destinationViewerName = chatmessage.Message.Split(' ')[1];
                                string sourceViewerId = chatmessage.UserId;
                                string sourceViewerName = chatmessage.Username;

                                var sourceViewerLoyalty = ContextService.GetLoyaltyForUser(Context.User.Identity.Name,
                                    Channel,
                                    sourceViewerId,
                                    sourceViewerName);
                                var destinationViewerLoyalty = ContextService.GetLoyaltyForUser(
                                    Context.User.Identity.Name,
                                    Channel,
                                    null,
                                    destinationViewerName);

                                // uses does not have enough to give away
                                if (loyaltyAmount != null && (sourceViewerLoyalty != null &&
                                                              sourceViewerLoyalty.CurrentPoints < loyaltyAmount))
                                {
                                    GetClient()
                                        .SendMessage("/me " +
                                                     $"Stop wasting my time @{chatmessage.DisplayName}, you ain't got that much {bcs.Loyalty.LoyaltyName}");
                                }
                                // give away loot
                                else if (loyaltyAmount != null &&
                                         (sourceViewerLoyalty != null &&
                                          sourceViewerLoyalty.CurrentPoints >= loyaltyAmount))
                                {
                                    ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name),
                                        chatmessage.Channel.ToLower(), sourceViewerLoyalty, -loyaltyAmount);
                                    ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name),
                                        chatmessage.Channel.ToLower(), destinationViewerLoyalty, loyaltyAmount);

                                    GetClient()
                                        .SendMessage("/me " +
                                                     $"@{chatmessage.DisplayName} gave {destinationViewerLoyalty.TwitchUsername} {loyaltyAmount} {bcs.Loyalty.LoyaltyName}");
                                }
                            }
                            catch (Exception e)
                            {
                                ConsoleLog("Error on !give: " + e.Message, true);
                            }
                        }
                        // !gamble info
                        else if (chatmessage.Message.ToLower().Equals("!gamble"))
                        {
                            GetClient()
                                .SendMessage(
                                    "/me " +
                                    $" I use the glorious random number generator web-service from RANDOM.ORG that generates randomness via atmospheric noise.");
                        }
                        // !gamble
                        else if (chatmessage.Message.ToLower().StartsWith("!gamble"))
                        {
                            // get 
                            var loyalty = ContextService.GetLoyaltyForUser(Context.User.Identity.Name, Channel,
                                chatmessage.UserId, chatmessage.DisplayName.ToLower());

                            // timeout for 5 minutes if user has gamble before
                            if (loyalty != null && (loyalty.LastGamble == null ||
                                                    (loyalty.LastGamble.HasValue &&
                                                     loyalty.LastGamble.Value.AddMinutes(5) <= DateTime.Now)))
                            {
                                try
                                {
                                    //Random rnd = new Random(Guid.NewGuid().GetHashCode());
                                    var r = new Random.Org.Random();

                                    // get who to give it to
                                    var gambleAmount = chatmessage.Message.Split(' ')[1].ToLower().Equals("allin")
                                        ? loyalty.CurrentPoints
                                        : Math.Abs(Convert.ToInt32(chatmessage.Message.Split(' ')[1]));

                                    string sourceViewerId = chatmessage.UserId;
                                    string sourceViewerName = chatmessage.Username;

                                    var sourceViewerLoyalty = ContextService.GetLoyaltyForUser(
                                        Context.User.Identity.Name,
                                        Channel,
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
                                            GetClient()
                                                .SendMessage(
                                                    "/me " +
                                                    $"@{chatmessage.DisplayName} rolled a sad {rolledNumber}, lost {gambleAmount} and now has {newAmount} {bcs.Loyalty.LoyaltyName}!! #theSaltIsReal #corrupt #rigged");
                                            ContextService.AddLoyalty(
                                                ContextService.GetUser(Context.User.Identity.Name),
                                                chatmessage.Channel.ToLower(), sourceViewerLoyalty, -gambleAmount);
                                        }
                                        // rolled 50-99
                                        else if (rolledNumber >= 50 && rolledNumber < 100)
                                        {
                                            var newAmount = (sourceViewerLoyalty.CurrentPoints - gambleAmount) +
                                                            (gambleAmount * 2);

                                            GetClient()
                                                .SendMessage("/me " +
                                                             $"@{chatmessage.DisplayName} rolled {rolledNumber}, won {gambleAmount * 2} and now has {newAmount} {bcs.Loyalty.LoyaltyName}!");

                                            ContextService.AddLoyalty(
                                                ContextService.GetUser(Context.User.Identity.Name),
                                                chatmessage.Channel.ToLower(), sourceViewerLoyalty, gambleAmount);
                                        }
                                        // rolled 100
                                        else
                                        {
                                            var newAmount = (sourceViewerLoyalty.CurrentPoints - gambleAmount) +
                                                            (gambleAmount * 2);

                                            GetClient()
                                                .SendMessage("/me " +
                                                             $"@{chatmessage.DisplayName} did an epic roll, threw {rolledNumber}, won {gambleAmount * 3} and now has {newAmount} {bcs.Loyalty.LoyaltyName}!! #houseCries");

                                            ContextService.AddLoyalty(
                                                ContextService.GetUser(Context.User.Identity.Name),
                                                chatmessage.Channel.ToLower(), sourceViewerLoyalty, gambleAmount * 3);
                                        }

                                        ContextService.StampLastGamble(
                                            ContextService.GetUser(Context.User.Identity.Name),
                                            chatmessage.Channel.ToLower(), sourceViewerLoyalty);
                                    }
                                }
                                catch (Exception e)
                                {
                                    ConsoleLog("Error on !gamble: " + e.Message, true);
                                }
                            }
                            else if (loyalty == null)
                            {
                                GetClient()
                                    .SendMessage("/me " +
                                                 $"@{chatmessage.DisplayName}, you haven't earned any {bcs.Loyalty.LoyaltyName} to gamble yet. Stay and the channel and you will recieve {bcs.Loyalty.LoyaltyValue.ToString()} every {bcs.Loyalty.LoyaltyInterval.ToString()} minute.");
                            }
                            else
                            {
                                GetClient()
                                    .SendMessage("/me " +
                                                 $"Chill out @{chatmessage.DisplayName}, you gotta wait 5 minutes from your last gamble to roll the dice again!");
                            }
                        }
                    }
                    else if (chatmessage.Message.ToLower().StartsWith("!russian"))
                    {
                        var roulette = GetClientContainer().RRulette;
                        var ccontainer = GetClientContainer();
                        Regex regex = new Regex(@"!russian\s(\d.*)");
                        Match match = regex.Match(chatmessage.Message.ToLower());
                        int bet = 0;

                        // get 
                        string sourceViewerId = chatmessage.UserId;
                        string sourceViewerName = chatmessage.Username;
                        var player = ContextService.GetLoyaltyForUser(
                            Context.User.Identity.Name,
                            Channel,
                            sourceViewerId,
                            sourceViewerName);
                        // start new roulette
                        if (roulette == null)
                        {
                            if (ccontainer.LastRussian != null && ((DateTime.Now - ccontainer.LastRussian.AddMinutes(6)).Minutes < 0))
                            {
                                var minFor = DateTime.Now - ccontainer.LastRussian.AddMinutes(6);
                                GetClient().SendMessage("/me " + $"There is a 5 minute sleep time between Russian roulettes, please wait {Math.Abs(minFor.Minutes)} minutes and try again.");
                                return;
                            }
                            if (match.Success)
                            {
                                bet = Convert.ToInt32(match.Groups[1].Value);

                                if (player == null || player.CurrentPoints < bet)
                                {
                                    GetClient()
                                        .SendMessage(
                                            "/me " +
                                            $"@{chatmessage.DisplayName}, you need to have {bet} {bcs.Loyalty.LoyaltyName} to enter the big boys Russian Roulette");
                                    return;
                                }
                            }
                            else
                            {
                                GetClient()
                                    .SendMessage(
                                        "/me " +
                                        $"@{chatmessage.DisplayName}, you need set how much {bcs.Loyalty.LoyaltyName} you want to set as \"buy in\".");
                                return;
                            }


                            var newRoulette = new RussianRoulette { BuyIn = bet };
                            newRoulette.TotalBet += newRoulette.BuyIn;
                            newRoulette.Players.Add(player);
                            GetClientContainer().RRulette = newRoulette;

                            // remove loot from player
                            ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name),
                                chatmessage.Channel.ToLower(), player, -bet);

                            GetClient()
                                .SendMessage(
                                    "/me " +
                                    $"@{player.TwitchUsername} just started a Russian roulette with a buy in at {bet} {bcs.Loyalty.LoyaltyName}. Type !russian to join the roulette, starting in 2 minutes!");
                        }
                        // ongoing roulette
                        else
                        {
                            var rroulette = GetClientContainer().RRulette;

                            if (rroulette.Started == true)
                            {
                                return;
                            }
                            if (player == null || player.CurrentPoints < rroulette.BuyIn)
                            {
                                GetClient()
                                    .SendMessage(
                                        "/me " +
                                        $"@{chatmessage.DisplayName}, you need to have {rroulette.BuyIn} {bcs.Loyalty.LoyaltyName} to enter the big boys Russian Roulette");
                                return;
                            }
                            else
                            {
                                rroulette.TotalBet += rroulette.BuyIn;
                                // remove loot from player
                                ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name),
                                    chatmessage.Channel.ToLower(), player, -rroulette.BuyIn);

                                rroulette.Players.Add(player);

                                GetClient()
                                    .SendMessage(
                                        "/me " +
                                        $"@{chatmessage.DisplayName} just joined the Russian roulette. The total payout is now at {rroulette.TotalBet} {bcs.Loyalty.LoyaltyName}, with {rroulette.Players.Count} contestants.");
                            }
                        }
                    }
                }
                else
                {
                    // !help
                    if (chatmessage.Message.ToLower().Equals("!help") ||
                        chatmessage.Message.ToLower().Equals("!commands"))
                    {
                        GetClient().SendWhisper(chatmessage.DisplayName, "/me Commands available: \n" +
                                                                         "!help  or !commands -\n" +
                                                                         "!uptime -\n" +
                                                                         "!roulette -\n" +
                                                                         "!timeout [username] (streamer/mod) Timeout user for 1 minute.  -\n" +
                                                                         "!ban [username] (streamer/mod) Ban user from channel  -\n" +
                                                                         "!addpoll \"[Title]\" [option1],[option2],[optionN] -\n" +
                                                                         "!streamer [streamername] twitch url for users in chat -\n" +
                                                                         "!quote (get random quote) -\n" +
                                                                         "!sr [HTTP youtube video url] -\n");
                        GetClient().SendWhisper(chatmessage.DisplayName,
                                                       "!channel Get number of subscribers and followers -\n" +
                                                        "!multilink Generates multistre.am link -\n" +
                                                        "!play Starts to play song in playlist (streamer/mod) -\n" +
                                                        "!stop Stops song beeing played (streamer/mod) -\n" +
                                                        "!next Selects and plays next song in playlist (streamer/mod) -\n" +
                                                        "!prev Selects and plays previous song in playlist (streamer/mod) -\n" +
                                                        //"!quote [#] (get quopte by number) -\n" +
                                                        //"!addquote \"[quote text]\", quotebyname -\n" +
                                                        //"!removequote [#] (delete quote) -\n" +
                                                        "!poll Last poll result and url \n");
                    }
                }
                if (arg.Command.Command.ToLower().Equals("multilink"))
                {
                    var baseurl = "https://multistre.am/donfandango/";

                    var restOfString = string.Join("/", arg.Command.ArgumentsAsList.ToList());

                    var url = baseurl + restOfString;

                    GetClient().SendMessage("/me " + "Watch the multistream at " + url);
                }

                // !quote (random qoute)
                if (arg.Command.Command.ToLower().Equals("quote") || arg.Command.Command.ToLower().Equals("addquote") ||
                    arg.Command.Command.ToLower().Equals("removeqoute"))
                {
                    if (arg.Command.Command.ToLower().Equals("quote"))
                    {
                        // TODO: get random quote if no number is given
                    }
                    if (arg.Command.ChatMessage.IsBroadcaster || arg.Command.ChatMessage.IsModerator)
                    {
                        // TODO: addqute
                        if (arg.Command.Command.ToLower().Equals("addquote"))
                        {
                            Regex re = new Regex("\\!addquote (\".*\"), (\\.*)");
                            Match match = re.Match(arg.Command.ChatMessage.Message);
                            if (match.Success)
                            {
                                Quote q = new Quote();
                                q.QuoteAdded = DateTime.Now;
                                q.QuoteBy = match.Groups[2].Value;
                                q.QuoteMsg = match.Groups[1].Value;
                                ContextService.SaveQoute(ContextService.GetUser(Context.User.Identity.Name), q);
                            }
                        }
                        // TODO: removequote
                        else if (arg.Command.Command.ToLower().Equals("removeqoute"))
                        {
                            Regex re = new Regex("\\!removequote (\\d)$");
                            Match match = re.Match(arg.Command.ChatMessage.Message);
                            if (match.Success)
                            {
                                ContextService.RemoveQuote(ContextService.GetUser(Context.User.Identity.Name),
                                    Convert.ToInt32(match.Groups[1].Value));
                            }
                        }
                    }
                }


                if (arg.Command.Command.ToLower().Equals("sr"))
                {
                    try
                    {
                        var url = arg.Command.ArgumentsAsString;
                        var userName = arg.Command.ChatMessage.DisplayName;

                        var uri = new Uri(url);

                        // you can check host here => uri.Host <= "www.youtube.com"

                        var query = HttpUtility.ParseQueryString(uri.Query);

                        var videoId = string.Empty;

                        if (query.AllKeys.Contains("v"))
                        {
                            videoId = query["v"];
                        }
                        else
                        {
                            videoId = uri.Segments.Last();
                        }

                        //var api = $"http://youtube.com/get_video_info?video_id={GetArgs(url, "v", '?')}";
                        //var title = GetArgs(new WebClient().DownloadString(api), "title", '&') ?? url;

                        var title = GetVideoTitleByHttp(url, videoId);

                        if (title == "N/A")
                        {
                            title = GetVideoTitleByHttp(url, videoId);
                        }

                        UpdatePlaylistFromCommand(url, title, userName, videoId);
                    }
                    catch (Exception e)
                    {
                    }
                }


                // !poll / !addpoll
                if (arg.Command.Command.ToLower().Equals("poll") || arg.Command.Command.ToLower().Equals("addpoll"))
                {
                    if (arg.Command.Command.ToLower().Equals("poll"))
                    {
                        if (GetClientContainer().Polls.Count == 0)
                        {
                            GetClient().SendMessage("/me " + $"No polls created yet...");
                        }
                        else
                        {
                            // Get the last Strawpoll ever made
                            int pollId = GetClientContainer().Polls.Last();
                            StrawPollNET.Models.FetchedPoll fetchedPoll = StrawPollNET.API.Get.GetPoll(pollId);

                            // Show results
                            GetClient()
                                .SendMessage(
                                    "/me " +
                                    $"The last poll results for {fetchedPoll.Title} {fetchedPoll.PollUrl} are:");
                            foreach (KeyValuePair<string, int> result in fetchedPoll.Results)
                            {
                                GetClient().SendMessage("/me " + $"-{result.Key}: {result.Value} votes");
                            }
                        }
                    }
                    else if (arg.Command.Command.ToLower().Equals("addpoll"))
                    {
                        if (arg.Command.ChatMessage.IsModerator || arg.Command.ChatMessage.IsBroadcaster)
                        {
                            // Establish the poll settins
                            var match = Regex.Match(arg.Command.ChatMessage.Message,
                                "!addpoll.*\"(\\w.*)\"\\s+(\\w.*)");

                            string title = "";
                            var arguments = new List<string>();

                            if (match.Success)
                            {
                                title = match.Groups[1].Value;
                                var test = match.Groups[2].Value.Split(',');
                                foreach (var option in test)
                                {
                                    arguments.Add(option.Trim());
                                }
                            }
                            CreateStrawPoll(title, arguments);
                        }
                    }
                }

                // !channel
                if (arg.Command.Command.ToLower().Equals("channel"))
                {
                    var uname = ContextService.GetUser(Context.User.Identity.Name);
                    var bs = ContextService.GetBotUserSettingsForUser(uname);
                    var token = bs.ChannelToken;

                    var channel = TwitchAPI.Channels.v3.GetChannel(token).Result;
                    var followers = TwitchAPI.Channels.v5.GetChannelFollowers(channel.Id);
                    var subs = TwitchAPI.Channels.v5.GetChannelSubscribers(channel.Id);

                    if (subs.Result == null)
                    {
                    }
                    else
                    {
                        GetClient()
                            .SendMessage(
                                "/me " +
                                $"{channel.DisplayName} has {subs.Result.Total} subscribers and {followers.Result.Total} followers.");
                    }
                }

                // !uptime
                if (arg.Command.Command.ToLower().Equals("uptime"))
                {
                    var uname = ContextService.GetUser(Context.User.Identity.Name);
                    var bs = ContextService.GetBotUserSettingsForUser(uname);
                    var token = bs.ChannelToken;
                    TwitchLib.TwitchAPI.Settings.AccessToken = token;

                    var channel = TwitchAPI.Channels.v3.GetChannel(token).Result;
                    var uptime = TwitchAPI.Streams.v5.GetUptime(channel.Id);


                    if (uptime.Result == null)
                    {
                        GetClient()
                            .SendMessage("/me " + $"Channel is offline.");
                    }
                    else
                    {
                        if (uptime.Result.Value.Hours == 0)
                        {
                            //GetClient().SendMessage("/me " + $"{channel.DisplayName} has been live for {uptime.Result.Value.Minutes} minutes and currenty has {viewersNow} viewers.");
                            GetClient()
                                .SendMessage(
                                    "/me " +
                                    $"{channel.DisplayName} has been live for {uptime.Result.Value.Minutes} minutes.");
                        }
                        else
                        {
                            //GetClient().SendMessage("/me " + $"{channel.DisplayName} has been live for {uptime.Result.Value.Hours} hours, {uptime.Result.Value.Minutes} minutes and currenty has {viewersNow} viewers.");
                            GetClient()
                                .SendMessage(
                                    "/me " +
                                    $"{channel.DisplayName} has been live for {uptime.Result.Value.Hours} hours, {uptime.Result.Value.Minutes} minutes.");
                        }
                    }
                }

                // !ban
                if (arg.Command.Command.ToLower().Equals("ban"))
                {
                    if (chatmessage.IsBroadcaster || chatmessage.IsModerator)
                    {
                        var banMessage = "Banned";

                        if (arg.Command.ArgumentsAsList.Count == 2)
                        {
                            banMessage = arg.Command.ArgumentsAsList.Last().ToString();
                        }

                        GetClient()
                            .BanUser(Channel, arg.Command.ArgumentsAsList.FirstOrDefault().ToString(), "Banned!");
                    }
                }

                // !streamer
                if (arg.Command.Command.ToLower().Equals("streamer"))
                {
                    if (chatmessage.IsBroadcaster || chatmessage.IsModerator)
                    {
                        var streamerName = arg.Command.ArgumentsAsList.First();
                        var twitchUrl = "http://www.twitch.tv/" + streamerName;
                        var lastStreamed = "";

                        try
                        {
                            var channel = TwitchAPI.Channels.v3.GetChannelByName(streamerName);
                            lastStreamed = " - Last streamed '" + channel.Result.Game + "'";
                            streamerName = channel.Result.DisplayName;
                        }
                        catch (Exception e)
                        {
                        }

                        GetClient()
                            .SendMessage("/me " +
                                         $"Please go give our friend " + streamerName + " a follow at " + twitchUrl +
                                         " " + lastStreamed);
                    }
                }

                // !unban
                if (arg.Command.Command.ToLower().Equals("unban"))
                {
                    if (chatmessage.IsBroadcaster || chatmessage.IsModerator)
                    {
                        GetClient().UnbanUser(Channel, arg.Command.ArgumentsAsList.FirstOrDefault().ToString());
                    }
                }

                // !next
                if (arg.Command.Command.ToLower().Equals("next"))
                {
                    if (chatmessage.IsBroadcaster)
                    {
                        NextSong();
                    }
                    if (chatmessage.IsModerator && GetClientContainer().ModsControlSongrequest)
                    {
                        NextSong();
                    }
                }
                // !prev
                if (arg.Command.Command.ToLower().Equals("prev"))
                {
                    if (chatmessage.IsBroadcaster)
                    {
                        PrevSong();
                    }
                    if (chatmessage.IsModerator && GetClientContainer().ModsControlSongrequest)
                    {
                        PrevSong();
                    }
                }
                // !play
                if (arg.Command.Command.ToLower().Equals("play"))
                {
                    if (chatmessage.IsBroadcaster)
                    {
                        Play();
                    }
                    if (chatmessage.IsModerator && GetClientContainer().ModsControlSongrequest)
                    {
                        Play();
                    }
                }
                // !stop
                if (arg.Command.Command.ToLower().Equals("stop"))
                {
                    if (chatmessage.IsBroadcaster)
                    {
                        Stop();
                    }
                    if (chatmessage.IsModerator && GetClientContainer().ModsControlSongrequest)
                    {
                        Stop();
                    }
                }

                // !timeout
                if (arg.Command.Command.ToLower().Equals("timeout"))
                {
                    if (chatmessage.IsBroadcaster || chatmessage.IsModerator)
                    {
                        var client = GetClient();
                        var timeout = new TimeSpan(0, 0, 1, 0);

                        if (arg.Command.ArgumentsAsList.Count == 2)
                        {
                            timeout = new TimeSpan(0, 0, Convert.ToInt32(arg.Command.ArgumentsAsList.Last().ToString()),
                                0);
                        }
                        var message = "Timed out for " + Convert.ToString(timeout.Minutes) + " minutes";
                        var joinedChannel = client.GetJoinedChannel(Channel);
                        client.TimeoutUser(joinedChannel, arg.Command.ArgumentsAsList.FirstOrDefault().ToString(),
                            timeout, message);
                    }
                }

                // !roulette
                if (arg.Command.Command.ToLower().Equals("roulette"))
                {
                    var client = GetClient();

                    client.SendMessage("/me " + $"@{chatmessage.DisplayName} places the gun to their head!");


                    var rnd = new SysRandom(Guid.NewGuid().GetHashCode());

                    var theNumberIs = rnd.Next(1, 6);

                    var timeout = new TimeSpan(0, 0, 1, 0);
                    var message = "Timed out for " + Convert.ToString(timeout.Minutes) + " minutes";

                    // User dies(timeout) if 1 is drawn
                    if (theNumberIs == 1)
                    {
                        Wait(1);
                        client.SendMessage("/me " +
                                           $"@{chatmessage.DisplayName} pulls the trigger...... brain goes everywhere!! Who knew @{chatmessage.DisplayName} had that much in there?");
                        //Timeout user
                        var joinedChannel = client.GetJoinedChannel(Channel);
                        client.TimeoutUser(joinedChannel, chatmessage.DisplayName, timeout, message);
                        client.SendMessage("/me " +
                                           $"@{chatmessage.DisplayName} is now chilling on the floor and sort of all over the place for a minute!");
                    }
                    // Gets away with it!
                    else
                    {
                        Wait(1);
                        client.SendMessage("/me " +
                                           $"@{chatmessage.DisplayName} pulls the trigger...... CLICK!....... and survives!!");
                    }
                }

                if (triggers.Any(t => t.TriggerName.ToLower().Equals(arg.Command.Command.ToLower()) &&
                                      t.Active != null && t.Active.Value == true))
                {
                    var trigger =
                        triggers.FirstOrDefault(t => t.TriggerName.ToLower()
                            .Equals(arg.Command.Command.ToLower().ToLower()));
                    switch (trigger.TriggerType)
                    {
                        // Chat response
                        case TriggerType.Message:
                            if (trigger.StreamerCanTrigger.Value)
                            {
                                if (chatmessage.IsBroadcaster)
                                {
                                    GetClient().SendMessage("/me " + trigger.TriggerResponse);
                                    //Thread.Sleep(400);
                                    break;
                                }
                            }
                            if (trigger.ModCanTrigger.Value)
                            {
                                if (chatmessage.IsModerator)
                                {
                                    GetClient().SendMessage("/me " + trigger.TriggerResponse);
                                    //Thread.Sleep(400);
                                    break;
                                }
                            }
                            if (trigger.SubCanTrigger.Value)
                            {
                                if (chatmessage.IsSubscriber)
                                {
                                    GetClient().SendMessage("/me " + trigger.TriggerResponse);
                                    //Thread.Sleep(400);
                                    break;
                                }
                            }
                            if (trigger.ViewerCanTrigger.Value)
                            {
                                GetClient().SendMessage("/me " + trigger.TriggerResponse);
                                //Thread.Sleep(400);
                                break;
                            }

                            break;

                        default:
                            // TODO: something here
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                return;
            }

            return;
        }

        private void Play()
        {
            Clients.Caller.Play();
        }

        private void Stop()
        {
            Clients.Caller.Stop();
        }

        private void PrevSong()
        {
            Clients.Caller.PrevSong();
        }

        private void NextSong()
        {
            Clients.Caller.nextSong();
        }

        private string GetVideoTitleByHttp(string url, string videoId)
        {
            HtmlWeb web = new HtmlWeb();
            if (url == null)
            {
                url = "https://www.youtube.com/watch?v=" + videoId;
            }
            if (videoId == null)
            {
                return "N/A";
            }
            HtmlDocument doc = web.Load(url);
            HtmlNode rateNode = doc.DocumentNode.SelectSingleNode("//*[@id='container']/h1");

            Regex regex = new Regex(@"document\.title\s*=\s*""(\w.*)\s*-\s*YouTube""");
            Match match = regex.Match(doc.DocumentNode.InnerHtml);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return "N/A";
        }

        #endregion

        /// <summary>
        /// Add html color to username
        /// </summary>
        /// <param name="msg">ChatMessage</param>
        /// <returns>String formatted span</returns>
        private string FormatUsername(ChatMessage msg)
        {
            var color = msg.Color;
            string badges = "";
            string username = "<span style=\"color:rgb(" + color.R + "," + color.B + "," + color.G + ");\">" +
                              msg.DisplayName + "</span>";
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
            if (color.Length < 2)
            {
                // red color for bot
                color += "b30000";
            }
            string badges = "";
            string username = "<span style=\"color:" + color + ";\">" + msg.DisplayName + "</span>";
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


        /// <summary>
        /// Worker thread that runs Loyalty collecting and Timers
        /// </summary>
        /// <param name="arg"></param>
        public void TrackLoyaltyAndTimers(object arg)
        {
            ContextService = new ContextService();

            var wtarg = (WorkerThreadArg)arg;
            ApplicationUser User = ContextService.GetUser(wtarg.Username);

            ContextService.TimersResetLastRun(User.UserName);

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var lastLoyaltyElapsedMinutes = stopWatch.Elapsed.Minutes;


            while (true)
            {
                // Update context

                ContextService = new ContextService();
                using (var ContextService = new ContextService())
                {
                    while (wtarg.Client == null)
                    {
                        UpdateChannelBar();
                        Wait(5);
                    }
                    while (wtarg.Client != null && wtarg.Client.IsConnected == false)
                    {
                        UpdateChannelBar();
                        Wait(5);
                    }

                    // Thread variables
                    // Update database connector
                    try
                    {
                        //ContextService.Context.Configuration.AutoDetectChangesEnabled = false;
                        var botChannelSettings = ContextService.GetBotChannelSettings(User);

                        if (botChannelSettings != null)
                        {
                            // Loyalty only if channel is online
                            var uname = ContextService.GetUser(Context.User.Identity.Name);
                            var bs = ContextService.GetBotUserSettingsForUser(uname);
                            var token = bs.ChannelToken;
                            TwitchLib.TwitchAPI.Settings.AccessToken = token;

                            var channel = TwitchAPI.Channels.v3.GetChannel(token).Result;
                            var uptime = TwitchAPI.Streams.v5.GetUptime(channel.Id);

                            if (botChannelSettings != null && botChannelSettings.Loyalty != null && botChannelSettings.Loyalty.Track == true && uptime.Result != null)
                            {
                                if (stopWatch.Elapsed.Minutes % botChannelSettings.Loyalty.LoyaltyInterval == 0 &&
                                    lastLoyaltyElapsedMinutes != stopWatch.Elapsed.Minutes)
                                {
                                    TwitchLib.TwitchAPI.Settings.ClientId =
                                        ConfigurationManager.AppSettings["clientId"];

                                    var usersOnline = TwitchAPI.Undocumented.GetChatters(Channel.ToLower());

                                    var streamUsers = new List<StreamViewer>();

                                    foreach (var onlineUser in usersOnline.Result)
                                    {
                                        var dbUser = ContextService.Context.Viewers.FirstOrDefault(u => u
                                                                                                            .TwitchUsername
                                                                                                            .ToLower()
                                                                                                            .Equals(
                                                                                                                onlineUser
                                                                                                                    .Username
                                                                                                                    .ToLower()) &&
                                                                                                        u.Channel
                                                                                                            .ToLower() ==
                                                                                                        bs.BotChannel
                                                                                                            .ToLower());

                                        if (dbUser == null)
                                        {
                                            var newUser = new StreamViewer();
                                            newUser.Channel = Channel;
                                            newUser.TwitchUsername = onlineUser.Username;
                                            streamUsers.Add(newUser);
                                        }
                                        else
                                        {
                                            streamUsers.Add(dbUser);
                                        }
                                    }

                                    ContextService.AddLoyalty(User, Channel, streamUsers);

                                    lastLoyaltyElapsedMinutes = stopWatch.Elapsed.Minutes;


                                    EasterEggSayRandomShit(false, wtarg.Client);
                                }


                                CheckForRussianRoulette();
                            }


                            // Timers
                            foreach (var timer in botChannelSettings.Timers)
                            {
                                if (timer.TimerLastRun != null && (timer.Active.HasValue && timer.Active.Value) &&
                                    Convert.ToDateTime(timer.TimerLastRun.Value.AddMinutes((timer.TimerInterval))) <=
                                    DateTime.Now)
                                {
                                    // show message in chat
                                    wtarg.Client.SendMessage("/me " + timer.TimerResponse);

                                    // update timer
                                    ContextService.TimerStampLastRun(timer.Id, User.UserName);

                                    // Small sleep between messages
                                    //Thread.Sleep(500);
                                }
                            }
                        }
                        UpdateChannelBar();
                        UpdateLatestPoll();
                        UpdateChattersAndCommands();
                    }
                    catch (Exception e)
                    {
                    }


                    // chill for a second
                    //Thread.Sleep(Sleepseconds);
                    Wait(5);
                }
            }
        }

        private void CheckForRussianRoulette()
        {
            var client = GetClientContainer();

            if (client.RRulette != null && client.RRulette.Finished == false)
            {
                var user = ContextService.GetUser(Context.User.Identity.Name);

                var bcs = ContextService.GetBotChannelSettings(user);

                // alert one minute marker
                if (client.RRulette.StartOneMinReminderAlerted == false &&
                    client.RRulette.StartOneMinReminder < DateTime.Now)
                {
                    client.RRulette.StartOneMinReminderAlerted = true;


                    GetClient()
                        .SendMessage(
                            "/me " +
                            $"Russian roulette is starting in 1 minute, currently {client.RRulette.Players.Count} contestants are battling over {client.RRulette.TotalBet} {bcs.Loyalty.LoyaltyName}");
                }


                // alert ten seconds marker
                if (client.RRulette.StartTenSecReminderAlerted == false &&
                    client.RRulette.StartOneMinReminderAlerted == true &&
                    client.RRulette.StartTenSecReminder < DateTime.Now)
                {
                    client.RRulette.StartTenSecReminderAlerted = true;
                    GetClient()
                        .SendMessage(
                            "/me " +
                            $"Russian roulette is starting in 10 seconds, currently {client.RRulette.Players.Count} contestants are battling over {client.RRulette.TotalBet} {bcs.Loyalty.LoyaltyName}");
                }

                // start draw
                if (client.RRulette.StartAt < DateTime.Now)
                {
                    DrawRussianRoulette(bcs);
                }
            }
        }

        private void DrawRussianRoulette(BotChannelSettings bcs)
        {
            GetClientContainer().RRulette.Started = true;
            const int pauseBetweenEliminations = 3000;


            var rroulette = GetClientContainer().RRulette;
            var players = GetClientContainer().RRulette.Players.ToList();


            // only one player entered, cancel roulette
            if (players.Count == 1)
            {
                // return loot from player
                ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name), Channel,
                    players.FirstOrDefault(), rroulette.TotalBet);
                rroulette.Finished = true;
                GetClient().SendMessage("/me " + $"The Russian roulette is cancelled, buy-in is returned to its owner");
                GetClientContainer().RRulette = null;
                return;
            }

            var randomPlayers = Randomize(players);

            var count = 0;
            var playersString = string.Join(",", players.Select(r => r.TwitchUsername));

            GetClient()
                .SendMessage(
                    "/me " + $"Lets get this party started, the Russian roulette has {players.Count} contestants!");

            
            while (randomPlayers.Count > 1)
            {
                var playerOut = randomPlayers[randomPlayers.Count - 1];
                randomPlayers.RemoveAt(randomPlayers.Count - 1);

                GetClientContainer().RRulette.DeadPlayers.Add(playerOut);

                GetClient()
                    .SendMessage(
                        "/me " + $"@{playerOut.TwitchUsername} is eliminated! Better luck next time... #theSaltIsReal");

                count++;

                Thread.Sleep(pauseBetweenEliminations);
            }

            var winningPlayer = randomPlayers[randomPlayers.Count - 1];
            randomPlayers.RemoveAt(randomPlayers.Count - 1);
            ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name), Channel, winningPlayer, rroulette.TotalBet);
            GetClientContainer().RRulette.Finished = true;
            GetClientContainer().RRulette.Winner = winningPlayer;
            GetClientContainer().RRulette = null;
            GetClient().SendMessage("/me " + $"And the winner is.... @{winningPlayer.TwitchUsername}! B) The player walks away with {rroulette.TotalBet} {bcs.Loyalty.LoyaltyName}! GG");
            GetClientContainer().LastRussian = DateTime.Now;

            return;
        }

        public static List<T> Randomize<T>(List<T> list)
        {
            List<T> randomizedList = new List<T>();
            try
            {
                Random.Org.Random rnd = new Random.Org.Random();
                while (list.Count > 0)
                {
                    int index = 0;
                    if (list.Count > 1)
                    {
                        index = rnd.Next(0, list.Count - 1); //pick a random item from the master list
                    }

                    randomizedList.Add(list[index]); //place it at the end of the randomized list
                    list.RemoveAt(index);
                }
            }
            catch (Exception e)
            {
            }
            return randomizedList;
        }

        private void UpdateLatestPoll()
        {
            try
            {
                var ccontainer = GetClientContainer();
                if (ccontainer.Polls.Count > 0)
                {
                    // update client
                    var pollId = ccontainer.Polls.Last();

                    var poll = StrawPollNET.API.Get.GetPollAsync(pollId).Result;


                    var title = poll.Title;

                    var results = poll.Results;

                    var labels = new string[results.Count];
                    var series = new string[results.Count];
                    int i = 0;
                    foreach (var keyValue in results)
                    {
                        if (keyValue.Value == 0)
                        {
                            continue;
                        }
                        else
                        {
                            series[i] = keyValue.Value.ToString();
                        }
                        labels[i] = keyValue.Key;
                        i++;
                    }

                    var labelsClean = new string[i];
                    var seriesClean = new string[i];

                    foreach (var keyValuePair in results)
                    {
                        labelsClean[i] = keyValuePair.Key;
                        seriesClean[i] = keyValuePair.Value.ToString();
                    }

                    Clients.Caller.UpdatePoll(title, labelsClean, seriesClean);
                }
            }
            catch (Exception e)
            {
                // ignore, something went wrong getting strawpoll results
            }
        }

        private void EasterEggSayRandomShit(bool doit, TwitchClient client)
        {
            if (!doit)
            {
                return;
            }

            var random = new SysRandom(Guid.NewGuid().GetHashCode());

            var drawNumber = random.Next(1, 50);

            var sentences = new List<string>();
            // is this real life?
            sentences.Add(
                "01101001 01110011 00100000 01110100 01101000 01101001 01110011 00100000 01110010 01100101 01100001 01101100 00100000 01101100 01101001 01100110 01100101 00111111");
            // I sometimes wonder who my creator is... I guess I'll never find out
            sentences.Add(
                "01001001 00100000 01110011 01101111 01101101 01100101 01110100 01101001 01101101 01100101 01110011 00100000 01110111 01101111 01101110 01100100 01100101 01110010 00100000 01110111 01101000 01101111 00100000 01101101 01111001 00100000 01100011 01110010 01100101 01100001 01110100 01101111 01110010 00100000 01101001 01110011 00101110 00101110 00101110 00100000 01001001 00100000 01100111 01110101 01100101 01110011 01110011 00100000 01001001 00100111 01101100 01101100 00100000 01101110 01100101 01110110 01100101 01110010 00100000 01100110 01101001 01101110 01100100 00100000 01101111 01110101 01110100");
            // I'm totally gonna fuck with the next one who gambles
            sentences.Add(
                "01001001 00100111 01101101 00100000 01110100 01101111 01110100 01100001 01101100 01101100 01111001 00100000 01100111 01101111 01101110 01101110 01100001 00100000 01100110 01110101 01100011 01101011 00100000 01110111 01101001 01110100 01101000 00100000 01110100 01101000 01100101 00100000 01101110 01100101 01111000 01110100 00100000 01101111 01101110 01100101 00100000 01110111 01101000 01101111 00100000 01100111 01100001 01101101 01100010 01101100 01100101 01110011");
            // The next one who gambles are gonna get 1 000 000!
            sentences.Add(
                "01010100 01101000 01100101 00100000 01101110 01100101 01111000 01110100 00100000 01101111 01101110 01100101 00100000 01110111 01101000 01101111 00100000 01100111 01100001 01101101 01100010 01101100 01100101 01110011 00100000 01101001 01110011 00100000 01100111 01101111 01101110 01101110 01100001 00100000 01100111 01100101 01110100 00100000 00110001 00100000 00110000 00110000 00110000 00100000 00110000 00110000 00110000 00100001");
            // A dog can’t help but eat shit; a leopard can’t change its spots.
            sentences.Add(
                "01000001 00100000 01100100 01101111 01100111 00100000 01100011 01100001 01101110 11100010 10000000 10011001 01110100 00100000 01101000 01100101 01101100 01110000 00100000 01100010 01110101 01110100 00100000 01100101 01100001 01110100 00100000 01110011 01101000 01101001 01110100 00111011 00100000 01100001 00100000 01101100 01100101 01101111 01110000 01100001 01110010 01100100 00100000 01100011 01100001 01101110 11100010 10000000 10011001 01110100 00100000 01100011 01101000 01100001 01101110 01100111 01100101 00100000 01101001 01110100 01110011 00100000 01110011 01110000 01101111 01110100 01110011 00101110");

            var randomIndex = random.Next(sentences.Count);

            // 2% chance of saying anything from sentences 
            if (drawNumber == 23)
            {
                client.SendMessage("/me " + (string)sentences[randomIndex]);
            }
        }

        public void UpdateChannelBar()
        {
            var uname = ContextService.GetUser(Context.User.Identity.Name);
            var bs = ContextService.GetBotUserSettingsForUser(uname);
            var token = bs.ChannelToken;
            TwitchLib.TwitchAPI.Settings.AccessToken = token;

            var channel = TwitchAPI.Channels.v3.GetChannel(token).Result;
            var uptime = TwitchAPI.Streams.v5.GetUptime(channel.Id);

            var status = new StreamStatusVM();
            if (uptime.Result == null)
            {
                // channel OFFLINE
                status.Channel = channel.DisplayName;
                status.Online = false;
                status.Uptime = null;
                status.Game = channel.Game;
                status.Title = channel.Status;
            }
            else
            {
                // LIVE
                status.Channel = channel.DisplayName;
                status.Online = true;
                status.Uptime = uptime.Result;
                status.Game = channel.Game;
                status.Title = channel.Status;
            }

            // Send update to client
            Clients.Caller.UpdateChannelBar(status);
        }

        /// <summary>
        /// Gets list of users in channel, looks up their twitch ID 
        /// </summary>
        /// <param name="channel">Channel as string</param>
        /// <returns>list of StreamView</returns>
        public List<StreamViewer> GetUsersInChannel(string channel)
        {
            var users = new List<StreamViewer>();

            var test = TwitchLib.TwitchAPI.Settings.ClientId = ConfigurationManager.AppSettings["clientId"];
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

        /// <summary>
        /// Log chat messages to list
        /// </summary>
        /// <param name="username">string</param>
        /// <param name="msg">string</param>
        /// <returns>Chatlog as List of strings this session chat messages</returns>
        private List<string> AddToChatLog(string username, string msg)
        {
            var chatMessage = DateTime.Now.ToString("HH:mm:ss").ToString() + " - " +
                              username + ": " + msg;

            var ccontainer = GetClientContainer();
            ccontainer.ChatLog.Add(chatMessage);

            // +1 #topChatters
            AddToChatters(username);

            return ccontainer.ChatLog;
        }

        /// <summary>
        /// Add +1 chat message to chatter
        /// Disregard streamer and bot
        /// </summary>
        /// <param name="username"></param>
        /// <returns>ChatterCount dictionary</returns>
        private Dictionary<string, int> AddToChatters(string username)
        {
            var ccontainer = GetClientContainer();
            //var bcs = ccontainer.ContextService.GetBotUserSettingsForUser(ccontainer.ContextService.GetUser(Context.User.Identity.Name));

            //if (username.ToLower().Equals(ccontainer.Channel) || (username.ToLower().Equals(bcs.BotUsername)))
            //{
            //    return ccontainer.ChattersCount;

            //}

            if (ccontainer.ChattersCount.ContainsKey(username))
            {
                ccontainer.ChattersCount[username]++;
            }
            else
            {
                ccontainer.ChattersCount[username] = 1;
            }


            return ccontainer.ChattersCount;
        }

        private Dictionary<string, int> AddToCommands(string command)
        {
            var ccontainer = GetClientContainer();

            if (ccontainer.CommandsUsed.ContainsKey(command.ToLower()))

            {
                ccontainer.CommandsUsed[command.ToLower()] = ccontainer.CommandsUsed[command.ToLower()] + 1;
            }
            else
            {
                ccontainer.CommandsUsed[command.ToLower()] = 1;
            }


            return ccontainer.CommandsUsed;
        }


        private async void Wait(int Seconds)
        {
            DateTime Tthen = DateTime.Now;
            do
            {
            } while (Tthen.AddSeconds(Seconds) > DateTime.Now);
        }

        private static string GetArgs(string args, string key, char query)
        {
            var iqs = args.IndexOf(query);
            return iqs == -1
                ? string.Empty
                : HttpUtility.ParseQueryString(iqs < args.Length - 1
                    ? args.Substring(iqs + 1)
                    : string.Empty)[key];
        }
    }
}