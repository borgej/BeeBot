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
using YoutubeExplode;
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
                var songThumbnails = new List<string>();
                var client = new YoutubeClient();
                foreach (var song in songRequests)
                {
                    var video = await client.GetVideoAsync(song.VideoId);
                    songThumbnails.Add(video.Thumbnails.LowResUrl);
                }

                ViewBag.Thumbnails = songThumbnails;
                return View(songRequests);
            }
            catch (Exception e)
            {
                ViewBag.Error = e.Message;
                return View(model);
            }

            return View(model);
        }

        public ActionResult KillStat(string channel, bool? dark = false)
        {
            ViewBag.channel = channel;
            ViewBag.DarkCss = dark;
            var killStats = new KillStat();

            try
            {
                var user = ContextService.GetUserFromChannelname(channel);
                var bcs = ContextService.GetBotChannelSettings(user);
                return View(bcs.KillStats);
            }
            catch (Exception e)
            {
                ViewBag.Error = e.Message;
                return View(killStats);
            }
        }

        public ActionResult KillStatAjax(string channel)
        {
            ViewBag.channel = channel;
            var killStats = new KillStat();

            try
            {
                var user = ContextService.GetUserFromChannelname(channel);
                var bcs = ContextService.GetBotChannelSettings(user);
                return Json(new {kills = bcs.KillStats.Kills, deaths = bcs.KillStats.Deaths, squad = bcs.KillStats.SquadKills}, JsonRequestBehavior.AllowGet);
            }
            catch (Exception e)
            {
                ViewBag.Error = e.Message;
                return Json(new { kills = killStats.Kills, deaths = killStats.Deaths, squad = killStats.SquadKills }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
