using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

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
        [StringLength(128)]
        public string TriggerResponse { get; set; }
        
        // Trigger restrictions
        public bool? StreamerCanTrigger { get; set; }
        public bool? ModCanTrigger { get; set; }
        public bool? SubCanTrigger { get; set; }
        public bool? ViewerCanTrigger { get; set; }
        public bool? FollowerCanTrigger { get; set; }

        // VideoOnDemand trigger video url
        public string VideoUrl { get; set; }
        

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
        BuiltIn = 9,
    }
}