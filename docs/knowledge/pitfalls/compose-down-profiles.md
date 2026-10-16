---
type: Pitfall
title: docker compose down pomija profile
description: "Samo docker compose down nie usuwa kontenerów z profili, których polecenie nie wskazuje - zostają razem z siecią. Sprzątanie: docker compose --profile \"*\" down."
resource: ../../../docker-compose.yml
tags: [docker, compose]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-16T07:05:00Z }
---

- **Objaw:** po `docker compose down` kontener API wariantu i jego baza dalej działają. Sieć `rekrutacja_default` też zostaje („Resource is still in use”).
- **Przyczyna:** `down` działa na usługach z aktywnych profili. Bez `--profile` aktywny jest tylko profil domyślny z `.env` (`postgres`), więc kontenery wariantów (`efcore-postgres` itd.) i SQL Server są pomijane. `--remove-orphans` też ich nie usuwa, bo usługi istnieją w pliku.
- **Rozwiązanie:** `docker compose --profile "*" down` - gwiazdka włącza wszystkie profile. Dane zostają w wolumenach (`down -v` usuwa także dane).
- **Profil z `.env` działa tylko bez `--profile`:** `COMPOSE_PROFILES=postgres` daje samo `docker compose up -d` = PostgreSQL, Redis i Kafka. `--profile efcore-sqlserver` go zastępuje, więc PostgreSQL wtedy nie startuje.

Powiązane: [Uruchomienie lokalne](../playbooks/run-locally.md)
