using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TicketAnalytics.Infrastructure.Persistence.Analytics;

/// <summary>
/// Crea la base LOCAL TicketAnalytics y su esquema. Usa EnsureCreated (que aplica
/// el modelo EF completo, incluido el HasData de CategoriaAlcance) y luego un SQL
/// idempotente que agrega tablas faltantes si la base ya existia de antes.
/// NUNCA toca MDB.
/// </summary>
public static class AnalyticsDbInitializer
{
    public static async Task InicializarAsync(AnalyticsContext ctx, ILogger logger, CancellationToken ct = default)
    {
        var servidor = ctx.Database.GetDbConnection().DataSource;
        var baseDatos = ctx.Database.GetDbConnection().Database;

        // 1. Crea base + TODAS las tablas del modelo + seed de HasData.
        //    Devuelve false si la base ya existia (entonces no crea nada).
        var creada = await ctx.Database.EnsureCreatedAsync(ct);
        logger.LogInformation("Base local {Db} en {Server}: {Estado}",
            baseDatos, servidor, creada ? "CREADA con todas las tablas y seed" : "ya existia");

        // 2. SIEMPRE ejecutamos el SQL idempotente: crea tablas faltantes y agrega
        //    columnas nuevas (ALTER TABLE ADD con IF COL_LENGTH). Es seguro correrlo
        //    en cada arranque, tanto si la base es nueva como si ya existia — así
        //    ninguna columna nueva queda sin aplicar. EnsureCreated por si solo no
        //    corre este SQL crudo ni los seeds, por eso va aparte y siempre.
        await ctx.Database.ExecuteSqlRawAsync(EsquemaFaltanteSql, ct);
        logger.LogInformation("Esquema verificado: tablas/columnas faltantes agregadas si aplicaba.");
        await SeedCategoriasSiVaciaAsync(ctx, logger, ct);
        await ctx.Database.ExecuteSqlRawAsync(SeedParametrosSql, ct);
        await ctx.Database.ExecuteSqlRawAsync(SistemasSql, ct);
        logger.LogInformation("Catálogo de sistemas verificado.");
        logger.LogInformation("Parámetros de negocio y grupo de analistas verificados.");

        var cats = await ctx.CategoriasAlcance.CountAsync(ct);
        logger.LogInformation("CategoriaAlcance tiene {N} categorias en alcance.", cats);
        if (cats == 0)
            logger.LogWarning("SIN CATEGORIAS EN ALCANCE: los endpoints devolveran vacio. " +
                              "Agrega con POST /api/admin/categorias.");
    }

    /// <summary>Siembra las categorias solo si la tabla existe y esta vacia
    /// (cubre el caso de base preexistente donde HasData no corrio).</summary>
    private static async Task SeedCategoriasSiVaciaAsync(AnalyticsContext ctx, ILogger logger, CancellationToken ct)
    {
        try
        {
            if (await ctx.CategoriasAlcance.AnyAsync(ct)) return;

            await ctx.Database.ExecuteSqlRawAsync(@"
INSERT INTO dbo.CategoriaAlcance (Persid, NombreCategoria, TipoCaso, Activo) VALUES
 ('pcat:405564', N'Tecnología de la Información.Problema/Error.Sistemas.Canales & Medios de Pago', 'Incidente', 1),
 ('pcat:405565', N'Tecnología de la Información.Problema/Error.Sistemas.Gestión e Información', 'Incidente', 1),
 ('pcat:405614', N'Tecnología de la Información.Solicitud.Sistemas.Canales & Medios de Pago', 'Solicitud', 1),
 ('pcat:405616', N'Tecnología de la Información.Solicitud.Sistemas.Gestión e Información', 'Solicitud', 1);", ct);
            logger.LogInformation("Seed: 4 categorias (2 Solicitud + 2 Problema/Error) insertadas.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo sembrar CategoriaAlcance (puede que la tabla no exista aun).");
        }
    }

    // SQL alineado EXACTAMENTE con las entidades del dominio.
    private const string EsquemaFaltanteSql = @"
IF OBJECT_ID('dbo.CategoriaAlcance','U') IS NULL
CREATE TABLE dbo.CategoriaAlcance (
    Persid          NVARCHAR(60)  NOT NULL PRIMARY KEY,
    NombreCategoria NVARCHAR(200) NOT NULL,
    TipoCaso        NVARCHAR(20)  NOT NULL,
    Activo          BIT           NOT NULL
);

IF OBJECT_ID('dbo.TicketSnapshot','U') IS NULL
CREATE TABLE dbo.TicketSnapshot (
    SnapshotId     BIGINT IDENTITY(1,1) PRIMARY KEY,
    CrId           INT           NOT NULL,
    RefNum         NVARCHAR(20)  NOT NULL,
    Persid         NVARCHAR(30)  NOT NULL,
    Categoria      NVARCHAR(60)  NOT NULL,
    Estado         NVARCHAR(24)  NULL,
    AssigneeUuid   BINARY(16)    NULL,
    GroupUuid      BINARY(16)    NULL,
    OpenDateEpoch  INT           NULL,
    CloseDateEpoch INT           NULL,
    SlaViolation   INT           NULL,
    ChangeId       INT           NULL,
    CapturadoEn    DATETIME2     NOT NULL,
    HashEstado     VARBINARY(32) NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TicketSnapshot_CrId_CapturadoEn')
CREATE INDEX IX_TicketSnapshot_CrId_CapturadoEn ON dbo.TicketSnapshot(CrId, CapturadoEn);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TicketSnapshot_RefNum')
CREATE INDEX IX_TicketSnapshot_RefNum ON dbo.TicketSnapshot(RefNum);

IF OBJECT_ID('dbo.TicketChangeLog','U') IS NULL
CREATE TABLE dbo.TicketChangeLog (
    ChangeLogId   BIGINT IDENTITY(1,1) PRIMARY KEY,
    CrId          INT           NOT NULL,
    RefNum        NVARCHAR(20)  NOT NULL,
    Campo         NVARCHAR(50)  NOT NULL,
    ValorAnterior NVARCHAR(200) NULL,
    ValorNuevo    NVARCHAR(200) NULL,
    DetectadoEn   DATETIME2     NOT NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TicketChangeLog_RefNum')
CREATE INDEX IX_TicketChangeLog_RefNum ON dbo.TicketChangeLog(RefNum);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TicketChangeLog_DetectadoEn')
CREATE INDEX IX_TicketChangeLog_DetectadoEn ON dbo.TicketChangeLog(DetectadoEn);

IF OBJECT_ID('dbo.TicketAnotacion','U') IS NULL
CREATE TABLE dbo.TicketAnotacion (
    AnotacionId BIGINT IDENTITY(1,1) PRIMARY KEY,
    RefNum      NVARCHAR(20)  NOT NULL,
    Autor       NVARCHAR(100) NOT NULL,
    Nota        NVARCHAR(MAX) NOT NULL,
    CreadoEn    DATETIME2     NOT NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TicketAnotacion_RefNum')
CREATE INDEX IX_TicketAnotacion_RefNum ON dbo.TicketAnotacion(RefNum);

IF OBJECT_ID('dbo.OrdenCambioComplejidad','U') IS NULL
CREATE TABLE dbo.OrdenCambioComplejidad (
    ChgRefNum   NVARCHAR(20)  NOT NULL PRIMARY KEY,
    Complejidad INT           NOT NULL,
    AsignadoPor NVARCHAR(100) NULL,
    AsignadoEn  DATETIME2     NOT NULL
);

IF OBJECT_ID('dbo.Proveedor','U') IS NULL
CREATE TABLE dbo.Proveedor (
    ProveedorId INT IDENTITY(1,1) PRIMARY KEY,
    Nombre      NVARCHAR(120) NOT NULL,
    Contacto    NVARCHAR(120) NULL,
    Email       NVARCHAR(150) NULL,
    Telefono    NVARCHAR(40)  NULL,
    Activo      BIT NOT NULL,
    SlaCriticoHoras INT NULL,
    SlaAltoHoras    INT NULL,
    SlaMedioHoras   INT NULL,
    SlaBajoHoras    INT NULL
);
-- Migración idempotente para bases anteriores
IF COL_LENGTH('dbo.Proveedor','Telefono') IS NULL ALTER TABLE dbo.Proveedor ADD Telefono NVARCHAR(40) NULL;
IF COL_LENGTH('dbo.Proveedor','SlaCriticoHoras') IS NULL ALTER TABLE dbo.Proveedor ADD SlaCriticoHoras INT NULL;
IF COL_LENGTH('dbo.Proveedor','SlaAltoHoras') IS NULL ALTER TABLE dbo.Proveedor ADD SlaAltoHoras INT NULL;
IF COL_LENGTH('dbo.Proveedor','SlaMedioHoras') IS NULL ALTER TABLE dbo.Proveedor ADD SlaMedioHoras INT NULL;
IF COL_LENGTH('dbo.Proveedor','SlaBajoHoras') IS NULL ALTER TABLE dbo.Proveedor ADD SlaBajoHoras INT NULL;


-- Seed de los 12 proveedores con su SLA por severidad (horas; días convertidos a horas).
-- Solo inserta si el nombre no existe, para no duplicar.
MERGE dbo.Proveedor AS t
USING (VALUES
    ('EIKON',                  4,  NULL, 12, 24),
    ('Extreme AlexSoft',       4,  NULL, 24, 72),
    ('ASM - Risk - FIM',       8,  NULL, 12, 24),
    ('IbSystems',              8,  NULL, 12, 36),
    ('Infocorp',              2,  NULL,  4, 16),
    ('LASISTEMAS - ISC',       4,  NULL, 24, 360),
    ('LEASING',                4,  NULL, 12, 24),
    ('LogicOne',              24,  48, NULL, 96),
    ('Monitor Plus',          10,  NULL, 48, 96),
    ('PKM Soluciones Digitales', 3, NULL, 24, 120),
    ('SAP-ITGES',              4,  48, 96, 192),
    ('HOSTSEVEN -VEOCRM',      4,  NULL, 12, 24),
    ('SII',                    3,  NULL,  8, 48)
) AS s (Nombre, Critico, Alto, Medio, Bajo)
ON t.Nombre = s.Nombre
WHEN NOT MATCHED THEN
    INSERT (Nombre, Activo, SlaCriticoHoras, SlaAltoHoras, SlaMedioHoras, SlaBajoHoras)
    VALUES (s.Nombre, 1, s.Critico, s.Alto, s.Medio, s.Bajo);

-- ============ Catálogo de sistemas (eje de clasificación) y tipos de corrección ============
-- El sistema reemplaza al antiguo enum SistemaAfectado. Cada sistema pertenece a UN
-- proveedor (N:1); ProveedorId NULL = por asignar o interno.
IF OBJECT_ID('dbo.Sistema','U') IS NULL
CREATE TABLE dbo.Sistema (
    SistemaId   INT IDENTITY(1,1) PRIMARY KEY,
    Nombre      NVARCHAR(120) NOT NULL,
    Descripcion NVARCHAR(400) NULL,
    ProveedorId INT           NULL,
    Criticidad  INT           NOT NULL CONSTRAINT DF_Sistema_Criticidad DEFAULT 0,
    Activo      BIT           NOT NULL CONSTRAINT DF_Sistema_Activo DEFAULT 1,
    Orden       INT           NOT NULL CONSTRAINT DF_Sistema_Orden DEFAULT 0,
    Color       NVARCHAR(20)  NULL,
    AplicaSubtipoActualizacion BIT NOT NULL CONSTRAINT DF_Sistema_AplicaSubtipo DEFAULT 0,
    CONSTRAINT FK_Sistema_Proveedor FOREIGN KEY (ProveedorId) REFERENCES dbo.Proveedor(ProveedorId)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Sistema_Nombre')
CREATE UNIQUE INDEX IX_Sistema_Nombre ON dbo.Sistema(Nombre);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Sistema_ProveedorId')
CREATE INDEX IX_Sistema_ProveedorId ON dbo.Sistema(ProveedorId);

IF OBJECT_ID('dbo.TipoCorreccion','U') IS NULL
CREATE TABLE dbo.TipoCorreccion (
    TipoCorreccionId INT IDENTITY(1,1) PRIMARY KEY,
    Nombre           NVARCHAR(120) NOT NULL,
    Descripcion      NVARCHAR(400) NULL,
    Activo           BIT           NOT NULL CONSTRAINT DF_TipoCorreccion_Activo DEFAULT 1,
    Orden            INT           NOT NULL CONSTRAINT DF_TipoCorreccion_Orden DEFAULT 0
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TipoCorreccion_Nombre')
CREATE UNIQUE INDEX IX_TipoCorreccion_Nombre ON dbo.TipoCorreccion(Nombre);

IF OBJECT_ID('dbo.TicketSeguimiento','U') IS NULL
CREATE TABLE dbo.TicketSeguimiento (
    SeguimientoId      BIGINT IDENTITY(1,1) PRIMARY KEY,
    RefNum             NVARCHAR(20)  NOT NULL,
    TipoOrigen         NVARCHAR(5)   NOT NULL DEFAULT 'cr',
    CrId               INT           NULL,
    Categoria          NVARCHAR(200) NULL,
    TipoCaso           NVARCHAR(20)  NULL,
    EtapaActual        INT           NOT NULL,
    TecnicoResponsable NVARCHAR(120) NULL,
    TecnicoUuid        BINARY(16)    NULL,
    OrdenCambioRef     NVARCHAR(20)  NULL,
    ContinuaBajoOC     BIT           NOT NULL,
    ProximosPasos      NVARCHAR(MAX) NULL,
    ProximoSeguimiento DATETIME2     NULL,
    Activo             BIT           NOT NULL,
    CreadoEn           DATETIME2     NOT NULL,
    ActualizadoEn      DATETIME2     NOT NULL,
    CreadoPor          NVARCHAR(100) NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TicketSeguimiento_RefNum_TipoOrigen')
CREATE UNIQUE INDEX IX_TicketSeguimiento_RefNum_TipoOrigen ON dbo.TicketSeguimiento(RefNum, TipoOrigen);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TicketSeguimiento_EtapaActual')
CREATE INDEX IX_TicketSeguimiento_EtapaActual ON dbo.TicketSeguimiento(EtapaActual);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TicketSeguimiento_Activo')
CREATE INDEX IX_TicketSeguimiento_Activo ON dbo.TicketSeguimiento(Activo);

-- ============================================================================
-- Reporte a proveedor y Reporte mensual VP
-- Orden correcto: la tabla ya existe arriba, ahora sí ALTER -> datos -> indices.
-- ============================================================================

-- Catalogo de categorias del reporte VP (debe existir ANTES de la FK)
IF OBJECT_ID('dbo.CategoriaReporteVP','U') IS NULL
CREATE TABLE dbo.CategoriaReporteVP (
    CategoriaId  INT IDENTITY(1,1) PRIMARY KEY,
    Nombre       NVARCHAR(120) NOT NULL,
    Descripcion  NVARCHAR(400) NULL,
    Orden        INT           NOT NULL DEFAULT 0,
    Activo       BIT           NOT NULL DEFAULT 1,
    Color        NVARCHAR(20)  NULL
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_CategoriaReporteVP_Nombre')
CREATE UNIQUE INDEX IX_CategoriaReporteVP_Nombre ON dbo.CategoriaReporteVP(Nombre);

-- Columnas nuevas del seguimiento.
-- IMPORTANTE: todo lo que LEA una columna recien agregada va dentro de EXEC(...).
-- SQL Server compila el batch completo y daria 'Invalid column name' si la
-- sentencia referenciara una columna creada en este mismo batch.
IF COL_LENGTH('dbo.TicketSeguimiento','ReportadoProveedor') IS NULL
    ALTER TABLE dbo.TicketSeguimiento ADD ReportadoProveedor INT NOT NULL CONSTRAINT DF_TS_ReportadoProveedor DEFAULT 2;
IF COL_LENGTH('dbo.TicketSeguimiento','VaReporteVP') IS NULL
    ALTER TABLE dbo.TicketSeguimiento ADD VaReporteVP BIT NOT NULL CONSTRAINT DF_TS_VaReporteVP DEFAULT 0;
IF COL_LENGTH('dbo.TicketSeguimiento','PeriodoReporteVP') IS NULL
    ALTER TABLE dbo.TicketSeguimiento ADD PeriodoReporteVP DATE NULL;
IF COL_LENGTH('dbo.TicketSeguimiento','CategoriaReporteVPId') IS NULL
    ALTER TABLE dbo.TicketSeguimiento ADD CategoriaReporteVPId INT NULL;
IF COL_LENGTH('dbo.TicketSeguimiento','CategoriaReporteVP') IS NULL
    ALTER TABLE dbo.TicketSeguimiento ADD CategoriaReporteVP NVARCHAR(120) NULL;
IF COL_LENGTH('dbo.TicketSeguimiento','EstadoReporteVP') IS NULL
    ALTER TABLE dbo.TicketSeguimiento ADD EstadoReporteVP INT NOT NULL CONSTRAINT DF_TS_EstadoReporteVP DEFAULT 0;
IF COL_LENGTH('dbo.TicketSeguimiento','NotaReporteVP') IS NULL
    ALTER TABLE dbo.TicketSeguimiento ADD NotaReporteVP NVARCHAR(2000) NULL;

-- Datos estructurados de la gestión (Sistema, Tipo Solución, causa raíz, etc.)
IF COL_LENGTH('dbo.TicketSeguimiento','TipoSolucion') IS NULL
    ALTER TABLE dbo.TicketSeguimiento ADD TipoSolucion INT NOT NULL CONSTRAINT DF_TS_TipoSolucion DEFAULT 0;
IF COL_LENGTH('dbo.TicketSeguimiento','TipoActualizacionDatos') IS NULL
    ALTER TABLE dbo.TicketSeguimiento ADD TipoActualizacionDatos INT NOT NULL CONSTRAINT DF_TS_TipoActDatos DEFAULT 0;
IF COL_LENGTH('dbo.TicketSeguimiento','FechaReporteProveedor') IS NULL
    ALTER TABLE dbo.TicketSeguimiento ADD FechaReporteProveedor DATETIME NULL;
IF COL_LENGTH('dbo.TicketSeguimiento','TicketProveedor') IS NULL
    ALTER TABLE dbo.TicketSeguimiento ADD TicketProveedor NVARCHAR(40) NULL;
IF COL_LENGTH('dbo.TicketSeguimiento','AreaDestino') IS NULL
    ALTER TABLE dbo.TicketSeguimiento ADD AreaDestino NVARCHAR(120) NULL;
IF COL_LENGTH('dbo.TicketSeguimiento','CausaRaiz') IS NULL
    ALTER TABLE dbo.TicketSeguimiento ADD CausaRaiz NVARCHAR(1000) NULL;
IF COL_LENGTH('dbo.TicketSeguimiento','TicketCausaRaiz') IS NULL
    ALTER TABLE dbo.TicketSeguimiento ADD TicketCausaRaiz NVARCHAR(40) NULL;
IF COL_LENGTH('dbo.TicketSeguimiento','SistemaId') IS NULL
    ALTER TABLE dbo.TicketSeguimiento ADD SistemaId INT NULL;
IF COL_LENGTH('dbo.TicketSeguimiento','TipoCorreccionId') IS NULL
    ALTER TABLE dbo.TicketSeguimiento ADD TipoCorreccionId INT NULL;

-- FK e índice de las columnas nuevas (en EXEC: se compilan después de agregarlas)
EXEC('
IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns fkc WHERE fkc.parent_object_id = OBJECT_ID(''dbo.TicketSeguimiento'') AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = ''SistemaId'')
    ALTER TABLE dbo.TicketSeguimiento ADD CONSTRAINT FK_TicketSeguimiento_Sistema
        FOREIGN KEY (SistemaId) REFERENCES dbo.Sistema(SistemaId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns fkc WHERE fkc.parent_object_id = OBJECT_ID(''dbo.TicketSeguimiento'') AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = ''TipoCorreccionId'')
    ALTER TABLE dbo.TicketSeguimiento ADD CONSTRAINT FK_TicketSeguimiento_TipoCorreccion
        FOREIGN KEY (TipoCorreccionId) REFERENCES dbo.TipoCorreccion(TipoCorreccionId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=''IX_TicketSeguimiento_SistemaId'')
    CREATE INDEX IX_TicketSeguimiento_SistemaId ON dbo.TicketSeguimiento(SistemaId);');

-- ============================================================================
-- Reparacion de columnas enteras del seguimiento.
-- Si una version anterior las dejo NULL, sin DEFAULT o con filas en NULL, aqui
-- se normalizan: se rellenan los NULL, se garantiza NOT NULL y se agrega el
-- constraint DEFAULT que faltaba. Es idempotente y no pierde informacion.
-- ============================================================================
EXEC('
DECLARE @col SYSNAME, @def INT, @sql NVARCHAR(MAX), @dc SYSNAME;
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT * FROM (VALUES
        (''ReportadoProveedor'', 2),
        (''EstadoReporteVP'', 0),
        (''TipoSolucion'', 0),
        (''TipoActualizacionDatos'', 0)
    ) AS t(c, d);
OPEN cur;
FETCH NEXT FROM cur INTO @col, @def;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF COL_LENGTH(''dbo.TicketSeguimiento'', @col) IS NOT NULL
    BEGIN
        -- 1) Rellenar filas existentes que quedaron en NULL
        SET @sql = N''UPDATE dbo.TicketSeguimiento SET ['' + @col + N''] = '' + CAST(@def AS NVARCHAR(10)) +
                   N'' WHERE ['' + @col + N''] IS NULL'';
        EXEC sp_executesql @sql;

        -- 2) Forzar NOT NULL
        SET @sql = N''ALTER TABLE dbo.TicketSeguimiento ALTER COLUMN ['' + @col + N''] INT NOT NULL'';
        BEGIN TRY EXEC sp_executesql @sql; END TRY BEGIN CATCH END CATCH;

        -- 3) Agregar el DEFAULT si no existe
        SET @dc = NULL;
        SELECT @dc = dc.name FROM sys.default_constraints dc
          JOIN sys.columns col ON col.object_id = dc.parent_object_id AND col.column_id = dc.parent_column_id
         WHERE dc.parent_object_id = OBJECT_ID(''dbo.TicketSeguimiento'') AND col.name = @col;
        IF @dc IS NULL
        BEGIN
            SET @sql = N''ALTER TABLE dbo.TicketSeguimiento ADD CONSTRAINT [DF_TS_'' + @col +
                       N''] DEFAULT '' + CAST(@def AS NVARCHAR(10)) + N'' FOR ['' + @col + N'']'';
            BEGIN TRY EXEC sp_executesql @sql; END TRY BEGIN CATCH END CATCH;
        END
    END
    FETCH NEXT FROM cur INTO @col, @def;
END
CLOSE cur; DEALLOCATE cur;');

-- Lo mismo para VaReporteVP, que es BIT
EXEC('
IF COL_LENGTH(''dbo.TicketSeguimiento'',''VaReporteVP'') IS NOT NULL
BEGIN
    UPDATE dbo.TicketSeguimiento SET VaReporteVP = 0 WHERE VaReporteVP IS NULL;
    BEGIN TRY ALTER TABLE dbo.TicketSeguimiento ALTER COLUMN VaReporteVP BIT NOT NULL; END TRY BEGIN CATCH END CATCH;
END');

-- Índices para agrupaciones del dashboard
EXEC('
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=''IX_TicketSeguimiento_TipoSolucion'')
CREATE INDEX IX_TicketSeguimiento_TipoSolucion ON dbo.TicketSeguimiento(TipoSolucion)');

-- Migracion segura: si una version anterior creo estas columnas como texto,
-- convertir a INT conservando el valor guardado (no se pierde informacion).
EXEC('
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(''dbo.TicketSeguimiento'')
             AND c.name = ''ReportadoProveedor'' AND t.name IN (''nvarchar'',''varchar''))
BEGIN
    ALTER TABLE dbo.TicketSeguimiento ADD ReportadoProveedor_Int INT NULL;
    EXEC(''UPDATE dbo.TicketSeguimiento SET ReportadoProveedor_Int =
        CASE WHEN LOWER(LTRIM(RTRIM(ReportadoProveedor))) IN (''''si'''',''''s'''') THEN 1
             WHEN LOWER(LTRIM(RTRIM(ReportadoProveedor))) = ''''no'''' THEN 0
             ELSE 2 END'');
    DECLARE @df SYSNAME;
    SELECT @df = dc.name FROM sys.default_constraints dc
      JOIN sys.columns col ON col.object_id = dc.parent_object_id AND col.column_id = dc.parent_column_id
     WHERE dc.parent_object_id = OBJECT_ID(''dbo.TicketSeguimiento'') AND col.name = ''ReportadoProveedor'';
    IF @df IS NOT NULL EXEC(''ALTER TABLE dbo.TicketSeguimiento DROP CONSTRAINT ['' + @df + '']'');
    ALTER TABLE dbo.TicketSeguimiento DROP COLUMN ReportadoProveedor;
    EXEC sp_rename ''dbo.TicketSeguimiento.ReportadoProveedor_Int'', ''ReportadoProveedor'', ''COLUMN'';
    EXEC(''UPDATE dbo.TicketSeguimiento SET ReportadoProveedor = 2 WHERE ReportadoProveedor IS NULL'');
    EXEC(''ALTER TABLE dbo.TicketSeguimiento ALTER COLUMN ReportadoProveedor INT NOT NULL'');
END');

EXEC('
IF EXISTS (SELECT 1 FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
           WHERE c.object_id = OBJECT_ID(''dbo.TicketSeguimiento'')
             AND c.name = ''EstadoReporteVP'' AND t.name IN (''nvarchar'',''varchar''))
BEGIN
    ALTER TABLE dbo.TicketSeguimiento ADD EstadoReporteVP_Int INT NULL;
    EXEC(''UPDATE dbo.TicketSeguimiento SET EstadoReporteVP_Int =
        CASE WHEN LOWER(LTRIM(RTRIM(EstadoReporteVP))) LIKE ''''%producc%'''' THEN 3
             WHEN LOWER(LTRIM(RTRIM(EstadoReporteVP))) = ''''qa'''' THEN 2
             WHEN LOWER(LTRIM(RTRIM(EstadoReporteVP))) LIKE ''''%desarrollo%'''' THEN 1
             WHEN LOWER(LTRIM(RTRIM(EstadoReporteVP))) LIKE ''''%complet%'''' THEN 4
             WHEN LOWER(LTRIM(RTRIM(EstadoReporteVP))) LIKE ''''%bloque%'''' THEN 5
             ELSE 0 END'');
    DECLARE @df2 SYSNAME;
    SELECT @df2 = dc.name FROM sys.default_constraints dc
      JOIN sys.columns col ON col.object_id = dc.parent_object_id AND col.column_id = dc.parent_column_id
     WHERE dc.parent_object_id = OBJECT_ID(''dbo.TicketSeguimiento'') AND col.name = ''EstadoReporteVP'';
    IF @df2 IS NOT NULL EXEC(''ALTER TABLE dbo.TicketSeguimiento DROP CONSTRAINT ['' + @df2 + '']'');
    ALTER TABLE dbo.TicketSeguimiento DROP COLUMN EstadoReporteVP;
    EXEC sp_rename ''dbo.TicketSeguimiento.EstadoReporteVP_Int'', ''EstadoReporteVP'', ''COLUMN'';
    EXEC(''UPDATE dbo.TicketSeguimiento SET EstadoReporteVP = 0 WHERE EstadoReporteVP IS NULL'');
    EXEC(''ALTER TABLE dbo.TicketSeguimiento ALTER COLUMN EstadoReporteVP INT NOT NULL'');
END');

-- Seed del catalogo (idempotente)
MERGE dbo.CategoriaReporteVP AS d
USING (VALUES
    (N'Canales & Medios de Pago', 10, '#15528E'),
    (N'Gestion e Informacion',     20, '#00897B'),
    (N'Core Bancario',             30, '#6B4EFF'),
    (N'Proveedores',               40, '#C75100'),
    (N'Infraestructura',           50, '#455A64'),
    (N'Otros',                     90, '#7B1FA2')
) AS s (Nombre, Orden, Color)
ON d.Nombre = s.Nombre
WHEN NOT MATCHED THEN INSERT (Nombre, Orden, Activo, Color) VALUES (s.Nombre, s.Orden, 1, s.Color);

-- Rescatar categorias que ya existieran como texto libre y enlazarlas al catalogo
EXEC('
INSERT INTO dbo.CategoriaReporteVP (Nombre, Orden, Activo)
SELECT DISTINCT LTRIM(RTRIM(ts.CategoriaReporteVP)), 100, 1
  FROM dbo.TicketSeguimiento ts
 WHERE ts.CategoriaReporteVP IS NOT NULL AND LTRIM(RTRIM(ts.CategoriaReporteVP)) <> ''''
   AND NOT EXISTS (SELECT 1 FROM dbo.CategoriaReporteVP c WHERE c.Nombre = LTRIM(RTRIM(ts.CategoriaReporteVP)))');

EXEC('
UPDATE ts SET ts.CategoriaReporteVPId = c.CategoriaId
  FROM dbo.TicketSeguimiento ts
  JOIN dbo.CategoriaReporteVP c ON c.Nombre = LTRIM(RTRIM(ts.CategoriaReporteVP))
 WHERE ts.CategoriaReporteVPId IS NULL AND ts.CategoriaReporteVP IS NOT NULL');

-- Indices del reporte VP (el reporte siempre consulta por VaReporteVP + Periodo)
EXEC('
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=''IX_TicketSeguimiento_VP_Periodo'')
CREATE INDEX IX_TicketSeguimiento_VP_Periodo ON dbo.TicketSeguimiento(VaReporteVP, PeriodoReporteVP)');
EXEC('
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=''IX_TicketSeguimiento_ReportadoProveedor'')
CREATE INDEX IX_TicketSeguimiento_ReportadoProveedor ON dbo.TicketSeguimiento(ReportadoProveedor)');

IF OBJECT_ID('dbo.SeguimientoActualizacion','U') IS NULL
CREATE TABLE dbo.SeguimientoActualizacion (
    ActualizacionId   BIGINT IDENTITY(1,1) PRIMARY KEY,
    SeguimientoId     BIGINT        NOT NULL,
    TipoActualizacion NVARCHAR(30)  NOT NULL,
    Contenido         NVARCHAR(MAX) NOT NULL,
    EtapaAnterior     INT           NULL,
    EtapaNueva        INT           NULL,
    Autor             NVARCHAR(100) NOT NULL,
    CreadoEn          DATETIME2     NOT NULL,
    CONSTRAINT FK_SeguimientoActualizacion_TicketSeguimiento FOREIGN KEY (SeguimientoId)
        REFERENCES dbo.TicketSeguimiento(SeguimientoId) ON DELETE CASCADE
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_SeguimientoActualizacion_SeguimientoId')
CREATE INDEX IX_SeguimientoActualizacion_SeguimientoId ON dbo.SeguimientoActualizacion(SeguimientoId);

IF OBJECT_ID('dbo.JiraRelacion','U') IS NULL
CREATE TABLE dbo.JiraRelacion (
    JiraId        BIGINT IDENTITY(1,1) PRIMARY KEY,
    SeguimientoId BIGINT        NOT NULL,
    ClaveJira     NVARCHAR(40)  NOT NULL,
    Titulo        NVARCHAR(300) NULL,
    Estado        INT           NOT NULL,
    Url           NVARCHAR(500) NULL,
    VinculadoEn   DATETIME2     NOT NULL,
    VinculadoPor  NVARCHAR(100) NULL,
    CONSTRAINT FK_JiraRelacion_TicketSeguimiento FOREIGN KEY (SeguimientoId)
        REFERENCES dbo.TicketSeguimiento(SeguimientoId) ON DELETE CASCADE
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_JiraRelacion_SeguimientoId')
CREATE INDEX IX_JiraRelacion_SeguimientoId ON dbo.JiraRelacion(SeguimientoId);

IF OBJECT_ID('dbo.ReporteProveedor','U') IS NULL
CREATE TABLE dbo.ReporteProveedor (
    ReporteId             BIGINT IDENTITY(1,1) PRIMARY KEY,
    SeguimientoId         BIGINT        NULL,
    ProveedorId           INT           NOT NULL,
    RefNumSistema         NVARCHAR(20)  NULL,
    TipoVinculo           NVARCHAR(5)   NULL,
    NumeroCasoExterno     NVARCHAR(60)  NULL,
    Titulo                NVARCHAR(250) NULL,
    Descripcion           NVARCHAR(MAX) NULL,
    FechaReporte          DATETIME2     NOT NULL,
    ContextoEnviado       NVARCHAR(MAX) NULL,
    RespuestasRecibidas   NVARCHAR(MAX) NULL,
    Urgencia              NVARCHAR(10)  NOT NULL DEFAULT 'Medio',
    SlaHorasRespuesta     INT           NULL,
    FechaPrimeraRespuesta DATETIME2     NULL,
    BrindoAsistencia      BIT           NULL,
    EntregoCambio         BIT           NULL,
    CumplioSla            BIT           NULL,
    Estado                INT           NOT NULL,
    UltimoSeguimiento     DATETIME2     NULL,
    ProximoSeguimiento    DATETIME2     NULL,
    ResponsableInterno    NVARCHAR(120) NULL,
    ResultadoSolucion     NVARCHAR(MAX) NULL,
    CreadoEn              DATETIME2     NOT NULL,
    CreadoPor             NVARCHAR(100) NULL,
    ModificadoEn          DATETIME2     NULL,
    ModificadoPor         NVARCHAR(100) NULL,
    CONSTRAINT FK_ReporteProveedor_TicketSeguimiento FOREIGN KEY (SeguimientoId)
        REFERENCES dbo.TicketSeguimiento(SeguimientoId),
    CONSTRAINT FK_ReporteProveedor_Proveedor FOREIGN KEY (ProveedorId)
        REFERENCES dbo.Proveedor(ProveedorId)
);
-- Migración idempotente para bases creadas con el esquema anterior
IF COL_LENGTH('dbo.ReporteProveedor','Titulo') IS NULL ALTER TABLE dbo.ReporteProveedor ADD Titulo NVARCHAR(250) NULL;
IF COL_LENGTH('dbo.ReporteProveedor','Descripcion') IS NULL ALTER TABLE dbo.ReporteProveedor ADD Descripcion NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.ReporteProveedor','RefNumSistema') IS NULL ALTER TABLE dbo.ReporteProveedor ADD RefNumSistema NVARCHAR(20) NULL;
IF COL_LENGTH('dbo.ReporteProveedor','TipoVinculo') IS NULL ALTER TABLE dbo.ReporteProveedor ADD TipoVinculo NVARCHAR(5) NULL;
IF COL_LENGTH('dbo.ReporteProveedor','Urgencia') IS NULL ALTER TABLE dbo.ReporteProveedor ADD Urgencia NVARCHAR(10) NOT NULL DEFAULT 'Medio';
IF COL_LENGTH('dbo.ReporteProveedor','SlaHorasRespuesta') IS NULL ALTER TABLE dbo.ReporteProveedor ADD SlaHorasRespuesta INT NULL;
IF COL_LENGTH('dbo.ReporteProveedor','FechaPrimeraRespuesta') IS NULL ALTER TABLE dbo.ReporteProveedor ADD FechaPrimeraRespuesta DATETIME2 NULL;
IF COL_LENGTH('dbo.ReporteProveedor','BrindoAsistencia') IS NULL ALTER TABLE dbo.ReporteProveedor ADD BrindoAsistencia BIT NULL;
IF COL_LENGTH('dbo.ReporteProveedor','EntregoCambio') IS NULL ALTER TABLE dbo.ReporteProveedor ADD EntregoCambio BIT NULL;
IF COL_LENGTH('dbo.ReporteProveedor','CumplioSla') IS NULL ALTER TABLE dbo.ReporteProveedor ADD CumplioSla BIT NULL;
IF COL_LENGTH('dbo.ReporteProveedor','CreadoPor') IS NULL ALTER TABLE dbo.ReporteProveedor ADD CreadoPor NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.ReporteProveedor','ModificadoEn') IS NULL ALTER TABLE dbo.ReporteProveedor ADD ModificadoEn DATETIME2 NULL;
IF COL_LENGTH('dbo.ReporteProveedor','ModificadoPor') IS NULL ALTER TABLE dbo.ReporteProveedor ADD ModificadoPor NVARCHAR(100) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ReporteProveedor_SeguimientoId')
CREATE INDEX IX_ReporteProveedor_SeguimientoId ON dbo.ReporteProveedor(SeguimientoId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ReporteProveedor_FechaReporte')
CREATE INDEX IX_ReporteProveedor_FechaReporte ON dbo.ReporteProveedor(FechaReporte);

-- Asegurar que SeguimientoId permita NULL (reportes sin ticket) y que la FK no sea CASCADE.
-- Solo actua si la columna quedo NOT NULL de una version anterior.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ReporteProveedor')
           AND name = 'SeguimientoId' AND is_nullable = 0)
BEGIN
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ReporteProveedor_TicketSeguimiento')
        ALTER TABLE dbo.ReporteProveedor DROP CONSTRAINT FK_ReporteProveedor_TicketSeguimiento;
    ALTER TABLE dbo.ReporteProveedor ALTER COLUMN SeguimientoId BIGINT NULL;
    ALTER TABLE dbo.ReporteProveedor ADD CONSTRAINT FK_ReporteProveedor_TicketSeguimiento
        FOREIGN KEY (SeguimientoId) REFERENCES dbo.TicketSeguimiento(SeguimientoId);
END

IF OBJECT_ID('dbo.Evidencia','U') IS NULL
CREATE TABLE dbo.Evidencia (
    EvidenciaId   BIGINT IDENTITY(1,1) PRIMARY KEY,
    SeguimientoId BIGINT        NOT NULL,
    Nombre        NVARCHAR(200) NOT NULL,
    Descripcion   NVARCHAR(MAX) NULL,
    RutaOUrl      NVARCHAR(500) NULL,
    TipoArchivo   NVARCHAR(50)  NULL,
    TamanoBytes   BIGINT        NULL,
    SubidoEn      DATETIME2     NOT NULL,
    SubidoPor     NVARCHAR(100) NULL,
    CONSTRAINT FK_Evidencia_TicketSeguimiento FOREIGN KEY (SeguimientoId)
        REFERENCES dbo.TicketSeguimiento(SeguimientoId) ON DELETE CASCADE
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Evidencia_SeguimientoId')
CREATE INDEX IX_Evidencia_SeguimientoId ON dbo.Evidencia(SeguimientoId);

-- ============ Trabajo diario ============
IF OBJECT_ID('dbo.Tecnico','U') IS NULL
CREATE TABLE dbo.Tecnico (
    TecnicoId   INT IDENTITY(1,1) PRIMARY KEY,
    Nombre      NVARCHAR(120) NOT NULL,
    ContactUuid BINARY(16)    NULL,
    Activo      BIT           NOT NULL DEFAULT 1,
    CreadoEn    DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Tecnico_Nombre')
CREATE UNIQUE INDEX IX_Tecnico_Nombre ON dbo.Tecnico(Nombre);

IF OBJECT_ID('dbo.ActividadDiaria','U') IS NULL
CREATE TABLE dbo.ActividadDiaria (
    ActividadId     BIGINT IDENTITY(1,1) PRIMARY KEY,
    Fecha           DATE          NOT NULL,
    TecnicoId       INT           NOT NULL,
    Actividad       NVARCHAR(MAX) NOT NULL,
    Tipo            INT           NOT NULL,
    NoSolicitud     NVARCHAR(60)  NULL,
    RefNumVinculado NVARCHAR(20)  NULL,
    TipoVinculo     NVARCHAR(5)   NULL,
    Proyecto        NVARCHAR(120) NULL,
    Horas           DECIMAL(5,2)  NOT NULL,
    Estado          INT           NOT NULL,
    Comentario      NVARCHAR(MAX) NULL,
    CreadoPor       NVARCHAR(100) NULL,
    CreadoEn        DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    ModificadoPor   NVARCHAR(100) NULL,
    ModificadoEn    DATETIME2     NULL,
    CONSTRAINT FK_ActividadDiaria_Tecnico FOREIGN KEY (TecnicoId) REFERENCES dbo.Tecnico(TecnicoId)
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ActividadDiaria_Fecha')
CREATE INDEX IX_ActividadDiaria_Fecha ON dbo.ActividadDiaria(Fecha);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ActividadDiaria_TecnicoId')
CREATE INDEX IX_ActividadDiaria_TecnicoId ON dbo.ActividadDiaria(TecnicoId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ActividadDiaria_RefNumVinculado')
CREATE INDEX IX_ActividadDiaria_RefNumVinculado ON dbo.ActividadDiaria(RefNumVinculado);

-- Clasificación de la actividad en conjunto con la gestión: sistema → proveedor → solución
IF COL_LENGTH('dbo.ActividadDiaria','SistemaId') IS NULL
    ALTER TABLE dbo.ActividadDiaria ADD SistemaId INT NULL;
IF COL_LENGTH('dbo.ActividadDiaria','ProveedorId') IS NULL
    ALTER TABLE dbo.ActividadDiaria ADD ProveedorId INT NULL;
IF COL_LENGTH('dbo.ActividadDiaria','TipoSolucion') IS NULL
    ALTER TABLE dbo.ActividadDiaria ADD TipoSolucion INT NOT NULL CONSTRAINT DF_AD_TipoSolucion DEFAULT 0;
EXEC('
IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns fkc WHERE fkc.parent_object_id = OBJECT_ID(''dbo.ActividadDiaria'') AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = ''SistemaId'')
    ALTER TABLE dbo.ActividadDiaria ADD CONSTRAINT FK_ActividadDiaria_Sistema
        FOREIGN KEY (SistemaId) REFERENCES dbo.Sistema(SistemaId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_key_columns fkc WHERE fkc.parent_object_id = OBJECT_ID(''dbo.ActividadDiaria'') AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = ''ProveedorId'')
    ALTER TABLE dbo.ActividadDiaria ADD CONSTRAINT FK_ActividadDiaria_Proveedor
        FOREIGN KEY (ProveedorId) REFERENCES dbo.Proveedor(ProveedorId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=''IX_ActividadDiaria_SistemaId'')
    CREATE INDEX IX_ActividadDiaria_SistemaId ON dbo.ActividadDiaria(SistemaId);');

-- ============ Parámetros de negocio (antes quemados en el código) ============
IF OBJECT_ID('dbo.ParametroSistema','U') IS NULL
CREATE TABLE dbo.ParametroSistema (
    Clave          NVARCHAR(80)  NOT NULL PRIMARY KEY,
    Valor          NVARCHAR(200) NOT NULL,
    Descripcion    NVARCHAR(400) NULL,
    ActualizadoEn  DATETIME2     NOT NULL,
    ActualizadoPor NVARCHAR(100) NULL
);

-- ============ Grupo de analistas del área (antes lista fija en GrupoAnalistas.cs) ============
IF OBJECT_ID('dbo.AnalistaGrupo','U') IS NULL
CREATE TABLE dbo.AnalistaGrupo (
    UserId   NVARCHAR(60) NOT NULL PRIMARY KEY,
    Activo   BIT          NOT NULL,
    Orden    INT          NOT NULL,
    CreadoEn DATETIME2    NOT NULL
);
";

    /// <summary>
    /// Semilla del catálogo de sistemas (solo inserta los que falten por nombre, para respetar
    /// lo que se edite desde la administración) y migración única desde el antiguo enum:
    /// si la columna entera TicketSeguimiento.Sistema todavía existe, sus valores (data de
    /// prueba) se pasan a SistemaId por nombre y la columna se elimina.
    /// Proveedores: solo EIKON está confirmado; el resto queda "por asignar" (NULL). No se
    /// inventan proveedores: Datawarehouse es interno de TI y no tiene proveedor externo.
    /// </summary>
    private const string SistemasSql = @"
MERGE dbo.Sistema AS d
USING (VALUES
    (N'BSC en Línea',             N'Banca en línea',                       10, '#15528E', 1, CAST(NULL AS NVARCHAR(120))),
    (N'ITC',                      NULL,                                    20, '#00897B', 0, NULL),
    (N'Extreme',                  NULL,                                    30, '#6B4EFF', 0, NULL),
    (N'Datawarehouse',            N'Interno — TI (sin proveedor externo)', 40, '#455A64', 0, NULL),
    (N'App Móvil',                NULL,                                    50, '#0288D1', 0, NULL),
    (N'Core Bancario',            NULL,                                    60, '#C75100', 0, NULL),
    (N'Cajeros / ATM',            NULL,                                    70, '#2E7D32', 0, NULL),
    (N'EIKON (Nómina / RRHH)',    N'Nómina y recursos humanos',            80, '#AD1457', 0, N'EIKON'),
    (N'Tarjetas de Crédito',      NULL,                                    90, '#F9A825', 0, NULL),
    (N'Canales / Medios de Pago', NULL,                                   100, '#5D4037', 0, NULL),
    (N'Otros',                    NULL,                                   990, '#7B1FA2', 0, NULL)
) AS s (Nombre, Descripcion, Orden, Color, AplicaSubtipo, Proveedor)
ON d.Nombre = s.Nombre
WHEN NOT MATCHED THEN
    INSERT (Nombre, Descripcion, ProveedorId, Criticidad, Activo, Orden, Color, AplicaSubtipoActualizacion)
    VALUES (s.Nombre, s.Descripcion,
            (SELECT p.ProveedorId FROM dbo.Proveedor p WHERE p.Nombre = s.Proveedor),
            0, 1, s.Orden, s.Color, s.AplicaSubtipo);

-- Migración única desde el enum (la columna vieja era INT con los valores del enum).
IF COL_LENGTH('dbo.TicketSeguimiento','Sistema') IS NOT NULL
BEGIN
    EXEC('
    UPDATE ts SET ts.SistemaId = si.SistemaId
      FROM dbo.TicketSeguimiento ts
      JOIN dbo.Sistema si ON si.Nombre = CASE ts.Sistema
            WHEN 1 THEN N''Extreme''       WHEN 2 THEN N''Datawarehouse''
            WHEN 3 THEN N''BSC en Línea''  WHEN 4 THEN N''ITC''
            WHEN 5 THEN N''App Móvil''     WHEN 6 THEN N''Core Bancario''
            WHEN 7 THEN N''Cajeros / ATM'' WHEN 99 THEN N''Otros'' END
     WHERE ts.SistemaId IS NULL;');

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TicketSeguimiento_Sistema' AND object_id = OBJECT_ID('dbo.TicketSeguimiento'))
        DROP INDEX IX_TicketSeguimiento_Sistema ON dbo.TicketSeguimiento;

    DECLARE @dfSis SYSNAME;
    SELECT @dfSis = dc.name FROM sys.default_constraints dc
      JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
     WHERE dc.parent_object_id = OBJECT_ID('dbo.TicketSeguimiento') AND c.name = 'Sistema';
    IF @dfSis IS NOT NULL EXEC('ALTER TABLE dbo.TicketSeguimiento DROP CONSTRAINT [' + @dfSis + ']');

    ALTER TABLE dbo.TicketSeguimiento DROP COLUMN Sistema;
END
";

    /// <summary>
    /// Semilla idempotente: inserta SOLO las claves que falten (los valores que el usuario ya
    /// cambió se respetan). El grupo de analistas se siembra solo si la tabla está vacía, para
    /// que una baja hecha desde la administración no vuelva a aparecer en el próximo arranque.
    /// Los valores son los que antes estaban quemados en el código.
    /// </summary>
    private const string SeedParametrosSql = @"
DECLARE @p TABLE (Clave NVARCHAR(80), Valor NVARCHAR(200), Descripcion NVARCHAR(400));
INSERT @p VALUES
 ('General.FechaBase',              '2026-01-01', N'Inicio del histórico analizado (yyyy-MM-dd). Ninguna consulta a MDB va antes de esta fecha.'),
 ('OC.DiasComplejidad.Alta',        '90',  N'Días hasta el vencimiento de una OC de complejidad Alta, contados desde su apertura.'),
 ('OC.DiasComplejidad.Media',       '60',  N'Días hasta el vencimiento de una OC de complejidad Media.'),
 ('OC.DiasComplejidad.Baja',        '30',  N'Días hasta el vencimiento de una OC de complejidad Baja.'),
 ('OC.DiasComplejidad.SinAsignar',  '60',  N'Días hasta el vencimiento de una OC sin complejidad asignada.'),
 ('OC.DiasVencePronto',             '7',   N'Una OC abierta ""vence pronto"" si le quedan estos días o menos.'),
 ('OC.DiasMargenVentana',           '10',  N'Días extra (además del mayor plazo por complejidad) que se miran hacia atrás desde la fecha pedida, para no perder OC abiertas antes que vencen en el período.'),
 ('Ticket.DiasVencePronto',         '2',   N'Un ticket abierto ""vence pronto"" si a su SLA le quedan estos días o menos.'),
 ('Ticket.PctRiesgoSla',            '80',  N'Porcentaje de SLA consumido a partir del cual un ticket abierto se considera próximo a vencer.'),
 ('Alertas.MinutosAntelacion',      '60',  N'Umbral urgente (minutos): faltando esto o menos para que venza el SLA, la alerta pasa a urgente.'),
 ('Alertas.MinutosAvisoTemprano',   '240', N'Aviso temprano (minutos): desde cuánto antes del vencimiento del SLA empiezan a avisarse los tickets.'),
 ('Seguimiento.DiasSinAvance',      '7',   N'Días sin actualización a partir de los cuales un caso se considera sin avance reciente.'),
 ('Cache.CatalogosMinutos',         '30',  N'Minutos que se conservan en memoria los catálogos de MDB (categorías, estados, contactos).'),
 ('Consultas.UmbralLentaMs',        '1000',N'Toda consulta que tarde más que esto (ms) queda registrada en el log con sus parámetros.');

INSERT INTO dbo.ParametroSistema (Clave, Valor, Descripcion, ActualizadoEn, ActualizadoPor)
SELECT p.Clave, p.Valor, p.Descripcion, SYSUTCDATETIME(), 'semilla'
FROM @p p
WHERE NOT EXISTS (SELECT 1 FROM dbo.ParametroSistema x WHERE x.Clave = p.Clave);

-- Alertas.MinutosAntelacion cambió de significado (ahora es el umbral urgente): solo se
-- actualiza su descripción; el valor que el usuario haya configurado se respeta.
UPDATE x SET x.Descripcion = p.Descripcion
FROM dbo.ParametroSistema x JOIN @p p ON p.Clave = x.Clave
WHERE x.Clave = 'Alertas.MinutosAntelacion' AND ISNULL(x.Descripcion, '') <> p.Descripcion;

IF NOT EXISTS (SELECT 1 FROM dbo.AnalistaGrupo)
INSERT INTO dbo.AnalistaGrupo (UserId, Activo, Orden, CreadoEn) VALUES
 ('jalvarado', 1, 1, SYSUTCDATETIME()),
 ('rceballos', 1, 2, SYSUTCDATETIME()),
 ('fsoto',     1, 3, SYSUTCDATETIME()),
 ('nimelo',    1, 4, SYSUTCDATETIME()),
 ('mpozo',     1, 5, SYSUTCDATETIME()),
 ('yasoto',    1, 6, SYSUTCDATETIME());
";
}
