using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderBridge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationAttempts");
        }
    }
}
