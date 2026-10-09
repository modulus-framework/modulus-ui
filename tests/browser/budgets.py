"""
Asset budget gate: the scripts and stylesheets every Modulus page loads must stay under their gzipped size budgets
(tests/browser/budgets.json). Reads the files from the repository, so it needs no server or browser.

    python tests/browser/budgets.py
"""
import gzip
import json
import os
import sys

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")


def main():
    config = json.load(open(os.path.join(os.path.dirname(__file__), "budgets.json")))
    level = config["gzip_level"]
    failed = False
    for path, budget in config["budgets"].items():
        size = len(gzip.compress(open(os.path.join(ROOT, path), "rb").read(), compresslevel=level))
        over = size > budget
        failed = failed or over
        print(f"{'OVER ' if over else 'ok   '}{size:7} / {budget:7}  {path}")
    print("budgets: " + ("failed" if failed else "passed"))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
