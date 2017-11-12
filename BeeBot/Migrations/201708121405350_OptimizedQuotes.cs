namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class OptimizedQuotes : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.Quotes", "Trigger_Id", "dbo.Triggers");
            DropIndex("dbo.Quotes", new[] { "Trigger_Id" });
            AddColumn("dbo.BotChannelSettings", "QuotesActive", c => c.Boolean());
            AddColumn("dbo.Quotes", "Nr", c => c.Int(nullable: false));
            AddColumn("dbo.Quotes", "BotChannelSettings_Id", c => c.Int());
            CreateIndex("dbo.Quotes", "BotChannelSettings_Id");
            AddForeignKey("dbo.Quotes", "BotChannelSettings_Id", "dbo.BotChannelSettings", "Id");
            DropColumn("dbo.Quotes", "Trigger_Id");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Quotes", "Trigger_Id", c => c.Int());
            DropForeignKey("dbo.Quotes", "BotChannelSettings_Id", "dbo.BotChannelSettings");
            DropIndex("dbo.Quotes", new[] { "BotChannelSettings_Id" });
            DropColumn("dbo.Quotes", "BotChannelSettings_Id");
            DropColumn("dbo.Quotes", "Nr");
            DropColumn("dbo.BotChannelSettings", "QuotesActive");
            CreateIndex("dbo.Quotes", "Trigger_Id");
            AddForeignKey("dbo.Quotes", "Trigger_Id", "dbo.Triggers", "Id");
        }
    }
}
