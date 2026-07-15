#!/usr/bin/env python3
"""Release helper for the ArturRios.Data multi-package repository.

Each backend ships as its own NuGet package and is published by
`.github/workflows/publish-package.yml`, which triggers on a per-package tag of
the form ``<PackageId>@<version>`` (e.g. ``ArturRios.Data.Export@1.0.0``) and
validates that the tagged commit's csproj ``<Version>`` matches the tag.

This script drives that flow:

  * bump a project's ``<Version>`` (patch / minor / major) and commit it,
  * create the ``<PackageId>@<version>`` tag from the committed version,
  * push the tag (which triggers the publish action).

Run with no arguments for an interactive menu, which loops until you exit: every
menu accepts ``q`` to quit, and sub-menus accept ``b`` to go back. Alongside the
per-package actions it can tag every package at its current csproj version, and
push the outstanding tags - either one at a time, or all at once.

Or use subcommands:

    python scripts/release.py list
    python scripts/release.py bump    <project> {patch|minor|major}
    python scripts/release.py tag     <project>
    python scripts/release.py push    <project>
    python scripts/release.py release <project> {patch|minor|major}   # bump -> tag -> push

``<project>`` may be the full PackageId (``ArturRios.Data.Export``) or its short
suffix (``Export``, ``Export.Excel`` - case-insensitive).

Only the Python standard library is used.
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
SRC_DIR = REPO_ROOT / "src"

# Packages that exist in the tree but cannot be published (excluded from the
# build). Keep in sync with the guard in publish-package.yml.
DEFERRED = {"ArturRios.Data.MySql"}

PACKAGE_ID_RE = re.compile(r"<PackageId>\s*(.*?)\s*</PackageId>")
VERSION_RE = re.compile(r"<Version>\s*(.*?)\s*</Version>")
SEMVER_RE = re.compile(r"^(\d+)\.(\d+)\.(\d+)$")

BUMP_PARTS = ("patch", "minor", "major")


# --------------------------------------------------------------------------- #
# Model
# --------------------------------------------------------------------------- #
@dataclass
class Package:
    package_id: str
    csproj: Path

    @property
    def deferred(self) -> bool:
        return self.package_id in DEFERRED

    def read_version(self) -> str:
        """Read the current <Version> from disk (the committed/working value)."""
        match = VERSION_RE.search(self.csproj.read_text(encoding="utf-8"))
        if not match:
            fail(f"No <Version> element found in {rel(self.csproj)}")
        return match.group(1)

    def tag_for(self, version: str) -> str:
        return f"{self.package_id}@{version}"


# --------------------------------------------------------------------------- #
# Small utilities
# --------------------------------------------------------------------------- #
class ReleaseError(Exception):
    """An operation could not be completed (aborted, or a precondition failed).

    In CLI mode this is printed and exits non-zero; in interactive mode it is
    caught by the menu loop, which reports it and redisplays the menu.
    """


class Back(Exception):
    """The user chose to return to the previous menu."""


class Quit(Exception):
    """The user chose to exit the script."""


def fail(message: str) -> "NoReturn":  # type: ignore[name-defined]
    raise ReleaseError(message)


def rel(path: Path) -> str:
    try:
        return str(path.relative_to(REPO_ROOT))
    except ValueError:
        return str(path)


def git(*args: str, capture: bool = False, check: bool = True) -> str:
    result = subprocess.run(
        ["git", *args],
        cwd=REPO_ROOT,
        text=True,
        capture_output=True,
    )
    if check and result.returncode != 0:
        fail(f"git {' '.join(args)} failed:\n{result.stderr.strip()}")
    return result.stdout.strip() if capture else ""


def confirm(prompt: str, assume_yes: bool) -> bool:
    if assume_yes:
        return True
    try:
        answer = input(f"{prompt} [y/N] ").strip().lower()
    except EOFError:
        return False
    return answer in ("y", "yes")


# --------------------------------------------------------------------------- #
# Discovery / selection
# --------------------------------------------------------------------------- #
def discover() -> list[Package]:
    packages: list[Package] = []
    for csproj in sorted(SRC_DIR.glob("*/*.csproj")):
        text = csproj.read_text(encoding="utf-8")
        id_match = PACKAGE_ID_RE.search(text)
        if not id_match:
            continue  # not a packable project (e.g. no PackageId)
        packages.append(Package(package_id=id_match.group(1), csproj=csproj))
    if not packages:
        fail(f"No packable projects found under {rel(SRC_DIR)}")
    return packages


def find_package(name: str, packages: list[Package] | None = None) -> Package:
    packages = packages or discover()
    name_lower = name.lower()
    candidates = [
        p
        for p in packages
        if p.package_id.lower() == name_lower
        or p.package_id.lower() == f"arturrios.data.{name_lower}"
        or p.package_id.lower().endswith(f".{name_lower}")
    ]
    if not candidates:
        available = ", ".join(p.package_id for p in packages)
        fail(f"No project matches '{name}'. Available: {available}")
    if len(candidates) > 1:
        matches = ", ".join(p.package_id for p in candidates)
        fail(f"'{name}' is ambiguous - matches: {matches}. Use the full PackageId.")
    return candidates[0]


# --------------------------------------------------------------------------- #
# Version handling
# --------------------------------------------------------------------------- #
def bump_version(version: str, part: str) -> str:
    match = SEMVER_RE.match(version)
    if not match:
        fail(
            f"Version '{version}' is not a simple MAJOR.MINOR.PATCH value; "
            "edit the csproj manually for pre-release/build-metadata versions."
        )
    major, minor, patch = (int(x) for x in match.groups())
    if part == "major":
        major, minor, patch = major + 1, 0, 0
    elif part == "minor":
        minor, patch = minor + 1, 0
    elif part == "patch":
        patch += 1
    else:
        fail(f"Unknown bump part '{part}' (expected one of {', '.join(BUMP_PARTS)})")
    return f"{major}.{minor}.{patch}"


def write_version(csproj: Path, new_version: str) -> None:
    text = csproj.read_text(encoding="utf-8")
    new_text, count = VERSION_RE.subn(f"<Version>{new_version}</Version>", text, count=1)
    if count != 1:
        fail(f"Could not update <Version> in {rel(csproj)}")
    csproj.write_text(new_text, encoding="utf-8")


# --------------------------------------------------------------------------- #
# Git state helpers
# --------------------------------------------------------------------------- #
def tag_exists(tag: str) -> bool:
    return bool(git("tag", "--list", tag, capture=True))


def path_is_dirty(path: Path) -> bool:
    status = git("status", "--porcelain", "--", str(path), capture=True)
    return bool(status)


def current_branch() -> str:
    return git("rev-parse", "--abbrev-ref", "HEAD", capture=True)


def branch_has_unpushed_commits() -> bool:
    # True when HEAD is ahead of its upstream, or has no upstream at all.
    upstream = git(
        "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}",
        capture=True, check=False,
    )
    if not upstream:
        return True
    ahead = git("rev-list", "--count", f"{upstream}..HEAD", capture=True)
    return ahead not in ("", "0")


# --------------------------------------------------------------------------- #
# Operations
# --------------------------------------------------------------------------- #
def do_bump(pkg: Package, part: str, *, commit: bool, assume_yes: bool) -> str:
    if pkg.deferred:
        print(f"warning: {pkg.package_id} is deferred and cannot be published.")
    old = pkg.read_version()
    new = bump_version(old, part)
    print(f"{pkg.package_id}: {old} -> {new}  ({part})")
    if not confirm(f"Write <Version>{new}</Version> to {rel(pkg.csproj)}?", assume_yes):
        fail("aborted")
    write_version(pkg.csproj, new)
    print(f"updated {rel(pkg.csproj)}")

    if commit:
        message = f"chore(release): bump {pkg.package_id} to {new}"
        git("add", "--", str(pkg.csproj))
        git("commit", "-m", message, "--", str(pkg.csproj))
        print(f"committed: {message}")
    else:
        print("note: not committed (--no-commit). Commit before tagging so the "
              "tag points at the bumped version.")
    return new


def do_tag(pkg: Package, *, assume_yes: bool) -> str:
    if pkg.deferred:
        fail(f"{pkg.package_id} is deferred and cannot be published - refusing to tag.")
    if path_is_dirty(pkg.csproj):
        fail(
            f"{rel(pkg.csproj)} has uncommitted changes. Commit the version bump "
            "first (the tag must point at a commit that contains the bumped "
            "<Version>) - e.g. `release.py bump` commits it for you."
        )
    version = pkg.read_version()
    tag = pkg.tag_for(version)
    if tag_exists(tag):
        fail(f"tag '{tag}' already exists. Bump the version before tagging again.")
    if not confirm(f"Create tag '{tag}' at HEAD ({git('rev-parse', '--short', 'HEAD', capture=True)})?", assume_yes):
        fail("aborted")
    git("tag", tag)
    print(f"created tag {tag}")
    return tag


def do_push(pkg: Package, *, assume_yes: bool) -> None:
    if pkg.deferred:
        fail(f"{pkg.package_id} is deferred and cannot be published - refusing to push.")
    version = pkg.read_version()
    tag = pkg.tag_for(version)
    if not tag_exists(tag):
        fail(f"tag '{tag}' does not exist locally. Run `release.py tag {pkg.package_id}` first.")

    branch = current_branch()
    if branch_has_unpushed_commits():
        print(
            f"note: branch '{branch}' has commits not on its remote (including the "
            "version bump). The tag alone will publish, but origin/"
            f"{branch} won't reflect the bump until the branch is pushed."
        )
        if confirm(f"Push branch '{branch}' to origin first?", assume_yes):
            git("push", "origin", "HEAD")
            print(f"pushed branch {branch}")

    if not confirm(f"Push tag '{tag}' to origin (this triggers the publish action)?", assume_yes):
        fail("aborted")
    git("push", "origin", tag)
    print(f"pushed tag {tag} - the Publish Package workflow should now run.")


def do_release(pkg: Package, part: str, *, assume_yes: bool) -> None:
    if pkg.deferred:
        fail(f"{pkg.package_id} is deferred and cannot be published.")
    do_bump(pkg, part, commit=True, assume_yes=assume_yes)
    do_tag(pkg, assume_yes=assume_yes)
    do_push(pkg, assume_yes=assume_yes)


def do_tag_all(packages: list[Package], *, assume_yes: bool) -> None:
    """Create the <PackageId>@<version> tag for every publishable package.

    Packages whose tag already exists - or whose csproj has uncommitted changes
    (the tag would point at a commit without the version) - are logged and
    skipped rather than treated as errors.
    """
    head = git("rev-parse", "--short", "HEAD", capture=True)
    print(f"\nTagging packages at HEAD ({head}):")

    pending: list[tuple[Package, str]] = []
    for pkg in packages:
        if pkg.deferred:
            print(f"  skip    {pkg.package_id} (deferred - not publishable)")
            continue
        tag = pkg.tag_for(pkg.read_version())
        if tag_exists(tag):
            print(f"  skip    {tag} (tag already exists)")
        elif path_is_dirty(pkg.csproj):
            print(f"  skip    {tag} ({rel(pkg.csproj)} has uncommitted changes)")
        else:
            print(f"  create  {tag}")
            pending.append((pkg, tag))

    if not pending:
        print("\nNothing to tag.")
        return
    if not confirm(f"\nCreate {len(pending)} tag(s) at {head}?", assume_yes):
        fail("aborted")
    for _pkg, tag in pending:
        git("tag", tag)
        print(f"created tag {tag}")


def remote_tags() -> set[str]:
    out = git("ls-remote", "--tags", "origin", capture=True, check=False)
    tags: set[str] = set()
    for line in out.splitlines():
        _sha, _sep, ref = line.partition("\trefs/tags/")
        if ref:
            # Annotated tags also list a peeled `^{}` ref - same tag name.
            tags.add(ref.removesuffix("^{}"))
    return tags


def local_package_tags(packages: list[Package]) -> list[str]:
    tags: list[str] = []
    for pkg in packages:
        listed = git("tag", "--list", f"{pkg.package_id}@*", capture=True)
        tags.extend(t for t in listed.splitlines() if t)
    return sorted(tags)


def do_push_all(packages: list[Package], *, assume_yes: bool,
                one_at_a_time: bool = True) -> None:
    """Push every package tag that origin does not have yet.

    With `one_at_a_time` (the default) each tag is confirmed separately, so the
    user can watch one publish run before releasing the next. Otherwise all
    pending tags go in a single `git push` after one confirmation, which starts
    every publish run at once.
    """
    print("\nComparing local package tags against origin...")
    known = local_package_tags(packages)
    if not known:
        print("No package tags exist locally.")
        return

    already = remote_tags()
    pending = [t for t in known if t not in already]
    for tag in known:
        if tag in already:
            print(f"  skip    {tag} (already on origin)")
    if not pending:
        print("\nNothing to push - origin has every local package tag.")
        return

    print(f"\n{len(pending)} tag(s) to push:")
    for tag in pending:
        print(f"  push    {tag}")

    branch = current_branch()
    if branch_has_unpushed_commits():
        print(
            f"\nnote: branch '{branch}' has commits not on its remote (including "
            f"version bumps). Tags alone will publish, but origin/{branch} won't "
            "reflect the bumps until the branch is pushed."
        )
        if confirm(f"Push branch '{branch}' to origin first?", assume_yes):
            git("push", "origin", "HEAD")
            print(f"pushed branch {branch}")

    if not one_at_a_time:
        prompt = (f"\nPush all {len(pending)} tag(s) to origin now (this triggers "
                  f"{len(pending)} publish run(s) at once)?")
        if not confirm(prompt, assume_yes):
            fail("aborted")
        # One `git push` per tag, deliberately: GitHub drops the push event for
        # tags when more than three arrive in a single push ("Events will not be
        # created for tags when more than three tags are pushed at once"), so a
        # batched `git push origin tag1 tag2 ...` uploads the tags but silently
        # publishes nothing. Only the confirmation is batched, not the pushes.
        for tag in pending:
            git("push", "origin", tag)
            print(f"pushed tag {tag}")
        print(f"\nPushed {len(pending)} tag(s) - the Publish Package workflow "
              "should now run for each.")
        return

    pushed = 0
    for i, tag in enumerate(pending, start=1):
        prompt = f"\n({i}/{len(pending)}) Push tag '{tag}' to origin (triggers publish)?"
        if not confirm(prompt, assume_yes):
            print(f"skipped {tag}")
            continue
        git("push", "origin", tag)
        pushed += 1
        print(f"pushed tag {tag} - the Publish Package workflow should now run.")
    print(f"\nPushed {pushed} of {len(pending)} tag(s).")


# --------------------------------------------------------------------------- #
# `list`
# --------------------------------------------------------------------------- #
def print_packages(packages: list[Package]) -> None:
    width = max(len(p.package_id) for p in packages)
    for i, pkg in enumerate(packages, start=1):
        suffix = "  [deferred - not publishable]" if pkg.deferred else ""
        print(f"  {i:>2}) {pkg.package_id:<{width}}  {pkg.read_version()}{suffix}")


# --------------------------------------------------------------------------- #
# Interactive mode
# --------------------------------------------------------------------------- #
def prompt_choice(prompt: str, count: int, *, back: bool = False) -> int:
    """Read a 1-based menu choice; returns a 0-based index.

    Always accepts 'q' (exit the script) and, when `back` is set, 'b' (return to
    the previous menu) - both raise so callers unwind to the right menu loop.
    """
    hints = ["b: back"] if back else []
    hints.append("q: exit")
    while True:
        try:
            raw = input(f"{prompt} [{', '.join(hints)}] ").strip().lower()
        except EOFError:
            raise Quit from None
        if raw == "q":
            raise Quit
        if raw == "b" and back:
            raise Back
        if raw.isdigit() and 1 <= int(raw) <= count:
            return int(raw) - 1
        options = f"a number between 1 and {count}"
        print(f"Please enter {options}, {'b, ' if back else ''}or q.")


def select_package(packages: list[Package]) -> Package:
    """Package picker used by the per-package actions. 'b' returns to the menu."""
    print("\nPackages:")
    print_packages(packages)
    pkg = packages[prompt_choice("\nSelect a package number:", len(packages), back=True)]
    print(f"\nSelected: {pkg.package_id}  (current {pkg.read_version()})")
    if pkg.deferred:
        fail(f"{pkg.package_id} is deferred and cannot be published.")
    return pkg


def select_bump_part(pkg: Package) -> str:
    print("\nBump:")
    for i, part in enumerate(BUMP_PARTS, start=1):
        example = bump_version(pkg.read_version(), part)
        print(f"  {i}) {part:<6} -> {example}")
    return BUMP_PARTS[prompt_choice("\nSelect bump type:", len(BUMP_PARTS), back=True)]


ACTIONS = (
    "List packages and current versions",
    "Bump version + commit",
    "Create tag from current version",
    "Push tag (trigger publish)",
    "Full release (bump -> tag -> push)",
    "Tag all packages at their current versions",
    "Push all package tags (one at a time)",
    "Push all package tags (all at once)",
)


def run_action(index: int, packages: list[Package]) -> None:
    if index == 0:
        print("\nPackages:")
        print_packages(packages)
    elif index == 1:
        pkg = select_package(packages)
        do_bump(pkg, select_bump_part(pkg), commit=True, assume_yes=False)
    elif index == 2:
        do_tag(select_package(packages), assume_yes=False)
    elif index == 3:
        do_push(select_package(packages), assume_yes=False)
    elif index == 4:
        pkg = select_package(packages)
        do_release(pkg, select_bump_part(pkg), assume_yes=False)
    elif index == 5:
        do_tag_all(packages, assume_yes=False)
    elif index == 6:
        do_push_all(packages, assume_yes=False)
    elif index == 7:
        do_push_all(packages, assume_yes=False, one_at_a_time=False)


def interactive() -> None:
    packages = discover()
    while True:
        print("\nAction:")
        for i, action in enumerate(ACTIONS, start=1):
            print(f"  {i}) {action}")
        try:
            run_action(prompt_choice("\nSelect an action:", len(ACTIONS)), packages)
        except Back:
            continue  # a sub-menu backed out; redisplay the action menu
        except Quit:
            print("bye")
            return
        except ReleaseError as exc:
            # Keep the session alive: report and fall through to the menu again.
            print(f"error: {exc}", file=sys.stderr)


# --------------------------------------------------------------------------- #
# CLI
# --------------------------------------------------------------------------- #
def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="release.py",
        description="Bump, tag, and publish an ArturRios.Data package. "
                    "Run without a subcommand for an interactive menu.",
    )
    sub = parser.add_subparsers(dest="command")

    # Shared option, added to each action subcommand so it can be passed after
    # the subcommand (e.g. `release.py bump Export patch -y`).
    common = argparse.ArgumentParser(add_help=False)
    common.add_argument(
        "-y", "--yes", action="store_true",
        help="skip confirmation prompts (for non-interactive use)",
    )

    sub.add_parser("list", help="list packages and their current versions")

    p_bump = sub.add_parser("bump", parents=[common],
                            help="bump a project's <Version> and commit it")
    p_bump.add_argument("project")
    p_bump.add_argument("part", choices=BUMP_PARTS)
    p_bump.add_argument("--no-commit", action="store_true",
                        help="edit the csproj but do not commit the change")

    p_tag = sub.add_parser("tag", parents=[common],
                           help="create the <PackageId>@<version> tag")
    p_tag.add_argument("project")

    p_push = sub.add_parser("push", parents=[common],
                            help="push the package's tag (triggers publish)")
    p_push.add_argument("project")

    p_release = sub.add_parser("release", parents=[common],
                               help="bump -> tag -> push in one step")
    p_release.add_argument("project")
    p_release.add_argument("part", choices=BUMP_PARTS)

    return parser


def main(argv: list[str] | None = None) -> None:
    parser = build_parser()
    args = parser.parse_args(argv)

    if args.command is None:
        interactive()
        return

    if args.command == "list":
        print_packages(discover())
        return

    pkg = find_package(args.project)

    if args.command == "bump":
        do_bump(pkg, args.part, commit=not args.no_commit, assume_yes=args.yes)
    elif args.command == "tag":
        do_tag(pkg, assume_yes=args.yes)
    elif args.command == "push":
        do_push(pkg, assume_yes=args.yes)
    elif args.command == "release":
        do_release(pkg, args.part, assume_yes=args.yes)


if __name__ == "__main__":
    try:
        main()
    except ReleaseError as exc:
        # Interactive mode handles these itself; this is the CLI path.
        print(f"error: {exc}", file=sys.stderr)
        sys.exit(1)
    except KeyboardInterrupt:
        print("\naborted", file=sys.stderr)
        sys.exit(130)
