using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using BeeBot.Models;
using HtmlAgilityPack;
using Microsoft.AspNet.SignalR;
using StrawpollNET;
using StrawpollNET.Data;
using TwitchLib.Api;
using TwitchLib.Api.Exceptions;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;
using YoutubeExplode;
using YTBot.Models;
using YTBot.Models.ViewModels;
using YTBot.Services;
using SysRandom = System.Random;
using Timer = YTBot.Models.Timer;


namespace BeeBot.Signalr
{
    [Authorize]
    public class TwitchHub : Hub
    {
        private const int Sleepseconds = 1;

        private const int NUMTOPCHATTERS = 5;
        private const int NUMTOPCOMMANDS = 5;

        private string Username { get; set; }
        private string Password { get; set; }
        private string Channel { get; set; }
        private string ConnectionId { get; set; }
        private BotUserSettings BotUserSettings { get; set; }

        private ContextService ContextService { get; set; }
        private TriggerService TriggerService { get; set; }
        private static List<TwitchClientContainer> ClientContainers { get; set; }
        private static Dictionary<string, Thread> RunningThreads { get; set; }
        private ConnectionCredentials ConnCred { get; set; }
        public static TwitchClient Client { get; set; }

        private static TwitchAPI Api { get; set; }

        private string ChannelToken { get; set; }

        public TwitchHub()
        {
            ContextService = new ContextService();

            if (ClientContainers == null)
                ClientContainers = new List<TwitchClientContainer>();
            if (RunningThreads == null)
                RunningThreads = new Dictionary<string, Thread>();
        }

        /// <summary>
        ///     OnConnected event, initializes TwitchApi
        /// </summary>
        /// <returns></returns>
        public override Task OnConnected()
        {
            InitializeAPI();

            if (ClientContainers.Any(c => c.Id == GetUsername()))
            {
                if (ClientContainers.Count(c => c.Id == GetUsername()) > 1)
                {
                    var tccLast = ClientContainers.Last(c => c.Id == GetUsername());
                    ClientContainers.Remove(tccLast);
                }

                var twitchClientContainer = ClientContainers.FirstOrDefault(c => c.Id == GetUsername());
                twitchClientContainer.Client = GetClient();
            }
            else
            {
                var twitchClientContainer = new TwitchClientContainer
                {
                    Id = GetUsername(),
                    Channel = Channel
                };

                ClientContainers.Add(twitchClientContainer);
            }

            var tcc = GetClientContainer();

            var arg = new WorkerThreadArg
            {
                Channel = Channel,
                Username = GetUsername(),
                Client = tcc.Client
            };

            try
            {
                if (!HasRunningThread())
                {

                    var parameterizedThreadStart = new ParameterizedThreadStart(TrackLoyaltyAndTimers);
                    var newThread = new Thread(parameterizedThreadStart) {IsBackground = true};
                    newThread.Name = GetUsername();
                    AddThread(newThread);
                    newThread.Start(arg);
                }
            }
            catch (Exception e)
            {
                ConsoleLog("Error on OnConnected(): " + e.Message);
            }

            var user = ContextService.GetUser(GetUsername());
            BotUserSettings = ContextService.GetBotUserSettingsForUser(user);
            GetClientContainer().ConnectionId = Context.ConnectionId;
            ConnectionId = Context.ConnectionId;

            return base.OnConnected();
        }

        private bool HasRunningThread()
        {
            var threadExists =  RunningThreads.ContainsKey(Context.User.Identity.Name);

            return threadExists;
        }

        private void AddThread(Thread thread)
        {
            RunningThreads.Add(Context.User.Identity.Name, thread);
        }

        private void RemoveThread()
        {
            RunningThreads[Context.User.Identity.Name].Abort();
            RunningThreads.Remove(Context.User.Identity.Name);
        }

        public void AddLinkPermit(ChatMessage chatmessage, StreamViewer streamViewer)
        {
            var chatterUsername = chatmessage.Username ?? streamViewer.TwitchUsername;
            chatterUsername = chatterUsername.ToLower();

            var tcc = GetClientContainer();

            if (tcc.LinkPermits != null)
            {
                if (tcc.LinkPermits.ContainsKey(chatterUsername))
                {
                    tcc.LinkPermits[chatterUsername] = DateTime.Now.AddMinutes(5);
                }
                else
                {
                    tcc.LinkPermits.Add(chatterUsername, DateTime.Now.AddMinutes(5));
                }
            }
        }

        private bool HasLinkPermission(ChatMessage chatmessage, StreamViewer streamViewer)
        {
            var chatterUsername = chatmessage.Username ?? streamViewer.TwitchUsername;
            chatterUsername = chatterUsername.ToLower();
            var tcc = GetClientContainer();

            if (tcc.LinkPermits != null)
            {
                if (tcc.LinkPermits.ContainsKey(chatterUsername) && tcc.LinkPermits[chatterUsername] >= DateTime.Now)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            // no link permits
            return false;
        }

        /// <summary>
        ///     Initialize TwitchAPI with clientId and clientSecret
        /// </summary>
        /// <returns></returns>
        public async Task InitializeAPI()
        {
            var user = ContextService.GetUser(GetUsername());
            BotUserSettings = ContextService.GetBotUserSettingsForUser(user);
            ChannelToken = BotUserSettings.ChannelToken;
            Channel = BotUserSettings.BotChannel;
            var clientId = ConfigurationManager.AppSettings["clientId"];
            var clientSecret = ConfigurationManager.AppSettings["clientSecret"];
            Api = new TwitchAPI();
            Api.Settings.AccessToken = clientSecret;
            Api.Settings.ClientId = clientId;
        }

        /// <summary>
        ///     Message playing song to channel chat
        /// </summary>
        /// <param name="id"></param>
        public void PlayingSong(string id)
        {
            try
            {
                var ccontainer = GetClientContainer();

                var username = ContextService.GetUser(GetUsername());
                var bcs = ContextService.GetBotChannelSettings(username);
                //var client = GetClientContainer();

                var video = bcs.SongRequests.FirstOrDefault(v => v.VideoId == id);

                ccontainer.Client.SendMessage(ccontainer.Channel,
                    $"/me is now playing: {video.Title} - ( {video.Url} ) added by @{video.RequestedBy}");
            }
            catch (Exception exception)
            {
                ConsoleLog("Error on PlayingSong(): " + exception.Message);
            }
        }

        /// <summary>
        ///     Saves a new trigger
        /// </summary>
        /// <param name="triggerid"></param>
        /// <param name="triggername"></param>
        /// <param name="triggerresponse"></param>
        /// <param name="modscantrigger"></param>
        /// <param name="subscantrigger"></param>
        /// <param name="viewercantrigger"></param>
        /// <param name="triggeractive"></param>
        public void SaveTrigger(string triggerid, string triggername, string triggerresponse, string modscantrigger,
            string subscantrigger, string viewercantrigger, string triggeractive, TriggerType triggertype)
        {
            try
            {
                var trigger = new Trigger();
                trigger.Id = Convert.ToInt32(triggerid);
                trigger.TriggerName = triggername;
                trigger.TriggerResponse = triggerresponse;
                trigger.Active = Convert.ToBoolean(triggeractive);
                trigger.ModCanTrigger = Convert.ToBoolean(modscantrigger);
                trigger.SubCanTrigger = Convert.ToBoolean(subscantrigger);
                trigger.ViewerCanTrigger = Convert.ToBoolean(viewercantrigger);
                trigger.TriggerType = triggertype;

                trigger.StreamerCanTrigger = true;

                ContextService.SaveTrigger(trigger, GetUsername());

                Clients.Caller.saveTrigger(new {data = "1", message = "Saved!", container = trigger});
            }
            catch (Exception e)
            {
                Clients.Caller.saveTrigger(new {data = "-1", message = e.Message});
            }
        }


        /// <summary>
        ///     TODO: Maybe async this method
        ///     Updates the client if Giveaways.cshtml is loaded in client
        /// </summary>
        /// <param name="giveaway"></param>
        public void UpdateGiveaway(Giveaway giveaway)
        {
            try
            {
                var tcc = GetClientContainer();
                Clients.Client(tcc.ConnectionId).refreshGiveaway(giveaway);
            }
            catch (Exception e)
            {
                ConsoleLog("Error on UpdateGiveaway(): " + e.Message);
            }
        }

        /// <summary>
        ///     TODO: Maybe async this method
        ///     Close giveaway
        /// </summary>
        /// <param name="id"></param>
        public void CloseGiveaway(string id)
        {
            try
            {
                var tcc = GetClientContainer();
                var giveaway = tcc.Giveaways.FirstOrDefault(g => g.Id == id);

                if (giveaway.EndsAt <= DateTime.Now)
                {
                    Clients.Caller.saveCallback(new {data = "1", message = "Already closed"});
                    return;
                }

                giveaway.EndsAt = DateTime.Now;

                GetClient().SendMessage(tcc.Channel,
                    "/me Giveaway \"" + giveaway.Prize + "\" - !" + giveaway.Trigger + " is now closed with " +
                    giveaway.Participants.Count + " participants.");

                Clients.Caller.saveCallback(new {data = "1", message = "Closed!"});
            }
            catch (Exception e)
            {
                ConsoleLog("Error on CloseGiveaway(): " + e);
            }
        }


        /// <summary>
        ///     Draws a winner from the participants in a giveaway
        /// </summary>
        /// <param name="id"></param>
        public void DrawGiveaway(string id, bool postToChat)
        {
            try
            {
                var tcc = GetClientContainer();
                var giveaway = tcc.Giveaways.FirstOrDefault(g => g.Id == id);

                // Close giveaway if not already closed
                if (giveaway.EndsAt >= DateTime.Now) CloseGiveaway(giveaway.Id);

                // draw winner 
                var rnd = new SysRandom();
                var winnerNumber = rnd.Next(giveaway.Participants.Count);
                var winner = giveaway.Participants.ElementAt(winnerNumber);
                giveaway.Winners.Add(winner);
                giveaway.Participants.RemoveAt(winnerNumber);

                // update client with winner
                Clients.Client(tcc.ConnectionId).refreshGiveaway(giveaway);

                // Post winner to channel chat if selected
                if (postToChat)
                    GetClient().SendMessage(tcc.Channel,
                        "/me The winner of \"" + giveaway.Prize + "\" - !" + giveaway.Trigger + " is @" +
                        winner.TwitchUsername);
            }
            catch (Exception e)
            {
                ConsoleLog("Error on DrawGiveaway(): " + e);
            }
        }

        /// <summary>
        ///     Draws a winner from the participants in a giveaway
        /// </summary>
        /// <param name="id"></param>
        public void RedrawGiveaway(string id, bool postToChat)
        {
            try
            {
                var tcc = GetClientContainer();
                var giveaway = tcc.Giveaways.FirstOrDefault(g => g.Id == id);

                // redraw winner 
                var rnd = new SysRandom();
                var winnerNumber = rnd.Next(giveaway.Participants.Count);
                var winner = giveaway.Participants.ElementAt(winnerNumber);
                var firstWinner = giveaway.Winners.FirstOrDefault();
                giveaway.Winners.Remove(firstWinner);
                giveaway.Winners.Add(winner);
                giveaway.Participants.RemoveAt(winnerNumber);
                giveaway.Participants.Add(firstWinner);

                // update client with winner
                Clients.Client(tcc.ConnectionId).refreshGiveaway(giveaway);

                // Post winner to channel chat if selected
                if (postToChat)
                    GetClient().SendMessage(tcc.Channel,
                        "/me The redraw-winner of \"" + giveaway.Prize + "\" - !" + giveaway.Trigger + " is @" +
                        winner.TwitchUsername);
            }
            catch (Exception e)
            {
                ConsoleLog("Error on DrawGiveaway(): " + e);
            }
        }

        /// <summary>
        ///     Save new giveaway
        /// </summary>
        /// <param name="trigger"></param>
        /// <param name="prize"></param>
        /// <param name="timer"></param>
        /// <param name="sub"></param>
        /// <param name="viewer"></param>
        /// <param name="follower"></param>
        /// <param name="mod"></param>
        public void SaveNewGiveAway(string trigger, string prize, string timer, bool sub, bool follower, bool viewer,
            bool mod)
        {
            try
            {
                var giveAway = new Giveaway
                {
                    Trigger = trigger,
                    Prize = prize,
                    EndsAt = DateTime.Now.AddMinutes(Convert.ToDouble(timer)),
                    Sub = sub,
                    Viewer = viewer,
                    Follower = follower,
                    Mod = mod
                };

                GetClientContainer().Giveaways.Add(giveAway);

                Clients.Caller.loadGiveaway(giveAway);
            }
            catch (Exception e)
            {
                ConsoleLog("Error on SaveNewGiveAway(): " + e.Message);
            }
        }

        public void GetAllGiveaways()
        {
            try
            {
                var giveaways = GetClientContainer().Giveaways;

                giveaways.Reverse();
                Clients.Caller.loadAllGiveaways(giveaways);
            }
            catch (Exception e)
            {
                ConsoleLog("Error on GetAllGiveaways(): " + e.Message);
            }
        }

        /// <summary>
        ///     Deletes a trigger from database
        /// </summary>
        /// <param name="triggerid"></param>
        public void DeleteTrigger(string triggerid)
        {
            try
            {
                var triggerIdInt = Convert.ToInt32(triggerid);
                ContextService.DeleteTrigger(triggerIdInt, GetUsername());

                Clients.Caller.saveTrigger(new {data = "1", message = "Deleted!"});
            }
            catch (Exception e)
            {
                Clients.Caller.saveTrigger(new {data = "-1", message = e.Message});
            }
        }

        /// <summary>
        ///     Saves a timer to database
        /// </summary>
        /// <param name="timerid"></param>
        /// <param name="timername"></param>
        /// <param name="timertext"></param>
        /// <param name="timerinterval"></param>
        /// <param name="triggeractive"></param>
        public void SaveTimer(string timerid, string timername, string timertext, string timerinterval,
            string triggeractive)
        {
            try
            {
                var timer = new Timer();
                timer.Id = Convert.ToInt32(timerid);
                timer.TimerName = timername;
                timer.TimerResponse = timertext;
                timer.TimerInterval = Convert.ToInt32(timerinterval);
                timer.Active = Convert.ToBoolean(triggeractive);


                ContextService.SaveTimer(timer, GetUsername());

                Clients.Caller.saveTimer(new {data = "1", message = "Saved!", container = timer});
            }
            catch (Exception e)
            {
                Clients.Caller.saveTimer(new {data = "-1", message = e.Message});
            }
        }

        /// <summary>
        ///     Deletes a timer from database
        /// </summary>
        /// <param name="timerid"></param>
        public void DeleteTimer(string timerid)
        {
            try
            {
                var timerIdInt = Convert.ToInt32(timerid);
                ContextService.DeleteTimer(timerIdInt, GetUsername());

                Clients.Caller.saveTimer(new {data = "1", message = "Deleted!"});
            }
            catch (Exception e)
            {
                Clients.Caller.saveTimer(new {data = "-1", message = e.Message});
            }
        }

        /// <summary>
        ///     Save loyalty currency for bot
        /// </summary>
        /// <param name="enabled"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="interval"></param>
        public void SaveLoyalty(bool enabled, string name, string value, string interval)
        {
            try
            {
                var username = ContextService.GetUser(GetUsername());

                var bcs = ContextService.GetBotChannelSettings(username);

                if (bcs.Loyalty == null)
                {
                    bcs.Loyalty = new Loyalty();
                    bcs.Loyalty.LoyaltyName = name;
                    bcs.Loyalty.LoyaltyValue = Convert.ToInt32(value);
                    bcs.Loyalty.LoyaltyInterval = Convert.ToInt32(interval);
                    bcs.Loyalty.Track = enabled;
                }
                else
                {
                    var loyalty = bcs.Loyalty;

                    loyalty.LoyaltyName = name;
                    loyalty.LoyaltyValue = Convert.ToInt32(value);
                    loyalty.LoyaltyInterval = Convert.ToInt32(interval);
                    loyalty.Track = enabled;
                }

                ContextService.Context.SaveChanges();

                Clients.Caller.SaveCallback(new {message = "Saved", data = "1"});
            }
            catch (Exception e)
            {
                Clients.Caller.SaveCallback(new {message = e.Message, data = "-1"});
            }
        }

        /// <summary>
        ///     Save banned words list
        /// </summary>
        /// <param name="words">csv list of words</param>
        /// <param name="channel">channel name</param>
        public void SaveBannedWords(string words)
        {
            try
            {
                var username = ContextService.GetUser(GetUsername());

                var bcs = ContextService.GetBotChannelSettings(username);

                if (bcs.BannedWords == null)
                {
                    bcs.BannedWords = new List<BannedWord>();
                }
                else
                {
                    var dbWords = bcs.BannedWords.ToList();

                    foreach (var bannedWord in dbWords) ContextService.Context.BannedWords.Remove(bannedWord);
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

                Clients.Caller.SavedBannedWords(new {message = "Saved", data = "1"});
                Clients.Caller.ConsoleLog(DateTime.Now.ToString("HH:mm:ss") + " - " +
                                          "Saved banned words list");
            }
            catch (Exception e)
            {
                Clients.Caller.SavedBannedWords(new {message = "Error: " + e.Message, data = "-1"});
            }
        }

        // Sets the flag modsCanControlPlaylist in the client.
        public void UpdateModsCanControlPlaylist(bool modsCanControlPlaylist)
        {
            try
            {
                GetClientContainer().ModsControlSongrequest = modsCanControlPlaylist;

                Clients.Caller.Notify(new {data = "1", message = "Saved"});
            }
            catch (Exception e)
            {
                Clients.Caller.Notify(new {data = "-1", message = e.Message});
            }
        }

        /// <summary>
        ///     Gets the default banned words list and calls the client with the list
        /// </summary>
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
        ///     Gets the bot connection status to channel and maintains the workerthread livability
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="channel"></param>
        public void BotStatus(string username, string password, string channel)
        {
            try
            {
                //ConsoleLog("Checking bot connection status...");
                var cc = GetClientContainer();
                if (cc.Client != null)
                {
                    var bs = new BotStatusVM
                    {
                        info = cc.Client.IsConnected ? "Bot connected" : "Bot disconnected",
                        message = "",
                        warning = "",
                        connected = cc.Client.IsConnected
                    };
                    //ConsoleLog("Bot is " + bs.info);
                    Clients.Caller.BotStatus(bs);
                }
                else
                {
                    var bs = new BotStatusVM
                    {
                        info = "Bot disconnected",
                        message = "",
                        warning = "",
                        connected = false
                    };
                    //ConsoleLog("Bot is " + bs.info);
                    Clients.Caller.BotStatus(bs);
                }
            }
            catch (Exception e)
            {
                ConsoleLog("BotStatus error: " + e.Message);
            }
        }

        public async Task ReconnectBot(string username, string password, string channel)
        {
            try
            {
                GetClientContainer().LogOutInProgress = false;
                ConnectBot(username, password, channel);
            }
            catch (Exception e)
            {
                ConsoleLog("ReConnectBot error: " + e.Message);
            }
        }

        /// <summary>
        ///     Client sent connect to channel
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="channel"></param>
        public async Task ConnectBot(string username, string password, string channel)
        {
            try
            {
                if (GetClientContainer().LogOutInProgress) return;

                var client = GetClient();
                if (client != null)
                {
                    if (client.IsConnected)
                    {
                        ConsoleLog("Already connected...");
                        ConnectionId = Context.ConnectionId;
                        var bs = new BotStatusVM
                        {
                            info = GetClientContainer().Client.IsConnected ? "Bot connected" : "Bot disconnected",
                            message = "",
                            warning = "",
                            connected = GetClientContainer().Client.IsConnected
                        };

                        Clients.Caller.BotStatus(bs);
                    }
                    else
                    {
                        ConnectionId = Context.ConnectionId;
                        var ccontainer = GetClientContainer();
                        ccontainer.Client.Reconnect();

                        if (!HasRunningThread())
                        {
                            var arg = new WorkerThreadArg
                            {
                                Channel = channel,
                                Username = GetUsername(),
                                Client = ccontainer.Client
                            };

                            var parameterizedThreadStart = new ParameterizedThreadStart(TrackLoyaltyAndTimers);
                            var newThread = new Thread(parameterizedThreadStart) { IsBackground = true };
                            newThread.Name = GetUsername();
                            AddThread(newThread);
                            newThread.Start(arg);
                        }

                        //ccontainer.Client.Initialize()
                        ccontainer.Client.Connect();
                        ConsoleLog("Reconnecting to channel " + channel);

                        var bs = new BotStatusVM
                        {
                            info = GetClientContainer().Client.IsConnected ? "Bot connected" : "Bot disconnected",
                            message = "",
                            warning = "",
                            connected = GetClientContainer().Client.IsConnected
                        };

                        Clients.Caller.BotStatus(bs);
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
                        string.IsNullOrWhiteSpace(channel))
                        throw new Exception("No user/pass/channel given");

                    Username = username;
                    Password = password;
                    Channel = channel;
                    ConnectionId = Context.ConnectionId;
                    ConnCred = new ConnectionCredentials(Username, Password);
                    var clientContainer = GetClientContainer();
                    clientContainer.Client = new TwitchClient();

                    clientContainer.Client.Initialize(ConnCred, Channel);

                    clientContainer.Client.OnLog += ConsoleLog;
                    clientContainer.Client.OnConnectionError += ConsoleLogConnectionError;

                    clientContainer.Client.AutoReListenOnException = true;
                    clientContainer.Client.OnBeingHosted += OnBeeingHosted;
                    clientContainer.Client.OnRaidNotification += OnBeeingRaided;


                    clientContainer.Client.OnConnected += OnConnectToChannel;
                    clientContainer.Client.OnJoinedChannel += OnJoinedChannel;
                    clientContainer.Client.OnDisconnected += OnDisconnectReconnect;

                    clientContainer.Client.OnModeratorsReceived += ChannelModerators;
                    clientContainer.Client.OnMessageReceived += ChatMessageCheck;
                    clientContainer.Client.OnChatCommandReceived += ChatMessageTriggerCheck;

                    clientContainer.Client.OnWhisperReceived += RelayToChatMessage;

                    clientContainer.Client.OverrideBeingHostedCheck = false;


                    clientContainer.Client.Connect();
                    ConsoleLog("Connecting to channel " + channel);

                    var arg = new WorkerThreadArg
                    {
                        Channel = Channel,
                        Username = GetUsername(),
                        Client = clientContainer.Client
                    };
                    var bs = new BotStatusVM
                    {
                        info = GetClientContainer().Client.IsConnected ? "Bot connected" : "Bot disconnected",
                        message = "",
                        warning = "",
                        connected = GetClientContainer().Client.IsConnected
                    };
                    Clients.Caller.BotStatus(bs);


                    if (!HasRunningThread())
                    {
                        var parameterizedThreadStart = new ParameterizedThreadStart(TrackLoyaltyAndTimers);
                        clientContainer.WorkerThread = new Thread(parameterizedThreadStart) {IsBackground = true};
                        clientContainer.WorkerThread.Name = GetUsername();
                        clientContainer.WorkerThread.Start(arg);
                    }
                    
                    
                }


                GetClientContainer().Client.WillReplaceEmotes = true;


                var botStatus = new BotStatusVM
                {
                    info = GetClientContainer().Client.IsConnected ? "Bot connected" : "Bot disconnected",
                    message = "",
                    warning = "",
                    connected = GetClientContainer().Client.IsConnected
                };
                Clients.Caller.BotStatus(botStatus);
                var bcs = GetClientContainer();
                if (bcs.SongRequests != null)
                    foreach (var song in bcs.SongRequests)
                        UpdatePlaylistFromCommand(song.Url, song.Title, song.RequestedBy, song.VideoId, song.Duration);
            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
            }
        }

        private void OnDisconnectReconnect(object sender, OnDisconnectedArgs e)
        {
        }

        /// <summary>
        ///     OnBeeingRaided event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnBeeingRaided(object sender, OnRaidNotificationArgs e)
        {
            var ccontainer = GetClientContainer();
            var bcs = ContextService.GetBotChannelSettings(ccontainer.User);
            var hoster = e.RaidNotificaiton.DisplayName;
            var number = e.RaidNotificaiton.MsgParamViewerCount;

            ccontainer.Client.SendMessage(Channel, "/me @" + hoster + " is raiding with a party of " + number + "!");
        }

        /// <summary>
        ///     OnConnectToChannel event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnConnectToChannel(object sender, OnConnectedArgs e)
        {
            try
            {
                var ccontainer = GetClientContainer();

                ccontainer.Client.SendMessage(Channel, "/me connected. - YTBot by @Borge_Jakobsen ");
                var botStatus = new BotStatusVM
                {
                    info = ccontainer.Client.IsConnected ? "Bot connected" : "Bot disconnected",
                    message = "",
                    warning = "",
                    connected = !ccontainer.Client.IsConnected
                };

                Clients.Caller.BotStatus(botStatus);
            }
            catch (Exception)
            {
                // Catch premature connection
            }
            
        }

        /// <summary>
        ///     OnJoinedChannel event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            ConsoleLog("Connected to channel " + Channel);
        }


        /// <summary>
        ///     Beeing hosted event, add bonus to hoster. Amount depending on how many viewers are brought along
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnBeeingHosted(object sender, OnBeingHostedArgs e)
        {
            var ccontainer = GetClientContainer();
            var bcs = ContextService.GetBotChannelSettings(ccontainer.User);
            var hoster = e.BeingHostedNotification.HostedByChannel;

            var viewers = e.BeingHostedNotification.Viewers;
            var bonusViewerLoot = 0;
            ccontainer.Client.SendMessage(Channel, "/me @" + hoster + " just brought " + viewers +
                                                   " viewers to the party! Welcome!");


            if (bcs.Loyalty.Track == false)
                return;

            #region bonus loyalty if tracking loyalty

            if (viewers < 5)
                bonusViewerLoot = 100;
            else if (viewers >= 5 && viewers < 10)
                bonusViewerLoot = 250;
            else if (viewers >= 10 && viewers < 20)
                bonusViewerLoot = 500;
            else if (viewers >= 20) bonusViewerLoot = 750;

            var hosterLoyalty = ContextService.GetLoyaltyForUser("", Channel, null, hoster);

            ContextService.AddLoyalty(ccontainer.User, Channel, hosterLoyalty, bonusViewerLoot);

            var hosterLoyaltyAfter = ContextService.GetLoyaltyForUser("", Channel, null, hoster);

            ccontainer.Client.SendMessage(Channel, "/me @" + hoster + " received " + bonusViewerLoot + " " +
                                                   bcs.Loyalty.LoyaltyName + " for the host and now has " +
                                                   hosterLoyaltyAfter.CurrentPoints + " " + bcs.Loyalty.LoyaltyName);

            #endregion
        }

        /// <summary>
        ///     Relays message to channel if whisper user is moderator or broadcaster
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RelayToChatMessage(object sender, OnWhisperReceivedArgs e)
        {
            try
            {
                var ccontainer = GetClientContainer();
                if (ccontainer.Channelmods.Contains(e.WhisperMessage.Username.ToLower()) ||
                    e.WhisperMessage.DisplayName.ToLower() == Channel.ToLower())
                    GetClient().SendMessage(Channel, e.WhisperMessage.Message);
            }
            catch (Exception exception)
            {
            }
        }

        /// <summary>
        ///     Gets current stream meta information. Uptime, title, game, delay, mature and online status.
        /// </summary>
        /// <returns>Call to Caller.SetStreamInfo</returns>
        public async Task GetStreamInfo()
        {
            try
            {
                await InitializeAPI();
                var channel = await Api.Channels.v5.GetChannelAsync(ChannelToken);
                var uptime = await Api.Streams.v5.BroadcasterOnlineAsync(channel.Id);
                var stream = await Api.Streams.v5.GetStreamByUserAsync(channel.Id);

                var delay = 0;
                if (stream == null) delay = stream.Stream.Delay;

                var streamStatus = new StreamStatusVM
                {
                    Channel = channel.DisplayName,
                    Game = channel.Game,
                    Title = channel.Status,
                    Mature = channel.Mature,
                    Delay = delay,
                    Online = uptime
                };


                Clients.Caller.SetStreamInfo(streamStatus);
                ConsoleLog("Retrieved stream title and game");
            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
            }
        }

        /// <summary>
        ///     Updates the channel info
        /// </summary>
        /// <param name="title">Title of the stream</param>
        /// <param name="game">Current playing game/category</param>
        /// <param name="channel">Channel to update</param>
        public async Task SaveStreamInfo(string title, string game, string channel, string mature, string delay)
        {
            try
            {
                var clientSecret = ConfigurationManager.AppSettings["clientSecret"];
                await InitializeAPI();
                Api.Settings.AccessToken = clientSecret;

                var channelId = await Api.Channels.v5.GetChannelAsync(ChannelToken);

                if (string.IsNullOrWhiteSpace(delay)) delay = "0";
                var user = ContextService.GetUser(GetUsername());
                BotUserSettings = ContextService.GetBotUserSettingsForUser(user);
                ChannelToken = BotUserSettings.ChannelToken;
                Channel = BotUserSettings.BotChannel;

                await Api.Channels.v5.UpdateChannelAsync(channelId.Id, title, game, delay, null, ChannelToken);

                var retval = new {data = "1", message = "Saved"};
                Clients.Caller.SaveCallback(retval);
            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
                var retval = new {data = "-1", message = e.Message};
                Clients.Caller.SaveCallback(retval);
            }
        }

        public async Task GetConsoleLog()
        {
            try
            {
                var cc = GetClientContainer();

                Clients.Caller.loadConsoleLog(cc.ConsoleLog);
            }
            catch (Exception e)
            {
            }
        }

        /// <summary>
        ///     Get Chat Preferences #TODO
        /// </summary>
        /// <returns></returns>
        public async Task GetChatOptions()
        {
            try
            {
                await InitializeAPI();

                var channel = await Api.Channels.v5.GetChannelAsync(ChannelToken);

                var chatOptions = channel;
                var retval = new {data = "1", message = "", container = chatOptions};
                Clients.Caller.SetChatOptions(retval);
            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
                var retval = new {data = "-1", message = e.Message};
                Clients.Caller.Fail(retval);
            }
        }

        public async Task GetChatLimitations()
        {
            try
            {
                var tcc = GetClientContainer();
                var channelChatOptions = await Api.Undocumented.GetChatPropertiesAsync(tcc.Channel);

                Clients.Caller.LoadChatLimitations(channelChatOptions, tcc.LinksInChatAllowed);
            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
            }
        }

        /// <summary>
        ///     Sends the default banned words list to client
        /// </summary>
        /// <returns></returns>
        public async Task GetDefaultBannedWords()
        {
            try
            {
                var words = ConfigurationManager.AppSettings["bannedWords"];
                Clients.Caller.SetDefaultBannedWords(words);
            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
                var retval = new {data = "1", message = e.Message, container = ""};

                Clients.Caller.Fail(retval);
            }
        }

        /// <summary>
        ///     Reconnect bot to channel,
        /// </summary>
        /// <param name="username">Username</param>
        /// <param name="password">OAuth</param>
        /// <param name="channel">Channelname</param>
        public async Task Reconnect(string username, string password, string channel)
        {
            try
            {
                var ccontainer = GetClientContainer();
                if (ccontainer.Client.IsConnected)
                {
                    ccontainer.Client.Disconnect();
                    ConsoleLog("Disconnecting channel " + Channel);
                    ConsoleLog("Reconnecting to channel " + Channel);
                    Thread.Sleep(1000);

                    await ConnectBot(username, password, channel);
                }
                else
                {
                    ConsoleLog("Reconnecting to channel " + Channel);
                    Thread.Sleep(1000);

                    await ConnectBot(username, password, channel);
                }

                var bs = new BotStatusVM
                {
                    info = GetClientContainer().Client.IsConnected ? "Bot connected" : "Bot disconnected",
                    message = "",
                    warning = "",
                    connected = GetClientContainer().Client.IsConnected
                };
                Clients.Caller.BotStatus(bs);
            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
            }
        }

        /// <summary>
        ///     OnChannelModeratorsReceived event, get all mods in channel.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ChannelModerators(object sender, OnModeratorsReceivedArgs e)
        {
            var mods = new List<string>();
            foreach (var moderator in e.Moderators)
            {
                var mod = moderator.ToLower();
                mods.Add(mod);
                GetClientContainer().Channelmods.Add(mod);
            }

            var botIsMod = mods.Contains(Username.ToLower());

            var botStatus = new BotStatusVM
            {
                info = GetClientContainer().Client.IsConnected ? "Bot connected" : "Bot disconnected",
                message = "",
                warning = botIsMod == false ? "Bot is not moderator in channel" : "",
                connected = GetClientContainer().Client.IsConnected
            };

            Clients.Caller.BotStatus(botStatus);
        }

        /// <summary>
        ///     Disconnect bot from channel
        /// </summary>
        public void DisconnectBot()
        {
            try
            {
                var container = GetClientContainer();

                // DisconnectBot only on logout from application, set value so bot is not trying to reconnect
                container.LogOutInProgress = true;

                container.Client.Disconnect();
                if (container.WorkerThread != null) container.WorkerThread.Abort();

                ConsoleLog("Disconnecting channel " + Channel);

                var botStatus = new BotStatusVM
                {
                    info = container.Client.IsConnected ? "Bot connected" : "Bot disconnected",
                    message = "",
                    warning = "",
                    connected = !container.Client.IsConnected
                };

                Clients.Caller.BotStatus(botStatus);
                Clients.Caller.onBotDisconnected(botStatus);
                DisposeSession();
            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
            }
        }

        public void DisposeSession()
        {
            GetClientContainer().Client = null;
            GetClientContainer().PubSubClient = null;
            GetClientContainer().WorkerThread.Abort();
            GetClientContainer().WorkerThread = null;
            ClientContainers = null;
        }

        /// <summary>
        ///     Check if Client is still connected
        /// </summary>
        public void IsConnected()
        {
            if (GetClientContainer().Client.IsConnected)
                Clients.Caller.ConsoleLog(DateTime.Now.ToString("HH:mm:ss") + " - " +
                                          "Bot is still connected!");
            else
                Clients.Caller.ConsoleLog(DateTime.Now.ToString("HH:mm:ss") + " - " +
                                          "Bot is no longer connected!");
        }

        /// <summary>
        ///     Update channel topic and game
        /// </summary>
        /// <param name="topic">Topic of the channel</param>
        /// <param name="game">Current game</param>
        /// <returns></returns>
        public bool UpdateChannel(string topic, string game)
        {
            return Api.Channels.v5.UpdateChannelAsync(Channel, topic, game, null, null, Password).IsCompleted;
        }


        /// <summary>
        ///     Log to Client console
        /// </summary>
        /// <param name="msg"></param>
        public void ConsoleLog(string msg, bool debug = false)
        {
            try
            {
                var logEntry = DateTime.Now.ToString("HH:mm:ss") + " - " + msg;
                if (GetClientContainer() != null) GetClientContainer().ConsoleLog.Add(logEntry);
                Clients.Caller.ConsoleLog(DateTime.Now.ToString("HH:mm:ss") + " - " + msg);
            }
            catch (Exception e)
            {
            }
        }

        public void ConsoleLog(object sender, OnLogArgs e)
        {
            var logEntry = e.DateTime.ToString("HH:mm:ss") + " - " + e.Data;
            if (GetClientContainer() != null) GetClientContainer().ConsoleLog.Add(logEntry);

            Clients.Caller.ConsoleLog(logEntry);
        }

        public void ConsoleLogConnectionError(object sender, OnConnectionErrorArgs e)
        {
            Clients.Caller.ConsoleLog(DateTime.Now.ToString("HH:mm:ss") + " - " + e.Error.Message);
        }

        /// <summary>
        ///     Call this to update top commands and top chatters
        /// </summary>
        public void UpdateChattersAndCommands()
        {
            try
            {
                InitializeAPI();
                var ccontainer = GetClientContainer();

                var topCommands = ccontainer.CommandsUsed.OrderByDescending(k => k.Value).Take(NUMTOPCOMMANDS);
                var topChatters = ccontainer.ChattersCount.OrderByDescending(k => k.Value).Take(NUMTOPCHATTERS);

                var retval = new {topcommands = topCommands, topchatters = topChatters};
                Clients.Caller.ChattersAndCommands(retval);
            }
            catch (Exception e)
            {
                ConsoleLog("Error on UpdateChattersAndCommands(): " + e.Message);
            }
        }

        /// <summary>
        ///     Call this to update client
        /// </summary>
        public void UpdateViewerCount()
        {
            try
            {
                InitializeAPI();
                var viewers = GetNumViewers();

                Clients.Caller.UpdateViewers(viewers);
            }
            catch (Exception e)
            {
                ConsoleLog("Error on UpdateViewerCount(): " + e.Message);
            }
        }

        /// <summary>
        ///     Returns number of viewers in stream
        /// </summary>
        /// <returns></returns>
        private string GetNumViewers()
        {
            try
            {
                var user = Api.Users.v5.GetUserByNameAsync(Channel).Result;
                var stream = Api.Streams.v5.GetStreamByUserAsync(user.Matches[0].Id).Result;

                var numViewers = stream.Stream.Viewers;

                return numViewers.ToString();
            }
            catch (Exception e)
            {
                return "offline";
            }
        }

        /// <summary>
        ///     Poll this to send songrequests to client
        /// </summary>
        public void PollPlaylist()
        {
            try
            {
                var username = ContextService.GetUser(GetUsername());
                var bcs = ContextService.GetBotChannelSettings(username);
                //var client = GetClientContainer();

                foreach (var video in bcs.SongRequests) Clients.Caller.UpdatePlaylist(video);
            }
            catch (Exception e)
            {
            }
        }

        /// <summary>
        ///     Poll this to send kill stats to client
        /// </summary>
        public void PollKillStats()
        {
            try
            {
                var username = ContextService.GetUser(GetUsername());
                var bcs = ContextService.GetBotChannelSettings(username);
                
                

                Clients.Caller.UpdateKillStats(GetClientContainer().KillStats);
            }
            catch (Exception e)
            {
                Clients.Caller.Notify(new { data = "-1", message = e.Message });
            }
        }

        /// <summary>
        ///     Deletes song from database of users songrequest
        /// </summary>
        /// <param name="id"></param>
        public void DeleteSong(string id)
        {
            try
            {
                var user = ContextService.GetUser(GetUsername());

                ContextService.DeleteSongRequest(user, id);
                var t = new {id};
                var retval = new StatusMessageVM
                {
                    message = "Deleted song with id: " + id,
                    data = 1,
                    obj = t
                };
                Clients.Caller.deleteSongAck(retval);
            }
            catch (Exception e)
            {
                var retval = new StatusMessageVM
                {
                    message = "Error DeleteSong(): " + e.Message,
                    data = -1
                };
                Clients.Caller.deleteSongAck(retval);
            }
        }

        /// <summary>
        ///     Adds song to users list, poll this list to get added songs
        /// </summary>
        /// <param name="url"></param>
        /// <param name="title"></param>
        /// <param name="user"></param>
        /// <param name="videoId"></param>
        /// <param name="duration"></param>
        /// <returns>PlayListItem</returns>
        public PlayListItem UpdatePlaylistFromCommand(string url, string title, string user, string videoId, TimeSpan? duration)
        {
            try
            {
                var obj = new {title, url, user, duration, videoid = videoId};
                var container = GetClientContainer();

                var usr = ContextService.GetUser(GetUsername());

                var item = new PlayListItem();
                item.VideoId = videoId;
                item.Title = Regex.Unescape(title);
                item.Deleted = false;
                item.RequestDate = DateTime.Now;
                item.RequestedBy = user;
                item.Url = url;

                if (duration != null)
                {
                    item.Duration = duration;
                }


                container.SongRequests.Add(item);

                ContextService.SaveSongRequest(usr, item);
                return item;
            }
            catch (Exception e)
            {
            }

            return null;
        }

        /// <summary>
        ///     Removes video from songrequests queue by youtube id
        /// </summary>
        /// <param name="id"></param>
        public void DeletePlaylistItem(string id)
        {
            try
            {
                var container = GetClientContainer();

                var video = container.SongRequests.FirstOrDefault(v => v.VideoId == id);
                container.SongRequests.Remove(video);

                Clients.Caller.Notify(new {data = "1", message = "Removed video with id: " + id});
            }
            catch (Exception e)
            {
                Clients.Caller.Notify(new {data = "-1", message = e.Message});
            }
        }

        /// <summary>
        ///     Send chat message to Client
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ChatScanForBadWords(ChatMessage e)
        {
            var username = ContextService.GetUser(GetUsername());
            var bcs = ContextService.GetBotChannelSettings(username);
            if (bcs.BannedWords != null && bcs.BannedWords.Count > 0)
            {
                // Check for banned words in chat message
                var wordsInMessage = e.Message.ToLower().Split(' ');
                foreach (var word in wordsInMessage)
                    if (bcs.BannedWords.Any(b => b.Word.ToLower() == word))
                    {
                        var client = GetClient();
                        var timeout = new TimeSpan(0, 0, 1, 0);
                        var message = "Those words are not allowed in this chat! Timed out for  " +
                                      Convert.ToString(timeout.Minutes) + " minutes";
                        var joinedChannel = client.GetJoinedChannel(Channel);
                        client.TimeoutUser(joinedChannel, e.DisplayName.ToLower(), timeout, message);
                    }
            }


            // Add to chat log
            AddToChatLog(e);
        }

        public void SetChatLinksAllowed(bool hidelinks)
        {
            try
            {
                GetClientContainer().LinksInChatAllowed = !hidelinks;

                Clients.Caller.Notify(new {data = "1", message = "Saved"});
            }
            catch (Exception e)
            {
                ConsoleLog("Error on SetChatLinksAllowed():" + e.Message);
            }
        }

        public void Play()
        {
            Clients.Client(GetClientContainer().ConnectionId).playSong();
        }

        public void Stop()
        {
            Clients.Client(GetClientContainer().ConnectionId).stopSong();
        }

        public void Pause()
        {
            Clients.Client(GetClientContainer().ConnectionId).pauseSong();
        }

        public void PrevSong()
        {
            Clients.Client(GetClientContainer().ConnectionId).PrevSong();
        }

        public void NextSong()
        {
            Clients.Client(GetClientContainer().ConnectionId).NextSong();
        }

        public void Volume(int volume)
        {
            Clients.Client(GetClientContainer().ConnectionId).Volume(volume);
        }

        /// <summary>
        ///     Get Title of youtube video url
        /// </summary>
        /// <param name="url"></param>
        /// <param name="videoId"></param>
        /// <returns>Title</returns>
        public async Task<VideoVm> GetVideoInfoByHttp(string url, string videoId)
        {
            var video = new VideoVm()
            {
                Id = videoId,
                Url = url,
                Title = "N/A"
            };
            var web = new HtmlWeb();
            if (url == null)
            {
                video.Url = "https://www.youtube.com/watch?v=" + videoId;
            }

            // no video given
            if (video.Id == null)
            {
                return null;
            }

            try
            {
                var client = new YoutubeClient();
                var videoMetaData = await client.GetVideoAsync(video.Id);
                video.Title = videoMetaData.Title;
                video.Length = videoMetaData.Duration;
            }
            catch (Exception e)
            {
                ConsoleLog("Error on GetVideoInfoByHttp: " + e.Message);
            }

            return video;
        }


        /// <summary>
        ///     Gets the current TwitchClient for the current web-connection
        /// </summary>
        /// <returns></returns>
        private TwitchClient GetClient()
        {
            return ClientContainers.FirstOrDefault(t => t.Id == GetUsername()).Client;
        }

        /// <summary>
        ///     Get username of logged in user
        /// </summary>
        /// <returns>Username as string</returns>
        private string GetUsername()
        {
            string username = null;
            try
            {
                username = Context.User.Identity.Name;
            }
            catch (Exception e)
            {
                username = HttpContext.Current.User.Identity.Name;
            }

            return username;
        }

        /// <summary>
        ///     Gets the current TwitchClientContainer for the current logged in user
        /// </summary>
        /// <returns>TwitchClientContainer</returns>
        private TwitchClientContainer GetClientContainer()
        {
            return ClientContainers.FirstOrDefault(t => t.Id == GetUsername());
        }


        /// <summary>
        ///     Worker thread that runs Loyalty collecting, triggers Timers and other timer based tasks
        /// </summary>
        /// <param name="arg"></param>
        private async void TrackLoyaltyAndTimers(object arg)
        {
            ContextService = new ContextService();

            var wtarg = (WorkerThreadArg) arg;
            var User = ContextService.GetUser(wtarg.Username);

            wtarg.Client = GetClientContainer().Client;

            ContextService.TimersResetLastRun(User.UserName);

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var lastLoyaltyElapsedMinutes = stopWatch.Elapsed.Minutes;

            while (true)
                // Update context
                using (var ContextService = new ContextService())
                {
                    while (wtarg.Client == null)
                    {
                        Thread.Sleep(5000);
                        wtarg.Client = GetClientContainer().Client;
                    }

                    while (wtarg.Client != null && wtarg.Client.IsConnected == false)
                    {
                        Thread.Sleep(5000);
                        try
                        {

                        }
                        catch (NullReferenceException e)
                        {
                            //Client logged out
                            Thread.CurrentThread.Abort();
                        }
                        wtarg.Client = GetClientContainer().Client;
                    }

                    // Thread variables
                    // Update database connector
                    try
                    {
                        await InitializeAPI();
                        var botChannelSettings = ContextService.GetBotChannelSettings(User);

                        if (botChannelSettings != null)
                        {
                            // Loyalty only if channel is online
                            var channel = Api.Channels.v5.GetChannelAsync(ChannelToken).Result;
                            var uptime = Api.Streams.v5.GetUptimeAsync(channel.Id);


#if DEBUG
                            if (botChannelSettings != null && botChannelSettings.Loyalty != null &&
                                botChannelSettings.Loyalty.Track == true)
                            {
#else
                            if (botChannelSettings != null && botChannelSettings.Loyalty != null &&
                                botChannelSettings.Loyalty.Track == true && uptime.Result != null)
                            {
#endif
                                if (stopWatch.Elapsed.Minutes % botChannelSettings.Loyalty.LoyaltyInterval == 0 &&
                                    lastLoyaltyElapsedMinutes != stopWatch.Elapsed.Minutes)
                                {
                                    var uname = ContextService.GetUser(GetUsername());
                                    var bs = ContextService.GetBotUserSettingsForUser(uname);

                                    var chattersInChannel = await Api.Undocumented.GetChattersAsync(Channel.ToLower());

                                    var streamUsers = new List<StreamViewer>();

                                    foreach (var onlineUser in chattersInChannel)
                                    {
                                        var dbUser = ContextService.Context.Viewers.FirstOrDefault(u =>
                                            u.TwitchUsername.ToLower().Equals(onlineUser.Username.ToLower()) &&
                                            u.Channel.ToLower() == bs.BotChannel.ToLower());
                                        if (dbUser == null)
                                        {
                                            var newUser = new StreamViewer
                                            {
                                                Channel = Channel,
                                                TwitchUsername = onlineUser.Username
                                            };

                                            streamUsers.Add(newUser);
                                        }
                                        else
                                        {
                                            streamUsers.Add(dbUser);
                                        }
                                    }

                                    ContextService.AddLoyalty(User, Channel, streamUsers);

                                    lastLoyaltyElapsedMinutes = stopWatch.Elapsed.Minutes;
                                }

                                CheckForRussianRoulette();
                            }


                            // Timers
                            var threadId = Thread.CurrentThread.ManagedThreadId;
                            foreach (var timer in botChannelSettings.Timers)
                                if (timer.TimerLastRun != null && timer.Active.HasValue && timer.Active.Value &&
                                    Convert.ToDateTime(timer.TimerLastRun.Value.AddMinutes(timer.TimerInterval)) <=
                                    DateTime.Now)
                                    try
                                    {
                                        // update timer
                                        ContextService.TimerStampLastRun(timer.Id, User.UserName);
                                        // show message in chat
                                        wtarg.Client.SendMessage(wtarg.Channel,
                                            "/me " + timer.TimerResponse);

                                        Thread.Sleep(5000);
                                    }
                                    catch (Exception e)
                                    {
                                        ConsoleLog("Error on ThreadTimerRun: " + e.Message);
                                    }
                        }
                    }
                    catch (Exception e)
                    {
                    }

                    // chill for a while
                    Thread.Sleep(5000);
                }
        }


        private void CheckForRussianRoulette()
        {
            var client = GetClientContainer();

            if (client.RRulette != null && client.RRulette.Finished == false)
            {
                var user = ContextService.GetUser(GetUsername());

                var bcs = ContextService.GetBotChannelSettings(user);

                // alert one minute marker
                if (client.RRulette.StartOneMinReminderAlerted == false &&
                    client.RRulette.StartOneMinReminder < DateTime.Now)
                {
                    client.RRulette.StartOneMinReminderAlerted = true;


                    GetClient()
                        .SendMessage(Channel,
                            "/me " +
                            $"Russian roulette is starting in 1 minute, currently {client.RRulette.Players.Count} contestants are battling over {client.RRulette.TotalBet} {bcs.Loyalty.LoyaltyName}");
                }


                // alert ten seconds marker
                if (client.RRulette.StartTenSecReminderAlerted == false &&
                    client.RRulette.StartOneMinReminderAlerted &&
                    client.RRulette.StartTenSecReminder < DateTime.Now)
                {
                    client.RRulette.StartTenSecReminderAlerted = true;
                    GetClient()
                        .SendMessage(Channel,
                            "/me " +
                            $"Russian roulette is starting in 10 seconds, currently {client.RRulette.Players.Count} contestants are battling over {client.RRulette.TotalBet} {bcs.Loyalty.LoyaltyName}");
                }

                // start draw
                if (client.RRulette.StartAt < DateTime.Now) DrawRussianRoulette(bcs);
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
                ContextService.AddLoyalty(ContextService.GetUser(GetUsername()), Channel,
                    players.FirstOrDefault(), rroulette.TotalBet);
                rroulette.Finished = true;
                GetClient().SendMessage(Channel,
                    "/me " + $"The Russian roulette is cancelled, buy-in is returned to its owner");
                GetClientContainer().RRulette = null;
                return;
            }

            var randomPlayers = Randomize(players);

            var count = 0;
            var playersString = string.Join(",", players.Select(r => r.TwitchUsername));

            GetClient()
                .SendMessage(Channel,
                    "/me " + $"Lets get this party started, the Russian roulette has {players.Count} contestants!");


            while (randomPlayers.Count > 1)
            {
                var playerOut = randomPlayers[randomPlayers.Count - 1];
                randomPlayers.RemoveAt(randomPlayers.Count - 1);

                GetClientContainer().RRulette.DeadPlayers.Add(playerOut);

                GetClient()
                    .SendMessage(Channel,
                        "/me " + $"@{playerOut.TwitchUsername} is eliminated! Better luck next time... #theSaltIsReal");

                count++;

                Thread.Sleep(pauseBetweenEliminations);
            }

            var winningPlayer = randomPlayers[randomPlayers.Count - 1];
            randomPlayers.RemoveAt(randomPlayers.Count - 1);
            ContextService.AddLoyalty(ContextService.GetUser(GetUsername()), Channel, winningPlayer,
                rroulette.TotalBet);
            GetClientContainer().RRulette.Finished = true;
            GetClientContainer().RRulette.Winner = winningPlayer;
            GetClientContainer().RRulette = null;
            GetClient().SendMessage(Channel,
                "/me " +
                $"And the winner is.... @{winningPlayer.TwitchUsername}! B) The player walks away with {rroulette.TotalBet} {bcs.Loyalty.LoyaltyName}! GG");
            GetClientContainer().LastRussian = DateTime.Now;
        }

        public void LoadChatLog()
        {
            try
            {
                var tcc = GetClientContainer();
                foreach (var chatMessage in tcc.ChatLog) Clients.Caller.updateChatLog(chatMessage);
                Clients.Caller.toggleTableSorter();
            }
            catch (Exception e)
            {
                ConsoleLog("Error on LoacChatLog(): " + e.Message);
            }
        }

        /// <summary>
        ///     Get poll data from Strawpoll and call client with each poll
        /// </summary>
        public async Task LoadPolls()
        {
            try
            {
                var polls = GetClientContainer().Polls;
                polls.Reverse();

                foreach (var poll in polls)
                {
                    var getPoll = new StrawPoll();
                    var pollResult = await getPoll.GetPollAsync(poll);
                    Clients.Caller.LoadPoll(pollResult);
                }
            }
            catch (Exception e)
            {
                ConsoleLog("Error on LoadPolls(): " + e.Message);
            }
        }

        public async Task SaveNewPoll(string question, List<string> option)
        {
            try
            {
                var optionsList = new List<string>(option);
                await CreateStrawPoll(question, optionsList);

                await Task.Delay(2000);

                var addedPoll = GetClientContainer().Polls.Last();

                var getPoll = new StrawPoll();
                var pollResult = await getPoll.GetPollAsync(addedPoll);
                Clients.Caller.LoadPoll(pollResult);
            }
            catch (Exception e)
            {
                ConsoleLog("Error on SaveNewPoll(): " + e.Message);
            }
        }

        /// <summary>
        ///     Randomizes a list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        public static List<T> Randomize<T>(List<T> list)
        {
            var randomizedList = new List<T>();
            try
            {
                var rnd = new Random.Org.Random();
                while (list.Count > 0)
                {
                    var index = 0;
                    if (list.Count > 1) index = rnd.Next(0, list.Count - 1); //pick a random item from the master list

                    randomizedList.Add(list[index]); //place it at the end of the randomized list
                    list.RemoveAt(index);
                }
            }
            catch (Exception e)
            {
            }

            return randomizedList;
        }

        /// <summary>
        ///     Gets list of users in channel, looks up their twitch ID
        /// </summary>
        /// <param name="channel">Channel as string</param>
        /// <returns>list of StreamView</returns>
        public List<StreamViewer> GetUsersInChannel(string channel)
        {
            var users = new List<StreamViewer>();

            var streamUsers = Api.Undocumented.GetChattersAsync(channel).Result;

            foreach (var user in streamUsers)
            {

                var t = new StreamViewer();

                t.TwitchUsername = user.Username;

                if (users.All(u => u.TwitchUserId != t.TwitchUserId)) users.Add(t);
            }

            return users;
        }

        /// <summary>
        ///     Log chat messages to list
        /// </summary>
        /// <param name="username">string</param>
        /// <param name="msg">string</param>
        /// <returns>Chatlog as List of strings this session chat messages</returns>
        private async Task AddToChatLog(ChatMessage chatmessage)
        {
            var ccontainer = GetClientContainer();

            // +1 #topChatters
            AddToChatStat(chatmessage.Username, ccontainer);
            AddToChatLog(chatmessage, ccontainer);
        }

        /// <summary>
        ///     Add +1 chat message to chatter
        ///     Disregard streamer and bot
        /// </summary>
        /// <param name="username"></param>
        /// <returns>ChatterCount dictionary</returns>
        private static void AddToChatStat(string username, TwitchClientContainer tcc)
        {
            if (tcc.ChattersCount.ContainsKey(username))
                tcc.ChattersCount[username]++;
            else
                tcc.ChattersCount[username] = 1;
        }

        private static void AddToChatLog(ChatMessage msg, TwitchClientContainer tcc)
        {
            tcc.ChatLog?.Add(msg);
        }

        private Dictionary<string, int> AddToCommands(string command)
        {
            var ccontainer = GetClientContainer();

            if (ccontainer.CommandsUsed.ContainsKey(command.ToLower()))
                ccontainer.CommandsUsed[command.ToLower()] = ccontainer.CommandsUsed[command.ToLower()] + 1;
            else
                ccontainer.CommandsUsed[command.ToLower()] = 1;

            return ccontainer.CommandsUsed;
        }


        private async void Wait(int Seconds)
        {
            var Tthen = DateTime.Now;
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
            string[] vowels = {"a", "e", "i", "o", "u", "ae", "y"};
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
        ///     Create a strawpoll
        /// </summary>
        /// <param name="title">Title</param>
        /// <param name="options">List<string>()</param>
        private async Task CreateStrawPoll(string title, List<string> options)
        {
            // Establish the poll settins
            var pollTitle = title;
            var allOptions = options;
            var multipleChoice = true;

            // Create the poll
            var poll = new StrawPoll();
            var pollResult = await poll.CreatePollAsync(title, options, true, DupCheck.NORMAL, false);

            GetClientContainer().Polls.Add(pollResult.Id);

            // Show poll link
            GetClientContainer().Client.SendMessage(GetClientContainer().Channel,
                $"/me Poll created, vote for \"{pollTitle}\" here: {pollResult.PollUrl}");

            ConsoleLog("Created poll '" + title + "' => " + pollResult.PollUrl);
            Clients.Caller.CreatedPoll(title, pollResult.Id);
            Clients.Caller.CreatePoll(title, allOptions);
        }



        #region Trigger checking

        private void ChatMessageCheck(object sender, OnMessageReceivedArgs e)
        {
            ChatScanForBadWords(e.ChatMessage);
            ChatScanMessageContainsUrl(e.ChatMessage);
            GiveawayWinnerChatCheck(e.ChatMessage);
        }

        private async void ChatScanMessageContainsUrl(ChatMessage chatMsg)
        {
            if (chatMsg.IsBroadcaster || chatMsg.IsModerator)
                return;

            var tcc = GetClientContainer();
            if (tcc.LinksInChatAllowed == true)
                return;

            var message = chatMsg.Message;
            if(message.StartsWith("!sr "))
                return;
            else if (message.StartsWith("https://clips.twitch.tv") || message.StartsWith("http://clips.twitch.tv"))
                return;
            
            Regex r = new Regex(@"(http|ftp|https)://([\w_-]+(?:(?:\.[\w_-]+)+))([\w.,@?^=%&:/~+#-]*[\w@?^=%&/~+#-])?");

            if (!r.IsMatch(message)) return;
            if (HasLinkPermission(chatMsg, null))
            {
                return;
            }

            var joinedChannel = tcc.Channel;

            var timeoutMessage = "@" + chatMsg.Username +
                          " Links are not allowed in chat, ask permission to post links first!";
            tcc.Client.TimeoutUser(joinedChannel, chatMsg.Username, new TimeSpan(0, 0, 1, 0), timeoutMessage);
        }

        /// <summary>
        ///     Function triggered on onCommandReceived
        ///     Tasking off check in case other commands are received at the same time
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChatMessageTriggerCheck(object sender, OnChatCommandReceivedArgs e)
        {
            ChatCommandTriggerCheck(e.Command, e);
        }

        private void GiveawayWinnerChatCheck(ChatMessage commandChatMessage)
        {
            var tcc = GetClientContainer();

            if (tcc.Giveaways.Any(g => g.EndsAt < DateTime.Now && g.EndsAt <= DateTime.Now.AddMinutes(30) && 
                                       g.Winners
                                           .Any(w => w.TwitchUsername.ToLower()
                                               .Equals(commandChatMessage.DisplayName.ToLower()))))
            {
                var giveaway = tcc.Giveaways.LastOrDefault(g => g.EndsAt < DateTime.Now &&
                                                                g.Winners
                                                                    .Any(w => w.TwitchUsername.ToLower()
                                                                        .Equals(
                                                                            commandChatMessage.DisplayName.ToLower())));

                Clients.Client(GetClientContainer().ConnectionId).winnerChatAdd(giveaway, commandChatMessage);
            }
        }

        /// <summary>
        /// Check for chat triggers
        /// </summary>
        /// <param name="command">Chatcommand</param>
        /// <param name="arg"></param>
        /// <returns></returns>
        private async Task ChatCommandTriggerCheck(ChatCommand command, OnChatCommandReceivedArgs arg)
        {
            try
            {
                var tcc = GetClientContainer();
                TriggerService = new TriggerService(ContextService.GetUser(GetUsername()), tcc, this, Api);

                var giveaways = TriggerService.GiveAwayCheck(command);
                var triggers = TriggerService.TriggerCheck(command);
                var loyalty = TriggerService.LoyaltyCheck(command);
                var killstat = TriggerService.KillStatCheck(command);

                if (giveaways.Any()) TriggerService.Run(null, null, command);
                if (loyalty) TriggerService.Run(null, null, command);
                if (killstat) TriggerService.Run(null, null, command);
                var enumerable = triggers as Trigger[] ?? triggers.ToArray();
                if (enumerable.Any())
                {
                    var twitchUser = await Api.Users.v5.GetUserByNameAsync(command.ChatMessage.Username);
                    var channel = await Api.Users.v5.GetUserByNameAsync(tcc.Channel);
                    var isFollower = false;
                    try
                    {
                        await Api.Users.v5.CheckUserFollowsByChannelAsync(twitchUser.Matches[0].Id,
                            channel.Matches[0].Id);
                        isFollower = true;
                    }
                    // will throw exception on not following
                    catch (BadResourceException e)
                    {
                        isFollower = false;
                    }

                    var chatter = new StreamViewer
                    {
                        Channel = tcc.Channel,
                        Follower = isFollower,
                        Subscriber = command.ChatMessage.IsSubscriber,
                        TwitchUsername = command.ChatMessage.DisplayName,
                        Mod = command.ChatMessage.IsModerator,
                        
                    };

                    foreach (var trigger in enumerable)
                    {
                        if (trigger.CanTrigger(chatter, command))
                        {
                            TriggerService.Run(trigger, chatter, command);
                        }
                        else
                        {
                            var t = trigger;
                        }
                    }
                        
                            
                }
            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
            }
        }

        #endregion
    }
}