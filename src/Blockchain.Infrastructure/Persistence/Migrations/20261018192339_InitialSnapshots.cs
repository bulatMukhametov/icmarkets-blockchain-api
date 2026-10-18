using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Blockchain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Snapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Coin = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Network = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Snapshots", x => x.Id);
                    table.CheckConstraint("CK_Snapshots_Chain", "(\"Coin\" IN ('btc','eth','dash','ltc') AND \"Network\" = 'main') OR (\"Coin\" = 'btc' AND \"Network\" = 'test3')");
                    table.CheckConstraint("CK_Snapshots_Payload", "json_typeof(\"Payload\"::json) = 'object'");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_Coin_Network_CreatedAt_Id",
                table: "Snapshots",
                columns: new[] { "Coin", "Network", "CreatedAt", "Id" },
                descending: new[] { false, false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Snapshots");
        }
    }
}
