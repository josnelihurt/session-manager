#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

TARGET="${1:-local}"

# Load credentials from .secrets/.env
if [ -f "$PROJECT_ROOT/.secrets/.env" ]; then
    set -a
    source "$PROJECT_ROOT/.secrets/.env"
    set +a
else
    echo "Error: .secrets/.env file not found"
    exit 1
fi

# Verify required environment variables are set
if [ -z "$E2E_LOCAL_USERNAME" ] || [ -z "$E2E_LOCAL_PASSWORD" ]; then
    echo "Error: E2E_LOCAL_USERNAME and E2E_LOCAL_PASSWORD must be set in .secrets/.env"
    exit 1
fi

if [ "$TARGET" = "remote" ]; then
    BASE_URL="https://session-manager.lab.josnelihurt.me"
else
    BASE_URL="http://localhost:5173"
    if ! curl -sf "$BASE_URL" > /dev/null 2>&1; then
        echo "Error: Local services not running at $BASE_URL"
        exit 1
    fi
fi

cd "$PROJECT_ROOT/e2e"
export BASE_URL="$BASE_URL"
export E2E_LOCAL_USERNAME="$E2E_LOCAL_USERNAME"
export E2E_LOCAL_PASSWORD="$E2E_LOCAL_PASSWORD"

if [ ! -d "$HOME/.cache/ms-playwright" ]; then
    npm run install:drivers
fi

npm test -- --reporter=list
