#!/usr/bin/env bash
# The `coverage` check: the tests with coverage collected, and the HTML coverage report in coverage-report/, which
# the check archives (`coverage-report/**`) and the library's docs site is built with (`deploy.inputs:
# [coverage]`, the docs Dockerfile). It ports the coverage steps of the library's build-docs-and-coverage-report.yml.
# yggdrasil.yml runs it in the pinned .NET SDK image from the repository's root, with the CI engine's Docker socket
# (`docker: true`).
#
# Every test category runs, the MySQL integration tests among them: ci/services.sh gives them the workflow's MySQL
# server (started with the Docker engine unless ARTURRIOS_DATA_MYSQL_TEST_CONNECTION names one) and the DynamoDB
# tests a Java runtime. As in the workflow, a test that skips fails the check, since it would under-report coverage
# (ArturRios.Data.MySql in particular).
#
# REPORTGENERATOR_LICENSE, when set and not empty, is ReportGenerator's PRO license (yggdrasil.yml binds the Jenkins
# credential reportgenerator-license to it). Without it -- a build of a renovate/* branch binds no credential, nor
# does a run on your machine -- the free report is made instead, and the output says so. The license is passed to
# ReportGenerator as an argument. At its default verbosity ReportGenerator prints its arguments, and the first 6
# characters of the license with them, which Jenkins' masking (it masks only the whole string) would not hide:
# `-verbosity:Warning` keeps that block out of the log.
set -euo pipefail

SOLUTION='src/ArturRios.Data.sln'
# renovate: datasource=nuget depName=dotnet-reportgenerator-globaltool
REPORTGENERATOR_VERSION=5.5.11

case $SOLUTION in
  *'<'*) echo "ci/coverage.sh: set SOLUTION at the top of the script to the library's solution (it still has the placeholder $SOLUTION)" >&2; exit 2 ;;
esac

# The repository's root, wherever this is run from.
cd "$(dirname "${BASH_SOURCE[0]}")/.."

# shellcheck source=ci/services.sh
source ci/services.sh
# The tool directory below, and what ci/services.sh started or fetched.
tools=$(mktemp -d)
trap 'rm -rf "$tools"; services_cleanup' EXIT

export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

ensure_java
start_mysql

# Raw results go to coverage/ and the report to coverage-report/: neither is committed.
rm -rf coverage coverage-report
dotnet test "$SOLUTION" --configuration Release --collect:"XPlat Code Coverage" \
  --logger "trx;LogFileName=coverage-tests.trx" --results-directory coverage
fail_if_skipped coverage/coverage-tests.trx "Tests"
stop_mysql

# The tool is installed into a directory of its own, not the user's global tools: the check runs as whoever the
# CI engine runs it as, with no promise about a writable home.
dotnet tool install dotnet-reportgenerator-globaltool --version "$REPORTGENERATOR_VERSION" --tool-path "$tools"

arguments=("-reports:coverage/**/coverage.cobertura.xml" "-targetdir:coverage-report" "-reporttypes:Html" "-verbosity:Warning")
if [ -n "${REPORTGENERATOR_LICENSE:-}" ]; then
  arguments+=("-license:$REPORTGENERATOR_LICENSE")
else
  echo "ci/coverage.sh: REPORTGENERATOR_LICENSE is not set (a renovate/* build, or no credential): making the free report"
fi
"$tools/reportgenerator" "${arguments[@]}"
