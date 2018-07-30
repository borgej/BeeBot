using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Newtonsoft.Json;

namespace BeeBot.Models
{
    public class BotStatusVM
    {
        [JsonProperty("message")]
        public string message { get; set; }

        [JsonProperty("info")]
        public string info { get; set; }

        [JsonProperty("warning")]
        public string warning { get; set; }

        [JsonProperty("connected")]
        public bool connected { get; set; }
    }
}