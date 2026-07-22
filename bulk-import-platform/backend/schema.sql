-- =====================================================================================
-- Customer and Vehicle Bulk Data Import Platform - SQL Server schema
--
-- This script is a manual fallback for environments where you cannot run
-- `dotnet ef migrations add` / `dotnet ef database update` (e.g. no NuGet access yet).
-- Once you have NuGet access, prefer letting EF Core generate + apply migrations instead,
-- since that keeps the schema and the C# model in sync automatically going forward:
--
--   cd backend/BulkImportPlatform.Api
--   dotnet tool install --global dotnet-ef
--   dotnet ef migrations add InitialCreate --project ../BulkImportPlatform.Infrastructure --startup-project .
--   dotnet ef database update --project ../BulkImportPlatform.Infrastructure --startup-project .
--
-- If you just want to stand the database up quickly, run this script instead against
-- an empty SQL Server database and skip the migrations step.
-- =====================================================================================
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'BulkImportPlatform')
BEGIN
    CREATE DATABASE BulkImportPlatform;
END
GO

USE BulkImportPlatform;
GO

CREATE TABLE MappingProfiles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    ImportType INT NOT NULL,
    ColumnMappingJson NVARCHAR(MAX) NOT NULL DEFAULT '{}',
    DefaultValuesJson NVARCHAR(MAX) NOT NULL DEFAULT '{}',
    IsSystemDefault BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(200) NULL
);

-- Per-client irentosoft login used by the RPA push-to-irentosoft step (see IRpaImportService).
-- EncryptedPassword is ciphertext produced by IClientAccountCredentialProtector - never plaintext.
CREATE TABLE ClientAccounts (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    IrentosoftUsername NVARCHAR(256) NOT NULL,
    EncryptedPassword NVARCHAR(MAX) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(200) NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy NVARCHAR(200) NULL
);

CREATE TABLE ImportJobs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ImportType INT NOT NULL,
    Status INT NOT NULL DEFAULT 1,
    FileName NVARCHAR(500) NOT NULL,
    StoredFilePath NVARCHAR(1000) NULL,
    UploadedBy NVARCHAR(200) NOT NULL,
    UploadedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CompletedAt DATETIME2 NULL,
    DetectedHeadersJson NVARCHAR(MAX) NOT NULL DEFAULT '[]',
    MappingProfileId INT NULL REFERENCES MappingProfiles(Id) ON DELETE SET NULL,
    TotalRecords INT NOT NULL DEFAULT 0,
    ValidRecords INT NOT NULL DEFAULT 0,
    WarningRecords INT NOT NULL DEFAULT 0,
    ErrorRecords INT NOT NULL DEFAULT 0,
    ImportedRecords INT NOT NULL DEFAULT 0,
    FailedRecords INT NOT NULL DEFAULT 0,
    ClientAccountId INT NULL REFERENCES ClientAccounts(Id),
    RpaOwningLocation NVARCHAR(MAX) NULL,
    RpaCurrentLocation NVARCHAR(MAX) NULL
);

CREATE TABLE ImportRecords (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ImportJobId INT NOT NULL REFERENCES ImportJobs(Id) ON DELETE CASCADE,
    RowNumber INT NOT NULL,
    RawDataJson NVARCHAR(MAX) NOT NULL DEFAULT '{}',
    MappedDataJson NVARCHAR(MAX) NOT NULL DEFAULT '{}',
    DefaultsAppliedJson NVARCHAR(MAX) NOT NULL DEFAULT '[]',
    Status INT NOT NULL DEFAULT 1,
    CreatedEntityId INT NULL,
    RpaStatus INT NOT NULL DEFAULT 1,
    RpaMessage NVARCHAR(MAX) NULL,
    RpaEntityReference NVARCHAR(MAX) NULL
);
CREATE INDEX IX_ImportRecords_ImportJobId ON ImportRecords(ImportJobId);

-- Full RPA attempt history per record (mirrors the ValidationResults one-parent-many-detail
-- pattern) so staff can see why an earlier attempt failed even after a later retry succeeds.
CREATE TABLE RpaAttempts (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ImportRecordId INT NOT NULL REFERENCES ImportRecords(Id) ON DELETE CASCADE,
    AttemptNumber INT NOT NULL,
    StartedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CompletedAt DATETIME2 NULL,
    Outcome INT NOT NULL,
    FailureStage NVARCHAR(200) NULL,
    ErrorMessage NVARCHAR(MAX) NULL,
    ScreenshotPath NVARCHAR(1000) NULL,
    PerformedBy NVARCHAR(200) NOT NULL
);
CREATE INDEX IX_RpaAttempts_ImportRecordId ON RpaAttempts(ImportRecordId);

CREATE TABLE ValidationResults (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ImportRecordId INT NOT NULL REFERENCES ImportRecords(Id) ON DELETE CASCADE,
    Severity INT NOT NULL,
    FieldName NVARCHAR(200) NOT NULL,
    RuleCode NVARCHAR(100) NOT NULL,
    Message NVARCHAR(1000) NOT NULL
);
CREATE INDEX IX_ValidationResults_ImportRecordId ON ValidationResults(ImportRecordId);

CREATE TABLE AuditLogs (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ImportJobId INT NULL REFERENCES ImportJobs(Id) ON DELETE SET NULL,
    Action NVARCHAR(200) NOT NULL,
    PerformedBy NVARCHAR(200) NOT NULL,
    Timestamp DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Details NVARCHAR(MAX) NULL
);
CREATE INDEX IX_AuditLogs_ImportJobId ON AuditLogs(ImportJobId);

CREATE TABLE Vehicles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VehicleNumber NVARCHAR(64) NOT NULL,
    VIN NVARCHAR(200) NULL,
    Make NVARCHAR(200) NULL,
    Model NVARCHAR(200) NULL,
    Year INT NULL,
    Color NVARCHAR(100) NULL,
    FuelType NVARCHAR(100) NULL,
    TankSize DECIMAL(18,2) NULL,
    Odometer DECIMAL(18,2) NULL,
    LicenseNumber NVARCHAR(100) NULL,
    SpotNumber NVARCHAR(100) NULL,
    Transmission NVARCHAR(100) NULL,
    VehicleType NVARCHAR(100) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(200) NULL,
    ImportJobId INT NULL REFERENCES ImportJobs(Id),
    ImportRecordId INT NULL,
    ClientAccountId INT NULL REFERENCES ClientAccounts(Id)
);
-- Unique per client, not globally - two different clients may legitimately use the same
-- vehicle number. Filtered (WHERE ClientAccountId IS NOT NULL) so legacy/unassigned rows
-- with a NULL account don't collide with each other.
CREATE UNIQUE INDEX IX_Vehicles_ClientAccountId_VehicleNumber ON Vehicles(ClientAccountId, VehicleNumber) WHERE ClientAccountId IS NOT NULL;

CREATE TABLE Customers (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CustomerName NVARCHAR(256) NOT NULL,
    Email NVARCHAR(256) NULL,
    Phone NVARCHAR(50) NULL,
    Address NVARCHAR(500) NULL,
    City NVARCHAR(100) NULL,
    State NVARCHAR(100) NULL,
    Country NVARCHAR(100) NULL,
    DateOfBirth DATE NULL,
    DriverLicenseNumber NVARCHAR(100) NULL,
    Status NVARCHAR(50) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(200) NULL,
    ImportJobId INT NULL REFERENCES ImportJobs(Id),
    ImportRecordId INT NULL,
    ClientAccountId INT NULL REFERENCES ClientAccounts(Id)
);
CREATE INDEX IX_Customers_ClientAccountId ON Customers(ClientAccountId);
GO
