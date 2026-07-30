using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMD.Marketing.Migrations
{
    /// <inheritdoc />
    public partial class AddCountryCodeAndPhoneToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CountryCode",
                schema: "public",
                table: "Users",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "+1");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
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
                name: "CountryCode",
                schema: "public",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Phone",
                schema: "public",
                table: "Users");
        }
    }
}
