namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UpdatedModelsLoyalty : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.LoyaltyPoints", "BotChannelSettings_Id", "dbo.BotChannelSettings");
            DropIndex("dbo.LoyaltyPoints", new[] { "BotChannelSettings_Id" });
            CreateTable(
                "dbo.StreamViewers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TwitchUsername = c.String(),
                        TwitchUserId = c.String(),
                        CurrentPoints = c.Int(nullable: false),
                        AllTimePoints = c.Int(nullable: false),
                        Follower = c.Boolean(nullable: false),
                        FollowerSince = c.DateTime(nullable: false),
                        Subscriber = c.Boolean(nullable: false),
                        SubscriberSince = c.DateTime(nullable: false),
                        Channel = c.String(),
                        BotChannelSettings_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.BotChannelSettings", t => t.BotChannelSettings_Id)
                .Index(t => t.BotChannelSettings_Id);
            
            AddColumn("dbo.Triggers", "TriggerType", c => c.Int(nullable: false));
            DropTable("dbo.LoyaltyPoints");
        }
        
        public override void Down()
        {
            CreateTable(
                "dbo.LoyaltyPoints",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TwitchUsername = c.String(),
                        TwitchUserId = c.Int(nullable: false),
                        CurrentPoints = c.Int(nullable: false),
                        AllTimePoints = c.Int(nullable: false),
                        BotChannelSettings_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id);
            
            DropForeignKey("dbo.StreamViewers", "BotChannelSettings_Id", "dbo.BotChannelSettings");
            DropIndex("dbo.StreamViewers", new[] { "BotChannelSettings_Id" });
            DropColumn("dbo.Triggers", "TriggerType");
            DropTable("dbo.StreamViewers");
            CreateIndex("dbo.LoyaltyPoints", "BotChannelSettings_Id");
            AddForeignKey("dbo.LoyaltyPoints", "BotChannelSettings_Id", "dbo.BotChannelSettings", "Id");
        }
    }
}
