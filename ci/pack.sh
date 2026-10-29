#!/usr/bin/env bash
# Packs ONE of the repository's NuGet packages into artifacts/ (`publish.nuget` in yggdrasil.yml: run in the pinned
# .NET SDK image on the release agent, from the checkout of the tag, with no credential at all; the package
# `artifacts/*.nupkg` is pushed afterwards by yggdrasil's scripts/nuget-push.sh). It ports the packing steps of the
# repository's publish-package.yml. Pushing is not done here: the NuGet key never reaches repository code.
#
# The packages are versioned apart, and each release is a tag <PackageId>@<version> (ArturRios.Data.Sqlite@3.1.0,
# made by scripts/release.py; the catalog's `tags: "*@*"`). The pipeline sets RELEASE_TAG to the whole tag and
# RELEASE_VERSION to what follows its last `@`. As the old workflow did, the tag names the package and its version:
#   - the project is the one of the solution whose <PackageId> is the part before the `@` (the workflow looked for
#     src/<PackageId>/<PackageId>.csproj, which is where each of them is), and no such project stops the script;
#   - its <Version> must be the part after the `@` (N.N.N, or N.N.N-pre), or nothing is built;
#   - only that project is restored and packed, so the other packages, at their own versions, are not published.
#
#   RELEASE_TAG=ArturRios.Data.Sqlite@3.1.0 bash ci/pack.sh      # on your machine
set -euo pipefail

SOLUTION='src/ArturRios.Data.sln'

# The repository's root, wherever this is run from.
cd "$(dirname "${BASH_SOURCE[0]}")/.."

export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

die() { echo "ci/pack.sh: $*" >&2; exit 1; }

tag=${RELEASE_TAG:-}
[ -n "$tag" ] || die "RELEASE_TAG is not set: it is the tag being released, <PackageId>@<version> (e.g. ArturRios.Data.Sqlite@3.1.0)"
case $tag in
  *@*) ;;
  *) die "the tag '$tag' is not of the form <PackageId>@<version>" ;;
esac
package=${tag%@*}
version=${tag##*@}
if [ -n "${RELEASE_VERSION:-}" ] && [ "$RELEASE_VERSION" != "$version" ]; then
  die "RELEASE_VERSION '$RELEASE_VERSION' is not the version of the tag '$tag' ($version)"
fi
if ! [[ $package =~ ^[A-Za-z0-9_]+(\.[A-Za-z0-9_]+)*$ ]]; then
  die "'$package' (the tag '$tag' before its @) is not a package id"
fi
if ! [[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]]; then
  die "'$version' (the tag '$tag' after its @) is not a version (N.N.N or N.N.N-pre)"
fi

# The projects of $SOLUTION (backslash paths and CRLF handled), and the package id of each packable one: every project
# that sets no <IsPackable>false</IsPackable> and does not use Microsoft.NET.Test.Sdk (a test project); a project
# without a <PackageId> is its file's name. Prints "<id> <project>" per line.
packable_projects() {
  local solution_dir project text id
  solution_dir=$(dirname "$SOLUTION")
  tr -d '\r' < "$SOLUTION" | grep -oE '"[^"]+\.csproj"' | tr -d '"' | sed "s|[\]|/|g; s|^|$solution_dir/|" \
    | while IFS= read -r project; do
      [ -f "$project" ] || continue
      text=$(tr -d '\r' < "$project")
      if printf '%s\n' "$text" | grep -qE '<IsPackable>[[:space:]]*false|Microsoft\.NET\.Test\.Sdk'; then
        continue
      fi
      id=$(printf '%s\n' "$text" | sed -n 's|.*<PackageId>[[:space:]]*\([^<[:space:]]*\)[[:space:]]*</PackageId>.*|\1|p' | head -n 1)
      printf '%s %s\n' "${id:-$(basename "$project" .csproj)}" "$project"
    done
}

projects=$(packable_projects)
csproj=$(printf '%s\n' "$projects" | awk -v id="$package" '$1 == id { print $2 }')
if [ -z "$csproj" ]; then
  die "no project of $SOLUTION has the PackageId '$package' (the tag '$tag'). The packages are: $(printf '%s\n' "$projects" | awk '{ print $1 }' | sort | tr '\n' ' ')"
fi
if [ "$(printf '%s\n' "$csproj" | wc -l)" -ne 1 ]; then
  die "more than one project of $SOLUTION has the PackageId '$package': $(printf '%s\n' "$csproj" | tr '\n' ' ')"
fi

# The project's own <Version> (the first one), as the workflow read it; the packages set it in their csproj.
project_version=$(tr -d '\r' < "$csproj" | sed -n 's|.*<Version>[[:space:]]*\([^<[:space:]]*\)[[:space:]]*</Version>.*|\1|p' | head -n 1)
if [ -z "$project_version" ]; then
  die "$csproj has no <Version> element, so it cannot be released as $version"
fi
if [ "$project_version" != "$version" ]; then
  die "$csproj has <Version>$project_version</Version>, but the tag '$tag' releases $version. Bump the project's <Version> (scripts/release.py bump) or tag the version it has"
fi
echo "ci/pack.sh: packing $package $version from $csproj"

# Only what this run packs is in artifacts/ (the test check's results are kept elsewhere under it).
rm -f artifacts/*.nupkg artifacts/*.snupkg
dotnet restore "$csproj"
dotnet pack "$csproj" --configuration Release --no-restore --output artifacts

shopt -s nullglob
packages=(artifacts/*.nupkg)
if [ "${#packages[@]}" -ne 1 ] || [ "${packages[0]}" != "artifacts/$package.$version.nupkg" ]; then
  die "expected artifacts/$package.$version.nupkg alone, but packed: ${packages[*]:-nothing}"
fi
echo "ci/pack.sh: packed ${packages[0]}"
