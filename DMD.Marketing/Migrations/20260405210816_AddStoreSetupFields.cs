using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMD.Marketing.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreSetupFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StoreName",
                schema: "public",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorePhone",
                schema: "public",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoreTimezone",
                schema: "public",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                schema: "public",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActivationStatus",
                schema: "public",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "StoreName",       schema: "public", table: "Users");
            migrationBuilder.DropColumn(name: "StorePhone",      schema: "public", table: "Users");
            migrationBuilder.DropColumn(name: "StoreTimezone",   schema: "public", table: "Users");
            migrationBuilder.DropColumn(name: "BusinessType",    schema: "public", table: "Users");
            migrationBuilder.DropColumn(name: "ActivationStatus", schema: "public", table: "Users");
        }
    }
}
