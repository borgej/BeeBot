namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class LoyaltyAddedLastGamble : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.StreamViewers", "LastGamble", c => c.DateTime());
        }
        
        public override void Down()
        {
            DropColumn("dbo.StreamViewers", "LastGamble");
        }
    }
}
