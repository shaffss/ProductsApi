# Instrukcje

* [Uruchomienie lokalne](run-locally.md) - Wszystko w Dockerze albo infrastruktura w Dockerze i API z dotnet run / Visual Studio: profile, okno wyboru wariantu, telemetria.
* [Testy, format i CI](run-tests.md) - Testy jednostkowe i integracyjne (Testcontainers), format i analizatory jak w CI.
* [Zmiana schematu bazy](schema-change.md) - Zmiana modelu EF: migracje dla obu baz, skrypty db/, porównanie schematów i bazy deweloperskie.
* [Ponowienie wiadomości z dead-letter](dead-letter-redrive.md) - Jak znaleźć wiadomości outboxa po limicie prób i wysłać je ponownie po usunięciu przyczyny.
* [Dane demo i do benchmarków](load-data.md) - Wgrywanie 10 produktów demo i 100 tys. produktów do benchmarków skryptami z db/*/Publish.
* [Benchmarki wariantów](run-benchmarks.md) - Pomiar 4 wariantów: repozytoria w BenchmarkDotNet i API przez HTTP w k6, na danych benchmarkowych, z porządkiem po pomiarach.
