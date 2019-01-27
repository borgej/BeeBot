namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class TriggerResponseMoreCharacters : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.Triggers", "TriggerResponse", c => c.String(maxLength: 500, unicode: false));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.Triggers", "TriggerResponse", c => c.String(maxLength: 128, unicode: false));
        }
    }
}
