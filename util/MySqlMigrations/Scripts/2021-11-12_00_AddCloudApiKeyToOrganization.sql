START TRANSACTION;

ALTER TABLE `Organization` ADD `CloudApiKey` longtext CHARACTER SET utf8mb4 NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20211112181907_AddCloudApiKeyToOrganization', '5.0.9');

COMMIT;
