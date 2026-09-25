#!/usr/bin/env bash
# Rebuilds the Tabler Icons sprite from icons.txt. Needs curl and python3.
set -euo pipefail
VERSION=3.35.0
here="$(cd "$(dirname "$0")" && pwd)"
out="$here/../../src/Modulus.Ui.Assets/wwwroot/icons/tabler.svg"
mkdir -p "$(dirname "$out")"
python3 - "$here/icons.txt" "$out" "$VERSION" <<'PY'
import re, sys, urllib.request
names = [l.strip() for l in open(sys.argv[1]) if l.strip() and not l.startswith('#')]
version = sys.argv[3]
symbols, missing = [], []
for n in sorted(set(names)):
    try:
        svg = urllib.request.urlopen(f"https://cdn.jsdelivr.net/npm/@tabler/icons@{version}/icons/outline/{n}.svg").read().decode()
    except Exception:
        missing.append(n); continue
    body = re.search(r'<svg[^>]*>(.*)</svg>', svg, re.S).group(1)
    body = re.sub(r'<path stroke="none" d="M0 0h24v24H0z" fill="none"\s*/>', '', body)
    body = re.sub(r'\s+', ' ', body).strip()
    symbols.append(f'<symbol id="ti-{n}" viewBox="0 0 24 24">{body}</symbol>')
head = ('<svg xmlns="http://www.w3.org/2000/svg">'
        f'<!-- Tabler Icons {version} (MIT License, Copyright (c) 2020-2024 Paweł Kuna, https://tabler.io/icons) -->')
open(sys.argv[2], 'w').write(head + ''.join(symbols) + '</svg>\n')
print(f"{len(symbols)} icons written to {sys.argv[2]}")
if missing: print("missing:", ", ".join(missing)); sys.exit(1)
PY
