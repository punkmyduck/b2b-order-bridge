using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalOrderId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CustomerCompanyName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CustomerTaxId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CustomerContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OrderStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PaymentStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(31,2)", precision: 31, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderIntegrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegrationTarget = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IntegrationOperation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IntegrationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalEntityId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderIntegrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderIntegrations_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Sku = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(31,2)", precision: 31, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(31,2)", precision: 31, scale: 2, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderLines_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderIntegrations_IntegrationStatus_NextAttemptAt",
                table: "OrderIntegrations",
                columns: new[] { "IntegrationStatus", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderIntegrations_IntegrationTarget_IdempotencyKey",
                table: "OrderIntegrations",
                columns: new[] { "IntegrationTarget", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderIntegrations_OrderId_IntegrationTarget_IntegrationOper~",
                table: "OrderIntegrations",
                columns: new[] { "OrderId", "IntegrationTarget", "IntegrationOperation" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_OrderId",
                table: "OrderLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_SourceSystem_ExternalOrderId",
                table: "Orders",
                columns: new[] { "SourceSystem", "ExternalOrderId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderIntegrations");

            migrationBuilder.DropTable(
                name: "OrderLines");

            migrationBuilder.DropTable(
                name: "Orders");
        }
    }
}
