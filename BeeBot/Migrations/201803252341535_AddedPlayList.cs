namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddedPlayList : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PlayListItems",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Title = c.String(),
                        Url = c.String(),
                        RequestedBy = c.String(),
                        RequestDate = c.DateTime(nullable: false),
                        Deleted = c.Boolean(),
                        Channel_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.BotChannelSettings", t => t.Channel_Id)
                .Index(t => t.Channel_Id);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.PlayListItems", "Channel_Id", "dbo.BotChannelSettings");
            DropIndex("dbo.PlayListItems", new[] { "Channel_Id" });
            DropTable("dbo.PlayListItems");
        }
    }
}
