namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class PlaylistItemAddedDuration : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PlayListItems", "Duration", c => c.Time(precision: 7));
        }
        
        public override void Down()
        {
            DropColumn("dbo.PlayListItems", "Duration");
        }
    }
}
