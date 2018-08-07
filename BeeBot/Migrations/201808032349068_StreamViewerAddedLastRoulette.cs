namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class StreamViewerAddedLastRoulette : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.StreamViewers", "LastRoulette", c => c.DateTime());
        }
        
        public override void Down()
        {
            DropColumn("dbo.StreamViewers", "LastRoulette");
        }
    }
}
