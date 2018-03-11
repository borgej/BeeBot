using System;
using System.Collections.Generic;

namespace YTBot.Models
{
    public class RussianRoulette
    {
        public List<StreamViewer> Players { get; set; }
        public List<StreamViewer> DeadPlayers { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime StartOneMinReminder { get; set; }
        public bool StartOneMinReminderAlerted { get; set; }
        public DateTime StartTenSecReminder { get; set; }
        public bool StartTenSecReminderAlerted { get; set; }
        public int BuyIn { get; set; }
        public int TotalBet { get; set; }

        public StreamViewer Winner { get; set; }
        public bool Started { get; set; }
        public bool Finished { get; set; }



        public RussianRoulette()
        {
            Players = new List<StreamViewer>();
            DeadPlayers = new List<StreamViewer>();

            StartedAt = DateTime.Now;
            StartAt = DateTime.Now.AddMinutes(2);
            StartOneMinReminder = StartAt.AddMinutes(-1);
            StartTenSecReminder = StartAt.AddSeconds(-10);

            StartOneMinReminderAlerted = false;
            StartTenSecReminderAlerted = false;

            Started = false;
            Finished = false;
        }
    }
}