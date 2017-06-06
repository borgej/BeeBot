using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace YTBot.Models.ViewModels
{
    public class LoyaltyViewModel
    {
        public string LoyaltyName { get; set; }
        public int LoyaltyValue { get; set; }
        public int LoyaltyInterval { get; set; }

        public bool Track { get; set; }
    }
}