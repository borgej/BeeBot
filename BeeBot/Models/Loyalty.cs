using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace YTBot.Models
{
    public class Loyalty
    {
        public int Id { get; set; }
        public String LoyaltyName { get; set; }
        /// <summary>
        /// Loyalty interval in minutes
        /// </summary>
        public int LoyaltyInterval { get; set; }
        public int LoyaltyValue { get; set; }
        public bool? Track { get; set; }
    }
}