#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

if [[ $# -lt 1 ]]; then
    echo "Usage: $0 <milestone> [additional args passed to chromium-reference]" >&2
    exit 1
fi

MILESTONE="$1"
shift

dotnet run \
    --project "$REPO_ROOT/src/Broiler.Engines.Baseline/Broiler.Engines.Baseline.csproj" \
    --configuration Release -- \
    chromium-reference \
    --milestone "$MILESTONE" \
    "$@"
