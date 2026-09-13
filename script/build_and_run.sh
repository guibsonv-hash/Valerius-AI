#!/bin/bash
set -euo pipefail

mode="${1:-run}"
root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
app_name="ValeriusAI"
app_path="$HOME/Applications/Valerius AI.app"
binary="$app_path/Contents/MacOS/$app_name"

cd "$root_dir"
pkill -x "$app_name" >/dev/null 2>&1 || true
./scripts/package-macos.sh

open_app() {
  /usr/bin/open -n "$app_path"
}

case "$mode" in
  run)
    open_app
    ;;
  --debug|debug)
    lldb -- "$binary"
    ;;
  --logs|logs)
    open_app
    /usr/bin/log stream --info --style compact --predicate 'process == "ValeriusAI"'
    ;;
  --telemetry|telemetry)
    open_app
    /usr/bin/log stream --info --style compact --predicate 'subsystem == "com.valerius.localai"'
    ;;
  --verify|verify)
    open_app
    for _ in 1 2 3 4 5; do
      pgrep -x "$app_name" >/dev/null && exit 0
      sleep 1
    done
    echo "Valerius AI não permaneceu em execução." >&2
    exit 1
    ;;
  *)
    echo "uso: $0 [run|--debug|--logs|--telemetry|--verify]" >&2
    exit 2
    ;;
esac
