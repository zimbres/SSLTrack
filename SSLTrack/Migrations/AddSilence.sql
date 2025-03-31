BEGIN TRANSACTION;

ALTER TABLE "Domains" ADD "Silenced" INTEGER NOT NULL DEFAULT 0;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20250329200311_AddSilence', '8.0.14');

COMMIT;

