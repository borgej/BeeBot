using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using BeeBot.Models;
using BeeBot.Signalr;
using Newtonsoft.Json;
using TwitchLib.Api;
using TwitchLib.Api.Models.v5.Games;
using TwitchLib.Client;
using YTBot.Migrations;
using YTBot.Models;
using YTBot.Models.ViewModels;
using YTBot.Services;

namespace BeeBot.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private ContextService ContextService { get; set; }
        private UserService UserService { get; set; }

        public TwitchClient Client { get; set; }

        public HomeController()
        {
            ContextService = new ContextService();
            UserService = new UserService();

        }

        public ActionResult Index()
        {

            // get users bot settings
            var userBotSettings = ContextService.GetBotUserSettingsForUser(ContextService.GetUser(User.Identity.Name));

            // send user to bot preferences page if not set
            if (string.IsNullOrWhiteSpace(userBotSettings.BotChannel) || string.IsNullOrWhiteSpace(userBotSettings.BotUsername) ||
                string.IsNullOrWhiteSpace(userBotSettings.BotUsername))
            {
                RedirectToAction("Preferences", new {message = "Please set your bot account and channel information"});
            }

            ViewBag.Channel = userBotSettings.BotChannel;

            // assert that
            return View("Control", userBotSettings);
        }

        public ActionResult GetUsername()
        {
            var userBotSettings = ContextService.GetBotUserSettingsForUser(ContextService.GetUser(User.Identity.Name));
            return Content(userBotSettings.BotUsername);
        }

        public ActionResult GetPassword()
        {
            var userBotSettings = ContextService.GetBotUserSettingsForUser(ContextService.GetUser(User.Identity.Name));
            return Content(userBotSettings.BotPassword);
        }

        public ActionResult GetChannel()
        {
            var userBotSettings = ContextService.GetBotUserSettingsForUser(ContextService.GetUser(User.Identity.Name));
            return Content(userBotSettings.BotChannel);
        }

        public ActionResult LoyaltyHtml()
        {
            var userLoyalty = ContextService.GetBotChannelSettings(ContextService.GetUser(User.Identity.Name)).Loyalty;

            return View(userLoyalty);
        }





        public ActionResult Preferences(string message = null)
        {
            var userName = User.Identity.Name;
            var user = ContextService.GetUser(userName);

            var userBotSettings = ContextService.GetBotUserSettingsForUser(user);
            ViewBag.Channel = userBotSettings.BotChannel;

            var model = new PrefViewModel()
            {
                username = userBotSettings.BotUsername,
                password = userBotSettings.BotPassword,
                channel = userBotSettings.BotChannel ,
                channelToken = userBotSettings.ChannelToken
            };

            ViewBag.Message = message;

            return View(model);
        }

        public ActionResult PreferencesSave(string botusername, string passwordinput, string channel, string channelToken)
        {

            try
            {
                var botUserSettings = new BotUserSettings()
                {
                    User = ContextService.GetUser(User.Identity.Name),
                    BotUsername = botusername ?? "",
                    BotPassword = passwordinput ?? "",
                    BotChannel = channel ?? "",
                    ChannelToken = channelToken ?? ""
                };

                ContextService.SetBotUserSettingsForUser(botUserSettings);


                return Json(new { data = "1", message = "Saved!" }, JsonRequestBehavior.AllowGet);

            }
            catch (Exception e)
            {
                return Json(new { data = "-1", message = "Error saving: " + e.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult Triggers()
        {
            var triggers = ContextService.GetTriggers(ContextService.GetUser(User.Identity.Name)).OrderBy(m => m.TriggerName);

            return View(triggers);
        }


        public async Task<JsonResult> GetGames(string phrase)
        {
            Game[] games = new Game[0];
            if (phrase.Length <= 3)
            {
                return Json(games);
            }
            var user = ContextService.GetUser(HttpContext.User.Identity.Name);
            var bs = ContextService.GetBotUserSettingsForUser(user);

            var twitchApi = new TwitchAPI();
            twitchApi.Settings.ClientId = ConfigurationManager.AppSettings["clientId"];
            twitchApi.Settings.AccessToken = bs.ChannelToken;
            var gamesResult = await twitchApi.Search.v5.SearchGamesAsync(phrase, null);

            var output = new List<object>();

            var settings = new JsonSerializerSettings();
            settings.StringEscapeHandling = StringEscapeHandling.EscapeHtml;

            return Json(gamesResult.Games);
        }

        #region Refacrored

        public ActionResult Control()
        {
            var userBotSettings = ContextService.GetBotUserSettingsForUser(ContextService.GetUser(User.Identity.Name));

            return View(userBotSettings);
        }

        public ActionResult Stream()
        {
            var userBotSettings = ContextService.GetBotUserSettingsForUser(ContextService.GetUser(User.Identity.Name));

            ViewBag.Channel = userBotSettings.BotChannel;
            return View();
        }

        #endregion

        public ActionResult ChatOptions()
        {
            var bannedWords = ContextService.GetBotChannelSettings(ContextService.GetUser(User.Identity.Name)).BannedWords;
            var userBotSettings = ContextService.GetBotUserSettingsForUser(ContextService.GetUser(User.Identity.Name));

            if(bannedWords == null)
                bannedWords = new List<BannedWord>();
            var bannedWordsString = new List<string>();

            foreach (var bannedWord in bannedWords)
            {
                bannedWordsString.Add(bannedWord.Word);
            }

            ViewBag.BannedWords = bannedWordsString;

            return View(userBotSettings);
        }



        public ActionResult Timers()
        {
            var timers = ContextService.GetTimers(ContextService.GetUser(User.Identity.Name)).OrderBy(m => m.TimerName);

            return View(timers);
        }

        public ActionResult Polls()
        {
            return View();
        }

        public ActionResult SongRequests()
        {
            var userBotSettings = ContextService.GetBotUserSettingsForUser(ContextService.GetUser(User.Identity.Name));

            ViewBag.Channel = userBotSettings.BotChannel;
            return View();
        }

        public ActionResult ChannelName()
        {
            var userBotSettings = ContextService.GetBotUserSettingsForUser(ContextService.GetUser(User.Identity.Name));

            return Content(userBotSettings.BotChannel);
        }

        public ActionResult ChatLog()
        {
            var userBotSettings = ContextService.GetBotUserSettingsForUser(ContextService.GetUser(User.Identity.Name));

            ViewBag.Channel = userBotSettings.BotChannel;

            return View(userBotSettings);
        }

        public ActionResult ChatStats()
        {
            var userBotSettings = ContextService.GetBotUserSettingsForUser(ContextService.GetUser(User.Identity.Name));

            ViewBag.Channel = userBotSettings.BotChannel;

            return View(userBotSettings);
        }

        public ActionResult LogOut()
        {
            var userBotSettings = ContextService.GetBotUserSettingsForUser(ContextService.GetUser(User.Identity.Name));

            ViewBag.Channel = userBotSettings.BotChannel;

            return View(userBotSettings);
        }
    }
}