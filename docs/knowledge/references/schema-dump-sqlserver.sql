-- Zrzut schematu SQL Server (kolumny, klucze, indeksy, CHECK, dane Currencies) do porównania baz.
-- Instrukcja: ../playbooks/schema-change.md
SET NOCOUNT ON;
SELECT line FROM (
 SELECT CONCAT('COL ', t.name, '.', c.name, ' ', ty.name,
   CASE WHEN ty.name IN ('char','varchar') THEN CONCAT('(', IIF(c.max_length = -1, 'max', CAST(c.max_length AS varchar)), ')')
        WHEN ty.name IN ('nchar','nvarchar') THEN CONCAT('(', IIF(c.max_length = -1, 'max', CAST(c.max_length / 2 AS varchar)), ')')
        WHEN ty.name IN ('decimal','numeric') THEN CONCAT('(', c.precision, ',', c.scale, ')')
        WHEN ty.name = 'datetime2' THEN CONCAT('(', c.scale, ')') ELSE '' END,
   IIF(c.is_nullable = 1, ' NULL', ' NOT NULL'), IIF(c.is_identity = 1, ' IDENTITY', ''),
   ' DEFAULT=', REPLACE(REPLACE(ISNULL(dc.definition, '-'), '(', ''), ')', '')) AS line
 FROM sys.tables t JOIN sys.columns c ON c.object_id = t.object_id JOIN sys.types ty ON ty.user_type_id = c.user_type_id
 LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = t.object_id AND dc.parent_column_id = c.column_id
 WHERE t.name <> '__EFMigrationsHistory'
 UNION ALL
 SELECT CONCAT('KEY ', t.name, ' ', kc.name, ' ', kc.type_desc, ' ', i.type_desc, ' (',
   (SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal) FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id), ')')
 FROM sys.key_constraints kc JOIN sys.tables t ON t.object_id = kc.parent_object_id JOIN sys.indexes i ON i.object_id = kc.parent_object_id AND i.index_id = kc.unique_index_id
 WHERE t.name <> '__EFMigrationsHistory'
 UNION ALL
 SELECT CONCAT('FK ', pt.name, ' ', fk.name, ' (', pc.name, ') -> ', rt.name, ' (', rc.name, ') ON DELETE ', fk.delete_referential_action_desc)
 FROM sys.foreign_keys fk JOIN sys.tables pt ON pt.object_id = fk.parent_object_id JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
 JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
 JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
 UNION ALL
 SELECT CONCAT('CHECK ', t.name, ' ', cc.name, ' ', cc.definition) FROM sys.check_constraints cc JOIN sys.tables t ON t.object_id = cc.parent_object_id
 UNION ALL
 SELECT CONCAT('INDEX ', t.name, ' ', i.name, ' ', i.type_desc, IIF(i.is_unique = 1, ' UNIQUE', ''), ' (',
   (SELECT STRING_AGG(c.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal) FROM sys.index_columns ic JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id), ')',
   ISNULL(' WHERE ' + i.filter_definition, ''))
 FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
 WHERE i.is_primary_key = 0 AND i.is_unique_constraint = 0 AND i.type > 0 AND t.name <> '__EFMigrationsHistory'
 UNION ALL
 SELECT CONCAT('DATA Currencies ', Id, ' ', Code, ' ', Name, ' ', IsActive) COLLATE Latin1_General_CI_AS_KS_WS FROM dbo.Currencies
) x ORDER BY line;
