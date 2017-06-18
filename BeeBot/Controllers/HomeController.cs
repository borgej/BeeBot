using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using BeeBot.Models;
using BeeBot.Signalr;
using Microsoft.ApplicationInsights.WindowsServer;
using Newtonsoft.Json;
using TwitchLib.Models.API.v5.Games;
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


        public HomeController()
        {
            ContextService = new ContextService();
            UserService = new UserService();
        }

        public ActionResult Index()
        {
            // get users bot settings
            var userBotSettings = ContextService.GetBotUserSettingsForUser(ContextService.GetUser(User.Identity.Name));
            var userLoyalty = ContextService.GetBotChannelSettings(ContextService.GetUser(User.Identity.Name)).Loyalty;
            var timers = ContextService.GetTimers(ContextService.GetUser(User.Identity.Name));
            var triggers = ContextService.GetTriggers(ContextService.GetUser(User.Identity.Name));

            var loyalty = userLoyalty ?? new Loyalty();
            var dashboardViewModel = new DashboardViewModel()
            {
                BotUserSettings = userBotSettings,
                LoyaltySettings = loyalty,
                Timers = timers,
                Triggers = triggers
            };

            // send user to bot preferences page if not set
            if (string.IsNullOrWhiteSpace(userBotSettings.BotChannel) || string.IsNullOrWhiteSpace(userBotSettings.BotUsername) ||
                string.IsNullOrWhiteSpace(userBotSettings.BotUsername))
            {
                RedirectToAction("Preferences", new {message = "Please set your bot account and channel information"});
            }

            // assert that
            return View(dashboardViewModel);
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public ActionResult Loyalty()
        {
            var botChannelSettings = ContextService.GetBotChannelSettings(ContextService.GetUser(User.Identity.Name));

            if (botChannelSettings.Loyalty != null)
            {
                return Json(JsonConvert.SerializeObject(botChannelSettings.Loyalty));
            }
            else
            {
                var loyalty = new Loyalty();
                return Json(JsonConvert.SerializeObject(loyalty));
            }
        }

        public ActionResult StreamInfoSave(string title, string game)
        {
            try
            {
                return Json(new { data = "1", message = "Stream title and game updated!" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception exception)
            {
                return Json(new { data = "-1", message = "Error on update: " + exception.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public ActionResult TimerSave(string timerId, string timerName, string timerMessage, string timerInterval, string timerActive)
        {
            int idInt = 0;

            if (!string.IsNullOrWhiteSpace(timerId))
            {
                idInt = Convert.ToInt32(timerId);
            }
            
            try
            {
                bool activeTimerBool = !string.IsNullOrEmpty(timerActive) && timerActive.Equals("1");
                var timer = new YTBot.Models.Timer()
                {
                    Id = idInt,
                    Active = activeTimerBool,
                    TimerInterval = Convert.ToInt32(timerInterval),
                    TimerName = timerName,
                    TimerResponse = timerMessage
                };

                var savedTimer = ContextService.SaveTimer(timer, User.Identity.Name);
                return Json(new { data = "1", message = "Saved timer", timerId = savedTimer.Id }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception e)
            {
                return Json(new { data = "-1", message = e.Message }, JsonRequestBehavior.AllowGet);
            }
            
        }

        [HttpPost]
        public ActionResult TimerDelete(string timerId)
        {
            try
            {
                var idInt = Convert.ToInt32(timerId);

                ContextService.DeleteTimer(idInt, User.Identity.Name);

                return Json(new { data = "1", message = "Deleted timer" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception e)
            {
                return Json(new { data = "-1", message = e.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult LoyaltySave(string loyaltyName, string loyaltyValue, string loyaltyInterval, string track)
        {
            try
            {
                var userName = User.Identity.Name;
                var user = ContextService.GetUser(userName);

                var loyalty = new Loyalty();
                if (track == null)
                {
                    loyalty.Track = false;
                }
                else
                {
                    if (Convert.ToInt32(track) == 1)
                    {
                        loyalty.Track = true;
                    }
                    else
                    {
                        loyalty.Track = false;
                    }
                }
                loyalty.LoyaltyName = loyaltyName;
                loyalty.LoyaltyInterval = Convert.ToInt32(loyaltyInterval);
                loyalty.LoyaltyValue = Convert.ToInt32(loyaltyValue);

                ContextService.SetLoyalty(user, loyalty);

                return Json(new { data = "1", message = "Saved loyalty" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception exception)
            {
                return Json(new { data = "-1", message = "Error on save: " + exception.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult Preferences(string message = null)
        {
            var userName = User.Identity.Name;
            var user = ContextService.GetUser(userName);

            var userBotSettings = ContextService.GetBotUserSettingsForUser(user);


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
            return View();
        }


        public async Task<JsonResult> GetGames(string phrase)
        {
            Game[] games = new Game[0];
            if (phrase.Length <= 3)
            {
                return Json(games);
            }

            TwitchLib.TwitchAPI.Settings.ClientId = "gokkk5ean0yksozv0ctvljwqpceuin";
            var gamesResult = await TwitchLib.TwitchAPI.Search.v5.SearchGames(phrase, null);

            var output = new List<object>();

            var settings = new JsonSerializerSettings();
            settings.StringEscapeHandling = StringEscapeHandling.EscapeHtml;

            return Json(gamesResult.Games);
        }

        public ActionResult TriggerDelete(string triggerId)
        {
            try
            {
                var idInt = Convert.ToInt32(triggerId);

                ContextService.DeleteTrigger(idInt, User.Identity.Name);

                return Json(new { data = "1", message = "Deleted trigger" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception e)
            {
                return Json(new { data = "-1", message = e.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult TriggerSave(string triggerId, string triggername, string triggerMessage, string mod, string viewer, string triggerActive)
        {
            int idInt = 0;

            if (!string.IsNullOrWhiteSpace(triggerId))
            {
                idInt = Convert.ToInt32(triggerId);
            }

            try
            {
                bool activeTriggerBool = !string.IsNullOrEmpty(triggerActive) && triggerActive.Equals("1");
                bool modsTriggerBool = !string.IsNullOrEmpty(mod) && mod.Equals("1");
                bool viewerTriggerBool = !string.IsNullOrEmpty(viewer) && viewer.Equals("1");
                var trigger = new YTBot.Models.Trigger()
                {
                    Id = idInt,
                    Active = activeTriggerBool,
                    ModCanTrigger = modsTriggerBool,
                    ViewerCanTrigger = viewerTriggerBool,
                    StreamerCanTrigger = true,
                    SubCanTrigger = viewerTriggerBool,
                    TriggerName = triggername,
                    TriggerType = TriggerType.Message,
                    TriggerResponse = triggerMessage
                };

                var savedTrigger = ContextService.SaveTrigger(trigger, User.Identity.Name);
                return Json(new { data = "1", message = "Saved trigger", triggerId = savedTrigger.Id }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception e)
            {
                return Json(new { data = "-1", message = e.Message }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}