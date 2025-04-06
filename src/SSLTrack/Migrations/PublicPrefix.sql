BEGIN TRANSACTION;

ALTER TABLE "Domains" ADD "PublicPrefix" INTEGER NOT NULL DEFAULT 0;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20250406144113_PublicPrefix', '8.0.14');

COMMIT;

