using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace YTBot.Models
{
    public class StreamViewer
    {
        public int Id { get; set; }
        [Column(TypeName = "VARCHAR")]
        [StringLength(128)]
        public string TwitchUsername { get; set; }
        [Column(TypeName = "VARCHAR")]
        [StringLength(128)]
        public string TwitchUserId { get; set; }
        public int CurrentPoints { get; set; }
        public int AllTimePoints { get; set; }
        public bool Follower { get; set; }
        public DateTime? FollowerSince { get; set; }
        public bool Subscriber { get; set; }
        public bool Mod { get; set; }
        public DateTime? SubscriberSince { get; set; }
        [Column(TypeName = "VARCHAR")]
        [StringLength(128)]
        public string Channel { get; set; }
        public DateTime? LastGamble { get; set; }
    }
}