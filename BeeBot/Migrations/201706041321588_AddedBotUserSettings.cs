namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddedBotUserSettings : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.BotUserSettings",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        BotUsername = c.String(),
                        BotPassword = c.String(),
                        BotChannel = c.String(),
                        User_Id = c.String(maxLength: 128),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AspNetUsers", t => t.User_Id)
                .Index(t => t.User_Id);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.BotUserSettings", "User_Id", "dbo.AspNetUsers");
            DropIndex("dbo.BotUserSettings", new[] { "User_Id" });
            DropTable("dbo.BotUserSettings");
        }
    }
}
