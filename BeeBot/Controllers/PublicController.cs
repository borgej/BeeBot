using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Mvc;
using TwitchLib.Api;
using YTBot.Models;
using YTBot.Services;

namespace YTBot.Controllers
{
    public class PublicController : Controller
    {
        private ContextService ContextService { get; set; }
        private TwitchAPI Api { get; set; }
        public PublicController()
        {
            ContextService = new ContextService();
            var clientId = ConfigurationManager.AppSettings["clientId"];
            var clientSecret = ConfigurationManager.AppSettings["clientSecret"];
            Api = new TwitchAPI();
            Api.Settings.AccessToken = clientSecret;
            Api.Settings.ClientId = clientId;
        }

        public async Task<ActionResult> GetPlaylist(string channel)
        {
            var model = new List<PlayListItem>();
            ViewBag.channel = channel;

            

            try
            {
                var user = ContextService.GetUserFromChannelname(channel);
                var bcs = ContextService.GetBotChannelSettings(user);
                var bus = ContextService.GetBotUserSettingsForUser(user);
                

                var channelMeta = await Api.Channels.v5.GetChannelAsync(bus.ChannelToken);

                ViewBag.ChannelProfileBannerUrl = channelMeta.ProfileBanner;
                ViewBag.ChannelLogo = channelMeta.Logo;
                var songRequests = bcs.SongRequests.Where(s => s.Deleted == false );
                return View(songRequests);
            }
            catch (Exception e)
            {
                ViewBag.Error = e.Message;
                return View(model);
            }

            return View(model);
        }
    }
}
