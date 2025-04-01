BEGIN TRANSACTION;

CREATE TABLE "Agents" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Agents" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NULL,
    "UserId" TEXT NOT NULL
);

CREATE UNIQUE INDEX "IX_Agents_Name" ON "Agents" ("Name");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20250331214118_Agents', '8.0.14');

COMMIT;

