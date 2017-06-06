namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ChangedNameOfTriggers : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Loyalties", "Track", c => c.Boolean());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Loyalties", "Track");
        }
    }
}
