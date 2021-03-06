IF OBJECT_ID('[dbo].[ProviderUser_ReadCountByOnlyOwner]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[ProviderUser_ReadCountByOnlyOwner]
END
GO

CREATE PROCEDURE [dbo].[ProviderUser_ReadCountByOnlyOwner]
@UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [OwnerCountCTE] AS
    (
        SELECT
            PU.[UserId],
            COUNT(1) OVER (PARTITION BY PU.[ProviderId]) [ConfirmedOwnerCount]
        FROM
            [dbo].[ProviderUser] PU
        WHERE
            PU.[Type] = 0 -- 0 = ProviderAdmin
            AND PU.[Status] = 2 -- 2 = Confirmed
    )
     SELECT
         COUNT(1)
     FROM
         [OwnerCountCTE] OC
     WHERE
        OC.[UserId] = @UserId
        AND OC.[ConfirmedOwnerCount] = 1
END
GO

IF OBJECT_ID('[dbo].[User_DeleteById]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[User_DeleteById]
    END
GO

CREATE PROCEDURE [dbo].[User_DeleteById]
    @Id UNIQUEIDENTIFIER
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @BatchSize INT = 100

    -- Delete ciphers
    WHILE @BatchSize > 0
        BEGIN
            BEGIN TRANSACTION User_DeleteById_Ciphers

                DELETE TOP(@BatchSize)
                FROM
                    [dbo].[Cipher]
                WHERE
                        [UserId] = @Id

                SET @BatchSize = @@ROWCOUNT

            COMMIT TRANSACTION User_DeleteById_Ciphers
        END

    BEGIN TRANSACTION User_DeleteById

        -- Delete folders
        DELETE
        FROM
            [dbo].[Folder]
        WHERE
            [UserId] = @Id

        -- Delete devices
        DELETE
        FROM
            [dbo].[Device]
        WHERE
            [UserId] = @Id

        -- Delete collection users
        DELETE
            CU
        FROM
            [dbo].[CollectionUser] CU
        INNER JOIN
            [dbo].[OrganizationUser] OU ON OU.[Id] = CU.[OrganizationUserId]
        WHERE
            OU.[UserId] = @Id

        -- Delete group users
        DELETE
            GU
        FROM
            [dbo].[GroupUser] GU
        INNER JOIN
            [dbo].[OrganizationUser] OU ON OU.[Id] = GU.[OrganizationUserId]
        WHERE
            OU.[UserId] = @Id

        -- Delete organization users
        DELETE
        FROM
            [dbo].[OrganizationUser]
        WHERE
            [UserId] = @Id

        -- Delete provider users
        DELETE
        FROM
            [dbo].[ProviderUser]
        WHERE
            [UserId] = @Id

        -- Delete U2F logins
        DELETE
        FROM
            [dbo].[U2f]
        WHERE
            [UserId] = @Id

        -- Delete SSO Users
        DELETE
        FROM
            [dbo].[SsoUser]
        WHERE
            [UserId] = @Id

        -- Delete Emergency Accesses
        DELETE
        FROM
            [dbo].[EmergencyAccess]
        WHERE
            [GrantorId] = @Id
        OR
            [GranteeId] = @Id

        -- Delete Sends
        DELETE
        FROM
            [dbo].[Send]
        WHERE
            [UserId] = @Id

        -- Finally, delete the user
        DELETE
        FROM
            [dbo].[User]
        WHERE
            [Id] = @Id

    COMMIT TRANSACTION User_DeleteById
END
go
