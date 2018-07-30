using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;

namespace BeeBot.Tests.Tests
{
    class TwitchTests
    {

            TwitchClient client;

            public void Bot()
            {
                ConnectionCredentials credentials = new ConnectionCredentials("borgej_Bot", "access_token");

                client = new TwitchClient();
                client.Initialize(credentials, "channel");

                client.OnJoinedChannel += onJoinedChannel;
                client.OnMessageReceived += onMessageReceived;
                client.OnWhisperReceived += onWhisperReceived;
                client.OnNewSubscriber += onNewSubscriber;
                client.OnConnected += Client_OnConnected;

                client.Connect();
            }
            private void Client_OnConnected(object sender, OnConnectedArgs e)
            {
                Console.WriteLine($"Connected to {e.AutoJoinChannel}");
            }
            private void onJoinedChannel(object sender, OnJoinedChannelArgs e)
            {
                Console.WriteLine("Hey guys! I am a bot connected via TwitchLib!");
                client.SendMessage(e.Channel, "Hey guys! I am a bot connected via TwitchLib!");
            }

            private void onMessageReceived(object sender, OnMessageReceivedArgs e)
            {
                if (e.ChatMessage.Message.Contains("badword"))
                    client.TimeoutUser(e.ChatMessage.Channel, e.ChatMessage.Username, TimeSpan.FromMinutes(30), "Bad word! 30 minute timeout!");
            }
            private void onWhisperReceived(object sender, OnWhisperReceivedArgs e)
            {
                if (e.WhisperMessage.Username == "my_friend")
                    client.SendWhisper(e.WhisperMessage.Username, "Hey! Whispers are so cool!!");
            }
            private void onNewSubscriber(object sender, OnNewSubscriberArgs e)
            {
                if (e.Subscriber.SubscriptionPlan == SubscriptionPlan.Prime)
                    client.SendMessage(e.Channel, $"Welcome {e.Subscriber.DisplayName} to the substers! You just earned 500 points! So kind of you to use your Twitch Prime on this channel!");
                else
                    client.SendMessage(e.Channel, $"Welcome {e.Subscriber.DisplayName} to the substers! You just earned 500 points!");
            }
        
    }
}

