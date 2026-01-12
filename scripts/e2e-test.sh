#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

TARGET="${1:-local}"

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

if [ ! -d "$HOME/.cache/ms-playwright" ]; then
    npm run install:drivers
fi

npm test -- --reporter=list
