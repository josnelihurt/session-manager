#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

cd "$PROJECT_ROOT/src/SessionManager.Api"
dotnet test --no-build --verbosity normal 2>&1 || dotnet build && dotnet test --verbosity normal

cd "$PROJECT_ROOT/frontend"
npm run test:run --
