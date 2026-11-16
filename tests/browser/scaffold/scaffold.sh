#!/usr/bin/env bash
# Scaffolds one app per UI engine with the Modulus CLI, builds it, runs it and crawls its pages at phone and desktop widths.
# It is the check that the scaffolded (Scriban) pages render, which the template tests cannot do.
#
#   MODULUS_CLI=/path/to/Modulus.Cli tests/browser/scaffold/scaffold.sh
#
# Isolation: the UI libraries are packed into a fresh feed and restored into a fresh NuGet cache (NUGET_PACKAGES), so no stale
# same-version package is reused. Each app gets a global.json that pins the SDK. The apps are created with no sign-in, so the
# protected pages answer 401; this script adds a development-only sign-in to the copies it generates (never to the repo), and
# the identity-backed calls (/account/*, /authorization/*) stay as the known limitation in docs/CLI.md.
#
# Environment: MODULUS_CLI (required), CHROME (optional, a Chromium binary), WORK (optional, a work folder; default: artifacts/),
# ENGINES (optional, default "razor-pages mvc blazor"), PORT (optional, the first port; default 5301).
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../../.." && pwd)"
cli="${MODULUS_CLI:?Set MODULUS_CLI to the Modulus.Cli executable}"
# Default: artifacts/scaffold-check (git-ignored, on the work disk). /tmp can be a small tmpfs, and a run fills it.
work="${WORK:-$repo/artifacts/scaffold-check/$(date +%Y%m%d-%H%M%S)}"
engines="${ENGINES:-razor-pages mvc blazor}"
port="${PORT:-5301}"
mkdir -p "$work"
echo "Work folder: $work"

# 1. A fresh feed and cache, and the SDK pin for the generated apps.
feed="$work/feed"
export NUGET_PACKAGES="$work/nuget"
export MODULUS_LOCAL_FEED="$feed"
dotnet pack "$repo/Modulus.slnx" -c Release -o "$feed" >/dev/null
printf '{ "sdk": { "version": "10.0.400", "rollForward": "latestFeature" } }\n' > "$work/global.json"

# 2. The crawler's own dependencies.
(cd "$here" && npm install --no-audit --no-fund --silent)

stop_port() {
  local pid
  pid="$(ss -ltnp 2>/dev/null | grep ":$1 " | grep -o 'pid=[0-9]*' | head -n1 | cut -d= -f2 || true)"
  [[ -n "$pid" ]] && kill "$pid" 2>/dev/null || true
}

failures=0
for engine in $engines; do
  suffix="$(echo "$engine" | sed -E 's/(^|-)([a-z])/\U\2/g')"
  name="Check$suffix"
  app_root="$work/$engine"
  mkdir -p "$app_root"
  cp "$work/global.json" "$app_root/"
  echo; echo "== $engine: scaffolding $name"

  (cd "$app_root" && "$cli" app "$name" --kind webapp+api --ui-engine "$engine" --database SQLite --auth none \
      --message-broker none --caching inmemory --with-example --json -o "$app_root" >"$work/$engine.create.log" 2>&1)
  app="$app_root/$name"
  (cd "$app" \
    && "$cli" ui add-component --all -o . \
    && "$cli" ui add-admin -o . \
    && "$cli" ui add-chart --type line -o . \
    && "$cli" ui create-dashboard overview -o . \
    && "$cli" ui create-permission-matrix Product --module Catalog -o . \
    && ("$cli" ui create-crud Product --module Catalog -o . || echo "   (create-crud: the example module already has its pages)") ) >"$work/$engine.ui.log" 2>&1 \
    || { echo "scaffolding failed, see $work/$engine.ui.log"; failures=$((failures+1)); continue; }

  # The development-only sign-in, in this generated copy: a signed-in user, so each page renders instead of answering 401.
  python3 - "$app" <<'PY'
import pathlib, re, sys
root = pathlib.Path(sys.argv[1])
found = [p for p in root.rglob("NoSignInAuthentication.cs") if "/obj/" not in str(p) and "/bin/" not in str(p)]
if not found:
    # The Blazor app has no such scheme; its pages answer without one, so there is nothing to patch.
    print("no no-sign-in scheme in this app; no sign-in patch needed")
    sys.exit(0)
f = found[0]
s = f.read_text()
m = re.search(r"protected override Task<AuthenticateResult> HandleAuthenticateAsync\(\) => Task\.FromResult\(AuthenticateResult\.NoResult\(\)\);", s)
assert m, "the no-sign-in handler changed; update scaffold.sh"
s = s[:m.start()] + """protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "Dev"), new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin")],
            SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(identity), SchemeName)));
    }""" + s[m.end():]
f.write_text(s)
PY

  (cd "$app" && dotnet build "$name.slnx" -c Release >"$work/$engine.build.log" 2>&1) \
    || { echo "build failed, see $work/$engine.build.log"; failures=$((failures+1)); continue; }

  web_dir="$(dirname "$(find "$app/src/Web" -maxdepth 2 -name '*.Web.csproj' | head -n1)")"
  stop_port "$port"
  (cd "$web_dir" && ASPNETCORE_ENVIRONMENT=Development nohup dotnet run --no-build -c Release --no-launch-profile \
      --urls "http://localhost:$port" >"$work/$engine.run.log" 2>&1 &)
  for _ in $(seq 1 90); do
    curl -s -o /dev/null "http://localhost:$port/" && break
    sleep 1
  done

  # The seeds: every static page route the app declares. The crawl then follows the links it finds.
  seeds="$work/$engine.seeds"
  if [[ "$engine" == "mvc" ]]; then
    grep -rhoE '\[HttpGet\("[^"]*"\)\]' "$app/src/Web" --include='*.cs' 2>/dev/null | sed -E 's/\[HttpGet\("//; s/"\)\]//' | grep -v '{' | sort -u > "$seeds" || true
  else
    grep -rhoE '^@page "[^"]*"' "$app/src/Web" --include='*.razor' --include='*.cshtml' 2>/dev/null | sed -E 's/^@page "//; s/"$//' | grep -v '{' | sort -u > "$seeds" || true
  fi

  echo "== $engine: crawling http://localhost:$port ($(wc -l < "$seeds") seed routes)"
  if ! (cd "$here" && CHROME="${CHROME:-}" REPORT="$work/$engine.report.json" node crawl.mjs "http://localhost:$port" "$seeds" 400); then
    failures=$((failures+1))
  fi
  stop_port "$port"
  port=$((port+1))
done

echo
if [[ $failures -gt 0 ]]; then
  echo "Scaffold check failed for $failures engine(s). Work folder: $work"
  exit 1
fi
echo "Scaffold check passed for: $engines"
