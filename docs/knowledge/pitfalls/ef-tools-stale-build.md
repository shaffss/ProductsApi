---
type: Pitfall
title: dotnet ef ze starym buildem
description: "dotnet ef z --no-build po migrations add używa starego builda bez nowej migracji - baza powstaje pusta."
resource: ../../../dotnet-tools.json
tags: [ef-core, migrations, tooling]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T21:30:00Z }
---

- **Objaw:** `dotnet ef database update --no-build` zgłasza „No migrations were applied. The database is already up to date.”, a w nowej bazie nie ma tabel.
- **Przyczyna:** `migrations add` buduje projekt przed wygenerowaniem plików migracji. Zbudowany assembly nie zawiera więc nowej migracji, a `--no-build` używa właśnie jego.
- **Rozwiązanie:** po `migrations add` zawsze `dotnet build`, dopiero potem `database update --no-build` - albo bez `--no-build`.

Powiązane: [Zmiana schematu bazy](../playbooks/schema-change.md)
