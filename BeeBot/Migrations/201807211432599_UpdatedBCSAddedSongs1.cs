namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UpdatedBCSAddedSongs1 : DbMigration
    {
        public override void Up()
        {
            RenameColumn(table: "dbo.PlayListItems", name: "Channel_Id", newName: "BotChannelSettings_Id");
            RenameIndex(table: "dbo.PlayListItems", name: "IX_Channel_Id", newName: "IX_BotChannelSettings_Id");
        }
        
        public override void Down()
        {
            RenameIndex(table: "dbo.PlayListItems", name: "IX_BotChannelSettings_Id", newName: "IX_Channel_Id");
            RenameColumn(table: "dbo.PlayListItems", name: "BotChannelSettings_Id", newName: "Channel_Id");
        }
    }
}
