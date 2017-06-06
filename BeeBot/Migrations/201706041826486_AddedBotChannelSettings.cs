namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddedBotChannelSettings : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.BotChannelSettings",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        StreamTitle = c.String(),
                        StreamGame = c.String(),
                        StreamComminuty = c.String(),
                        Loyalty_Id = c.Int(),
                        User_Id = c.String(maxLength: 128),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Loyalties", t => t.Loyalty_Id)
                .ForeignKey("dbo.AspNetUsers", t => t.User_Id)
                .Index(t => t.Loyalty_Id)
                .Index(t => t.User_Id);
            
            CreateTable(
                "dbo.Loyalties",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        LoyaltyName = c.String(),
                        LoyaltyInterval = c.Int(nullable: false),
                        LoyaltyPoints = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Timers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TimerName = c.String(),
                        TimerResponse = c.String(),
                        TimerInterval = c.Int(nullable: false),
                        Active = c.Boolean(),
                        BotChannelSettings_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.BotChannelSettings", t => t.BotChannelSettings_Id)
                .Index(t => t.BotChannelSettings_Id);
            
            CreateTable(
                "dbo.Triggers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        TriggerName = c.String(),
                        TruggerResponse = c.String(),
                        Active = c.Boolean(),
                        BotChannelSettings_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.BotChannelSettings", t => t.BotChannelSettings_Id)
                .Index(t => t.BotChannelSettings_Id);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.BotChannelSettings", "User_Id", "dbo.AspNetUsers");
            DropForeignKey("dbo.Triggers", "BotChannelSettings_Id", "dbo.BotChannelSettings");
            DropForeignKey("dbo.Timers", "BotChannelSettings_Id", "dbo.BotChannelSettings");
            DropForeignKey("dbo.BotChannelSettings", "Loyalty_Id", "dbo.Loyalties");
            DropIndex("dbo.Triggers", new[] { "BotChannelSettings_Id" });
            DropIndex("dbo.Timers", new[] { "BotChannelSettings_Id" });
            DropIndex("dbo.BotChannelSettings", new[] { "User_Id" });
            DropIndex("dbo.BotChannelSettings", new[] { "Loyalty_Id" });
            DropTable("dbo.Triggers");
            DropTable("dbo.Timers");
            DropTable("dbo.Loyalties");
            DropTable("dbo.BotChannelSettings");
        }
    }
}
