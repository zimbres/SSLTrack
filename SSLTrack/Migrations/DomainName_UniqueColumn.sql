BEGIN TRANSACTION;

DROP INDEX "IX_Domains_DomainName";

CREATE UNIQUE INDEX "IX_Domains_DomainName" ON "Domains" ("DomainName");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20250331000127_DomainName_UniqueColumn', '8.0.14');

COMMIT;

