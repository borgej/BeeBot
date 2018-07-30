using System;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            Bot.Go();
            Console.ReadLine();
        }
    }

    static class Bot
    {
        static TwitchClient client;
        public static async void Go() {
            

            ConnectionCredentials credentials = new ConnectionCredentials("borgej_bot", "oauth:34o7axgn0pjg4kmsv4zokmp3dntles");

            client = new TwitchClient();
            client.Initialize(credentials, "borge_jakobsen");

            client.OnJoinedChannel += onJoinedChannel;
            client.OnMessageReceived += onMessageReceived;
            client.OnWhisperReceived += onWhisperReceived;
            client.OnNewSubscriber += onNewSubscriber;
            client.OnConnected += Client_OnConnected;

            client.Connect();
            var clientId = "gokkk5ean0yksozv0ctvljwqpceuin";
            var clientSecret = "1e0p2pttaf7072fwp67u0jtbj3emmz";
            var channelToken = "di2a0g95f02avo4x7fso2hjc2b2nea";
            var api = new TwitchAPI();
            api.Settings.AccessToken = clientSecret;
            api.Settings.ClientId = clientId;

            var channelId = await api.Channels.v5.GetChannelAsync(channelToken);
            var channelSubsData = await api.Channels.v5.GetAllSubscribersAsync(channelId.Id, channelToken);
            api.Channels.v5.Get
            await api.Channels.v5.UpdateChannelAsync(channelId.Id, "CodeSunday!", "Visual Studio 2017", "0", null, channelToken);

            Console.WriteLine($"Subs: {channelSubsData.Count}");
        }
        


        private static void Client_OnConnected(object sender, OnConnectedArgs e)
        {
            Console.WriteLine($"Connected to {e.AutoJoinChannel}");
        }
        private static void onJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            Console.WriteLine("Hey guys! I am a bot connected via TwitchLib!");
            client.SendMessage(e.Channel, "Hey guys! I am a bot connected via TwitchLib!");
        }

        private static void onMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            if (e.ChatMessage.Message.Contains("badword"))
                client.TimeoutUser(e.ChatMessage.Channel, e.ChatMessage.Username, TimeSpan.FromMinutes(30), "Bad word! 30 minute timeout!");
            client.SendMessage(e.ChatMessage.Channel, "Status: " + client.IsConnected.ToString());
        }
        private static void onWhisperReceived(object sender, OnWhisperReceivedArgs e)
        {
            if (e.WhisperMessage.Username == "my_friend")
                client.SendWhisper(e.WhisperMessage.Username, "Hey! Whispers are so cool!!");
        }
        private static void onNewSubscriber(object sender, OnNewSubscriberArgs e)
        {
            if (e.Subscriber.SubscriptionPlan == SubscriptionPlan.Prime)
                client.SendMessage(e.Channel, $"Welcome {e.Subscriber.DisplayName} to the substers! You just earned 500 points! So kind of you to use your Twitch Prime on this channel!");
            else
                client.SendMessage(e.Channel, $"Welcome {e.Subscriber.DisplayName} to the substers! You just earned 500 points!");
        }
    }
}