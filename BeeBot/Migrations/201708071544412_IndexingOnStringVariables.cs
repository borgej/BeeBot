namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class IndexingOnStringVariables : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.BannedWords", "Word", c => c.String(maxLength: 128, unicode: false));
            AlterColumn("dbo.StreamViewers", "TwitchUsername", c => c.String(maxLength: 128, unicode: false));
            AlterColumn("dbo.StreamViewers", "TwitchUserId", c => c.String(maxLength: 128, unicode: false));
            AlterColumn("dbo.StreamViewers", "Channel", c => c.String(maxLength: 128, unicode: false));
            AlterColumn("dbo.Timers", "TimerName", c => c.String(maxLength: 128, unicode: false));
            AlterColumn("dbo.Timers", "TimerResponse", c => c.String(maxLength: 512, unicode: false));
            AlterColumn("dbo.Triggers", "TriggerName", c => c.String(maxLength: 128, unicode: false));
            AlterColumn("dbo.Triggers", "TriggerResponse", c => c.String(maxLength: 128, unicode: false));
            AlterColumn("dbo.Quotes", "QuoteMsg", c => c.String(maxLength: 128, unicode: false));
            AlterColumn("dbo.Quotes", "QuoteBy", c => c.String(maxLength: 128, unicode: false));
            AlterColumn("dbo.BotUserSettings", "BotUsername", c => c.String(maxLength: 128, unicode: false));
            AlterColumn("dbo.BotUserSettings", "BotPassword", c => c.String(maxLength: 128, unicode: false));
            AlterColumn("dbo.BotUserSettings", "BotChannel", c => c.String(maxLength: 128, unicode: false));
            AlterColumn("dbo.BotUserSettings", "ChannelToken", c => c.String(maxLength: 128, unicode: false));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.BotUserSettings", "ChannelToken", c => c.String());
            AlterColumn("dbo.BotUserSettings", "BotChannel", c => c.String());
            AlterColumn("dbo.BotUserSettings", "BotPassword", c => c.String());
            AlterColumn("dbo.BotUserSettings", "BotUsername", c => c.String());
            AlterColumn("dbo.Quotes", "QuoteBy", c => c.String());
            AlterColumn("dbo.Quotes", "QuoteMsg", c => c.String());
            AlterColumn("dbo.Triggers", "TriggerResponse", c => c.String());
            AlterColumn("dbo.Triggers", "TriggerName", c => c.String());
            AlterColumn("dbo.Timers", "TimerResponse", c => c.String());
            AlterColumn("dbo.Timers", "TimerName", c => c.String());
            AlterColumn("dbo.StreamViewers", "Channel", c => c.String());
            AlterColumn("dbo.StreamViewers", "TwitchUserId", c => c.String());
            AlterColumn("dbo.StreamViewers", "TwitchUsername", c => c.String());
            AlterColumn("dbo.BannedWords", "Word", c => c.String());
        }
    }
}
