#!/usr/bin/env bash
# Launcher dla Linuksa - odpowiednik launcher.ps1 z Windows (profil "Launcher" w launchSettings.json albo
# dotnet run --project src/Products.Api -- --launcher). Wybór wariantu w terminalu (whiptail; bez niego zwykłe
# pytania), potem kontenery wariantu w Dockerze. Bez panelu po starcie, bo terminal zajmują logi API.
# Wynik na stdout (fd 3, dla DevLauncher): argumenty konfiguracji "--Klucz=wartość" albo adres API w kontenerze,
# po jednym w linii, zakończone pustą linią; brak wyniku - anulowano. Menu i wyjście dockera idą na terminal.
# Ostatni wybór zapamiętany w ${XDG_CONFIG_HOME:-~/.config}/ProductsApi/launcher.conf.
set -uo pipefail

if ! (: </dev/tty) 2>/dev/null; then
    echo 'Launcher wymaga terminala: dotnet run --project src/Products.Api -- --launcher' >&2
    exit 1
fi
exec 3>&1 1>/dev/tty

config="${XDG_CONFIG_HOME:-$HOME/.config}/ProductsApi/launcher.conf"
ORM=EfCore DATABASE=Postgres API_CONTAINER=false CACHE=true KAFKA=true DASHBOARD=false SCHEMA=None DOCKER=true
if [[ -f $config ]]; then
    while IFS='=' read -r key value; do
        case $key in ORM | DATABASE | API_CONTAINER | CACHE | KAFKA | DASHBOARD | SCHEMA | DOCKER) printf -v "$key" '%s' "$value" ;; esac
    done <"$config"
fi

root=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
while [[ $root != / && ! -f $root/docker-compose.yml ]]; do root=$(dirname "$root"); done

# docker z limitem czasu: zawieszony Docker nie blokuje startu bez końca. timeout uruchamia polecenie poza
# pierwszym planem terminala, więc bez wejścia z terminala (odczyt zatrzymałby proces sygnałem SIGTTIN).
docker_t() { timeout -k 5 "$1" docker "${@:2}" </dev/null; }
compose() { docker_t "$1" compose -f "$root/docker-compose.yml" --progress plain "${@:2}"; }

variant() { echo "${ORM,,}-${DATABASE,,}"; }
toggle() { [[ $1 == true ]] && echo false || echo true; }
yesno() { [[ $1 == true ]] && echo tak || echo nie; }

# Kontenery potrzebne wariantowi (bez kontenera API). API w kontenerze: zawsze Redis i Kafka (zależności w compose).
needed() {
    local list=("${DATABASE,,}")
    [[ $API_CONTAINER == true || $CACHE == true ]] && list+=(redis)
    [[ $API_CONTAINER == true || $KAFKA == true ]] && list+=(kafka)
    [[ $DASHBOARD == true ]] && list+=(aspire-dashboard)
    echo "${list[@]}"
}

# --- status kontenerów z docker ps (szybkie, nie tworzy kontenerów); "created" to objaw zawieszonego Dockera ---

declare -A states
refresh_status() {
    states=() docker_ok=true stuck=false refreshed=$(date '+%d.%m.%Y %H:%M:%S')
    local lines service state status
    if ! lines=$(docker_t 10 ps -a --filter label=com.docker.compose.project=rekrutacja \
        --format '{{.Label "com.docker.compose.service"}}|{{.State}}|{{.Status}}' 2>/dev/null); then
        docker_ok=false
        return
    fi
    while IFS='|' read -r service state status; do
        [[ -n $service ]] && states[$service]="$state|$status"
        [[ $state == created ]] && stuck=true
    done <<<"$lines"
}

state_text() {
    local state status
    [[ $docker_ok == true ]] || { echo 'Docker nie odpowiada'; return; }
    [[ -n ${states[$1]:-} ]] || { echo 'brak kontenera'; return; }
    IFS='|' read -r state status <<<"${states[$1]}"
    case $state in
        running) [[ $status == *'health: starting'* ]] && echo 'startuje' || echo 'działa' ;;
        created) echo 'utknął przy starcie (zrestartuj Dockera)' ;;
        *) echo "zatrzymany ($state)" ;;
    esac
}

status_text() {
    local api needed_list service mark missing=()
    api="api-$(variant)"
    needed_list=" $(needed) $([[ $API_CONTAINER == true ]] && echo "$api") "
    echo "Kontenery (* - potrzebne wariantowi), odświeżono $refreshed:"
    for service in postgres sqlserver redis kafka aspire-dashboard "$api"; do
        mark=' '
        if [[ $needed_list == *" $service "* ]]; then
            mark='*'
            [[ $(state_text "$service") == 'działa' ]] || missing+=("$service")
        fi
        printf ' %s %-22s %s\n' "$mark" "$service" "$(state_text "$service")"
    done
    if ((${#missing[@]} == 0)); then
        echo 'Wszystkie potrzebne kontenery działają - start API od razu.'
    elif [[ $stuck == true ]]; then
        echo "Nie działa: ${missing[*]}. Kontener utknął przy starcie - zrestartuj Dockera."
    elif [[ $API_CONTAINER == true || $DOCKER == true ]]; then
        echo "Do uruchomienia: ${missing[*]}."
    else
        echo "Nie działa: ${missing[*]} - włącz 'Kontenery' albo uruchom sam."
    fi
}

# --- menu: wybór pozycji przełącza jej wartość; "Uruchom" kończy wybór ---

# Opcje bez znaczenia dla API w kontenerze (ustawienia z docker-compose.yml) pokazane jako stałe
option() { [[ $API_CONTAINER == true ]] && echo '(z docker-compose.yml)' || echo "$1"; }
menu_items() {
    items=(
        orm "ORM: $ORM"
        database "Baza danych: $DATABASE"
        api "API: $([[ $API_CONTAINER == true ]] && echo 'w kontenerze (compose, bez debuggera)' || echo 'z dotnet run / IDE')"
        cache "Cache (Redis): $(option "$(yesno "$CACHE")")"
        kafka "Zdarzenia (Kafka): $(option "$(yesno "$KAFKA")")"
        dashboard "Telemetria (Aspire Dashboard): $(yesno "$DASHBOARD")"
        schema "Schemat przy starcie: $(option "$SCHEMA")"
        docker "Kontenery (compose): $(option "$([[ $DOCKER == true ]] && echo 'uruchom bazę i zaznaczone usługi' || echo nie)")"
        refresh 'Odśwież status kontenerów'
        start '>> Uruchom'
    )
}

# $1 - tekst nad listą, $2 - domyślna pozycja, dalej pary: znacznik opis. Wynik - znacznik; kod 1 - anulowano.
choose() {
    local text=$1 default=$2 line lines=0 rows list
    shift 2
    if command -v whiptail >/dev/null; then
        while IFS= read -r line; do lines=$((lines + 1 + ${#line} / 75)); done <<<"$text" # z zawijaniem
        rows=$(tput lines 2>/dev/null || echo 24)
        list=$((rows - lines - 8))
        ((list > $# / 2)) && list=$(($# / 2))
        ((list < 3)) && list=3
        whiptail --title 'Products API - uruchomienie' --notags --ok-button Wybierz --cancel-button Anuluj \
            --default-item "$default" --menu "$text" $((lines + list + 6)) 78 "$list" "$@" \
            4>&1 1>/dev/tty 2>&4 </dev/tty
        return
    fi
    local tags=() i=0 answer
    printf '\n%s\n\n' "$text" >/dev/tty
    while (($#)); do
        tags+=("$1")
        printf '%3d) %s\n' $((++i)) "$2" >/dev/tty
        shift 2
    done
    printf 'Numer pozycji (Enter - Uruchom, q - anuluj): ' >/dev/tty
    read -r answer </dev/tty || return 1
    case $answer in
        q | Q) return 1 ;;
        '') echo start ;;
        *[!0-9]*) echo "$default" ;;
        *) ((answer >= 1 && answer <= i)) && echo "${tags[answer - 1]}" || echo "$default" ;;
    esac
}

refresh_status
choice=start
while true; do
    menu_items
    choice=$(choose "$(status_text)" "$choice" "${items[@]}") || exit 1
    case $choice in
        orm) ORM=$([[ $ORM == EfCore ]] && echo Dapper || echo EfCore) ;;
        database) DATABASE=$([[ $DATABASE == Postgres ]] && echo SqlServer || echo Postgres) ;;
        api) API_CONTAINER=$(toggle "$API_CONTAINER") ;;
        dashboard) DASHBOARD=$(toggle "$DASHBOARD") ;;
        refresh) refresh_status ;;
        start) break ;;
    esac
    if [[ $API_CONTAINER == false ]]; then
        case $choice in
            cache) CACHE=$(toggle "$CACHE") ;;
            kafka) KAFKA=$(toggle "$KAFKA") ;;
            docker) DOCKER=$(toggle "$DOCKER") ;;
            schema) SCHEMA=$(case $SCHEMA in None) echo Migrations ;; Migrations) echo Scripts ;; *) echo None ;; esac) ;;
        esac
    fi
done
clear 2>/dev/null

mkdir -p "$(dirname "$config")"
printf '%s\n' "ORM=$ORM" "DATABASE=$DATABASE" "API_CONTAINER=$API_CONTAINER" "CACHE=$CACHE" "KAFKA=$KAFKA" \
    "DASHBOARD=$DASHBOARD" "SCHEMA=$SCHEMA" "DOCKER=$DOCKER" >"$config"

# --- start: wyjście docker compose na bieżąco w terminalu ---

fail() {
    printf '\nBłąd: %s\nStart API anulowany.\n' "$1"
    exit 1
}

# GET przez /dev/tcp - bez curl i wget, których czysta instalacja może nie mieć; sukces - status 2xx
http_ok() {
    (
        exec 5<>"/dev/tcp/127.0.0.1/$1" &&
            printf 'GET %s HTTP/1.0\r\nHost: localhost\r\n\r\n' "$2" >&5 &&
            read -r -t 2 _ status _ <&5 && [[ $status == 2* ]]
    ) 2>/dev/null
}

services=$(needed)
if [[ $API_CONTAINER == true ]]; then
    # kontenery API innych wariantów usuwane (działa jeden wariant), obraz z bieżącego kodu, czekanie na /health/live
    current=$(variant) others=()
    for other in efcore-postgres efcore-sqlserver dapper-postgres dapper-sqlserver; do
        [[ $other == "$current" ]] || others+=("api-$other")
    done
    echo '> Inne warianty API: usuwanie kontenerów - działa jeden wariant'
    compose 120 --profile '*' rm -s -f "${others[@]}" || fail 'docker compose rm'
    echo "> API w kontenerze: $current - docker compose up --build (obraz z bieżącego kodu), $services"
    # shellcheck disable=SC2086 # lista usług rozdzielona spacjami
    compose 900 --profile "$current" up -d --build "api-$current" $services || fail 'docker compose up'
    port=$(compose 10 --profile "$current" port "api-$current" 8080 | head -n 1)
    port=${port##*:}
    url="http://localhost:$port/"
    echo "> Czekanie na ${url}health/live (schemat bazy przy starcie)"
    deadline=$((SECONDS + 180))
    until http_ok "$port" /health/live; do
        ((SECONDS < deadline)) || fail "API nie odpowiada pod ${url}health/live po 180 s - sprawdź docker logs api-$current"
        sleep 1
    done
    echo "> API działa: $url"
    if [[ -n ${DISPLAY:-}${WAYLAND_DISPLAY:-} ]] && command -v xdg-open >/dev/null; then
        xdg-open "$url" >/dev/null 2>&1 &
    fi
    printf '%s\n\n' "$url" >&3
    exit 0
fi

if [[ $DOCKER == true ]]; then
    # obrazy już pobrane: start trwa sekundy, więc krótki limit szybko wykrywa zawieszony Docker;
    # brakujące obrazy (pierwsze uruchomienie) - zapas na pobieranie
    # shellcheck disable=SC2086
    images=$(compose 30 config --images $services) || fail 'docker compose config'
    limit=90
    # shellcheck disable=SC2086
    docker_t 30 image inspect $images >/dev/null 2>&1 || limit=600
    echo "> Kontenery: $services$( ((limit > 90)) && echo ' - pobieranie brakujących obrazów')"
    # shellcheck disable=SC2086
    compose "$limit" up -d --wait --wait-timeout $((limit - 10)) $services || fail 'docker compose up'
    if [[ $DATABASE == SqlServer ]]; then
        # jednorazowy kontener sqlserver-init z compose (--wait traktuje jego zakończenie jak błąd, więc osobno)
        echo '> Baza Products w SQL Server (sqlserver-init)'
        compose 120 --profile sqlserver run --rm --no-deps -T sqlserver-init || fail 'sqlserver-init'
    fi
fi

{
    echo "--Persistence:Orm=$ORM"
    echo "--Persistence:Database=$DATABASE"
    echo "--Cache:Enabled=$CACHE"
    echo "--Kafka:Enabled=$KAFKA"
    echo "--Database:Schema=$SCHEMA"
    [[ $DASHBOARD == true ]] || echo '--OTEL_EXPORTER_OTLP_ENDPOINT=' # pusty adres wyłącza telemetrię
    echo
} >&3
