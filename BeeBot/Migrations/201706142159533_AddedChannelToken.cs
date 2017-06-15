namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddedChannelToken : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BotUserSettings", "ChannelToken", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.BotUserSettings", "ChannelToken");
        }
    }
}
