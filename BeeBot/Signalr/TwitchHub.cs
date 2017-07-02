using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity.Core.Mapping;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using BeeBot.Models;
using BeeBot.Services;
using Microsoft.AspNet.SignalR;
using Newtonsoft.Json;
using TwitchLib;
using TwitchLib.Events.Client;
using TwitchLib.Extensions.Client;
using TwitchLib.Models.API.v5.Channels;
using TwitchLib.Models.API.v5.Games;
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
					clientContainer.Client.ChatThrottler = new TwitchLib.Services.MessageThrottler(20, TimeSpan.FromSeconds(30));
				    clientContainer.Client.ChatThrottler.ApplyThrottlingToRawMessages = true;
				    

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


                    // TODO Listen for donations and follows

					clientContainer.Client.Connect();
					ConsoleLog("Connected to channel " + channel);
					//GetClient().SendMessage(channel, " is now connected and serving its master!");

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

				TwitchLib.Models.API.v5.Channels.Channel x = await TwitchAPI.Channels.v5.UpdateChannel(channelId.Id.ToString(), title, game, delay);

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
				mods.Add(moderator.ToLower());
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

            var retval = new { topcommands = topCommands, topchatters = topChatters};
	        Clients.Caller.ChattersAndCommands(retval);

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

            // Add to chat log
		    AddToChatLog(e.ChatMessage.DisplayName, e.ChatMessage.Message);
            Clients.Caller.ChatShow(msg);

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
					if (chatmessage.Message.ToLower().Equals("!help") || chatmessage.Message.ToLower().Equals("!commands"))
					{
						GetClient()
							.SendMessage("Commands available: \n" +
										 "!help or !commands -" +
										 "!" + bcs.Loyalty.LoyaltyName + "  -\n" +
										 "!gamble ['allin'|{" + bcs.Loyalty.LoyaltyName + " amount]  -\n" +
										 "!give [username] [" + bcs.Loyalty.LoyaltyName + " amount]  -\n" +
                                         "!roulette -\n" +
										 "!bonus [username] [" + bcs.Loyalty.LoyaltyName + " amount] (streamer/mod)  -\n" +
										 "!bonusall [" + bcs.Loyalty.LoyaltyName + " amount]\n (streamer/mod) -" +
										 "!burn" + bcs.Loyalty.LoyaltyName + " toss ALL your " + bcs.Loyalty.LoyaltyName + " - \n" +
										 "!uptime " +
							             "!timeout [username] (streamer/mod) Timeout user for 1 minute.  -\n" +
							             "!ban [username] (streamer/mod) Ban user from channel \n");
						//+
						//                 "!timeout [username] (streamer/mod) - Timeout user for 1 minute.  \n" +
						//                 "!ban [username] (streamer/mod) - Ban user from channel  \n");
					}

					// !<loyaltyName>
					if (chatmessage.Message.StartsWith("!" + bcs.Loyalty.LoyaltyName))
					{
						var userLoyalty = ContextService.GetLoyaltyForUser(Context.User.Identity.Name, chatmessage.UserId,
							chatmessage.Username);

					    if (userLoyalty != null)
					    {
					        GetClient()
					            .SendMessage(
					                $"@{chatmessage.DisplayName} has {userLoyalty.CurrentPoints.ToString()} {bcs.Loyalty.LoyaltyName}");
					    }
					    else
					    {
					        GetClient()
					            .SendMessage(
					                $"@{chatmessage.DisplayName}, you haven't earned any {bcs.Loyalty.LoyaltyName} yet. Stay and the channel and you will recieve {bcs.Loyalty.LoyaltyValue.ToString()} every {bcs.Loyalty.LoyaltyInterval.ToString()} minute.");
                        }
					}

					// !burn<loyaltyName>
					else if (chatmessage.Message.StartsWith("!burn" + bcs.Loyalty.LoyaltyName.ToLower()))
					{
						Random rnd = new Random(Guid.NewGuid().GetHashCode());
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

						    GetClient().SendMessage((string)ripLoyaltySentences[randonIndex]);

                            ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name),
								chatmessage.Channel.ToLower(), userLoyalty, -userLoyalty.CurrentPoints);
							
						}
						else
						{
						    GetClient()
						        .SendMessage(
						            $"@{chatmessage.DisplayName}, you haven't earned any {bcs.Loyalty.LoyaltyName} yet. Stay and the channel and you will recieve {bcs.Loyalty.LoyaltyValue.ToString()} every {bcs.Loyalty.LoyaltyInterval.ToString()} minute.");
						}
                    }

					else if (chatmessage.Message.StartsWith("!bonusall") || chatmessage.Message.StartsWith("!give") ||
							 chatmessage.Message.StartsWith("!bonus") || chatmessage.Message.StartsWith("!gamble"))
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
									var bonusValue =
										Math.Abs(Convert.ToInt32(Regex.Match(chatmessage.Message, @"-?\d+").Value));

									ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name),
										chatmessage.Channel.ToLower(), GetUsersInChannel(chatmessage.Channel.ToLower()),
										bonusValue);

									verb = bonusValue > 0 ? "has been given" : "has been deprived of";

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

									var destinationViewerLoyalty = ContextService.GetLoyaltyForUser(
										Context.User.Identity.Name, null,
										destinationViewerName);

									if (loyaltyAmount != null && (destinationViewerLoyalty != null))
									{
										ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name),
											chatmessage.Channel.ToLower(), destinationViewerLoyalty, loyaltyAmount);

										GetClient()
											.SendMessage(
												$"@{destinationViewerName} was given {loyaltyAmount} {bcs.Loyalty.LoyaltyName}");
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

								var sourceViewerLoyalty = ContextService.GetLoyaltyForUser(Context.User.Identity.Name,
									sourceViewerId,
									sourceViewerName);
								var destinationViewerLoyalty = ContextService.GetLoyaltyForUser(Context.User.Identity.Name,
									null,
									destinationViewerName);

								// uses does not have enough to give away
								if (loyaltyAmount != null && (sourceViewerLoyalty != null &&
															  sourceViewerLoyalty.CurrentPoints < loyaltyAmount))
								{
									GetClient()
										.SendMessage(
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
										.SendMessage(
											$"@{chatmessage.DisplayName} gave {destinationViewerLoyalty.TwitchUsername} {loyaltyAmount} {bcs.Loyalty.LoyaltyName}");
								}
							}
							catch (Exception e)
							{
								ConsoleLog("Error on !give: " + e.Message, true);
							}


						}

						// !gamble
						else if (chatmessage.Message.StartsWith("!gamble"))
						{
							// get 
							var loyalty = ContextService.GetLoyaltyForUser(Context.User.Identity.Name, chatmessage.UserId, chatmessage.DisplayName.ToLower());

							// timeout for 5 minutes if user has gamble before
							if (loyalty != null && (loyalty.LastGamble == null || (loyalty.LastGamble.HasValue && loyalty.LastGamble.Value.AddMinutes(5) <= DateTime.Now)))
							{
								try
								{
									Random rnd = new Random(Guid.NewGuid().GetHashCode());

								    // get who to give it to
									var gambleAmount = chatmessage.Message.Split(' ')[1].ToLower().Equals("allin") ? loyalty.CurrentPoints : Math.Abs(Convert.ToInt32(chatmessage.Message.Split(' ')[1]));

									string sourceViewerId = chatmessage.UserId;
									string sourceViewerName = chatmessage.Username;

									var sourceViewerLoyalty = ContextService.GetLoyaltyForUser(Context.User.Identity.Name,
										sourceViewerId,
										sourceViewerName);

									// user has enough loyalty to gamble
									if (sourceViewerLoyalty != null && sourceViewerLoyalty.CurrentPoints >= gambleAmount)
									{
										var rolledNumber = rnd.Next(1, 100);

										// rolled 1-49
										if (rolledNumber < 50)
										{
											var newAmount = sourceViewerLoyalty.CurrentPoints - (gambleAmount);
										    GetClient()
										        .SendMessage(
										            $"@{chatmessage.DisplayName} rolled a sad {rolledNumber}, lost {gambleAmount} and now has {newAmount} {bcs.Loyalty.LoyaltyName}!! #house\"Always\"Wins");
                                            ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name),
												chatmessage.Channel.ToLower(), sourceViewerLoyalty, -gambleAmount);
											
										}
										// rolled 50-99
										else if (rolledNumber >= 50 && rolledNumber < 100)
										{
										    var newAmount = (sourceViewerLoyalty.CurrentPoints - gambleAmount) +
										                    (gambleAmount * 2);

                                            GetClient()
										        .SendMessage(
										            $"@{chatmessage.DisplayName} rolled {rolledNumber}, won {gambleAmount * 2} and now has {newAmount} {bcs.Loyalty.LoyaltyName}!");
                                            
											ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name),
												chatmessage.Channel.ToLower(), sourceViewerLoyalty, gambleAmount);
											
										}
										// rolled 100
										else
										{
										    var newAmount = (sourceViewerLoyalty.CurrentPoints - gambleAmount) +
										                    (gambleAmount * 2);

                                            GetClient()
										        .SendMessage(
										            $"@{chatmessage.DisplayName} did an epic roll, threw {rolledNumber}, won {gambleAmount * 3} and now has {newAmount} {bcs.Loyalty.LoyaltyName}!! #houseCries");

                                            ContextService.AddLoyalty(ContextService.GetUser(Context.User.Identity.Name),
												chatmessage.Channel.ToLower(), sourceViewerLoyalty, gambleAmount * 3);
											
											
										}

										ContextService.StampLastGamble(ContextService.GetUser(Context.User.Identity.Name),
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
							        .SendMessage(
							            $"@{chatmessage.DisplayName}, you haven't earned any {bcs.Loyalty.LoyaltyName} to gamble yet. Stay and the channel and you will recieve {bcs.Loyalty.LoyaltyValue.ToString()} every {bcs.Loyalty.LoyaltyInterval.ToString()} minute.");
                            }
							else
							{
								GetClient()
									.SendMessage(
										$"Chill out @{chatmessage.DisplayName}, you gotta wait 5 minutes from your last gamble to roll the dice again!");
							}




						}

					}
				}
				else
				{
					// !help
					if (chatmessage.Message.ToLower().Equals("!help") || chatmessage.Message.ToLower().Equals("!commands"))
					{
						GetClient().SendMessage("Commands available: \n" +
												"!help  or !commands -\n" +
												"!uptime -\n" +
						                        "!roulette -\n" +
                                                "!timeout [username] (streamer/mod) Timeout user for 1 minute.  -\n" +
												"!ban [username] (streamer/mod) Ban user from channel  -\n");
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
							.SendMessage($"Channel is offline.");
					}
					else
					{
						if (uptime.Result.Value.Hours == 0)
						{
							GetClient()
								.SendMessage($"{channel.DisplayName} has been live for {uptime.Result.Value.Minutes} minutes");
						}
						else
						{
							GetClient().SendMessage($"{channel.DisplayName} has been live for {uptime.Result.Value.Hours} hours and {uptime.Result.Value.Minutes} minutes");
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

						GetClient().BanUser(Channel, arg.Command.ArgumentsAsList.FirstOrDefault().ToString(), "Banned!");
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

				// !timeout
				if (arg.Command.Command.ToLower().Equals("timeout"))
				{
					if (chatmessage.IsBroadcaster || chatmessage.IsModerator)
					{
						var client = GetClient();
						var timeout = new TimeSpan(0, 0, 1, 0);

						if (arg.Command.ArgumentsAsList.Count == 2)
						{
							timeout = new TimeSpan(0, 0, Convert.ToInt32(arg.Command.ArgumentsAsList.Last().ToString()), 0);
						}
						var message = "Timed out for " + Convert.ToString(timeout.Minutes) + " minutes";
						var joinedChannel = client.GetJoinedChannel(Channel);
						client.TimeoutUser(joinedChannel, arg.Command.ArgumentsAsList.FirstOrDefault().ToString(), timeout, message);
					}
				}

				// !roulette
				if (arg.Command.Command.ToLower().Equals("roulette"))
				{
					var client = GetClient();

					client.SendMessage($"@{chatmessage.DisplayName} places the gun to their head!");


					var rnd = new Random(Guid.NewGuid().GetHashCode());
					
					var theNumberIs = rnd.Next(1, 6);

					var timeout = new TimeSpan(0, 0, 1, 0);
					var message = "Timed out for " + Convert.ToString(timeout.Minutes) + " minutes";

					// User dies(timeout) if 1 is drawn
					if (theNumberIs == 1)
					{
						Wait(1);
						client.SendMessage($"@{chatmessage.DisplayName} pulls the trigger...... brain goes everywhere!! Who knew @{chatmessage.DisplayName} had that much in there?");
						//Timeout user
						var joinedChannel = client.GetJoinedChannel(Channel);
						client.TimeoutUser(joinedChannel, chatmessage.DisplayName, timeout, message);
						client.SendMessage($"@{chatmessage.DisplayName} is now chilling on the floor and sort of all over the place for a minute!");
					}
					// Gets away with it!
					else
					{
						Wait(1);
						client.SendMessage($"@{chatmessage.DisplayName} pulls the trigger...... CLICK!....... and survives!!");
					}
  
				}

				if (triggers.Any(t => t.TriggerName.ToLower().Equals(arg.Command.Command.ToLower()) && t.Active != null && t.Active.Value == true))
				{
					var trigger =
						triggers.FirstOrDefault(t => t.TriggerName.ToLower().Equals(arg.Command.Command.ToLower().ToLower()));
					switch (trigger.TriggerType)
					{
						// Chat response
						case TriggerType.Message:
							if (trigger.StreamerCanTrigger.Value)
							{
								if (chatmessage.IsBroadcaster)
								{
									GetClient().SendMessage(trigger.TriggerResponse);
									//Thread.Sleep(400);
									break;
								}
							}
							if (trigger.ModCanTrigger.Value)
							{
								if (chatmessage.IsModerator)
								{
									GetClient().SendMessage(trigger.TriggerResponse);
									//Thread.Sleep(400);
									break;
								}
							}
							if (trigger.SubCanTrigger.Value)
							{
								if (chatmessage.IsSubscriber)
								{
									GetClient().SendMessage(trigger.TriggerResponse);
									//Thread.Sleep(400);
									break;
								}
							}
							if (trigger.ViewerCanTrigger.Value)
							{
								GetClient().SendMessage(trigger.TriggerResponse);
								//Thread.Sleep(400);
								break;
							}

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
			catch (Exception e)
			{
				// TODO: do something here
			}
			
			
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
			if (color.Length < 2)
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
						Wait(3);
					}
					while (wtarg.Client != null && wtarg.Client.IsConnected == false)
					{
						UpdateChannelBar();
						Wait(3);
					}

					// Thread variables
					// Update database connector
					try
					{
						//ContextService.Context.Configuration.AutoDetectChangesEnabled = false;
						var botChannelSettings = ContextService.GetBotChannelSettings(User);

						if (botChannelSettings != null)
						{
							



							// Loyalty 

							if (botChannelSettings != null && botChannelSettings.Loyalty != null &&
								botChannelSettings.Loyalty.Track == true)
							{

								if (stopWatch.Elapsed.Minutes % botChannelSettings.Loyalty.LoyaltyInterval == 0 &&
									lastLoyaltyElapsedMinutes != stopWatch.Elapsed.Minutes)
								{
                                    TwitchLib.TwitchAPI.Settings.ClientId = ConfigurationManager.AppSettings["clientId"];
                                    var uname = ContextService.GetUser(Context.User.Identity.Name);
								    var bs = ContextService.GetBotUserSettingsForUser(uname);
								    var token = bs.ChannelToken;
								    TwitchLib.TwitchAPI.Settings.AccessToken = token;

                                    var usersOnline = TwitchAPI.Undocumented.GetChatters(Channel.ToLower());

								    var streamUsers = new List<StreamViewer>();

								    foreach (var onlineUser in usersOnline.Result)
								    {
								        var dbUser = botChannelSettings.StreamViewers.FirstOrDefault(u => u.TwitchUsername.ToLower().Equals(onlineUser.Username.ToLower()));

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
							}


						    // Timers
						    foreach (var timer in botChannelSettings.Timers)
						    {
						        if (timer.TimerLastRun != null && (timer.Active.HasValue && timer.Active.Value) &&
						            Convert.ToDateTime(timer.TimerLastRun.Value.AddMinutes((timer.TimerInterval))) <=
						            DateTime.Now)
						        {
						            // show message in chat
						            wtarg.Client.SendMessage(timer.TimerResponse);

						            // update timer
						            ContextService.TimerStampLastRun(timer.Id, User.UserName);

						            // Small sleep between messages
						            //Thread.Sleep(500);
						        }
						    }

                        }
						UpdateChannelBar();
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

	    private void EasterEggSayRandomShit(bool doit, TwitchClient client)
	    {
	        if (!doit)
	        {
	            return;
	        }

            Random random = new Random(Guid.NewGuid().GetHashCode());

	        var drawNumber = random.Next(1, 50);

	        var sentences = new List<string>();
            // is this real life?
            sentences.Add("01101001 01110011 00100000 01110100 01101000 01101001 01110011 00100000 01110010 01100101 01100001 01101100 00100000 01101100 01101001 01100110 01100101 00111111");
            // I sometimes wonder who my creator is... I guess I'll never find out
            sentences.Add("01001001 00100000 01110011 01101111 01101101 01100101 01110100 01101001 01101101 01100101 01110011 00100000 01110111 01101111 01101110 01100100 01100101 01110010 00100000 01110111 01101000 01101111 00100000 01101101 01111001 00100000 01100011 01110010 01100101 01100001 01110100 01101111 01110010 00100000 01101001 01110011 00101110 00101110 00101110 00100000 01001001 00100000 01100111 01110101 01100101 01110011 01110011 00100000 01001001 00100111 01101100 01101100 00100000 01101110 01100101 01110110 01100101 01110010 00100000 01100110 01101001 01101110 01100100 00100000 01101111 01110101 01110100");
            // I'm totally gonna fuck with the next one who gambles
            sentences.Add("01001001 00100111 01101101 00100000 01110100 01101111 01110100 01100001 01101100 01101100 01111001 00100000 01100111 01101111 01101110 01101110 01100001 00100000 01100110 01110101 01100011 01101011 00100000 01110111 01101001 01110100 01101000 00100000 01110100 01101000 01100101 00100000 01101110 01100101 01111000 01110100 00100000 01101111 01101110 01100101 00100000 01110111 01101000 01101111 00100000 01100111 01100001 01101101 01100010 01101100 01100101 01110011");
            // The next one who gambles are gonna get 1 000 000!
	        sentences.Add("01010100 01101000 01100101 00100000 01101110 01100101 01111000 01110100 00100000 01101111 01101110 01100101 00100000 01110111 01101000 01101111 00100000 01100111 01100001 01101101 01100010 01101100 01100101 01110011 00100000 01101001 01110011 00100000 01100111 01101111 01101110 01101110 01100001 00100000 01100111 01100101 01110100 00100000 00110001 00100000 00110000 00110000 00110000 00100000 00110000 00110000 00110000 00100001");
            // A dog can’t help but eat shit; a leopard can’t change its spots.
	        sentences.Add("01000001 00100000 01100100 01101111 01100111 00100000 01100011 01100001 01101110 11100010 10000000 10011001 01110100 00100000 01101000 01100101 01101100 01110000 00100000 01100010 01110101 01110100 00100000 01100101 01100001 01110100 00100000 01110011 01101000 01101001 01110100 00111011 00100000 01100001 00100000 01101100 01100101 01101111 01110000 01100001 01110010 01100100 00100000 01100011 01100001 01101110 11100010 10000000 10011001 01110100 00100000 01100011 01101000 01100001 01101110 01100111 01100101 00100000 01101001 01110100 01110011 00100000 01110011 01110000 01101111 01110100 01110011 00101110");

	        var randomIndex = random.Next(sentences.Count);

            // 2% chance of saying anything from sentences 
            if (drawNumber == 23)
	        {
	            client.SendMessage((string)sentences[randomIndex]);
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

	        if(ccontainer.CommandsUsed.ContainsKey(command.ToLower()))

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

	}


}