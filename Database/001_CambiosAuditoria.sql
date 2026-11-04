-- 001: auditoría de cambios manuales (una fila por campo modificado en cada guardado).
-- Ejecutar en IVC_TEST y luego en IVC. Idempotente. No fija la base con USE:
-- test y producción conviven en la misma instancia; elegir la base en la conexión.
-- Debe coincidir con Data/IvcDbContext.cs (entidad CambioAuditoria).

IF OBJECT_ID(N'dbo.CambiosAuditoria', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CambiosAuditoria
    (
        Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CambiosAuditoria PRIMARY KEY,
        Lote           UNIQUEIDENTIFIER  NOT NULL,
        Fecha          DATETIME2         NOT NULL,
        Usuario        NVARCHAR(150)     NOT NULL,
        Vista          NVARCHAR(50)      NOT NULL,
        EntidadId      INT               NULL,
        ClaveNegocio   NVARCHAR(200)     NOT NULL,
        Accion         NVARCHAR(10)      NOT NULL,
        Campo          NVARCHAR(100)     NULL,
        ValorAnterior  NVARCHAR(500)     NULL,
        ValorNuevo     NVARCHAR(500)     NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CambiosAuditoria_Vista_EntidadId_Fecha' AND object_id = OBJECT_ID(N'dbo.CambiosAuditoria'))
    CREATE INDEX IX_CambiosAuditoria_Vista_EntidadId_Fecha ON dbo.CambiosAuditoria (Vista, EntidadId, Fecha);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CambiosAuditoria_Vista_ClaveNegocio_Fecha' AND object_id = OBJECT_ID(N'dbo.CambiosAuditoria'))
    CREATE INDEX IX_CambiosAuditoria_Vista_ClaveNegocio_Fecha ON dbo.CambiosAuditoria (Vista, ClaveNegocio, Fecha);
