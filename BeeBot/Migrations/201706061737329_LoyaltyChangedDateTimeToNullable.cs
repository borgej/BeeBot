namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class LoyaltyChangedDateTimeToNullable : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.StreamViewers", "FollowerSince", c => c.DateTime());
            AlterColumn("dbo.StreamViewers", "SubscriberSince", c => c.DateTime());
        }
        
        public override void Down()
        {
            AlterColumn("dbo.StreamViewers", "SubscriberSince", c => c.DateTime(nullable: false));
            AlterColumn("dbo.StreamViewers", "FollowerSince", c => c.DateTime(nullable: false));
        }
    }
}
