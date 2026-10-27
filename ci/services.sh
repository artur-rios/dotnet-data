# shellcheck shell=bash
# Sourced by ci/test.sh and ci/coverage.sh (not run on its own): what the GitHub runner gave the tests and the pinned
# .NET SDK image does not have.
#
#   ensure_java          a Java runtime, for the DynamoDB functional tests (they download DynamoDB Local and start it
#                        with `java`, from JAVA_HOME or the PATH). The runner had Temurin 17 installed. A java already
#                        there is used; otherwise the pinned Temurin JRE below is fetched, checked against its sha256
#                        and JAVA_HOME pointed at it.
#   start_mysql          the MySQL server of the integration tests: the workflows' `mysql:8.0` service container,
#                        pinned. Started with the Docker engine the check is given (`docker: true` in yggdrasil.yml),
#                        and ARTURRIOS_DATA_MYSQL_TEST_CONNECTION exported for the tests. When that variable is set
#                        already (a server of your own), nothing is started. stop_mysql removes the server; the
#                        callers run it on EXIT. A check killed outright (SIGKILL: a timeout, an agent restart) runs no
#                        trap, so each server is labelled with the check container it serves, and start_mysql first
#                        removes the servers whose check container is gone (see _services_sweep).
#   fail_if_skipped <trx> <what>   fails when a test of the .trx did not run, or the .trx is missing: the MySQL
#                        tests skip themselves when the server is not configured, and `dotnet test` exits 0 when every
#                        test skips.
#
# Renovate follows the versions (the comment above each, and the image through renovate.json's MySQL rule), not the
# checksums: when it moves the JRE or the Docker client, copy the new file's sha256 here by hand (Adoptium's release
# page lists the JRE's; download.docker.com publishes none for the static Docker client, so that one is taken from the
# download, over HTTPS, as yggdrasil's ci/tools.sh does).

# Regex versioning: Adoptium's versions are 17.0.21+9, and the build after the + counts (semver would ignore it).
# renovate: datasource=java-version depName=java-jre versioning=regex:^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)\+(?<build>\d+)$
JAVA_VERSION=17.0.21+9
JAVA_SHA256=9f41bf88c7900c7097444a1bdeb84146ae692bc08842490ab098063c74bf9a8a
# renovate: datasource=docker depName=docker versioning=docker
DOCKER_VERSION=29.9.2
DOCKER_SHA256=9ac225999fd5e863f7694c728ef23d480616b1e5639328fa309befd4243d69eb
# The workflows' service was mysql:8.0; renovate.json keeps it on 8.0 and moves the patch and the digest.
MYSQL_IMAGE=mysql:8.0.46@sha256:7dcddc01f13bab2f15cde676d44d01f61fc9f99fe7785e86196dfc07d358ae2b
# The workflows' root password: a throwaway server that only this run can reach, not a secret.
MYSQL_ROOT_PASSWORD=arturrios-data-ci

# The label of every server start_mysql runs, and the one that names the check container it serves.
_services_label=yggdrasil.ci=dotnet-data-mysql
_services_parent_label=yggdrasil.ci-parent
# A server with no check container to follow (started outside a container) is an orphan after this long.
_services_orphan_seconds=$((4 * 60 * 60))

_services_mysql_name=""

# Where fetched tools go: a directory of its own, removed by services_cleanup.
_services_dir() {
  if [ -z "${_services_tools:-}" ]; then
    _services_tools=$(mktemp -d)
  fi
}

# _services_fetch <url> <sha256> <file>: downloads the url to the file and checks it, or fails naming it.
_services_fetch() {
  curl --fail --silent --show-error --location --retry 3 -o "$3" "$1"
  if ! echo "$2  $3" | sha256sum --check --strict --status; then
    echo "ci/services.sh: $1 does not match its pinned checksum" >&2
    rm -f "$3"
    return 1
  fi
}

_services_x86_64() {
  [ "$(uname -m)" = x86_64 ] || { echo "ci/services.sh: no pinned $1 for $(uname -m)" >&2; return 1; }
}

ensure_java() {
  if [ -n "${JAVA_HOME:-}" ] && [ -x "$JAVA_HOME/bin/java" ]; then return 0; fi
  command -v java >/dev/null 2>&1 && return 0
  _services_x86_64 "Java runtime" || return 1
  _services_dir
  echo "ci/services.sh: no java on the PATH; fetching the Temurin $JAVA_VERSION JRE (DynamoDB Local runs on it)"
  local tag=${JAVA_VERSION/+/%2B} file=${JAVA_VERSION/+/_}
  _services_fetch "https://github.com/adoptium/temurin17-binaries/releases/download/jdk-$tag/OpenJDK17U-jre_x64_linux_hotspot_$file.tar.gz" \
    "$JAVA_SHA256" "$_services_tools/jre.tar.gz"
  mkdir -p "$_services_tools/jre"
  tar -xzf "$_services_tools/jre.tar.gz" -C "$_services_tools/jre" --strip-components=1
  rm -f "$_services_tools/jre.tar.gz"
  export JAVA_HOME="$_services_tools/jre"
  export PATH="$JAVA_HOME/bin:$PATH"
}

_services_docker() {
  command -v docker >/dev/null 2>&1 && return 0
  _services_x86_64 "Docker client" || return 1
  _services_dir
  echo "ci/services.sh: no docker on the PATH; fetching the Docker $DOCKER_VERSION client"
  _services_fetch "https://download.docker.com/linux/static/stable/x86_64/docker-$DOCKER_VERSION.tgz" "$DOCKER_SHA256" \
    "$_services_tools/docker.tgz"
  # The client only: the engine is the one whose socket the check was given (docker: true).
  mkdir -p "$_services_tools/bin"
  tar -xzf "$_services_tools/docker.tgz" -C "$_services_tools/bin" --strip-components=1 docker/docker
  rm -f "$_services_tools/docker.tgz"
  export PATH="$_services_tools/bin:$PATH"
}

# Removes the servers that a check killed outright left behind: those whose check container (the parent label) is no
# longer running, and those without a parent started more than 4 hours ago. A running check's server is left alone.
_services_sweep() {
  local id parent created created_seconds
  docker ps --all --quiet --filter "label=$_services_label" | while IFS= read -r id; do
    parent=$(docker inspect --format "{{index .Config.Labels \"$_services_parent_label\"}}" "$id" 2>/dev/null) || continue
    if [ -n "$parent" ]; then
      if [ "$(docker inspect --format '{{.State.Running}}' "$parent" 2>/dev/null)" = true ]; then continue; fi
      echo "ci/services.sh: removing MySQL container $id: its check container $parent is gone"
    else
      created=$(docker inspect --format '{{.Created}}' "$id" 2>/dev/null) || continue
      created_seconds=$(date -d "$created" +%s 2>/dev/null) || continue
      [ $(($(date +%s) - created_seconds)) -gt "$_services_orphan_seconds" ] || continue
      echo "ci/services.sh: removing MySQL container $id: started outside a check container more than 4 hours ago"
    fi
    docker rm --force "$id" >/dev/null 2>&1 || true
  done
}

start_mysql() {
  if [ -n "${ARTURRIOS_DATA_MYSQL_TEST_CONNECTION:-}" ]; then
    echo "ci/services.sh: ARTURRIOS_DATA_MYSQL_TEST_CONNECTION is set: the integration tests use that server"
    return 0
  fi
  _services_docker
  if ! docker version >/dev/null 2>&1; then
    echo "ci/services.sh: the integration tests need MySQL, and there is no Docker engine to start it with (docker: true in yggdrasil.yml), nor ARTURRIOS_DATA_MYSQL_TEST_CONNECTION" >&2
    return 1
  fi
  _services_sweep
  local network host port
  _services_mysql_name="dotnet-data-mysql-$(hostname)-$$"
  if [ -f /.dockerenv ]; then
    # In a container (the check's): the server joins its network, so it is at 127.0.0.1:3306, as the service
    # container was on the runner. A port published on the engine's host would not be reachable from here. The
    # container's hostname is its id, which the parent label records for _services_sweep.
    network=(--network "container:$(hostname)" --label "$_services_parent_label=$(hostname)")
  else
    network=(--publish 127.0.0.1::3306)
  fi
  echo "ci/services.sh: starting $MYSQL_IMAGE"
  docker run --detach --rm --name "$_services_mysql_name" --label "$_services_label" "${network[@]}" \
    --env "MYSQL_ROOT_PASSWORD=$MYSQL_ROOT_PASSWORD" "$MYSQL_IMAGE" >/dev/null
  if [ -f /.dockerenv ]; then
    host=127.0.0.1 port=3306
  else
    host=127.0.0.1 port=$(docker port "$_services_mysql_name" 3306/tcp | head -n 1 | sed 's/.*://')
  fi
  # The service's health check, as the workflows had it: mysqladmin ping over TCP, which only the final server
  # answers (the image's first, initializing, server listens on no port). Up to 3 minutes.
  local tries=0
  until docker exec "$_services_mysql_name" mysqladmin ping --host=127.0.0.1 --user=root \
      "--password=$MYSQL_ROOT_PASSWORD" --silent >/dev/null 2>&1; do
    tries=$((tries + 1))
    if [ "$tries" -ge 90 ] || ! docker inspect "$_services_mysql_name" >/dev/null 2>&1; then
      echo "ci/services.sh: MySQL did not become ready" >&2
      docker logs "$_services_mysql_name" 2>&1 | tail -n 30 >&2 || true
      return 1
    fi
    sleep 2
  done
  echo "ci/services.sh: MySQL is ready at $host:$port"
  export ARTURRIOS_DATA_MYSQL_TEST_CONNECTION="Server=$host;Port=$port;User ID=root;Password=$MYSQL_ROOT_PASSWORD;"
}

stop_mysql() {
  if [ -n "$_services_mysql_name" ]; then
    docker rm --force "$_services_mysql_name" >/dev/null 2>&1 || true
    _services_mysql_name=""
  fi
}

# Everything this file started or fetched: the callers' EXIT trap.
services_cleanup() {
  stop_mysql
  if [ -n "${_services_tools:-}" ]; then
    rm -rf "$_services_tools"
  fi
}

fail_if_skipped() {
  [ -f "$1" ] || { echo "ci/services.sh: $2 left no results file: $1" >&2; return 1; }
  if grep -q 'outcome="NotExecuted"' "$1"; then
    echo "ci/services.sh: $2 were skipped: is the MySQL server reachable?" >&2
    return 1
  fi
}
