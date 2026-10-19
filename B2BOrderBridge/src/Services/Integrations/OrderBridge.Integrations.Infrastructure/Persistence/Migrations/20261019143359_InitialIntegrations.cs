using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderBridge.Integrations.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialIntegrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    Consumer = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => new { x.Consumer, x.EventId });
                });

            migrationBuilder.CreateTable(
                name: "IntegrationOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationOrders", x => x.Id);
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
                        name: "FK_OrderIntegrations_IntegrationOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "IntegrationOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderIntegrationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationAttempts", x => x.Id);
                    table.CheckConstraint("CK_IntegrationAttempts_AttemptNumber_Positive", "\"AttemptNumber\" > 0");
                    table.CheckConstraint("CK_IntegrationAttempts_ResponseStatusCode_Range", "\"ResponseStatusCode\" IS NULL OR \"ResponseStatusCode\" BETWEEN 100 AND 599");
                    table.ForeignKey(
                        name: "FK_IntegrationAttempts_OrderIntegrations_OrderIntegrationId",
                        column: x => x.OrderIntegrationId,
                        principalTable: "OrderIntegrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationAttempts_OrderIntegrationId_AttemptNumber",
                table: "IntegrationAttempts",
                columns: new[] { "OrderIntegrationId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationAttempts_OrderIntegrationId_StartedAt",
                table: "IntegrationAttempts",
                columns: new[] { "OrderIntegrationId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationAttempts_Outcome_StartedAt",
                table: "IntegrationAttempts",
                columns: new[] { "Outcome", "StartedAt" });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxMessages");

            migrationBuilder.DropTable(
                name: "IntegrationAttempts");

            migrationBuilder.DropTable(
                name: "OrderIntegrations");

            migrationBuilder.DropTable(
                name: "IntegrationOrders");
        }
    }
}
