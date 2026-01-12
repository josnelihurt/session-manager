#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

REGISTRY="${REGISTRY:-ghcr.io/josnelihurt}"
IMAGE_API="$REGISTRY/session-manager-api"
IMAGE_FRONTEND="$REGISTRY/session-manager-frontend"
TAG="${TAG:-latest}"
PUSH=false

for arg in "$@"; do
    case $arg in
        --push) PUSH=true ;;
        --tag=*) TAG="${arg#*=}" ;;
        --registry=*)
            REGISTRY="${arg#*=}"
            IMAGE_API="$REGISTRY/session-manager-api"
            IMAGE_FRONTEND="$REGISTRY/session-manager-frontend"
            ;;
    esac
done

docker build -f "$PROJECT_ROOT/Dockerfile" -t "$IMAGE_API:$TAG" "$PROJECT_ROOT"
docker build -f "$PROJECT_ROOT/frontend/Dockerfile" -t "$IMAGE_FRONTEND:$TAG" "$PROJECT_ROOT/frontend"

if [ "$PUSH" = true ]; then
    docker push "$IMAGE_API:$TAG"
    docker push "$IMAGE_FRONTEND:$TAG"
fi

echo "API: $IMAGE_API:$TAG"
echo "Frontend: $IMAGE_FRONTEND:$TAG"
