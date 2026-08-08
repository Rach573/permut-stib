using Microsoft.EntityFrameworkCore;

namespace PermutStib.Data.Persistence;

public static class DatabaseSchemaInitializer
{
    public static async Task ApplyAdditiveUpdatesAsync(PermutStibDbContext database, CancellationToken cancellationToken = default)
    {
        await database.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "SignatureAvailabilities" (
                "Id" uuid NOT NULL,
                "AgentId" uuid NOT NULL,
                "ServiceDate" date NOT NULL,
                "Comment" character varying(200),
                "IsActive" boolean NOT NULL DEFAULT TRUE,
                "CreatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_SignatureAvailabilities" PRIMARY KEY ("Id")
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_SignatureAvailabilities_AgentId_ServiceDate"
                ON "SignatureAvailabilities" ("AgentId", "ServiceDate");
            CREATE INDEX IF NOT EXISTS "IX_SignatureAvailabilities_ServiceDate_IsActive"
                ON "SignatureAvailabilities" ("ServiceDate", "IsActive");
            ALTER TABLE "SignatureOffers" ADD COLUMN IF NOT EXISTS "AvailabilityId" uuid;
            """, cancellationToken);
    }
}
