---
type: Pitfall
title: Kafka zapisuje dane poza wolumenem
description: "Obraz apache/kafka bez KAFKA_LOG_DIRS zapisuje dane w /tmp/kafka-logs w kontenerze - wolumen zostaje pusty, a topic i offsety giną przy odtworzeniu kontenera."
resource: ../../../docker-compose.yml
tags: [docker, kafka]
generated: { by: claude-code/claude-opus-5-5, at: 2026-10-15T22:00:00Z }
---

- **Objaw:** wolumen `kafka-data` jest pusty. Po odtworzeniu kontenera Kafki (np. `docker compose up -d` po zmianie konfiguracji) topic `product-price-changed` jest tworzony od nowa, a grupy konsumentów tracą offsety.
- **Przyczyna:** domyślne `log.dirs` w obrazie `apache/kafka` to `/tmp/kafka-logs`. Sam wolumen podpięty pod `/var/lib/kafka/data` niczego nie zmienia.
- **Rozwiązanie:** `KAFKA_LOG_DIRS: /var/lib/kafka/data`. Katalog należy do użytkownika `appuser`, a skrypt startowy obrazu sam formatuje pusty katalog (KRaft).
- **Sprawdzenie:** `docker exec rekrutacja-kafka ls /var/lib/kafka/data` pokazuje katalogi partycji, w tym `product-price-changed-*`.

Powiązane: [Obraz Dockera i przypięte wersje](../decisions/container-image.md)
