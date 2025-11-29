using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sitiowebb.Migrations
{
    /// <inheritdoc />
    public partial class FixBooleanColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only run on PostgreSQL - SQLite doesn't support ALTER COLUMN TYPE
            if (!migrationBuilder.ActiveProvider?.Contains("Sqlite") ?? true)
            {
                // Convert INTEGER boolean columns to BOOLEAN for PostgreSQL compatibility
                migrationBuilder.Sql(@"
                    ALTER TABLE ""AspNetUsers"" 
                    ALTER COLUMN ""EmailConfirmed"" TYPE boolean USING ""EmailConfirmed""::boolean;
                ");
                
                migrationBuilder.Sql(@"
                    ALTER TABLE ""AspNetUsers"" 
                    ALTER COLUMN ""PhoneNumberConfirmed"" TYPE boolean USING ""PhoneNumberConfirmed""::boolean;
                ");
                
                migrationBuilder.Sql(@"
                    ALTER TABLE ""AspNetUsers"" 
                    ALTER COLUMN ""TwoFactorEnabled"" TYPE boolean USING ""TwoFactorEnabled""::boolean;
                ");
                
                migrationBuilder.Sql(@"
                    ALTER TABLE ""AspNetUsers"" 
                    ALTER COLUMN ""LockoutEnabled"" TYPE boolean USING ""LockoutEnabled""::boolean;
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only run on PostgreSQL
            if (!migrationBuilder.ActiveProvider?.Contains("Sqlite") ?? true)
            {
                // Revert to INTEGER if needed
                migrationBuilder.Sql(@"
                    ALTER TABLE ""AspNetUsers"" 
                    ALTER COLUMN ""EmailConfirmed"" TYPE integer USING CASE WHEN ""EmailConfirmed"" THEN 1 ELSE 0 END;
                ");
                
                migrationBuilder.Sql(@"
                    ALTER TABLE ""AspNetUsers"" 
                    ALTER COLUMN ""PhoneNumberConfirmed"" TYPE integer USING CASE WHEN ""PhoneNumberConfirmed"" THEN 1 ELSE 0 END;
                ");
                
                migrationBuilder.Sql(@"
                    ALTER TABLE ""AspNetUsers"" 
                    ALTER COLUMN ""TwoFactorEnabled"" TYPE integer USING CASE WHEN ""TwoFactorEnabled"" THEN 1 ELSE 0 END;
                ");
                
                migrationBuilder.Sql(@"
                    ALTER TABLE ""AspNetUsers"" 
                    ALTER COLUMN ""LockoutEnabled"" TYPE integer USING CASE WHEN ""LockoutEnabled"" THEN 1 ELSE 0 END;
                ");
            }
        }
    }
}
