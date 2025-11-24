using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sitiowebb.Data.Migrations
{
    public partial class FixEmailConfirmed : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"AspNetUsers\" " +
                "ALTER COLUMN \"LockoutEnd\" TYPE timestamp with time zone " +
                "USING \"LockoutEnd\"::timestamp with time zone;"
            );
            // 🔧 Corrección de columnas tipo fecha en VacationRequests
            migrationBuilder.Sql(
                "ALTER TABLE \"VacationRequests\" " +
                "ALTER COLUMN \"From\" TYPE timestamp with time zone " +
                "USING \"From\"::timestamp with time zone;"
            );

            migrationBuilder.Sql(
                "ALTER TABLE \"VacationRequests\" " +
                "ALTER COLUMN \"To\" TYPE timestamp with time zone " +
                "USING \"To\"::timestamp with time zone;"
            );

            migrationBuilder.Sql(
                "ALTER TABLE \"VacationRequests\" " +
                "ALTER COLUMN \"DecidedUtc\" TYPE timestamp with time zone " +
                "USING \"DecidedUtc\"::timestamp with time zone;"
            );

            migrationBuilder.Sql(
                "ALTER TABLE \"VacationRequests\" " +
                "ALTER COLUMN \"CreatedUtc\" TYPE timestamp with time zone " +
                "USING \"CreatedUtc\"::timestamp with time zone;"
            );

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "LockoutEnd",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            // 🔧 Ajuste en Unavailabilities
            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Unavailabilities",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
                // ======== Fix boolean fields migrated from SQLite (0/1 -> boolean) ========

            migrationBuilder.Sql(
                "ALTER TABLE \"AspNetUsers\" " +
                "ALTER COLUMN \"EmailConfirmed\" TYPE boolean " +
                "USING (\"EmailConfirmed\"::integer)::boolean;"
            );

            migrationBuilder.Sql(
                "ALTER TABLE \"AspNetUsers\" " +
                "ALTER COLUMN \"PhoneNumberConfirmed\" TYPE boolean " +
                "USING (\"PhoneNumberConfirmed\"::integer)::boolean;"
            );

            migrationBuilder.Sql(
                "ALTER TABLE \"AspNetUsers\" " +
                "ALTER COLUMN \"TwoFactorEnabled\" TYPE boolean " +
                "USING (\"TwoFactorEnabled\"::integer)::boolean;"
            );

            migrationBuilder.Sql(
                "ALTER TABLE \"AspNetUsers\" " +
                "ALTER COLUMN \"LockoutEnabled\" TYPE boolean " +
                "USING (\"LockoutEnabled\"::integer)::boolean;"
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LockoutEnd",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true
            );
            // Revertir los cambios de tipo fecha si se revierte la migración
            migrationBuilder.AlterColumn<string>(
                name: "From",
                table: "VacationRequests",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "To",
                table: "VacationRequests",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "DecidedUtc",
                table: "VacationRequests",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedUtc",
                table: "VacationRequests",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");
        }
    }
}