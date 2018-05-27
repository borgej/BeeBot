namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class PlayListItemAddedVideoId : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PlayListItems", "VideoId", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.PlayListItems", "VideoId");
        }
    }
}
