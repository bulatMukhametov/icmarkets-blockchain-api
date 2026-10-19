using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Blockchain.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IntroduceNetworkCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Snapshots_Coin_Network_CreatedAt_Id",
                table: "Snapshots");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Snapshots_Chain",
                table: "Snapshots");

            migrationBuilder.AddColumn<Guid>(
                name: "BlockchainNetworkId",
                table: "Snapshots",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Cryptocurrencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cryptocurrencies", x => x.Id);
                    table.CheckConstraint("CK_Cryptocurrencies_Code", "\"Code\" ~ '^[a-z0-9-]{1,32}$'");
                });

            migrationBuilder.CreateTable(
                name: "BlockchainNetworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CryptocurrencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockchainNetworks", x => x.Id);
                    table.CheckConstraint("CK_BlockchainNetworks_Code", "\"Code\" ~ '^[a-z0-9-]{1,32}$'");
                    table.CheckConstraint("CK_BlockchainNetworks_Kind", "\"Kind\" IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_BlockchainNetworks_Cryptocurrencies_CryptocurrencyId",
                        column: x => x.CryptocurrencyId,
                        principalTable: "Cryptocurrencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BlockCypherNetworkMappings",
                columns: table => new
                {
                    BlockchainNetworkId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NetworkCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockCypherNetworkMappings", x => x.BlockchainNetworkId);
                    table.CheckConstraint("CK_BlockCypherMappings_Currency", "\"CurrencyCode\" ~ '^[a-z0-9]{1,32}$'");
                    table.CheckConstraint("CK_BlockCypherMappings_Network", "\"NetworkCode\" ~ '^[a-z0-9]{1,32}$'");
                    table.ForeignKey(
                        name: "FK_BlockCypherNetworkMappings_BlockchainNetworks_BlockchainNet~",
                        column: x => x.BlockchainNetworkId,
                        principalTable: "BlockchainNetworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Cryptocurrencies",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "btc", "Bitcoin" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "eth", "Ethereum" },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "dash", "Dash" },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "ltc", "Litecoin" }
                });

            migrationBuilder.InsertData(
                table: "BlockchainNetworks",
                columns: new[] { "Id", "Code", "CryptocurrencyId", "Kind" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), "main", new Guid("10000000-0000-0000-0000-000000000001"), 1 },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "test3", new Guid("10000000-0000-0000-0000-000000000001"), 2 },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "main", new Guid("10000000-0000-0000-0000-000000000002"), 1 },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "main", new Guid("10000000-0000-0000-0000-000000000003"), 1 },
                    { new Guid("20000000-0000-0000-0000-000000000005"), "main", new Guid("10000000-0000-0000-0000-000000000004"), 1 }
                });

            migrationBuilder.InsertData(
                table: "BlockCypherNetworkMappings",
                columns: new[] { "BlockchainNetworkId", "CurrencyCode", "NetworkCode" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), "btc", "main" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), "btc", "test3" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), "eth", "main" },
                    { new Guid("20000000-0000-0000-0000-000000000004"), "dash", "main" },
                    { new Guid("20000000-0000-0000-0000-000000000005"), "ltc", "main" }
                });

            // Resolve the existing currency/network pair before removing the legacy columns.
            migrationBuilder.Sql("""
                UPDATE "Snapshots" AS snapshot
                SET "BlockchainNetworkId" = network."Id"
                FROM "BlockchainNetworks" AS network
                JOIN "Cryptocurrencies" AS currency ON currency."Id" = network."CryptocurrencyId"
                WHERE snapshot."Coin" = currency."Code" AND snapshot."Network" = network."Code";
                """);
            // An unmatched row aborts the transaction instead of losing its network identity.
            migrationBuilder.AlterColumn<Guid>(
                name: "BlockchainNetworkId", table: "Snapshots", type: "uuid", nullable: false,
                oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
            migrationBuilder.DropColumn(name: "Coin", table: "Snapshots");
            migrationBuilder.DropColumn(name: "Network", table: "Snapshots");

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_BlockchainNetworkId_CreatedAt_Id",
                table: "Snapshots",
                columns: new[] { "BlockchainNetworkId", "CreatedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_BlockchainNetworks_CryptocurrencyId_Code",
                table: "BlockchainNetworks",
                columns: new[] { "CryptocurrencyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlockCypherNetworkMappings_CurrencyCode_NetworkCode",
                table: "BlockCypherNetworkMappings",
                columns: new[] { "CurrencyCode", "NetworkCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cryptocurrencies_Code",
                table: "Cryptocurrencies",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Snapshots_BlockchainNetworks_BlockchainNetworkId",
                table: "Snapshots",
                column: "BlockchainNetworkId",
                principalTable: "BlockchainNetworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("The legacy schema cannot represent an extended network catalog. Restore a verified pre-migration backup to roll back.");
        }
    }
}