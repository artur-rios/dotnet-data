#!/usr/bin/env bash
# The `test` check: format, build, test categories, tests. It ports the library's run-tests.yml (the Unit tests,
# Functional tests and Integration tests jobs). yggdrasil.yml runs it in the pinned .NET SDK image from the
# repository's root, with the CI engine's Docker socket (`docker: true`); it runs the same on your machine with the
# SDK and Docker installed.
#
# A test with no Category trait (Unit, Functional or Integration) fails the check: CI runs the categories one by
# one, so a test without one would run in none of them.
#
# What the runner had and the image lacks comes from ci/services.sh: a Java runtime for the DynamoDB functional tests
# (DynamoDB Local), and the MySQL server of the integration tests (the workflow's service container), which it starts
# with the Docker engine unless ARTURRIOS_DATA_MYSQL_TEST_CONNECTION names a server already. As in the workflow, an
# integration test that skips fails the check.
set -euo pipefail

SOLUTION='src/ArturRios.Data.sln'

case $SOLUTION in
  *'<'*) echo "ci/test.sh: set SOLUTION at the top of the script to the library's solution (it still has the placeholder $SOLUTION)" >&2; exit 2 ;;
esac

# The repository's root, wherever this is run from.
cd "$(dirname "${BASH_SOURCE[0]}")/.."

# shellcheck source=ci/services.sh
source ci/services.sh
trap services_cleanup EXIT

export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

dotnet restore "$SOLUTION"
dotnet format "$SOLUTION" --verify-no-changes --no-restore
dotnet build "$SOLUTION" --configuration Release --no-restore

# The tests outside every category, listed by `dotnet test --list-tests` indented by four spaces.
uncategorized=$(dotnet test "$SOLUTION" --configuration Release --no-build --list-tests \
  --filter "Category!=Unit&Category!=Functional&Category!=Integration" | sed -n 's/^    //p')
if [ -n "$uncategorized" ]; then
  echo "ci/test.sh: these tests have no Category trait (Unit, Functional or Integration), so no CI run would run them:" >&2
  echo "$uncategorized" >&2
  exit 1
fi

ensure_java

# One run per category, with its results (.trx) under artifacts/test-results/<category>, which the check archives.
for category in Unit Functional Integration; do
  directory=$(printf '%s' "$category" | tr '[:upper:]' '[:lower:]')
  if [ "$category" = Integration ]; then
    start_mysql
  fi
  dotnet test "$SOLUTION" --configuration Release --no-build \
    --filter "Category=$category" \
    --logger "console;verbosity=normal" \
    --logger "trx;LogFileName=$directory-tests.trx" \
    --results-directory "artifacts/test-results/$directory"
done

fail_if_skipped artifacts/test-results/integration/integration-tests.trx "Integration tests"
