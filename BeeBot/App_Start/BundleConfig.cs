using System.Web;
using System.Web.Optimization;

namespace BeeBot
{
    public class BundleConfig
    {
        // For more information on bundling, visit https://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js"));

            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                        "~/Scripts/jquery.validate*"));

            // Use the development version of Modernizr to develop with and learn from. Then, when you're
            // ready for production, use the build tool at https://modernizr.com to pick only the tests you need.
            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                "~/Scripts/tether/tether.js",
                "~/Scripts/bootstrap.js",
                "~/Scripts/respond.js",
                "~/Scripts/chartist.js",
                "~/Scripts/jquery.easy-autocomplete.js",
                "~/Scripts/jquery.amaran.min.js"));

            bundles.Add(new ScriptBundle("~/bundles/signalr").Include(
                "~/Scripts/jquery.signalR-2.2.2.*"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                "~/Content/bootstrap.css",
                "~/Content/tether/tether.css",
                "~/Content/chartist.css",
                "~/Content/font-awesome.css",
                "~/Content/font-awesome-animation.min.css",
                "~/Content/site.css",
                "~/Content/amaran.min.css",
                "~/Content/animate.min.css",
                "~/Content/easy-autocomplete.css",
                "~/Content/easy-autocomplete.themes.css"));

            System.Web.Optimization.BundleTable.EnableOptimizations = false;

            foreach (var bundle in BundleTable.Bundles)
            {
                bundle.Transforms.Clear();
            }
        }
    }
}
