#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

REGISTRY="${REGISTRY:-ghcr.io/josnelihurt}"
IMAGE_API="$REGISTRY/session-manager-api"
IMAGE_FRONTEND="$REGISTRY/session-manager-frontend"
TAG="${TAG:-latest}"
PUSH=false
LINT=false
LINT_FIX=false

for arg in "$@"; do
    case $arg in
        --push) PUSH=true ;;
        --lint) LINT=true ;;
        --lint-fix) LINT=true; LINT_FIX=true ;;
        --tag=*) TAG="${arg#*=}" ;;
        --registry=*)
            REGISTRY="${arg#*=}"
            IMAGE_API="$REGISTRY/session-manager-api"
            IMAGE_FRONTEND="$REGISTRY/session-manager-frontend"
            ;;
    esac
done

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to run linting
run_lint() {
    echo -e "${YELLOW}=== Running .NET Code Analysis ===${NC}"

    local has_errors=0

    # Find all .csproj files
    local projects=$(find "$PROJECT_ROOT/src" "$PROJECT_ROOT/tests" -name "*.csproj" 2>/dev/null)

    if [ -z "$projects" ]; then
        echo -e "${RED}✗ No .csproj files found in src/ or tests/${NC}"
        exit 1
    fi

    # 1. dotnet format - check code style and formatting
    echo -e "\n${YELLOW}[1/2] Checking code style with dotnet format...${NC}"

    for proj in $projects; do
        echo -e "  Checking: $(basename "$proj")"
        if [ "$LINT_FIX" = true ]; then
            if ! dotnet format "$proj" --verbosity quiet 2>&1; then
                echo -e "${RED}  ✗ Failed to format: $(basename "$proj")${NC}"
                has_errors=1
            fi
        else
            if ! dotnet format "$proj" --verify-no-changes --verbosity quiet 2>/dev/null; then
                echo -e "${RED}  ✗ Style issues in: $(basename "$proj")${NC}"
                dotnet format "$proj" --verify-no-changes --verbosity diagnostic 2>&1 | grep -E "^(  |Code)" | head -20
                has_errors=1
            fi
        fi
    done

    if [ $has_errors -eq 0 ]; then
        echo -e "${GREEN}✓ Code style check passed${NC}"
    fi

    # 2. Roslynator - deep code analysis (if installed)
    if command -v roslynator &> /dev/null; then
        echo -e "\n${YELLOW}[2/2] Running Roslynator analysis...${NC}"

        for proj in $projects; do
            echo -e "  Analyzing: $(basename "$proj")"
            if ! roslynator analyze "$proj" \
                --severity-level warning \
                --ignore-analyzer-results \
                2>&1 | grep -q "0 diagnostics"; then
                echo -e "${YELLOW}  ⚠ Issues found in: $(basename "$proj")${NC}"
            fi
        done
        echo -e "${GREEN}✓ Roslynator analysis complete${NC}"
    else
        echo -e "\n${YELLOW}[2/2] Roslynator not found (optional). Install with:${NC}"
        echo -e "  ${YELLOW}dotnet tool install -g roslynator.dotnet.cli${NC}"
        echo -e "  ${YELLOW}Then add to PATH: export PATH=\"\$PATH:\$HOME/.dotnet/tools\"${NC}"
    fi

    if [ $has_errors -eq 1 ]; then
        echo -e "\n${RED}=== LINTING FAILED ===${NC}"
        echo -e "${YELLOW}Run with --lint-fix to auto-fix formatting issues${NC}"
        exit 1
    fi

    echo -e "\n${GREEN}=== LINTING PASSED ===${NC}"
}

# Run lint if requested
if [ "$LINT" = true ]; then
    run_lint
fi

docker build -f "$PROJECT_ROOT/Dockerfile" -t "$IMAGE_API:$TAG" "$PROJECT_ROOT"
docker build -f "$PROJECT_ROOT/frontend/Dockerfile" -t "$IMAGE_FRONTEND:$TAG" "$PROJECT_ROOT/frontend"

if [ "$PUSH" = true ]; then
    docker push "$IMAGE_API:$TAG"
    docker push "$IMAGE_FRONTEND:$TAG"
fi

echo "API: $IMAGE_API:$TAG"
echo "Frontend: $IMAGE_FRONTEND:$TAG"
