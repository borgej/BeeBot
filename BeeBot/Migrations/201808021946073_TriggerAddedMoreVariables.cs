namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class TriggerAddedMoreVariables : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Triggers", "FollowerCanTrigger", c => c.Boolean());
            AddColumn("dbo.Triggers", "VideoUrl", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Triggers", "VideoUrl");
            DropColumn("dbo.Triggers", "FollowerCanTrigger");
        }
    }
}
