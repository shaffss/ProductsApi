-- PostgreSQL: baza products
-- Skrypty są idempotentne - można je uruchamiać wielokrotnie, osobno lub scalone (merge_files.cmd).
-- Ten plik wymaga psql (\gexec, \connect); pozostałe to czysty SQL - uruchamiać w bazie products.

SELECT 'CREATE DATABASE products'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'products')\gexec

\connect products
