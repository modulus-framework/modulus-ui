"""
Markup parity gate: the pivot table is drawn from one shared renderer, so its markup must be the same in the MVC, Razor Pages and
Blazor samples. The script takes each app's rendered `.m-pivot` element and fails when they differ.

    MODULUS_MVC=http://localhost:5100 MODULUS_RAZOR=http://localhost:5101 MODULUS_BLAZOR=http://localhost:5102 python tests/browser/parity.py
"""
import hashlib
import os
import sys
from playwright.sync_api import sync_playwright

PAGE = "/tables/pivot"


def main():
    apps = {name: os.environ.get(var, "").rstrip("/") for name, var in
            [("mvc", "MODULUS_MVC"), ("razor", "MODULUS_RAZOR"), ("blazor", "MODULUS_BLAZOR")]}
    apps = {name: url for name, url in apps.items() if url}
    if len(apps) < 2:
        print("Set at least two of MODULUS_MVC, MODULUS_RAZOR, MODULUS_BLAZOR.")
        return 2
    markup = {}
    with sync_playwright() as p:
        options = {"executable_path": os.environ["MODULUS_CHROMIUM"]} if os.environ.get("MODULUS_CHROMIUM") else {}
        browser = p.chromium.launch(args=["--no-sandbox"], **options)
        for name, url in apps.items():
            page = browser.new_page(viewport={"width": 1440, "height": 900})
            page.goto(url + PAGE, wait_until="networkidle")
            page.wait_for_timeout(500)
            markup[name] = page.locator(".m-pivot").first.evaluate("e => e.outerHTML")
            page.close()
        browser.close()
    failed = False
    reference_name = next(iter(markup))
    reference = markup[reference_name]
    for name, html in markup.items():
        digest = hashlib.sha256(html.encode()).hexdigest()[:16]
        same = html == reference
        print(f"{name:7} {len(html):7} chars  sha256 {digest}  {'same as ' + reference_name if name != reference_name and same else ''}{'' if same or name == reference_name else 'DIFFERS from ' + reference_name}")
        failed = failed or not same
    print("parity: " + ("failed" if failed else "passed"))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
