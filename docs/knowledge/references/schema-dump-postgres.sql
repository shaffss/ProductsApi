-- Zrzut schematu PostgreSQL (kolumny, klucze, indeksy, CHECK, dane Currencies) do porównania baz.
-- Instrukcja: ../playbooks/schema-change.md
SELECT line FROM (
 SELECT 'COL ' || c.table_name || '.' || c.column_name || ' ' || format_type(a.atttypid, a.atttypmod) || CASE WHEN c.is_nullable = 'YES' THEN ' NULL' ELSE ' NOT NULL' END
   || CASE WHEN c.is_identity = 'YES' THEN ' IDENTITY ' || c.identity_generation ELSE '' END
   || ' DEFAULT=' || coalesce(replace(replace(pg_get_expr(d.adbin, d.adrelid), '(', ''), ')', ''), '-') AS line
 FROM information_schema.columns c
 JOIN pg_attribute a ON a.attrelid = format('%I', c.table_name)::regclass AND a.attname = c.column_name
 LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
 WHERE c.table_schema = 'public' AND c.table_name <> '__EFMigrationsHistory'
 UNION ALL
 SELECT CASE con.contype WHEN 'p' THEN 'KEY ' WHEN 'u' THEN 'KEY ' WHEN 'f' THEN 'FK ' ELSE 'CHECK ' END || rel.relname || ' ' || con.conname || ' ' || pg_get_constraintdef(con.oid)
 FROM pg_constraint con JOIN pg_class rel ON rel.oid = con.conrelid JOIN pg_namespace n ON n.oid = rel.relnamespace
 WHERE n.nspname = 'public' AND rel.relname <> '__EFMigrationsHistory' AND con.contype IN ('p','u','f','c')
 UNION ALL
 SELECT 'INDEX ' || tablename || ' ' || indexname || ' ' || regexp_replace(indexdef, '^.* USING ', 'USING ')
 FROM pg_indexes i WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
   AND NOT EXISTS (SELECT 1 FROM pg_constraint con WHERE con.conname = i.indexname)
 UNION ALL
 SELECT 'DATA Currencies ' || "Id" || ' ' || "Code" || ' ' || "Name" || ' ' || "IsActive" FROM "Currencies"
) x ORDER BY line;
