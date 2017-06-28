namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class TriggerChangedResponseMessageObjectName : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Triggers", "TriggerResponse", c => c.String());
            DropColumn("dbo.Triggers", "TruggerResponse");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Triggers", "TruggerResponse", c => c.String());
            DropColumn("dbo.Triggers", "TriggerResponse");
        }
    }
}
