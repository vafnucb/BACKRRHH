namespace UcbBack.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class FirstMigration : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "ADMNALRRHH.BRANCHES",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        NAME = c.String(nullable: false, maxLength: 20),
                        ABR = c.String(nullable: false, maxLength: 10),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "ADMNALRRHH.People",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        COD_UCB = c.String(maxLength: 11),
                        TYPE_DOCUMENT = c.String(nullable: false, maxLength: 25),
                        DOCUMENTO = c.String(nullable: false, maxLength: 20),
                        ISSUED = c.String(nullable: false, maxLength: 20),
                        NAMES = c.String(nullable: false, maxLength: 100),
                        FIRSTSURNAME = c.String(nullable: false, maxLength: 50),
                        SECONDSURNAME = c.String(nullable: false, maxLength: 50),
                        MARIEDSURNAME = c.String(maxLength: 50),
                        BIRTHDATE = c.DateTime(nullable: false, storeType: "date"),
                        GENDER = c.String(nullable: false, maxLength: 1),
                        NATIONALITY = c.String(nullable: false, maxLength: 20),
                        PHOTO = c.String(maxLength: 100),
                        PHONENUMBER = c.String(maxLength: 25),
                        PERSONALEMAIL = c.String(maxLength: 50),
                        UCBMAIL = c.String(maxLength: 50),
                        OFFICEPHONENUMBER = c.String(maxLength: 25),
                        HOMEADDRESS = c.String(maxLength: 200),
                        MARITALSTATUS = c.String(maxLength: 30),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "ADMNALRRHH.Positions",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        NAME = c.String(),
                        LevelId = c.Int(nullable: false),
                        BranchesId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("ADMNALRRHH.BRANCHES", t => t.BranchesId, cascadeDelete: true)
                .ForeignKey("ADMNALRRHH.Levels", t => t.LevelId, cascadeDelete: true)
                .Index(t => t.LevelId)
                .Index(t => t.BranchesId);
            
            CreateTable(
                "ADMNALRRHH.Levels",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Cod = c.String(),
                        Category = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "ADMNALRRHH.AspNetRoles",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 128),
                        Name = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Name, unique: true, name: "RoleNameIndex");
            
            CreateTable(
                "ADMNALRRHH.AspNetUserRoles",
                c => new
                    {
                        UserId = c.String(nullable: false, maxLength: 128),
                        RoleId = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => new { t.UserId, t.RoleId })
                .ForeignKey("ADMNALRRHH.AspNetRoles", t => t.RoleId, cascadeDelete: true)
                .ForeignKey("ADMNALRRHH.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId)
                .Index(t => t.RoleId);
            
            CreateTable(
                "ADMNALRRHH.AspNetUsers",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 128),
                        Email = c.String(maxLength: 256),
                        EmailConfirmed = c.Boolean(nullable: false),
                        PasswordHash = c.String(),
                        SecurityStamp = c.String(),
                        PhoneNumber = c.String(),
                        PhoneNumberConfirmed = c.Boolean(nullable: false),
                        TwoFactorEnabled = c.Boolean(nullable: false),
                        LockoutEndDateUtc = c.DateTime(),
                        LockoutEnabled = c.Boolean(nullable: false),
                        AccessFailedCount = c.Int(nullable: false),
                        UserName = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.UserName, unique: true, name: "UserNameIndex");
            
            CreateTable(
                "ADMNALRRHH.AspNetUserClaims",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.String(nullable: false, maxLength: 128),
                        ClaimType = c.String(),
                        ClaimValue = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("ADMNALRRHH.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "ADMNALRRHH.AspNetUserLogins",
                c => new
                    {
                        LoginProvider = c.String(nullable: false, maxLength: 128),
                        ProviderKey = c.String(nullable: false, maxLength: 128),
                        UserId = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => new { t.LoginProvider, t.ProviderKey, t.UserId })
                .ForeignKey("ADMNALRRHH.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("ADMNALRRHH.AspNetUserRoles", "UserId", "ADMNALRRHH.AspNetUsers");
            DropForeignKey("ADMNALRRHH.AspNetUserLogins", "UserId", "ADMNALRRHH.AspNetUsers");
            DropForeignKey("ADMNALRRHH.AspNetUserClaims", "UserId", "ADMNALRRHH.AspNetUsers");
            DropForeignKey("ADMNALRRHH.AspNetUserRoles", "RoleId", "ADMNALRRHH.AspNetRoles");
            DropForeignKey("ADMNALRRHH.Positions", "LevelId", "ADMNALRRHH.Levels");
            DropForeignKey("ADMNALRRHH.Positions", "BranchesId", "ADMNALRRHH.BRANCHES");
            DropIndex("ADMNALRRHH.AspNetUserLogins", new[] { "UserId" });
            DropIndex("ADMNALRRHH.AspNetUserClaims", new[] { "UserId" });
            DropIndex("ADMNALRRHH.AspNetUsers", "UserNameIndex");
            DropIndex("ADMNALRRHH.AspNetUserRoles", new[] { "RoleId" });
            DropIndex("ADMNALRRHH.AspNetUserRoles", new[] { "UserId" });
            DropIndex("ADMNALRRHH.AspNetRoles", "RoleNameIndex");
            DropIndex("ADMNALRRHH.Positions", new[] { "BranchesId" });
            DropIndex("ADMNALRRHH.Positions", new[] { "LevelId" });
            DropTable("ADMNALRRHH.AspNetUserLogins");
            DropTable("ADMNALRRHH.AspNetUserClaims");
            DropTable("ADMNALRRHH.AspNetUsers");
            DropTable("ADMNALRRHH.AspNetUserRoles");
            DropTable("ADMNALRRHH.AspNetRoles");
            DropTable("ADMNALRRHH.Levels");
            DropTable("ADMNALRRHH.Positions");
            DropTable("ADMNALRRHH.People");
            DropTable("ADMNALRRHH.BRANCHES");
        }
    }
}
