BEGIN TRANSACTION;

DROP INDEX "IX_Domains_DomainName";

CREATE INDEX "IX_Domains_DomainName" ON "Domains" ("DomainName");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20250402014826_Remove_DomainName_UniqueColumn', '8.0.14');

COMMIT;

