#!/usr/bin/env bash
# run-broiler-cli.sh — Capture output from wpt.live using Broiler's Browser.CLI
#
# Usage:
#   ./tests/wpt/run-broiler-cli.sh [--urls <manifest>] [--output-dir <dir>] [--timeout <sec>]
#
# For each URL in the manifest, the script runs:
#   dotnet run --project src/Broiler.Cli -- --url <URL> --output <file>.html
#   dotnet run --project src/Broiler.Cli -- --url <URL> --output <file>.txt
#
# Prerequisites: .NET 8 SDK

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
MANIFEST="$SCRIPT_DIR/wpt-urls.txt"
OUTPUT_DIR="$SCRIPT_DIR/results/broiler"
TIMEOUT=30

# ── Argument parsing ─────────────────────────────────────────────────────
while [[ $# -gt 0 ]]; do
    case "$1" in
        --urls)      MANIFEST="$2";   shift 2 ;;
        --output-dir) OUTPUT_DIR="$2"; shift 2 ;;
        --timeout)   TIMEOUT="$2";    shift 2 ;;
        -h|--help)
            echo "Usage: $0 [--urls <manifest>] [--output-dir <dir>] [--timeout <sec>]"
            exit 0 ;;
        *) echo "Unknown option: $1" >&2; exit 1 ;;
    esac
done

mkdir -p "$OUTPUT_DIR"

# ── Build CLI once ───────────────────────────────────────────────────────
echo "Building Broiler.Cli …"
dotnet build "$REPO_ROOT/src/Broiler.Cli/Broiler.Cli.csproj" --configuration Release --nologo -v q

CLI_PROJECT="$REPO_ROOT/src/Broiler.Cli/Broiler.Cli.csproj"

# ── Helper to run one test ───────────────────────────────────────────────
run_one() {
    local id="$1" url="$2" ext="$3"
    local outfile="$OUTPUT_DIR/${id}.${ext}"
    local status="OK"
    local errmsg=""

    if ! dotnet run --project "$CLI_PROJECT" --configuration Release --no-build -- \
            --url "$url" --output "$outfile" --timeout "$TIMEOUT" 2>/dev/null; then
        status="ERROR"
        errmsg="CLI exited with non-zero status"
        # Create an empty file so downstream comparison has something to diff
        [[ -f "$outfile" ]] || touch "$outfile"
    fi

    echo "$status"
}

# ── Read manifest and iterate ────────────────────────────────────────────
total=0
ok=0
fail=0

declare -a SUMMARY_LINES=()

while IFS= read -r line; do
    line="$(echo "$line" | sed 's/#.*//' | xargs)"
    [[ -z "$line" ]] && continue

    IFS='|' read -r category id url <<< "$line"
    label="[$category] $id"
    total=$((total + 1))

    printf "  %-50s " "$label"

    # Capture HTML output
    html_status=$(run_one "$id" "$url" "html")

    # Capture text output
    txt_status=$(run_one "$id" "$url" "txt")

    if [[ "$html_status" == "OK" && "$txt_status" == "OK" ]]; then
        echo "OK"
        ok=$((ok + 1))
        SUMMARY_LINES+=("{\"category\":\"$category\",\"id\":\"$id\",\"url\":\"$url\",\"status\":\"OK\",\"error\":\"\"}")
    else
        echo "ERROR"
        fail=$((fail + 1))
        SUMMARY_LINES+=("{\"category\":\"$category\",\"id\":\"$id\",\"url\":\"$url\",\"status\":\"ERROR\",\"error\":\"CLI capture failed\"}")
    fi

done < "$MANIFEST"

# ── Write machine-readable summary ──────────────────────────────────────
SUMMARY_FILE="$OUTPUT_DIR/_summary.json"
{
    echo "["
    for i in "${!SUMMARY_LINES[@]}"; do
        if [[ $i -lt $((${#SUMMARY_LINES[@]} - 1)) ]]; then
            echo "  ${SUMMARY_LINES[$i]},"
        else
            echo "  ${SUMMARY_LINES[$i]}"
        fi
    done
    echo "]"
} > "$SUMMARY_FILE"

echo ""
echo "Done — $ok OK, $fail errors out of $total tests.  Summary → $SUMMARY_FILE"
