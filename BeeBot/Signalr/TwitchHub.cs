using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
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
using YTBot.Models;
using YTBot.Services;
using SysRandom = System.Random;

using TwitchLib.Api;
using TwitchLib.Api.Models.v5.Clips;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;
using TwitchLib.PubSub;
using YoutubeSearch;
using YTBot.Models.ViewModels;
using Timer = YTBot.Models.Timer;


namespace BeeBot.Signalr
{
    public class TwitchHub : Hub
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Channel { get; set; }
        public string ConnectionId { get; set; }
        private BotUserSettings BotUserSettings { get; set; }

        private ContextService ContextService { get; set; }

        public static List<TwitchClientContainer> ClientContainers { get; set; }

        private ConnectionCredentials ConnCred { get; set; }
        public static TwitchClient Client { get; set; }

        private static TwitchAPI Api { get; set; }

        private string ChannelToken { get; set; }

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

        /// <summary>
        /// OnConnected event, initializes TwitchApi
        /// </summary>
        /// <returns></returns>
        public override Task OnConnected()
        {
            InitializeAPI();

            if (ClientContainers.Any(c => c.Id == GetUsername()))
            {
                Client = GetClient();
            }
            else
            {
                var tcc = new TwitchClientContainer()
                {
                    Id = GetUsername(),
                    Channel = Channel
                };

                ClientContainers.Add(tcc);
            }

            var user = ContextService.GetUser(GetUsername());
            BotUserSettings = ContextService.GetBotUserSettingsForUser(user);
            GetClientContainer().ConnectionId = Context.ConnectionId;
            ConnectionId = Context.ConnectionId;

            return base.OnConnected();
        }

        /// <summary>
        /// Initialize TwitchAPI with clientId and clientSecret
        /// </summary>
        /// <returns></returns>
        private async Task InitializeAPI()
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
        /// Message playing song to channel chat
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

                ccontainer.Client.SendMessage(ccontainer.Channel, $"/me is now playing: {video.Title} - ( {video.Url} ) added by @{video.RequestedBy}");
            }
            catch (Exception exception)
            {
                ConsoleLog("Error on PlayingSong(): " + exception.Message);
            }
        }

        /// <summary>
        /// Saves a new trigger
        /// </summary>
        /// <param name="triggerid"></param>
        /// <param name="triggername"></param>
        /// <param name="triggerresponse"></param>
        /// <param name="modscantrigger"></param>
        /// <param name="subscantrigger"></param>
        /// <param name="viewercantrigger"></param>
        /// <param name="triggeractive"></param>
        public void SaveTrigger(string triggerid, string triggername, string triggerresponse, string modscantrigger,
            string subscantrigger, string viewercantrigger, string triggeractive)
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

                // depricated
                trigger.TriggerType = TriggerType.Message;
                trigger.StreamerCanTrigger = true;

                ContextService.SaveTrigger(trigger, GetUsername());

                Clients.Caller.saveTrigger(new { data = "1", message = "Saved!", container = trigger });
            }
            catch (Exception e)
            {
                Clients.Caller.saveTrigger(new { data = "-1", message = e.Message });
            }
        }

        /// <summary>
        /// Deletes a trigger from database
        /// </summary>
        /// <param name="triggerid"></param>
        public void DeleteTrigger(string triggerid)
        {
            try
            {
                var triggerIdInt = Convert.ToInt32(triggerid);
                ContextService.DeleteTrigger(triggerIdInt, GetUsername());

                Clients.Caller.saveTrigger(new { data = "1", message = "Deleted!" });
            }
            catch (Exception e)
            {
                Clients.Caller.saveTrigger(new { data = "-1", message = e.Message });
            }
        }

        /// <summary>
        /// Saves a timer to database
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

                Clients.Caller.saveTimer(new { data = "1", message = "Saved!", container = timer });
            }
            catch (Exception e)
            {
                Clients.Caller.saveTimer(new { data = "-1", message = e.Message });
            }
        }

        /// <summary>
        /// Deletes a timer from database
        /// </summary>
        /// <param name="timerid"></param>
        public void DeleteTimer(string timerid)
        {
            try
            {
                var timerIdInt = Convert.ToInt32(timerid);
                ContextService.DeleteTimer(timerIdInt, GetUsername());

                Clients.Caller.saveTimer(new { data = "1", message = "Deleted!" });
            }
            catch (Exception e)
            {
                Clients.Caller.saveTimer(new { data = "-1", message = e.Message });
            }
        }

        /// <summary>
        /// Save loyalty currency for bot
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

                Clients.Caller.SaveCallback(new { message = "Saved", data = "1" });
            }
            catch (Exception e)
            {
                Clients.Caller.SaveCallback(new { message = e.Message, data = "-1" });
            }
        }

        /// <summary>
        /// Save banned words list
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

        // Sets the flag modsCanControlPlaylist in the client.
        public void UpdateModsCanControlPlaylist(bool modsCanControlPlaylist)
        {
            try
            {
                GetClientContainer().ModsControlSongrequest = modsCanControlPlaylist;

                Clients.Caller.Notify(new { data = "1", message = "Saved" });
            }
            catch (Exception e)
            {
                Clients.Caller.Notify(new { data = "-1", message = e.Message });
            }
        }

        /// <summary>
        /// Gets the default banned words list and calls the client with the list
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
        /// Gets the bot connection status to channel and maintains the workerthread livability
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


                    var bs = new BotStatusVM()
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

                    var bs = new BotStatusVM()
                    {
                        info = "Bot disconnected",
                        message = "",
                        warning = "",
                        connected = false
                    };
                    //ConsoleLog("Bot is " + bs.info);
                    Clients.Caller.BotStatus(bs);
                }

                if (cc.WorkerThread != null && cc.WorkerThread.IsAlive == false)
                {
                    cc.WorkerThread.Abort();
                    cc.WorkerThread = null;

                    var arg = new WorkerThreadArg()
                    {
                        Channel = channel,
                        Username = GetUsername(),
                        Client = cc.Client
                    };
                    var parameterizedThreadStart = new ParameterizedThreadStart(TrackLoyaltyAndTimers);

                    cc.WorkerThread = new Thread(parameterizedThreadStart);
                    cc.WorkerThread.Start(arg);
                }
                else if (cc.WorkerThread == null)
                {
                    var arg = new WorkerThreadArg()
                    {
                        Channel = channel,
                        Username = HttpContext.Current.User.Identity.Name,
                        Client = cc.Client
                    };

                    var parameterizedThreadStart = new ParameterizedThreadStart(TrackLoyaltyAndTimers);
                    cc.WorkerThread = new Thread(parameterizedThreadStart);
                    cc.WorkerThread.Start(arg);
                }
            }
            catch (Exception e)
            {
                ConsoleLog("BotStatus error: " + e.Message);
            }


        }

        /// <summary>
        /// Client sent connect to channel
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="channel"></param>
        public async Task ConnectBot(string username, string password, string channel)
        {
            try
            {
                
                var client = GetClient();
                if (client != null)
                {
                    if (client.IsConnected)
                    {
                        ConsoleLog("Already connected...");
                        ConnectionId = Context.ConnectionId;
                        var bs = new BotStatusVM()
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
                        if (ccontainer.WorkerThread != null && ccontainer.WorkerThread.IsAlive)
                        {
                            ccontainer.WorkerThread.Abort();
                            ccontainer.WorkerThread = null;

                            var arg = new WorkerThreadArg()
                            {
                                Channel = channel,
                                Username = GetUsername(),
                                Client = ccontainer.Client
                            };
                            var parameterizedThreadStart = new ParameterizedThreadStart(TrackLoyaltyAndTimers);

                            ccontainer.WorkerThread = new Thread(parameterizedThreadStart);
                            ccontainer.WorkerThread.Start(arg);
                        }
                        else if (ccontainer.WorkerThread == null)
                        {
                            var arg = new WorkerThreadArg()
                            {
                                Channel = channel,
                                Username = HttpContext.Current.User.Identity.Name,
                                Client = ccontainer.Client
                            };

                            var parameterizedThreadStart = new ParameterizedThreadStart(TrackLoyaltyAndTimers);
                            ccontainer.WorkerThread = new Thread(parameterizedThreadStart);
                            ccontainer.WorkerThread.Start(arg);
                        }

                        //ccontainer.Client.Initialize()
                        ccontainer.Client.Connect();
                        ConsoleLog("Reconnecting to channel " + channel);

                        var bs = new BotStatusVM()
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
                    if ((string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
                         string.IsNullOrWhiteSpace(channel)))
                    {
                        throw new Exception("No user/pass/channel given");
                    }

                    Username = username;
                    Password = password;
                    Channel = channel;
                    ConnectionId = Context.ConnectionId;
                    ConnCred = new ConnectionCredentials(Username, Password);
                    var clientContainer = GetClientContainer();
                    clientContainer.Client = new TwitchClient();

                    clientContainer.Client.Initialize(ConnCred, Channel);

                    //clientContainer.Client.ChatThrottler = new MessageThrottler(clientContainer.Client, 250, new TimeSpan(0, 0, 1), null, false, 255, 255);

                    clientContainer.Client.OnLog += ConsoleLog;
                    clientContainer.Client.OnConnectionError += ConsoleLogConnectionError;
                    clientContainer.Client.OnMessageReceived += ChatShowMessage;

                    clientContainer.Client.AutoReListenOnException = true;
                    clientContainer.Client.OnBeingHosted += OnBeeingHosted;
                    clientContainer.Client.OnRaidNotification += OnBeeingRaided;


                    clientContainer.Client.OnConnected += OnConnectToChannel;
                    clientContainer.Client.OnJoinedChannel += OnJoinedChannel;
                    clientContainer.Client.OnDisconnected += OnDisconnectReconnect;

                    clientContainer.Client.OnModeratorsReceived += ChannelModerators;
                    clientContainer.Client.OnChatCommandReceived += ChatMessageTriggerCheck;

                    clientContainer.Client.OnWhisperReceived += RelayToChatMessage;

                    clientContainer.Client.OverrideBeingHostedCheck = false;


                    clientContainer.Client.Connect();
                    ConsoleLog("Connecting to channel " + channel);

                    var arg = new WorkerThreadArg()
                    {
                        Channel = Channel,
                        Username = GetUsername(),
                        Client = clientContainer.Client
                    };
                    var bs = new BotStatusVM()
                    {
                        info = GetClientContainer().Client.IsConnected ? "Bot connected" : "Bot disconnected",
                        message = "",
                        warning = "",
                        connected = GetClientContainer().Client.IsConnected
                    };
                    Clients.Caller.BotStatus(bs);

                    var parameterizedThreadStart = new ParameterizedThreadStart(TrackLoyaltyAndTimers);
                    clientContainer.WorkerThread = new Thread(parameterizedThreadStart);
                    clientContainer.WorkerThread.Start(arg);
                }


                GetClientContainer().Client.WillReplaceEmotes = true;


                var botStatus = new BotStatusVM()
                {
                    info = GetClientContainer().Client.IsConnected ? "Bot connected" : "Bot disconnected",
                    message = "",
                    warning = "",
                    connected = GetClientContainer().Client.IsConnected
                };
                Clients.Caller.BotStatus(botStatus);
                var bcs = GetClientContainer();
                if (bcs.SongRequests != null)
                {
                    foreach (var song in bcs.SongRequests)
                    {
                        UpdatePlaylistFromCommand(song.Url, song.Title, song.RequestedBy, song.VideoId);
                    }
                }

            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
            }
        }

        private void OnDisconnectReconnect(object sender, OnDisconnectedArgs e)
        {
            //await ConnectBot("test", "test", "test");
        }

        /// <summary>
        /// OnBeeingRaided event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnBeeingRaided(object sender, OnRaidNotificationArgs e)
        {
            var ccontainer = GetClientContainer();
            var bcs = ContextService.GetBotChannelSettings(ccontainer.User);
            var hoster = e.Channel;

            ccontainer.Client.SendMessage(Channel, "/me @" + hoster + " is raiding!");
        }

        /// <summary>
        /// OnConnectToChannel event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnConnectToChannel(object sender, OnConnectedArgs e)
        {
            var ccontainer = GetClientContainer();

            ccontainer.Client.SendMessage(Channel, "/me is connected! YTBot 2018 by @Borge_Jakobsen ");
        }

        /// <summary>
        /// OnJoinedChannel event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            var ccontainer = GetClientContainer();
            var bcs = ContextService.GetBotChannelSettings(ccontainer.User);

            ConsoleLog("Connected to channel " + Channel);
            ccontainer.Client.SendMessage(Channel, "/me @ Hello! ;) ");
        }


        /// <summary>
        /// Beeing hosted event, add bonus to hoster. Amount depending on how many viewers are brought along
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
            ccontainer.Client.SendMessage(Channel, "/me @" + hoster + " just brought " + viewers.ToString() +
                                                   " viewers to the party! Welcome!");


            if (bcs.Loyalty.Track == false)
                return;

            #region bonus loyalty if tracking loyalty

            if (viewers < 5)
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

            ccontainer.Client.SendMessage(Channel, "/me @" + hoster + " received " + bonusViewerLoot + " " +
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
            try
            {
                var ccontainer = GetClientContainer();
                if (ccontainer.Channelmods.Contains(e.WhisperMessage.Username.ToLower()) ||
                    e.WhisperMessage.DisplayName.ToLower() == Channel.ToLower())
                {
                    GetClient().SendMessage(Channel, e.WhisperMessage.Message);
                }
            }
            catch (Exception exception)
            {

            }
        }

        /// <summary>
        /// Gets current stream meta information. Uptime, title, game, delay, mature and online status.
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
                if (stream == null)
                {
                    delay = stream.Stream.Delay;
                }



                var streamStatus = new StreamStatusVM();

                streamStatus.Channel = channel.DisplayName;
                streamStatus.Game = channel.Game;
                streamStatus.Title = channel.Status;
                streamStatus.Mature = channel.Mature;
                streamStatus.Delay = delay;
                streamStatus.Online = uptime;

                Clients.Caller.SetStreamInfo(streamStatus);
                ConsoleLog("Retrieved stream title and game");
            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);

            }
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
                var clientSecret = ConfigurationManager.AppSettings["clientSecret"];
                await InitializeAPI();
                Api.Settings.AccessToken = clientSecret;

                var channelId = await Api.Channels.v5.GetChannelAsync(ChannelToken);

                if (string.IsNullOrWhiteSpace(delay))
                {
                    delay = "0";
                }
                var user = ContextService.GetUser(GetUsername());
                BotUserSettings = ContextService.GetBotUserSettingsForUser(user);
                ChannelToken = BotUserSettings.ChannelToken;
                Channel = BotUserSettings.BotChannel;

                await Api.Channels.v5.UpdateChannelAsync(channelId.Id, title, game, delay, null, ChannelToken);

                var retval = new { data = "1", message = "Saved" };
                Clients.Caller.SaveCallback(retval);
            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
                var retval = new { data = "-1", message = e.Message };
                Clients.Caller.SaveCallback(retval);
            }
        }

        public async Task GetChatOptions()
        {
            try
            {
                await InitializeAPI();

                var channel = await Api.Channels.v5.GetChannelAsync(ChannelToken);

                var chatOptions = channel;

                var retval = new { data = "1", message = "", container = chatOptions };
                Clients.Caller.SetChatOptions(retval);
            }
            catch (Exception e)
            {
                ConsoleLog(e.Message);
                var retval = new { data = "-1", message = e.Message };
                Clients.Caller.Fail(retval);
            }
        }

        /// <summary>
        /// Sends the default banned words list to client
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
                var retval = new { data = "1", message = e.Message, container = "" };

                Clients.Caller.Fail(retval);
            }
        }

        /// <summary>
        /// Reconnect bot to channel, 
        /// </summary>
        /// <param name="username">Username</param>
        /// <param name="password">OAuth</param>
        /// <param name="channel">Channelname</param>
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

                var bs = new BotStatusVM()
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
        /// OnChannelModeratorsReceived event, get all mods in channel.
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

            bool botIsMod = mods.Contains(Username.ToLower());

            var botStatus = new BotStatusVM()
            {
                info = GetClientContainer().Client.IsConnected ? "Bot connected" : "Bot disconnected",
                message = "",
                warning = botIsMod == false ? "Bot is not moderator in channel" : "",
                connected = GetClientContainer().Client.IsConnected
            };

            Clients.Caller.BotStatus(botStatus);
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

                ConsoleLog("Disconnected channel " + Channel);

                var botStatus = new BotStatusVM()
                {
                    info = container.Client.IsConnected ? "Bot connected" : "Bot disconnected",
                    message = "",
                    warning = "",
                    connected = !container.Client.IsConnected
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
            return Api.Channels.v5.UpdateChannelAsync(Channel, topic, game, null, null, Password).IsCompleted;
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
            var logEntry = e.DateTime.ToString("HH:mm:ss").ToString() + " - " + e.Data;
            if (GetClientContainer() != null)
            {
                GetClientContainer().ConsoleLog.Add(logEntry);
            }

            Clients.Caller.ConsoleLog(logEntry);
        }

        public void ConsoleLogConnectionError(object sender, OnConnectionErrorArgs e)
        {
            Clients.Caller.ConsoleLog(DateTime.Now.ToString("HH:mm:ss").ToString() + " - " + e.Error.Message);
        }

        /// <summary>
        /// Call this to update top commands and top chatters
        /// </summary>
        public void UpdateChattersAndCommands()
        {
            try
            {
                InitializeAPI();
                var ccontainer = GetClientContainer();

                var topCommands = ccontainer.CommandsUsed.OrderByDescending(k => k.Value).Take(NUMTOPCOMMANDS);
                var topChatters = ccontainer.ChattersCount.OrderByDescending(k => k.Value).Take(NUMTOPCHATTERS);

                var retval = new { topcommands = topCommands, topchatters = topChatters };
                Clients.Caller.ChattersAndCommands(retval);
            }
            catch (Exception e)
            {
                ConsoleLog("Error on UpdateChattersAndCommands(): " + e.Message);
            }

        }

        /// <summary>
        /// Call this to update client
        /// </summary>
        public void UpdateViewerCount()
        {
            try
            {
                InitializeAPI();
                string viewers = GetNumViewers();

                Clients.Caller.UpdateViewers(viewers);
            }
            catch (Exception e)
            {
                ConsoleLog("Error on UpdateViewerCount(): " + e.Message);
            }
        }

        /// <summary>
        /// Returns number of viewers in stream
        /// </summary>
        /// <returns></returns>
        private string GetNumViewers()
        {
            try
            {
                var ccontainer = GetClientContainer();

                var stream = Api.Streams.v5.GetStreamByUserAsync(ccontainer.Id);

                var numViewers = stream.Result.Stream.Viewers;

                return numViewers.ToString();
            }
            catch (Exception e)
            {
                return "-";
            }
        }

        /// <summary>
        /// Poll this to send songrequests to client
        /// </summary>
        public void PollPlaylist()
        {
            try
            {
                var username = ContextService.GetUser(GetUsername());
                var bcs = ContextService.GetBotChannelSettings(username);
                //var client = GetClientContainer();

                foreach (var video in bcs.SongRequests)
                {
                    Clients.Caller.UpdatePlaylist(video);
                }
            }
            catch (Exception e)
            {

            }
        }

        /// <summary>
        ///  Deletes song from database of users songrequest
        /// </summary>
        /// <param name="id"></param>
        public void DeleteSong(string id)
        {
            try
            {
                var user = ContextService.GetUser(GetUsername());

                ContextService.DeleteSongRequest(user, id);
                var t = new { id = id };
                var retval = new StatusMessageVM()
                {
                    message = "Deleted song with id: " + id,
                    data = 1,
                    obj = t

                };
                Clients.Caller.deleteSongAck(retval);
            }
            catch (Exception e)
            {
                var retval = new StatusMessageVM()
                {
                    message = "Error DeleteSong(): " + e.Message,
                    data = -1
                };
                Clients.Caller.deleteSongAck(retval);
            }
        }

        /// <summary>
        /// Adds song to users list, poll this list to get added songs
        /// </summary>
        /// <param name="url"></param>
        /// <param name="title"></param>
        /// <param name="user"></param>
        /// <param name="videoId"></param>
        /// <returns>PlayListItem</returns>
        public PlayListItem UpdatePlaylistFromCommand(string url, string title, string user, string videoId)
        {
            try
            {
                var obj = new { title = title, url = url, user = user, videoid = videoId };
                var container = GetClientContainer();

                var usr = ContextService.GetUser(GetUsername());

                var item = new PlayListItem();
                item.VideoId = videoId;
                item.Title = System.Text.RegularExpressions.Regex.Unescape(title);
                item.Deleted = false;
                item.RequestDate = DateTime.Now;
                item.RequestedBy = user;
                item.Url = url;


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
        /// Removes video from songrequests queue by youtube id
        /// </summary>
        /// <param name="id"></param>
        public void DeletePlaylistItem(string id)
        {

            try
            {
                var container = GetClientContainer();

                var video = container.SongRequests.FirstOrDefault(v => v.VideoId == id);
                container.SongRequests.Remove(video);

                Clients.Caller.Notify(new { data = "1", message = "Removed video with id: " + id });
            }
            catch (Exception e)
            {
                Clients.Caller.Notify(new { data = "1", message = e.Message });
            }
        }

        /// <summary>
        /// Send chat message to Client
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void ChatShowMessage(object sender, OnMessageReceivedArgs e)
        {
            var username = ContextService.GetUser(GetUsername());
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
            AddToChatLog(e.ChatMessage);
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

            GetClientContainer().Polls.Add(pollResult.Id);

            // Show poll link
            GetClient().SendMessage(Channel, $"/me Vote for \"{pollTitle}\" here => {pollResult.PollUrl}");

            ConsoleLog("Created poll '" + title + "' => " + pollResult.PollUrl);
            Clients.Caller.CreatedPoll(title, pollResult.Id);
            Clients.Caller.CreatePoll(title, allOptions);
        }

        #region Trigger checking

        private void ChatMessageTriggerCheck(object sender, OnChatCommandReceivedArgs e)
        {
            ChatCommandTriggerCheck(e.Command.ChatMessage, e);
        }

        /// <summary>
        /// Check for chat triggers
        /// </summary>
        /// <param name="chatmessage"></param>
        public async Task ChatCommandTriggerCheck(ChatMessage chatmessage, OnChatCommandReceivedArgs arg)
        {
            try
            {
                await InitializeAPI();
                AddToCommands(chatmessage.Message);
                // loot name
                var user = ContextService.GetUser(GetUsername());

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
                                                                         "!stats -\n" +
                                                                         "!" + bcs.Loyalty.LoyaltyName + "  -\n" +
                                                                         "!gamble ['allin'|" + bcs.Loyalty.LoyaltyName +
                                                                         " amount]  -\n" +
                                                                         "!give [username] [" +
                                                                         bcs.Loyalty.LoyaltyName + " amount]  -\n" +
                                                                         "!top[number] -\n" +
                                                                         "!roulette -\n" +
                                                                         "!russian [amount] Start a Russian roulette -\n" +
                                                                         "!burn" + bcs.Loyalty.LoyaltyName +
                                                                         " toss ALL your " +
                                                                         bcs.Loyalty.LoyaltyName + " - \n" +
                                                                         //"!quote (get random quote)" +
                                                                         //"!quote [#] (get quopte by number)" +
                                                                         //"!addquote \"[quote text]\", quotebyname" +
                                                                         //"!removequote [#] (delete quote)" +
                                                                         "!uptime How long has the stream been online -\n"
                        );

                        GetClient().SendWhisper(chatmessage.DisplayName, "/me " +
                                                                         "!sr [HTTP youtube video url] -\n" +
                                                                         "!bonus [username] [" +
                                                                         bcs.Loyalty.LoyaltyName +
                                                                         " amount] (streamer/mod)  -\n" +
                                                                         "!bonusall [" + bcs.Loyalty.LoyaltyName +
                                                                         " amount]\n (streamer/mod) -\n" +
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
                                                                         "!addpoll \"[Title]\" [option1]|[option2]|[optionN] \n"
                        );
                    }

                    // !loot
                    if (chatmessage.Message.ToLower().StartsWith("!" + bcs.Loyalty.LoyaltyName))
                    {
                        var userLoyalty = ContextService.GetLoyaltyForUser(GetUsername(), Channel,
                            chatmessage.UserId,
                            chatmessage.Username);
                        var client = GetClient();

                        if (userLoyalty != null)
                        {
                            GetClient()
                                .SendMessage(Channel, "/me " +
                                                      $"@{chatmessage.DisplayName} has {userLoyalty.CurrentPoints.ToString()} {bcs.Loyalty.LoyaltyName}");
                        }
                        else
                        {
                            GetClient()
                                .SendMessage(Channel, "/me " +
                                                      $"@{chatmessage.DisplayName}, you haven't earned any {bcs.Loyalty.LoyaltyName} yet. Hang out in the channel and you will recieve {bcs.Loyalty.LoyaltyValue.ToString()} every {bcs.Loyalty.LoyaltyInterval.ToString()} minute.");
                        }
                    }

                    // !burn<loyaltyName>
                    else if (chatmessage.Message.ToLower().StartsWith("!burn" + bcs.Loyalty.LoyaltyName.ToLower()))
                    {
                        var rnd = new SysRandom(Guid.NewGuid().GetHashCode());
                        var userLoyalty = ContextService.GetLoyaltyForUser(GetUsername(), Channel,
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

                            GetClient().SendMessage(Channel, "/me " + (string)ripLoyaltySentences[randonIndex]);

                            ContextService.AddLoyalty(ContextService.GetUser(GetUsername()),
                                chatmessage.Channel.ToLower(), userLoyalty, -userLoyalty.CurrentPoints);
                        }
                        else
                        {
                            GetClient()
                                .SendMessage(Channel, "/me " +
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

                        var thisUser = ContextService.GetUser(GetUsername());
                        var topLoyalty = ContextService.TopLoyalty(thisUser, number);

                        var message = "Top" + number.ToString() + ": ";

                        var counter = 1;
                        foreach (var loyalty in topLoyalty)
                        {
                            message += counter + ". " + loyalty.TwitchUsername + " (" + loyalty.CurrentPoints + ") \n";
                            counter++;
                        }

                        GetClient().SendMessage(Channel, "/me " + message);
                    }

                    // !bonus !bonusall !give !gamble
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

                                    ContextService.AddLoyalty(ContextService.GetUser(GetUsername()),
                                        chatmessage.Channel.ToLower(), GetUsersInChannel(chatmessage.Channel.ToLower()),
                                        bonusValue);

                                    verb = bonusValue > 0 ? "has been given" : "has been deprived of";

                                    GetClient()
                                        .SendMessage(Channel, "/me " +
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
                                        HttpContext.Current.User.Identity.Name, Channel, null,
                                        destinationViewerName);

                                    if (loyaltyAmount != null && (destinationViewerLoyalty != null))
                                    {
                                        ContextService.AddLoyalty(ContextService.GetUser(GetUsername()),
                                            chatmessage.Channel.ToLower(), destinationViewerLoyalty, loyaltyAmount);

                                        GetClient()
                                            .SendMessage(Channel, "/me " +
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

                                var sourceViewerLoyalty = ContextService.GetLoyaltyForUser(GetUsername(),
                                    Channel,
                                    sourceViewerId,
                                    sourceViewerName);
                                var destinationViewerLoyalty = ContextService.GetLoyaltyForUser(
                                    HttpContext.Current.User.Identity.Name,
                                    Channel,
                                    null,
                                    destinationViewerName);

                                // uses does not have enough to give away
                                if (loyaltyAmount != null && (sourceViewerLoyalty != null &&
                                                              sourceViewerLoyalty.CurrentPoints < loyaltyAmount))
                                {
                                    GetClient()
                                        .SendMessage(Channel, "/me " +
                                                              $"Stop wasting my time @{chatmessage.DisplayName}, you ain't got that much {bcs.Loyalty.LoyaltyName}");
                                }
                                // give away loot
                                else if (loyaltyAmount != null &&
                                         (sourceViewerLoyalty != null &&
                                          sourceViewerLoyalty.CurrentPoints >= loyaltyAmount))
                                {
                                    ContextService.AddLoyalty(ContextService.GetUser(GetUsername()),
                                        chatmessage.Channel.ToLower(), sourceViewerLoyalty, -loyaltyAmount);
                                    ContextService.AddLoyalty(ContextService.GetUser(GetUsername()),
                                        chatmessage.Channel.ToLower(), destinationViewerLoyalty, loyaltyAmount);

                                    GetClient()
                                        .SendMessage(Channel, "/me " +
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
                                .SendMessage(Channel,
                                    "/me " +
                                    $" Type !gamble <amount> or !gamble allin. I use the glorious random number generator web-service from RANDOM.ORG that generates randomness via atmospheric noise.");
                        }
                        // !gamble
                        else if (chatmessage.Message.ToLower().StartsWith("!gamble"))
                        {
                            // get 
                            var loyalty = ContextService.GetLoyaltyForUser(GetUsername(), Channel,
                                chatmessage.UserId, chatmessage.DisplayName.ToLower());

                            // timeout for 5 minutes if user has gamble before
                            if (loyalty != null && (loyalty.LastGamble == null ||
                                                    (loyalty.LastGamble.HasValue &&
                                                     loyalty.LastGamble.Value.AddMinutes(6) <= DateTime.Now)))
                            {
                                try
                                {
                                    var r = new Random.Org.Random();

                                    // get who to give it to
                                    var gambleAmount = chatmessage.Message.Split(' ')[1].ToLower().Equals("allin")
                                        ? loyalty.CurrentPoints
                                        : Math.Abs(Convert.ToInt32(chatmessage.Message.Split(' ')[1]));

                                    string sourceViewerId = chatmessage.UserId;
                                    string sourceViewerName = chatmessage.Username;

                                    var sourceViewerLoyalty = ContextService.GetLoyaltyForUser(
                                        HttpContext.Current.User.Identity.Name,
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
                                                .SendMessage(Channel,
                                                    "/me " +
                                                    $"@{chatmessage.DisplayName} rolled a sad {rolledNumber}, lost {gambleAmount} and now has {newAmount} {bcs.Loyalty.LoyaltyName}!! #theSaltIsReal #rigged");
                                            ContextService.AddLoyalty(
                                                ContextService.GetUser(HttpContext.Current.User.Identity.Name),
                                                chatmessage.Channel.ToLower(), sourceViewerLoyalty, -gambleAmount);
                                        }
                                        // rolled 50-99
                                        else if (rolledNumber >= 50 && rolledNumber < 100)
                                        {
                                            var newAmount = (sourceViewerLoyalty.CurrentPoints - gambleAmount) +
                                                            (gambleAmount * 2);

                                            GetClient()
                                                .SendMessage(Channel, "/me " +
                                                                      $"@{chatmessage.DisplayName} rolled {rolledNumber}, won {gambleAmount * 2} and now has {newAmount} {bcs.Loyalty.LoyaltyName}!");

                                            ContextService.AddLoyalty(
                                                ContextService.GetUser(HttpContext.Current.User.Identity.Name),
                                                chatmessage.Channel.ToLower(), sourceViewerLoyalty, gambleAmount);
                                        }
                                        // rolled 100 win * 10
                                        else
                                        {
                                            var newAmount = (sourceViewerLoyalty.CurrentPoints - gambleAmount) +
                                                            (gambleAmount * 10);

                                            GetClient()
                                                .SendMessage(Channel, "/me " +
                                                                      $"@{chatmessage.DisplayName} did an epic roll, threw {rolledNumber}, won {gambleAmount * 10} and now has {newAmount} {bcs.Loyalty.LoyaltyName}!! #houseCries");

                                            ContextService.AddLoyalty(
                                                ContextService.GetUser(GetUsername()),
                                                chatmessage.Channel.ToLower(), sourceViewerLoyalty, gambleAmount * 3);
                                        }

                                        ContextService.StampLastGamble(
                                            ContextService.GetUser(GetUsername()),
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
                                    .SendMessage(Channel, "/me " +
                                                          $"@{chatmessage.DisplayName}, you haven't earned any {bcs.Loyalty.LoyaltyName} to gamble yet. Stay and the channel and you will recieve {bcs.Loyalty.LoyaltyValue.ToString()} every {bcs.Loyalty.LoyaltyInterval.ToString()} minute.");
                            }
                            else
                            {
                                GetClient()
                                    .SendMessage(Channel, "/me " +
                                                          $"Chill out @{chatmessage.DisplayName}, you gotta wait 5 minutes from your last gamble to roll the dice again!");
                            }
                        }
                    }
                    // !russian
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
                            HttpContext.Current.User.Identity.Name,
                            Channel,
                            sourceViewerId,
                            sourceViewerName);
                        // start new roulette
                        if (roulette == null)
                        {
                            if (ccontainer.LastRussian != null &&
                                ((DateTime.Now - ccontainer.LastRussian.AddMinutes(6)).Minutes < 0))
                            {
                                var minFor = DateTime.Now - ccontainer.LastRussian.AddMinutes(6);
                                GetClient().SendMessage(Channel,
                                    "/me " +
                                    $"There is a 5 minute sleep time between Russian roulettes, please wait {Math.Abs(minFor.Minutes)} minutes and try again.");
                                return;
                            }

                            if (match.Success)
                            {
                                bet = Convert.ToInt32(match.Groups[1].Value);

                                if (player == null || player.CurrentPoints < bet)
                                {
                                    GetClient()
                                        .SendMessage(Channel,
                                            "/me " +
                                            $"@{chatmessage.DisplayName}, you need to have {bet} {bcs.Loyalty.LoyaltyName} to enter the big boys Russian Roulette");
                                    return;
                                }
                            }
                            else
                            {
                                GetClient()
                                    .SendMessage(Channel,
                                        "/me " +
                                        $"@{chatmessage.DisplayName}, you need set how much {bcs.Loyalty.LoyaltyName} you want to set as \"buy in\".");
                                return;
                            }


                            var newRoulette = new RussianRoulette { BuyIn = bet };
                            newRoulette.TotalBet += newRoulette.BuyIn;
                            newRoulette.Players.Add(player);
                            GetClientContainer().RRulette = newRoulette;

                            // remove loot from player
                            ContextService.AddLoyalty(ContextService.GetUser(GetUsername()),
                                chatmessage.Channel.ToLower(), player, -bet);

                            GetClient()
                                .SendMessage(Channel,
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
                                    .SendMessage(Channel,
                                        "/me " +
                                        $"@{chatmessage.DisplayName}, you need to have {rroulette.BuyIn} {bcs.Loyalty.LoyaltyName} to enter the big boys Russian Roulette");
                                return;
                            }
                            else
                            {
                                rroulette.TotalBet += rroulette.BuyIn;
                                // remove loot from player
                                ContextService.AddLoyalty(
                                    ContextService.GetUser(HttpContext.Current.User.Identity.Name),
                                    chatmessage.Channel.ToLower(), player, -rroulette.BuyIn);

                                rroulette.Players.Add(player);

                                GetClient()
                                    .SendMessage(Channel,
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
                                                                         "!stats -\n" +
                                                                         "!uptime -\n" +
                                                                         "!roulette -\n" +
                                                                         "!timeout [username] (streamer/mod) Timeout user for 1 minute.  -\n" +
                                                                         "!ban [username] (streamer/mod) Ban user from channel  -\n" +
                                                                         "!addpoll \"[Title]\" [option1]|[option2]|[optionN] -\n" +
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
                // !stats
                if (arg.Command.CommandText.ToLower().Equals("stats"))
                {
                    var channelData = await Api.Channels.v5.GetChannelAsync(ChannelToken);
                    var channelSubsData =
                        await Api.Channels.v5.GetChannelSubscribersAsync(channelData.Id, null, null, null,
                            ChannelToken);

                    GetClient().SendMessage(Channel, $"{Channel} has {channelData.Followers} followers and {channelSubsData.Total} subscribers.");
                }
                // !multilink
                else if (arg.Command.CommandText.ToLower().Equals("multilink"))
                {
                    var baseurl = "https://multistre.am/donfandango/";

                    var restOfString = string.Join("/", arg.Command.ArgumentsAsList.ToList());

                    var url = baseurl + restOfString;

                    GetClient().SendMessage(Channel, "/me " + "Watch the multistream at " + url);
                }

                // !clip
                else if (arg.Command.CommandText.ToLower().Equals("clip"))
                {
                    try
                    {
                        var channelData = await Api.Channels.v5.GetChannelAsync(ChannelToken);
                        var clip = await Api.Clips.helix.CreateClipAsync(channelData.Id, ChannelToken);
                        
                       
                        var clipUrl = clip.CreatedClips.Last().EditUrl;
                        GetClient().SendMessage(Channel, $"/me Clip created => {clipUrl}");
                    }
                    catch (Exception e)
                    {
                        ConsoleLog("Error on !clip: " + e.Message);
                    }
                    

                }

                // !quote (random qoute)
                else if (arg.Command.CommandText.ToLower().Equals("quote") ||
                         arg.Command.CommandText.ToLower().Equals("addquote") ||
                         arg.Command.CommandText.ToLower().Equals("removeqoute"))
                {
                    if (arg.Command.CommandText.ToLower().Equals("quote"))
                    {
                        // TODO: get random quote if no number is given
                    }

                    if (arg.Command.ChatMessage.IsBroadcaster || arg.Command.ChatMessage.IsModerator)
                    {
                        // TODO: addqute
                        if (arg.Command.CommandText.ToLower().Equals("addquote"))
                        {
                            Regex re = new Regex("\\!addquote (\".*\"), (\\.*)");
                            Match match = re.Match(arg.Command.ChatMessage.Message);
                            if (match.Success)
                            {
                                Quote q = new Quote();
                                q.QuoteAdded = DateTime.Now;
                                q.QuoteBy = match.Groups[2].Value;
                                q.QuoteMsg = match.Groups[1].Value;
                                ContextService.SaveQoute(ContextService.GetUser(GetUsername()), q);
                            }
                        }
                        // TODO: removequote
                        else if (arg.Command.CommandText.ToLower().Equals("removeqoute"))
                        {
                            Regex re = new Regex("\\!removequote (\\d)$");
                            Match match = re.Match(arg.Command.ChatMessage.Message);
                            if (match.Success)
                            {
                                ContextService.RemoveQuote(ContextService.GetUser(GetUsername()),
                                    Convert.ToInt32(match.Groups[1].Value));
                            }
                        }
                    }
                }

                // !sr songrequest
                else if (arg.Command.CommandText.ToLower().Equals("sr"))
                {
                    try
                    {

                        var commandArguments = arg.Command.ArgumentsAsString;
                        var userName = arg.Command.ChatMessage.DisplayName;



                        // video link fron youtube
                        if (commandArguments.ToLower().Contains("www.youtube.com"))
                        {
                            var uri = new Uri(commandArguments);

                            var query = HttpUtility.ParseQueryString(uri.Query);

                            var videoId = string.Empty;

                            videoId = query.AllKeys.Contains("v") ? query["v"] : uri.Segments.Last();

                            var title = GetVideoTitleByHttp(commandArguments, videoId);

                            // Try again if title cannot be found
                            if (title == "N/A")
                            {
                                title = GetVideoTitleByHttp(commandArguments, videoId);
                            }
                            if (GetClientContainer().SongRequests.Any(a => a.VideoId == videoId))
                            {
                                GetClient().SendMessage(Channel,
                                    $"\"{title}\" is already in the playlist.");
                            }
                            else
                            {
                                var song = UpdatePlaylistFromCommand(commandArguments, title, userName, videoId);
                                GetClient().SendMessage(Channel,
                                    $"\"{song.Title}\" was added to the playlist by @{song.RequestedBy}.");
                            }

                        }
                        // search for the song on youtube
                        else
                        {
                            // Keyword
                            string querystring = arg.Command.ArgumentsAsString;

                            var youtubeSearch = new VideoSearch();
                            var youtubeSearchResult = youtubeSearch.SearchQuery(querystring, 1);

                            if (youtubeSearchResult != null && youtubeSearchResult.Count > 0)
                            {
                                var firstHit = youtubeSearchResult.FirstOrDefault();
                                var firstVideoUrl = new Uri(firstHit.Url);
                                var videoId = string.Empty;
                                var query = HttpUtility.ParseQueryString(firstVideoUrl.Query);
                                if (query.AllKeys.Contains("v"))
                                {
                                    videoId = query["v"];
                                }
                                else
                                {
                                    videoId = firstVideoUrl.Segments.Last();
                                }
                                if (GetClientContainer().SongRequests.Any(a => a.VideoId == videoId))
                                {
                                    GetClient().SendMessage(Channel,
                                        $"\"{firstHit.Title}\" is already in the playlist.");
                                }
                                else
                                {
                                    var song = UpdatePlaylistFromCommand(firstHit.Url, firstHit.Title, userName, videoId);
                                    GetClient().SendMessage(Channel,
                                        $"\"{song.Title}\" was added to the playlist by @{song.RequestedBy}.");
                                }

                            }

                        }


                    }
                    catch (Exception e)
                    {
                    }
                }


                // !poll / !addpoll
                else if (arg.Command.CommandText.ToLower().Equals("poll") ||
                         arg.Command.CommandText.ToLower().Equals("addpoll"))
                {
                    if (arg.Command.CommandText.ToLower().Equals("poll"))
                    {
                        if (GetClientContainer().Polls.Count == 0)
                        {
                            GetClient().SendMessage(Channel, "/me " + $"No polls created yet...");
                        }
                        else
                        {
                            // Get the last Strawpoll ever made
                            int pollId = GetClientContainer().Polls.Last();
                            var poll = new StrawPoll();
                            var pollFetch = await poll.GetPollAsync(pollId);

                            // Show results
                            GetClient()
                                .SendMessage(Channel,
                                    "/me " +
                                    $"The last poll results for {pollFetch.Title} {pollFetch.PollUrl} are:");
                            var results = pollFetch.Options.Zip(pollFetch.Votes, (a, b) => new { Option = a, Vote = b });
                            foreach (var result in results)
                            {
                                string percentage;
                                if (result.Vote == 0)
                                {
                                    percentage = "0";
                                }
                                else
                                {
                                    percentage = ((result.Vote / pollFetch.Options.Count) * 100).ToString();
                                }
                                GetClient().SendMessage(Channel, "/me " + $"{result.Option} => {result.Vote} votes ({percentage}%)");
                            }
                        }
                    }
                    else if (arg.Command.CommandText.ToLower().Equals("addpoll"))
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
                                var test = match.Groups[2].Value.Split('|');
                                foreach (var option in test)
                                {
                                    arguments.Add(option.Trim());
                                }
                            }

                            CreateStrawPoll(title, arguments);
                        }
                    }
                }

                // !uptime
                else if (arg.Command.CommandText.ToLower().Equals("uptime"))
                {
                    var channel = Api.Channels.v5.GetChannelAsync(ChannelToken).Result;
                    var uptime = Api.Streams.v5.GetUptimeAsync(channel.Id);


                    if (uptime.Result == null)
                    {
                        GetClient()
                            .SendMessage(Channel, "/me " + $"Channel is offline.");
                    }
                    else
                    {
                        if (uptime.Result.Value.Hours == 0)
                        {
                            //GetClient().SendMessage(Channel, "/me " + $"{channel.DisplayName} has been live for {uptime.Result.Value.Minutes} minutes and currenty has {viewersNow} viewers.");
                            GetClient()
                                .SendMessage(Channel,
                                    "/me " +
                                    $"{channel.DisplayName} has been live for {uptime.Result.Value.Minutes} minutes.");
                        }
                        else
                        {
                            //GetClient().SendMessage(Channel, "/me " + $"{channel.DisplayName} has been live for {uptime.Result.Value.Hours} hours, {uptime.Result.Value.Minutes} minutes and currenty has {viewersNow} viewers.");
                            GetClient()
                                .SendMessage(Channel,
                                    "/me " +
                                    $"{channel.DisplayName} has been live for {uptime.Result.Value.Hours} hours, {uptime.Result.Value.Minutes} minutes.");
                        }
                    }
                }

                // !ban
                else if (arg.Command.CommandText.ToLower().Equals("ban"))
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
                else if (arg.Command.CommandText.ToLower().Equals("streamer"))
                {
                    if (chatmessage.IsBroadcaster || chatmessage.IsModerator)
                    {
                        await InitializeAPI();
                        var streamerName = arg.Command.ArgumentsAsList.First();
                        var twitchUrl = "http://www.Twitch.tv/" + streamerName;
                        var lastStreamed = "";

                        try
                        {
                            var twitchUser = await Api.Users.v5.GetUserByNameAsync(streamerName);
                            var channelData = await Api.Channels.v5.GetChannelByIDAsync(twitchUser.Matches[0].Id);

                            lastStreamed = " - Last streamed '" + channelData.Game + "'";
                            streamerName = twitchUser.Matches[0].DisplayName;

                            GetClient()
                                .SendMessage(Channel, "" +
                                                      $"Please go give our friend " + streamerName + " a follow at " +
                                                      twitchUrl +
                                                      " " + lastStreamed);
                        }
                        catch (Exception e)
                        {

                        }


                    }
                }

                // !unban
                else if (arg.Command.CommandText.ToLower().Equals("unban"))
                {
                    if (chatmessage.IsBroadcaster || chatmessage.IsModerator)
                    {
                        GetClient().UnbanUser(Channel, arg.Command.ArgumentsAsList.FirstOrDefault().ToString());
                    }
                }

                // !next
                else if (arg.Command.CommandText.ToLower().Equals("next"))
                {
                    if (chatmessage.IsBroadcaster)
                    {
                        NextSong();
                    }
                    else if (chatmessage.IsModerator && GetClientContainer().ModsControlSongrequest)
                    {
                        NextSong();
                    }
                }
                // !prev
                else if (arg.Command.CommandText.ToLower().Equals("prev"))
                {
                    if (chatmessage.IsBroadcaster)
                    {
                        PrevSong();
                    }
                    else if (chatmessage.IsModerator && GetClientContainer().ModsControlSongrequest)
                    {
                        PrevSong();
                    }
                }
                // !play
                else if (arg.Command.CommandText.ToLower().Equals("play"))
                {
                    if (chatmessage.IsBroadcaster)
                    {
                        Play();
                    }
                    else if (chatmessage.IsModerator && GetClientContainer().ModsControlSongrequest)
                    {
                        Play();
                    }
                }
                // !stop
                else if (arg.Command.CommandText.ToLower().Equals("stop"))
                {
                    if (chatmessage.IsBroadcaster)
                    {
                        Stop();
                    }
                    else if (chatmessage.IsModerator && GetClientContainer().ModsControlSongrequest)
                    {
                        Stop();
                    }
                }
                // !volume
                else if (arg.Command.CommandText.ToLower().Equals("volume"))
                {

                }
                // !timeout
                else if (arg.Command.CommandText.ToLower().Equals("timeout"))
                {
                    if (chatmessage.IsBroadcaster || chatmessage.IsModerator)
                    {
                        var timeout = new TimeSpan(0, 0, 1, 0);

                        if (arg.Command.ArgumentsAsList.Count == 2)
                        {
                            timeout = new TimeSpan(0, 0, Convert.ToInt32(arg.Command.ArgumentsAsList.Last().ToString()),
                                0);
                        }

                        var message = "Timed out for " + Convert.ToString(timeout.Minutes) + " minutes";
                        var joinedChannel = GetClient().GetJoinedChannel(Channel);
                        GetClient().TimeoutUser(joinedChannel, arg.Command.ArgumentsAsList.FirstOrDefault().ToString(),
                            timeout, message);
                    }
                }

                // !roulette
                else if (arg.Command.CommandText.ToLower().Equals("roulette"))
                {
                    var client = GetClient();

                    client.SendMessage(Channel, "/me " + $"@{chatmessage.DisplayName} places the gun to their head!");

                    var rnd = new SysRandom(Guid.NewGuid().GetHashCode());

                    var theNumberIs = rnd.Next(1, 6);

                    var timeout = new TimeSpan(0, 0, 1, 0);
                    var message = "Timed out for " + Convert.ToString(timeout.Minutes) + " minutes";

                    // User dies(timeout) if 1 is drawn
                    if (theNumberIs == 1)
                    {
                        Wait(1);
                        client.SendMessage(Channel, "/me " +
                                                    $"@{chatmessage.DisplayName} pulls the trigger...... brain goes everywhere!! Who knew @{chatmessage.DisplayName} had that much in there?");
                        //Timeout user
                        var joinedChannel = client.GetJoinedChannel(Channel);
                        client.TimeoutUser(joinedChannel, chatmessage.DisplayName, timeout, message);
                        client.SendMessage(Channel, "/me " +
                                                    $"@{chatmessage.DisplayName} is now chilling on the floor and sort of all over the place for a minute!");
                    }
                    // Gets away with it!
                    else
                    {
                        Wait(1);
                        client.SendMessage(Channel, "/me " +
                                                    $"@{chatmessage.DisplayName} pulls the trigger...... CLICK!....... and survives!!");
                    }
                }

                // triggers
                else if (triggers.Any(t => t.TriggerName.ToLower().Equals(arg.Command.CommandText.ToLower()) &&
                                           t.Active != null && t.Active.Value == true))
                {
                    var trigger =
                        triggers.FirstOrDefault(t => t.TriggerName.ToLower()
                            .Equals(arg.Command.CommandText.ToLower().ToLower()));
                    switch (trigger.TriggerType)
                    {
                        // Chat response
                        case TriggerType.Message:
                            if (trigger.StreamerCanTrigger.Value)
                            {
                                if (chatmessage.IsBroadcaster)
                                {
                                    GetClient().SendMessage(Channel, "/me " + trigger.TriggerResponse);
                                    break;
                                }
                            }

                            if (trigger.ModCanTrigger.Value)
                            {
                                if (chatmessage.IsModerator)
                                {
                                    GetClient().SendMessage(Channel, "/me " + trigger.TriggerResponse);
                                    break;
                                }
                            }

                            if (trigger.SubCanTrigger.Value)
                            {
                                if (chatmessage.IsSubscriber)
                                {
                                    GetClient().SendMessage(Channel, "/me " + trigger.TriggerResponse);
                                    break;
                                }
                            }

                            if (trigger.ViewerCanTrigger.Value)
                            {
                                GetClient().SendMessage(Channel, "/me " + trigger.TriggerResponse);
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
                ConsoleLog(e.Message);
            }

            return;
        }

        private void Play()
        {
            Clients.Client(GetClientContainer().ConnectionId).playSong();
        }

        private void Stop()
        {
            Clients.Client(GetClientContainer().ConnectionId).stopSong();
        }

        private void PrevSong()
        {
            Clients.Client(GetClientContainer().ConnectionId).PrevSong();
        }

        private void NextSong()
        {
            Clients.Client(GetClientContainer().ConnectionId).NextSong();
        }

        /// <summary>
        /// Get Title of youtube video url
        /// </summary>
        /// <param name="url"></param>
        /// <param name="videoId"></param>
        /// <returns>Title</returns>
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
        /// Gets the current TwitchClient for the current web-connection
        /// </summary>
        /// <returns></returns>
        private TwitchClient GetClient()
        {
            return ClientContainers.FirstOrDefault(t => t.Id == GetUsername()).Client;
        }

        /// <summary>
        /// Get username of logged in user
        /// </summary>
        /// <returns>Username as string</returns>
        private String GetUsername()
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
        /// Gets the current TwitchClientContainer for the current logged in user
        /// </summary>
        /// <returns>TwitchClientContainer</returns>
        private TwitchClientContainer GetClientContainer()
        {
            return ClientContainers.FirstOrDefault(t => t.Id == GetUsername());
        }


        /// <summary>
        /// Worker thread that runs Loyalty collecting, triggers Timers and other timer based tasks
        /// </summary>
        /// <param name="arg"></param>
        public async void TrackLoyaltyAndTimers(object arg)
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
                using (var ContextService = new ContextService())
                {
                    while (wtarg.Client == null)
                    {
                        Wait(5);
                    }

                    while (wtarg.Client != null && wtarg.Client.IsConnected == false)
                    {
                        Wait(5);
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

                            if (botChannelSettings != null && botChannelSettings.Loyalty != null &&
                                botChannelSettings.Loyalty.Track == true && uptime.Result != null)
                            {
                                if (stopWatch.Elapsed.Minutes % botChannelSettings.Loyalty.LoyaltyInterval == 0 &&
                                    lastLoyaltyElapsedMinutes != stopWatch.Elapsed.Minutes)
                                {
                                    var uname = ContextService.GetUser(GetUsername());
                                    var bs = ContextService.GetBotUserSettingsForUser(uname);

                                    var usersOnline = Api.Undocumented.GetChattersAsync(Channel.ToLower());

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
                                    // update timer
                                    ContextService.TimerStampLastRun(timer.Id, User.UserName);
                                    // show message in chat
                                    wtarg.Client.SendMessage(Channel, "/me " + timer.TimerResponse);
                                    
                                    Wait(1);
                                }
                            }
                        }

                    }
                    catch (Exception e)
                    {
                    }
                    // chill for a second
                    Wait(5);
                }
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
                    client.RRulette.StartOneMinReminderAlerted == true &&
                    client.RRulette.StartTenSecReminder < DateTime.Now)
                {
                    client.RRulette.StartTenSecReminderAlerted = true;
                    GetClient()
                        .SendMessage(Channel,
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

            return;
        }

        public void LoadChatLog()
        {
            try
            {
                var tcc = GetClientContainer();
                foreach (var chatMessage in tcc.ChatLog)
                {
                    Clients.Caller.updateChatLog(chatMessage);
                }
                Clients.Caller.toggleTableSorter();
            }
            catch (Exception e)
            {
                ConsoleLog("Error on LoacChatLog(): " + e.Message );
            }
        }

        /// <summary>
        /// Get poll data from Strawpoll and call client with each poll
        /// </summary>
        public async Task LoadPolls()
        {
            try
            {
                var polls = GetClientContainer().Polls;

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

        /// <summary>
        /// Randomizes a list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Gets list of users in channel, looks up their twitch ID 
        /// </summary>
        /// <param name="channel">Channel as string</param>
        /// <returns>list of StreamView</returns>
        public List<StreamViewer> GetUsersInChannel(string channel)
        {
            var users = new List<StreamViewer>();

            var streamUsers = Api.Undocumented.GetChattersAsync(channel).Result;

            foreach (var user in streamUsers)
            {
                var tmpUser = Api.Users.v5.GetUserByNameAsync(user.Username);

                var t = new StreamViewer();

                t.TwitchUsername = user.Username;
                t.TwitchUserId = tmpUser.Result.Matches[0].Id;

                if (users.All(u => u.TwitchUserId != t.TwitchUserId))
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
        private List<ChatMessage> AddToChatLog(ChatMessage chatmessage)
        {

            var ccontainer = GetClientContainer();

            // +1 #topChatters
            AddToChatStat(chatmessage.Username, ccontainer);
            AddToChatLog(chatmessage, ccontainer);

            return ccontainer.ChatLog;
        }

        /// <summary>
        /// Add +1 chat message to chatter
        /// Disregard streamer and bot
        /// </summary>
        /// <param name="username"></param>
        /// <returns>ChatterCount dictionary</returns>
        private void AddToChatStat(string username, TwitchClientContainer tcc)
        {
            if (tcc.ChattersCount.ContainsKey(username))
            {
                tcc.ChattersCount[username]++;
            }
            else
            {
                tcc.ChattersCount[username] = 1;
            }
        }

        private void AddToChatLog(ChatMessage msg, TwitchClientContainer tcc)
        {
            if (tcc.ChatLog != null)
            {
                tcc.ChatLog.Add(msg);
            }
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