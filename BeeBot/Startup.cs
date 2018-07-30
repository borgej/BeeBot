using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(BeeBot.Startup))]
namespace BeeBot
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);


        }
    }
}
