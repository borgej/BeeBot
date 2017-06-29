using System;

namespace BeeBot.Models
{
    public class StreamStatusVM
    {
        public string Channel { get; set; }
        public bool Online { get; set; }
        public TimeSpan? Uptime { get; set; }
        public string Game { get; set; }
        public string Title { get; set; }
        public bool Mature { get; set; }
        public int Delay { get; set; }
    }
}