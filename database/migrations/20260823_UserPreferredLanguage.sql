SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.UserProfiles', N'PreferredLanguage') IS NULL
BEGIN
    ALTER TABLE dbo.UserProfiles
        ADD PreferredLanguage NVARCHAR(20) NOT NULL
            CONSTRAINT DF_UserProfiles_PreferredLanguage DEFAULT (N'English');
END;

EXEC sys.sp_executesql N'
    UPDATE dbo.UserProfiles
    SET PreferredLanguage = N''English''
    WHERE PreferredLanguage IS NULL
       OR LTRIM(RTRIM(PreferredLanguage)) = N'''';
';

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_UserProfiles_PreferredLanguage'
      AND parent_object_id = OBJECT_ID(N'dbo.UserProfiles'))
BEGIN
    EXEC sys.sp_executesql N'
        ALTER TABLE dbo.UserProfiles
            ADD CONSTRAINT CK_UserProfiles_PreferredLanguage
                CHECK (PreferredLanguage IN (N''English'', N''Filipino''));
    ';
END;

COMMIT TRANSACTION;
