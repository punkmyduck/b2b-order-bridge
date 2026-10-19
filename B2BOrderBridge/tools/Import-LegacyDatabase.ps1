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
foreach ($table in @('Orders', 'OrderLines', 'OutboxMessages')) {
    if ((Read-LegacySql "SELECT to_regclass('public.`"$table`"') IS NOT NULL;") -eq 't') {
        $ordersSql += Export-Table "public.`"$table`""
    }
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
foreach ($table in @('OrderIntegrations', 'IntegrationAttempts', 'InboxMessages')) {
    if ((Read-LegacySql "SELECT to_regclass('public.`"$table`"') IS NOT NULL;") -eq 't') {
        $integrationSql += Export-Table "public.`"$table`""
    }
}
$integrationSql += 'COMMIT;'

Import-Sql 'orders-db' ($ordersSql -join "`n")
Import-Sql 'integrations-db' ($integrationSql -join "`n")
Write-Host 'Legacy orders, snapshots, integration identities and attempt history imported. Source data is unchanged.'
