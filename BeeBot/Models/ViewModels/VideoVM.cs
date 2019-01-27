using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace YTBot.Models.ViewModels
{
    public class VideoVm
    {
        public string Id { get; set; }
        public string Url { get; set; }
        public string Title { get; set; }
        public TimeSpan Length { get; set; }
    }
}