namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddedBannedWords : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.BannedWords",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Word = c.String(),
                        BotChannelSettings_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.BotChannelSettings", t => t.BotChannelSettings_Id)
                .Index(t => t.BotChannelSettings_Id);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.BannedWords", "BotChannelSettings_Id", "dbo.BotChannelSettings");
            DropIndex("dbo.BannedWords", new[] { "BotChannelSettings_Id" });
            DropTable("dbo.BannedWords");
        }
    }
}
