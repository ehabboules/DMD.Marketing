using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMD.Marketing.Migrations
{
    /// <inheritdoc />
    public partial class AddUserTenantSlug : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantSlug",
                schema: "public",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantSlug",
                schema: "public",
                table: "Users");
        }
    }
}
