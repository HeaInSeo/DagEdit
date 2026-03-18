#!/usr/bin/env bash
set -Eeuo pipefail

readonly ROOT="/opt/dotnet/src/github.com/HeaInSeo"
readonly LEAD_REPO="${ROOT}/DagEdit"
readonly ADD_DIR_1="${ROOT}/virtualcanvas-avalonia"
readonly ADD_DIR_2="${ROOT}/VirtualCanvas"

main() {
  if [[ ! -d "${LEAD_REPO}" ]]; then
    printf 'Error: lead repo not found: %s\n' "${LEAD_REPO}" >&2
    exit 1
  fi

  if [[ ! -d "${ADD_DIR_1}" ]]; then
    printf 'Error: add-dir not found: %s\n' "${ADD_DIR_1}" >&2
    exit 1
  fi

  if [[ ! -d "${ADD_DIR_2}" ]]; then
    printf 'Error: add-dir not found: %s\n' "${ADD_DIR_2}" >&2
    exit 1
  fi

  cd "${LEAD_REPO}"

  exec claude \
    --add-dir "${ADD_DIR_1}" \
    --add-dir "${ADD_DIR_2}"
}

main "$@"
