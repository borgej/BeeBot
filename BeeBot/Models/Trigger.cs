using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using TwitchLib.Client.Models;

namespace YTBot.Models
{
    public class Trigger
    {
        public int Id { get; set; }
        [Column(TypeName = "VARCHAR")]
        [StringLength(128)]
        public string TriggerName { get; set; }
        public bool? Active { get; set; }
        public TriggerType TriggerType { get; set; }
        [Column(TypeName = "VARCHAR")]
        [StringLength(500)]
        public string TriggerResponse { get; set; }
        
        // Trigger restrictions
        public bool? StreamerCanTrigger { get; set; }
        public bool? ModCanTrigger { get; set; }
        public bool? SubCanTrigger { get; set; }
        public bool? ViewerCanTrigger { get; set; }
        public bool? FollowerCanTrigger { get; set; }

        // VideoOnDemand trigger video url
        public string VideoUrl { get; set; }

        public bool CanTrigger(StreamViewer user, ChatCommand command)
        {
            
            if (command.ChatMessage.IsBroadcaster)
            {
                return true;
            }

            bool viewer = Convert.ToBoolean(ViewerCanTrigger);
            if (viewer)
            {
                return true;
            }
            if (FollowerCanTrigger == true)
            {
                if (user.Follower)
                    return true;
            }
            if (SubCanTrigger == true)
            {
                if (user.Subscriber)
                    return true;
            }
            if (ModCanTrigger == true)
            {
                if (user.Mod)
                    return true;
            }

            return false;
        }
    }

    
    /// <summary>
    /// Trigger Type
    /// </summary>
    public enum TriggerType
    {
        Message = 0,
        Quote = 1,
        Stat = 2,
        Game = 3,
        Loyalty = 4,
        VideoOnDemand = 5,
        PlayList = 6,
        BuiltIn = 9,
    }


}