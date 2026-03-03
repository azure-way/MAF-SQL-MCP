IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AgentSessions' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.AgentSessions
    (
        SessionId   NVARCHAR(64)  NOT NULL,
        SessionState NVARCHAR(MAX) NOT NULL,
        CreatedAt   DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt   DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_AgentSessions PRIMARY KEY (SessionId)
    );
END;
