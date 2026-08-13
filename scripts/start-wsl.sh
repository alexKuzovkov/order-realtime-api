#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPOSITORY_ROOT="$(cd -- "${SCRIPT_DIR}/.." && pwd)"
RUN_TESTS="${RUN_TESTS:-true}"

command -v docker >/dev/null 2>&1 || {
  echo "Docker CLI is not installed or is not available in WSL." >&2
  exit 1
}

docker info >/dev/null 2>&1 || {
  echo "Docker daemon is unavailable. Start Docker Desktop and enable WSL integration." >&2
  exit 1
}

docker compose version >/dev/null
cd "${REPOSITORY_ROOT}"

if [[ "${RUN_TESTS}" == "true" ]]; then
  echo "Running tests in the .NET SDK container..."
  docker run --rm \
    --volume "${REPOSITORY_ROOT}:/workspace" \
    --workdir /workspace \
    mcr.microsoft.com/dotnet/sdk:9.0 \
    dotnet test --configuration Release
fi

echo "Building and starting Nginx, two API instances, and Redis..."
docker compose up --build --detach --wait
docker compose ps

echo "Application is ready:"
echo "  UI:      http://localhost:8080"
echo "  Swagger: http://localhost:8080/swagger"
echo "  Health:  http://localhost:8080/health"
echo "Logs: docker compose logs --follow gateway api-1 api-2"
echo "Stop: docker compose down"
