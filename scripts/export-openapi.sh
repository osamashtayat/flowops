#!/usr/bin/env bash
# Regenerates contracts/openapi.json from the API (build-time generation via
# Microsoft.Extensions.ApiDescription.Server). Deterministic: re-running yields an identical file.
# Commit the result; CI and FlowOps.Api.IntegrationTests fail when it drifts from the code.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
output="$repo_root/contracts/openapi.json"

dotnet build "$repo_root/src/backend/FlowOps.Api/FlowOps.Api.csproj" \
  --configuration Release \
  --nologo \
  --verbosity quiet \
  -p:OpenApiGenerateDocuments=true

if [[ ! -s "$output" ]]; then
  echo "export-openapi: $output was not produced" >&2
  exit 1
fi

# Normalise the trailing newline so editors and the generator agree byte for byte.
if [[ -n "$(tail -c 1 "$output")" ]]; then
  printf '\n' >> "$output"
fi

echo "Wrote $output"
