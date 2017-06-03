using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using BeeBot.Models;
using BeeBot.Signalr;

namespace BeeBot.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        public HomeController()
        {

        }
        public ActionResult Index()
        {

            return View();
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

        public ActionResult Preferences()
        {
            var model = new PrefViewModel()
            {
                username = Session["botusername"]?.ToString() ?? "borgej_bot",
                password = Session["botpassword"]?.ToString() ?? "",
                channel = Session["botchannel"]?.ToString() ?? "",
            };

            return View(model);
        }

        public ActionResult PreferencesSave(string botusername, string passwordinput, string channel)
        {
            if (Session["botusername"] != null)
            {
                Session["botusername"] = botusername;
                Session["botpassword"] = passwordinput;
                Session["botchannel"] = channel;
                return Json(new { data = "1", message = "Saved!" }, JsonRequestBehavior.AllowGet);
            }
            else
            {
                Session["botusername"] = botusername;
                Session["botpassword"] = passwordinput;
                Session["botchannel"] = channel;
                return Json(new { data = "1", message = "Already set, resaved!" }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult Triggers()
        {
            return View();
        }
    }
}