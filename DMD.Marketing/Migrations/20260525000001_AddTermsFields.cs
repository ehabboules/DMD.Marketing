using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMD.Marketing.Migrations
{
    /// <inheritdoc />
    public partial class AddTermsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TermsAcceptedAt",
                schema: "public",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsVersion",
                schema: "public",
                table: "Users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TermsAcceptedAt",
                schema: "public",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TermsVersion",
                schema: "public",
                table: "Users");
        }
    }
}
