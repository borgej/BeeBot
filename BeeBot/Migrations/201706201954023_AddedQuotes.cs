namespace YTBot.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddedQuotes : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Quotes",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        QuoteMsg = c.String(),
                        QuoteBy = c.String(),
                        QuoteAdded = c.DateTime(),
                        Trigger_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Triggers", t => t.Trigger_Id)
                .Index(t => t.Trigger_Id);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Quotes", "Trigger_Id", "dbo.Triggers");
            DropIndex("dbo.Quotes", new[] { "Trigger_Id" });
            DropTable("dbo.Quotes");
        }
    }
}
