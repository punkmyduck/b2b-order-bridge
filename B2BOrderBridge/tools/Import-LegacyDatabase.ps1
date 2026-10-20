param([string]$LegacyContainer = 'order-bridge-postgres')
$ErrorActionPreference = 'Stop'
$composeFile = Join-Path $PSScriptRoot '../docker-compose.yml'

# Run with the old API/worker and the new APIs stopped. Both target schemas must already be migrated.
# The source is read-only. Target inserts never overwrite existing rows. Each target import is transactional.
function Read-LegacySql([string]$Sql) {
    $result = $Sql | docker exec -i $LegacyContainer sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At'
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the legacy database.' }
    return $result
}
function Export-Table([string]$Table) {
    $result = docker exec $LegacyContainer sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --data-only --column-inserts --on-conflict-do-nothing --no-owner --no-privileges --table="$1"' sh $Table
    if ($LASTEXITCODE -ne 0) { throw "Could not export $Table." }
    return ($result -join "`n")
}
function Import-Sql([string]$Service, [string]$Sql) {
    $Sql | docker compose -f $composeFile exec -T $Service sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Import into $Service failed; that transaction was rolled back." }
}

if ((Read-LegacySql "SELECT to_regclass('public.`"Orders`"') IS NOT NULL;") -ne 't') {
    throw 'The source container is not a legacy OrderBridge database.'
}
$ordersSql = @('BEGIN;')
foreach ($table in @('Orders', 'OrderLines')) {
    if ((Read-LegacySql "SELECT to_regclass('public.`"$table`"') IS NOT NULL;") -eq 't') {
        $ordersSql += Export-Table "public.`"$table`""
    }
}
if ((Read-LegacySql "SELECT to_regclass('public.`"OutboxMessages`"') IS NOT NULL;") -eq 't') {
    $outboxQuery = @'
COPY (
 SELECT "Id", "EventType", 'orderbridge.orders.created.v1', replace("OrderId"::text, '-', ''),
   replace("Id"::text, '-', ''), jsonb_build_object(
     'metadata', jsonb_build_object('eventId', "Id", 'eventType', "EventType", 'schemaVersion', 1,
       'source', 'orders', 'occurredAt', "OccurredAt", 'idempotencyKey', replace("Id"::text, '-', ''),
       'orderingKey', replace("OrderId"::text, '-', ''), 'correlationId', replace("Id"::text, '-', '')),
     'payload', jsonb_build_object('processingScenario', "Payload"->'processingScenario', 'order', "Payload"->'order')),
   "OccurredAt", "NextAttemptAt", "PublishedAt", "PublishAttempts", "LastError"
 FROM "OutboxMessages"
) TO STDOUT WITH CSV;
'@
    $outbox = (Read-LegacySql $outboxQuery) -join "`n"
    $ordersSql += @(
        'SET LOCAL search_path TO public;',
        'CREATE TEMP TABLE "LegacyOutbox" (LIKE "OutboxMessages" INCLUDING DEFAULTS) ON COMMIT DROP;',
        'COPY "LegacyOutbox" ("Id", "EventType", "Topic", "OrderingKey", "IdempotencyKey", "Payload", "OccurredAt", "NextAttemptAt", "PublishedAt", "PublishAttempts", "LastError") FROM STDIN WITH CSV;',
        $outbox, '\.',
        'INSERT INTO "OutboxMessages" SELECT * FROM "LegacyOutbox" ON CONFLICT DO NOTHING;'
    )
}
$ordersSql += 'COMMIT;'

$projectionSql = @'
COPY (
 SELECT o."Id", jsonb_build_object(
   'id', o."Id", 'sourceSystem', o."SourceSystem", 'externalOrderId', o."ExternalOrderId",
   'customer', jsonb_build_object('companyName', o."CustomerCompanyName", 'taxId', o."CustomerTaxId", 'contactEmail', o."CustomerContactEmail"),
   'currency', o."Currency", 'totalAmount', o."TotalAmount",
   'lines', COALESCE((SELECT jsonb_agg(jsonb_build_object('sku', l."Sku", 'name', l."Name", 'quantity', l."Quantity", 'unitPrice', l."UnitPrice") ORDER BY l."Id")
                      FROM "OrderLines" l WHERE l."OrderId" = o."Id"), '[]'::jsonb)
 ), o."CreatedAt" FROM "Orders" o
) TO STDOUT WITH CSV;
'@
$projection = (Read-LegacySql $projectionSql) -join "`n"
$integrationSql = @(
    'BEGIN;',
    'CREATE TEMP TABLE "LegacyOrderSnapshots" ("Id" uuid, "Payload" jsonb, "ReceivedAt" timestamptz) ON COMMIT DROP;',
    'COPY "LegacyOrderSnapshots" ("Id", "Payload", "ReceivedAt") FROM STDIN WITH CSV;',
    $projection,
    '\.',
    'INSERT INTO "IntegrationOrders" SELECT * FROM "LegacyOrderSnapshots" ON CONFLICT ("Id") DO NOTHING;'
)
foreach ($table in @('OrderIntegrations', 'IntegrationAttempts')) {
    if ((Read-LegacySql "SELECT to_regclass('public.`"$table`"') IS NOT NULL;") -eq 't') {
        $integrationSql += Export-Table "public.`"$table`""
    }
}
if ((Read-LegacySql "SELECT to_regclass('public.`"InboxMessages`"') IS NOT NULL;") -eq 't') {
    $inboxQuery = @'
COPY (
 SELECT "Consumer", "EventId", "ProcessedAt", 'orders', 'order.created.v1', replace("EventId"::text, '-', ''), ''
 FROM "InboxMessages"
) TO STDOUT WITH CSV;
'@
    $inbox = (Read-LegacySql $inboxQuery) -join "`n"
    $integrationSql += @(
        'SET LOCAL search_path TO public;',
        'CREATE TEMP TABLE "LegacyInbox" (LIKE "InboxMessages" INCLUDING DEFAULTS) ON COMMIT DROP;',
        'COPY "LegacyInbox" ("Consumer", "EventId", "ProcessedAt", "Source", "EventType", "IdempotencyKey", "OrderingKey") FROM STDIN WITH CSV;',
        $inbox, '\.',
        'INSERT INTO "InboxMessages" SELECT * FROM "LegacyInbox" ON CONFLICT DO NOTHING;'
    )
}
$integrationSql += 'COMMIT;'

Import-Sql 'orders-db' (($ordersSql | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join "`n")
Import-Sql 'integrations-db' (($integrationSql | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join "`n")
Write-Host 'Legacy orders, snapshots, integration identities and attempt history imported. Source data is unchanged.'
