using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace YTBot.Models
{
    public class PlayListItem
    {

        public int Id { get; set; }
        public string VideoId { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string RequestedBy { get; set; }
        public DateTime RequestDate { get; set; }
        public bool? Deleted { get; set; }

        

    }
}