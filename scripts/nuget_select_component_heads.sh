#!/usr/bin/env bash
set -euo pipefail

git submodule foreach '
  case "$sm_path" in
    Broiler.*)
      git fetch --depth=1 origin HEAD &&
      git checkout --detach FETCH_HEAD &&
      git submodule sync --recursive &&
      git submodule update --init --recursive
      ;;
  esac
'
