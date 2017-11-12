using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using BeeBot.Models;

namespace BeeBot.Models
{
    public class BotUserSettings
    {
        public int Id { get; set; }
        [Column(TypeName = "VARCHAR")]
        [StringLength(128)]
        public string BotUsername { get; set; }
        [Column(TypeName = "VARCHAR")]
        [StringLength(128)]
        public string BotPassword { get; set; }
        [Column(TypeName = "VARCHAR")]
        [StringLength(128)]
        public string BotChannel { get; set; }
        [Column(TypeName = "VARCHAR")]
        [StringLength(128)]
        public string ChannelToken { get; set; }

        public virtual ApplicationUser User { get; set; }
    }
}