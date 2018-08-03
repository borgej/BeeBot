namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddedmodToStreamViewer : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.StreamViewers", "Mod", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.StreamViewers", "Mod");
        }
    }
}
