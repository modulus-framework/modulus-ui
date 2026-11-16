#!/usr/bin/env bash
# Packs the UI libraries into the local feed the CLI reads, and clears the NuGet cache entries for this version.
#
# Why both steps: the CLI resolves Synvro.Modulus.Ui.Templates from ~/.modulus/feed (or MODULUS_LOCAL_FEED), and NuGet keeps
# a copy of each package version in ~/.nuget/packages. A same-version package in either place is reused without a check, so
# after a change to the templates or the libraries, the feed and the cache both hold stale code until they are refreshed.
#
#   tools/refresh-local-feed.sh            # the feed in ~/.modulus/feed (or $MODULUS_LOCAL_FEED)
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
feed="${MODULUS_LOCAL_FEED:-$HOME/.modulus/feed}"
version="$(sed -n 's:.*<VersionPrefix>\(.*\)</VersionPrefix>.*:\1:p' "$root/src/Directory.Build.props" | head -n1)"

if [[ -z "$version" ]]; then
  echo "Could not read VersionPrefix from src/Directory.Build.props." >&2
  exit 1
fi

mkdir -p "$feed"
echo "Packing version $version into $feed"
dotnet pack "$root/Modulus.slnx" -c Release -o "$feed" >/dev/null

shopt -s nullglob
for cached in "$HOME"/.nuget/packages/synvro.modulus.*/"$version"; do
  echo "Clearing NuGet cache: $cached"
  rm -rf "$cached"
done

echo "Done. Restores now take $version from $feed."
