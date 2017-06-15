using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BeeBot.Models
{
    public class PrefViewModel
    {
        public string username { get; set; }
        public string password { get; set; }
        public string channel { get; set; }

        public string channelToken { get; set; }

    }
}