using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using BeeBot.Models;
using Microsoft.AspNet.Identity.EntityFramework;
using YTBot.Models;

namespace YTBot.Context
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext()
            : base("DefaultConnection", throwIfV1Schema: false)
        {
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }



        public DbSet<BotUserSettings> BotUserSettings { get; set; }
        public DbSet<BotChannelSettings> BotChannelSettings { get; set; }
        public DbSet<StreamViewer> Viewers { get; set; }
        public DbSet<Timer> Timers { get; set; }
        public DbSet<Trigger> Triggers { get; set; }
        public DbSet<Quote> Quotes { get; set; }
        public DbSet<BannedWord> BannedWords { get; set; }
        public DbSet<PlayListItem> PlaylistItems { get; set; }


    }
}