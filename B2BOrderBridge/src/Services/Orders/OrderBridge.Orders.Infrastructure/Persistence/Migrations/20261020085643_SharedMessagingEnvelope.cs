using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderBridge.Orders.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SharedMessagingEnvelope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "OutboxMessages",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OrderingKey",
                table: "OutboxMessages",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Topic",
                table: "OutboxMessages",
                type: "character varying(249)",
                maxLength: 249,
                nullable: false,
                defaultValue: "");
            // Preserve already committed and still pending messages; do not regenerate their EventId.
            migrationBuilder.Sql("""
                UPDATE "OutboxMessages" SET
                    "Topic" = 'orderbridge.orders.created.v1',
                    "OrderingKey" = replace("OrderId"::text, '-', ''),
                    "IdempotencyKey" = replace("Id"::text, '-', ''),
                    "Payload" = jsonb_build_object(
                        'metadata', jsonb_build_object(
                            'eventId', "Id", 'eventType', "EventType", 'schemaVersion', 1,
                            'source', 'orders', 'occurredAt', "OccurredAt",
                            'idempotencyKey', replace("Id"::text, '-', ''),
                            'orderingKey', replace("OrderId"::text, '-', ''),
                            'correlationId', replace("Id"::text, '-', '')),
                        'payload', jsonb_build_object(
                            'processingScenario', "Payload"->'processingScenario',
                            'order', "Payload"->'order'));
                """);
            migrationBuilder.DropColumn(
                name: "OrderId",
                table: "OutboxMessages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "OrderingKey",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "Topic",
                table: "OutboxMessages");

            migrationBuilder.AddColumn<Guid>(
                name: "OrderId",
                table: "OutboxMessages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
            migrationBuilder.Sql("""
                UPDATE "OutboxMessages" SET
                    "OrderId" = ("Payload"->'payload'->'order'->>'id')::uuid,
                    "Payload" = jsonb_build_object(
                        'eventId', "Id", 'occurredAt', "OccurredAt",
                        'processingScenario', "Payload"->'payload'->'processingScenario',
                        'order', "Payload"->'payload'->'order');
                """);
        }
    }
}
