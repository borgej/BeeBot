namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class BotUserSettingsIdToIdentity : DbMigration
    {
        public override void Up()
        {
            DropPrimaryKey("dbo.BotUserSettings");
            AlterColumn("dbo.BotUserSettings", "Id", c => c.Int(nullable: false, identity: true));
            AddPrimaryKey("dbo.BotUserSettings", "Id");
        }
        
        public override void Down()
        {
            DropPrimaryKey("dbo.BotUserSettings");
            AlterColumn("dbo.BotUserSettings", "Id", c => c.String(nullable: false, maxLength: 128));
            AddPrimaryKey("dbo.BotUserSettings", "Id");
        }
    }
}
