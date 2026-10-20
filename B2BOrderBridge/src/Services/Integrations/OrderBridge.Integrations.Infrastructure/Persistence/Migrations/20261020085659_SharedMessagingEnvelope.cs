using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderBridge.Integrations.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SharedMessagingEnvelope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "InboxMessages",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "InboxMessages",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OrderingKey",
                table: "InboxMessages",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Payload",
                table: "InboxMessages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "InboxMessages",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            // Historical inbox rows contain receipts only; retain their identity for redeliveries.
            migrationBuilder.Sql("""
                UPDATE "InboxMessages" SET "Source" = 'orders', "EventType" = 'order.created.v1',
                    "IdempotencyKey" = replace("EventId"::text, '-', '');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_Consumer_Source_EventType_IdempotencyKey",
                table: "InboxMessages",
                columns: new[] { "Consumer", "Source", "EventType", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InboxMessages_Consumer_Source_EventType_IdempotencyKey",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "OrderingKey",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "Payload",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "InboxMessages");
        }
    }
}
