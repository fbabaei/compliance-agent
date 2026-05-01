-- Issue #8: Chat / Session DB model.
-- Idempotent: safe to re-run on an existing Azure SQL Database.
-- Tables align with Technical-Design.md §7 (Sessions, Messages).

IF OBJECT_ID('dbo.Sessions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sessions (
        SessionId        NVARCHAR(64)   NOT NULL PRIMARY KEY,
        OwnerId          NVARCHAR(128)  NOT NULL,
        Status           NVARCHAR(32)   NOT NULL,
        CreatedAtUtc     DATETIMEOFFSET NOT NULL,
        UpdatedAtUtc     DATETIMEOFFSET NOT NULL,
        LastActivityUtc  DATETIMEOFFSET NOT NULL
    );

    CREATE INDEX IX_Sessions_OwnerId_Status
        ON dbo.Sessions (OwnerId, Status);
END;

IF OBJECT_ID('dbo.SessionTurns', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SessionTurns (
        TurnId        BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SessionId     NVARCHAR(64)         NOT NULL,
        TurnIndex     INT                  NOT NULL,
        Role          NVARCHAR(20)         NOT NULL,    -- 'user' | 'agent'
        Content       NVARCHAR(MAX)        NOT NULL,
        CreatedAtUtc  DATETIMEOFFSET       NOT NULL,
        CONSTRAINT FK_SessionTurns_Sessions
            FOREIGN KEY (SessionId) REFERENCES dbo.Sessions(SessionId)
    );

    CREATE INDEX IX_SessionTurns_SessionId_TurnIndex
        ON dbo.SessionTurns (SessionId, TurnIndex);
END;
