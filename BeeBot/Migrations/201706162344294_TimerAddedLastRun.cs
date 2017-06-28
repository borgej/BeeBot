namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class TimerAddedLastRun : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Timers", "TimerLastRun", c => c.DateTime());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Timers", "TimerLastRun");
        }
    }
}
