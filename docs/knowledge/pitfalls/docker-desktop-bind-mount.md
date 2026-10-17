---
type: Pitfall
title: Docker Desktop zawiesza start kontenera z plikiem z Windows
description: "Start kontenera z plikiem montowanym z dysku Windows potrafi zawisnąć, a potem silnik nie startuje żadnego nowego kontenera. Compose nie montuje więc plików, a launcher nie tworzy zbędnych kontenerów i ma limit czasu."
resource: ../../../docker-compose.yml
tags: [docker, windows, compose]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-16T11:00:00Z }
---

- **Objaw:**
  - `docker compose up` albo `docker compose run` nie kończy się, a kontener zostaje w stanie `Created`;
  - potem żaden nowy kontener nie startuje, nawet bez montowania (`docker run --rm alpine echo ok` wisi);
  - działające kontenery pracują normalnie.
- **Przyczyna:** udostępnianie plików Docker Desktop (backend Hyper-V) dla ścieżek Windows. Problem zdarzał się nieregularnie, zawsze przy kontenerze z plikiem z dysku - u nas `sqlserver-init` montował `000_database.sql`.
- **Rozwiązanie w projekcie:**
  - **bez montowania plików w compose:** `sqlserver-init` ma instrukcje z `000_database.sql` wpisane w polecenie `sqlcmd -Q` (dwa miejsca do utrzymania, oba z komentarzem); zostały tylko nazwane wolumeny;
  - **launcher bez nowych kontenerów, gdy infrastruktura działa:** bazę SQL Server zakłada przez `docker compose exec` w działającym kontenerze `sqlserver` (polecenie z definicji `sqlserver-init`), zamiast `run --rm sqlserver-init`. Przy zawieszonym silniku start API z działającą infrastrukturą nadal się udaje;
  - **limit czasu w launcherze:** `docker compose` jest zabijany po 90 s (`up --wait`, obrazy pobrane; 600 s, gdy trzeba je pobrać) albo 120 s (`exec`), a okno pokazuje komunikat zamiast wisieć bez końca. Okno postępu ma licznik i „Przerwij”;
  - **wykrycie w statusie launchera:** kontener w stanie `created` jest czerwony („utknął przy starcie”), a podsumowanie każe najpierw zrestartować Docker Desktop.
- **Gdy się zdarzy:**
  - zrestartuj Docker Desktop; po restarcie nic nie wstaje samo (brak polityki restartu), więc uruchom wariant ponownie;
  - zawieszone kontenery `Created` usuń: `docker rm -f <nazwa>`;
  - jeśli Docker Desktop nie wstaje po restarcie, w `%LOCALAPPDATA%\Docker\backend.error.json` może być błąd usuwania gniazda `%LOCALAPPDATA%\Docker\run\dockerInference` (Docker Model Runner). Usuń stare pliki z `run\` albo wyłącz Model Runner w ustawieniach Docker Desktop.

Powiązane: [Równoległe tworzenie bazy](concurrent-database-creation.md), [Uruchomienie lokalne](../playbooks/run-locally.md)
