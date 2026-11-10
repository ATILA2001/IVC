-- Verificación del mapeo EF Core (Data/Configurations) contra el esquema real.
-- Sólo lectura. Ejecutar en IVC_TEST (y en IVC antes del corte) y comparar la salida con
-- las configuraciones: nombre de columna, tipo, largo, precisión/escala y nulabilidad.
-- No fija la base con USE: elegir la base en la conexión.

SELECT
    c.TABLE_NAME                                   AS Tabla,
    c.ORDINAL_POSITION                             AS Posicion,
    c.COLUMN_NAME                                  AS Columna,
    c.DATA_TYPE                                    AS Tipo,
    c.CHARACTER_MAXIMUM_LENGTH                     AS Largo,
    c.NUMERIC_PRECISION                            AS Precisio,
    c.NUMERIC_SCALE                                AS Escala,
    c.IS_NULLABLE                                  AS Nulable,
    COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') AS EsIdentity,
    COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsComputed') AS Calculada,
    CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS EsPK
FROM INFORMATION_SCHEMA.COLUMNS c
LEFT JOIN (
    SELECT ku.TABLE_NAME, ku.COLUMN_NAME
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
    JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
      ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME AND tc.TABLE_NAME = ku.TABLE_NAME
    WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
) pk ON pk.TABLE_NAME = c.TABLE_NAME AND pk.COLUMN_NAME = c.COLUMN_NAME
WHERE c.TABLE_SCHEMA = 'dbo'
  AND c.TABLE_NAME IN (
      'Obras', 'BD_PROYECTOS', 'MOVIMIENTOS_GESTION', 'FORMULACION', 'LEGITIMOS_ABONOS',
      'Autorizantes', 'Certificados', 'Redeterminaciones', 'EXPEDIENTES_RELIQ',
      'Empresas', 'Contrata', 'Barrios', 'Areas', 'Conceptos', 'Estados_Autorizantes',
      'Estados_Redet', 'Tipo_Pago', 'PRIORIDADES', 'UNIDADES_MEDIDA',
      'Linea_de_Gestion', 'LINEA_DE_GESTION_FF',
      'ABM_PLANI', 'UsuariosVinculados', 'Usuarios',
      'PASES_SADE', 'GEDO', 'DEVENGADOS', 'CambiosAuditoria')
ORDER BY c.TABLE_NAME, c.ORDINAL_POSITION;

-- Claves únicas e índices (para confirmar BD_PROYECTOS.ID_BASE único y los índices por CODIGO_AUTORIZANTE).
SELECT
    OBJECT_NAME(i.object_id) AS Tabla,
    i.name                   AS Indice,
    i.is_unique              AS Unico,
    i.is_primary_key         AS EsPK,
    STRING_AGG(COL_NAME(ic.object_id, ic.column_id), ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columnas
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
WHERE OBJECT_SCHEMA_NAME(i.object_id) = 'dbo'
  AND OBJECT_NAME(i.object_id) IN ('Obras', 'BD_PROYECTOS', 'Autorizantes', 'Certificados', 'Redeterminaciones',
                                   'EXPEDIENTES_RELIQ', 'PASES_SADE', 'UsuariosVinculados')
GROUP BY i.object_id, i.name, i.is_unique, i.is_primary_key
ORDER BY Tabla, Indice;
