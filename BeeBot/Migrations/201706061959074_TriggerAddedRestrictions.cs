namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class TriggerAddedRestrictions : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Triggers", "StreamerCanTrigger", c => c.Boolean());
            AddColumn("dbo.Triggers", "ModCanTrigger", c => c.Boolean());
            AddColumn("dbo.Triggers", "SubCanTrigger", c => c.Boolean());
            AddColumn("dbo.Triggers", "ViewerCanTrigger", c => c.Boolean());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Triggers", "ViewerCanTrigger");
            DropColumn("dbo.Triggers", "SubCanTrigger");
            DropColumn("dbo.Triggers", "ModCanTrigger");
            DropColumn("dbo.Triggers", "StreamerCanTrigger");
        }
    }
}
