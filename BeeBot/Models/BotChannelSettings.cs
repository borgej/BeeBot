using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using BeeBot.Models;


namespace YTBot.Models
{
    public class BotChannelSettings
    {
        public int Id { get; set; }
        public string StreamTitle { get; set; }
        public string StreamGame { get; set; }
        public string StreamComminuty { get; set; }
        
        public virtual Loyalty Loyalty { get; set; }

        public List<StreamViewer> StreamViewers { get; set; }

        public virtual List<Trigger> Triggers { get; set; }

        public virtual List<Timer> Timers { get; set; }
        public virtual List<BannedWord> BannedWords { get; set; }
        public virtual List<Quote> Quotes {get; set; }
        public virtual List<PlayListItem> SongRequests { get; set; }
        public bool? QuotesActive {get;set;}
        public virtual ApplicationUser User { get; set; }
    }

    

    

    

    



    
}