START TRANSACTION;

ALTER TABLE `Organization` ADD `UseKeyConnector` tinyint(1) NOT NULL DEFAULT FALSE;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20211115145402_KeyConnectorFlag', '5.0.9');

COMMIT;
