namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddedKillStats : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.BotChannelSettings", "KillStats_Kills", c => c.Int(nullable: false));
            AddColumn("dbo.BotChannelSettings", "KillStats_SquadKills", c => c.Int(nullable: false));
            AddColumn("dbo.BotChannelSettings", "KillStats_Deaths", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.BotChannelSettings", "KillStats_Deaths");
            DropColumn("dbo.BotChannelSettings", "KillStats_SquadKills");
            DropColumn("dbo.BotChannelSettings", "KillStats_Kills");
        }
    }
}
