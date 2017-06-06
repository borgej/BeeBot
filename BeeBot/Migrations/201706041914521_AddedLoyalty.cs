namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddedLoyalty : DbMigration
    {
        public override void Up()
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
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.BotChannelSettings", t => t.BotChannelSettings_Id)
                .Index(t => t.BotChannelSettings_Id);
            
            AddColumn("dbo.Loyalties", "LoyaltyValue", c => c.Int(nullable: false));
            DropColumn("dbo.Loyalties", "LoyaltyPoints");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Loyalties", "LoyaltyPoints", c => c.Int(nullable: false));
            DropForeignKey("dbo.LoyaltyPoints", "BotChannelSettings_Id", "dbo.BotChannelSettings");
            DropIndex("dbo.LoyaltyPoints", new[] { "BotChannelSettings_Id" });
            DropColumn("dbo.Loyalties", "LoyaltyValue");
            DropTable("dbo.LoyaltyPoints");
        }
    }
}
