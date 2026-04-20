using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMD.Marketing.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // StoreName, StorePhone, StoreTimezone, BusinessType, ActivationStatus
            // were already added in AddStoreSetupFields — only new columns here.

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "public",
                table: "Users",
                type: "text",
                nullable: true,
                defaultValue: "CAD");

            migrationBuilder.AddColumn<decimal>(
                name: "FederalTaxRate",
                schema: "public",
                table: "Users",
                type: "numeric(5,3)",
                nullable: false,
                defaultValue: 5m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProvincialTaxRate",
                schema: "public",
                table: "Users",
                type: "numeric(5,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "TaxInclusive",
                schema: "public",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionExpiresAt",
                schema: "public",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Currency",              schema: "public", table: "Users");
            migrationBuilder.DropColumn(name: "FederalTaxRate",        schema: "public", table: "Users");
            migrationBuilder.DropColumn(name: "ProvincialTaxRate",     schema: "public", table: "Users");
            migrationBuilder.DropColumn(name: "TaxInclusive",          schema: "public", table: "Users");
            migrationBuilder.DropColumn(name: "SubscriptionExpiresAt", schema: "public", table: "Users");
        }
    }
}
