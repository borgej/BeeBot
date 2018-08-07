using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using TwitchLib.Client.Models;

namespace YTBot.Models
{
    public class Giveaway
    {
        public string Id { get; set; }
        public string Trigger { get; set; }
        public string Prize { get; set; }
        public DateTime EndsAt { get; set; }
        public bool Sub { get; set; }
        public bool Follower { get; set; }
        public bool Viewer { get; set; }
        public bool Mod { get; set; }

        /// <summary>
        /// All perticipants
        /// </summary>
        public List<StreamViewer> Participants { get; set; }
        /// <summary>
        /// One or multiple winners could be drawn
        /// </summary>
        public List<StreamViewer> Winners { get; set; }

        public Giveaway()
        {
            Id = Guid.NewGuid().ToString();
            Participants = new List<StreamViewer>();
            Winners = new List<StreamViewer>();
        }

        /// <summary>
        /// Check if user is able to enroll in giveaway
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public bool CanEnroll(StreamViewer user)
        {
            bool follower = Follower == user.Follower;
            bool sub = Sub == user.Subscriber;
            bool mod = Mod == user.Mod;

            return (follower | sub | mod);
        }

        /// <summary>
        /// Enroll user in giveaway
        /// </summary>
        /// <param name="viewer">StreamViewer to add to perticipants list</param>
        public void Enroll(StreamViewer viewer)
        {
            Participants.Add(viewer);
        }
    }
}