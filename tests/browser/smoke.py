"""
Browser smoke gate for the three admin templates (and the Liquid sample). Run it against samples that are already listening:

    MODULUS_MVC=http://localhost:5100 MODULUS_RAZOR=http://localhost:5101 MODULUS_BLAZOR=http://localhost:5102 \
    MODULUS_FLUID=http://localhost:5103 python tests/browser/smoke.py

It fails (exit 1) when a page does not answer 200, logs a console error, or the Content-Security-Policy reports a violation.
Blazor sends its policy report-only and its import map is an inline script with no nonce, so its only allowed violation is that
one (see docs/DOCUMENTATION.md#upgrading-ant-design-blazor-htmx-alpinejs-and-the-plugin-libraries). The Liquid sample is checked for status, health and headers only.

Set MODULUS_CHROMIUM to a browser executable to use one other than Playwright's own.
"""
import json
import base64
import os
import sys
import urllib.request
from playwright.sync_api import sync_playwright

PAGES = [
    "/", "/tables/large", "/tables/pivot", "/forms/business", "/forms/dynamic", "/forms/elements", "/merchandising/packing-lists", "/plugins/dropzone",
    "/plugins/code-editor", "/plugins/image-cropper", "/plugins/editor", "/plugins/spreadsheet", "/plugins/pdf",
    "/plugins/map", "/plugins/date-picker", "/tables/tree", "/apps/workflow", "/apps/flow", "/dashboards/crm", "/pages/faq",
]
BLAZOR_IMPORT_MAP = "script-src-elem | inline"

failures = []


def fail(message):
    failures.append(message)
    print("FAIL", message)


def base_urls():
    apps = {"mvc": os.environ.get("MODULUS_MVC"), "razor": os.environ.get("MODULUS_RAZOR"),
            "blazor": os.environ.get("MODULUS_BLAZOR"), "fluid": os.environ.get("MODULUS_FLUID")}
    return {name: url.rstrip("/") for name, url in apps.items() if url}


def health(base, name):
    try:
        with urllib.request.urlopen(base + "/health", timeout=10) as r:
            body = r.read().decode().strip()
            if r.status != 200 or body != "Healthy":
                fail(f"{name}: /health answered {r.status} {body!r}")
    except Exception as e:
        fail(f"{name}: /health did not answer ({e})")


def headers(base, name):
    """Compression on the page, and the hardening headers. The policy may be sent enforced or report-only; a framework header
    (Blazor's own frame-ancestors) can be sent beside it, so every Content-Security-Policy header is read."""
    req = urllib.request.Request(base + "/", headers={"Accept-Encoding": "br, gzip"})
    try:
        with urllib.request.urlopen(req, timeout=20) as r:
            encoding = r.headers.get("Content-Encoding", "")
            if encoding not in ("br", "gzip"):
                fail(f"{name}: the page is not compressed (Content-Encoding {encoding!r})")
            if r.headers.get("X-Content-Type-Options") != "nosniff":
                fail(f"{name}: X-Content-Type-Options is missing")
            policies = r.headers.get_all("Content-Security-Policy") or []
            policies += r.headers.get_all("Content-Security-Policy-Report-Only") or []
            if not any("script-src 'self' 'nonce-" in policy for policy in policies):
                fail(f"{name}: no Content-Security-Policy with a nonce")
    except Exception as e:
        fail(f"{name}: the page did not answer ({e})")


# A 1x1 transparent PNG, used for every external map tile.
BLANK_TILE = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=")


def check_pages(base, name, enforced):
    with sync_playwright() as p:
        options = {"executable_path": os.environ["MODULUS_CHROMIUM"]} if os.environ.get("MODULUS_CHROMIUM") else {}
        browser = p.chromium.launch(args=["--no-sandbox"], **options)
        for path in PAGES:
            page = browser.new_page(viewport={"width": 1280, "height": 900})
            # Map tiles come from an external server. The gate answers them with a blank tile, so the map pages do not depend
            # on the network (CI has no tile server access guarantee); the app's own requests are not affected.
            page.route("https://tile.openstreetmap.org/**", lambda route: route.fulfill(status=200, content_type="image/png", body=BLANK_TILE))
            page.add_init_script("""document.addEventListener('securitypolicyviolation', e => {
                window.__csp = (window.__csp || []).concat([e.violatedDirective + ' | ' + (e.blockedURI || e.sample || '').slice(0, 60)]); });""")
            errors = []
            page.on("console", lambda m: errors.append(m.text) if m.type == "error" and "favicon" not in m.text else None)
            page.on("pageerror", lambda e: errors.append(str(e)))
            response = page.goto(base + path, wait_until="networkidle")
            page.wait_for_timeout(500)
            if response is None or response.status != 200:
                fail(f"{name}{path}: answered {response.status if response else 'nothing'}")
            for message in errors:
                fail(f"{name}{path}: console error: {message[:160]}")
            violations = page.evaluate("() => window.__csp || []")
            for violation in violations:
                if enforced or not violation.startswith(BLAZOR_IMPORT_MAP):
                    fail(f"{name}{path}: CSP violation: {violation}")
            page.close()
        browser.close()


def spa(base, name):
    """A menu link swaps the page inside the same document (MVC and Razor Pages with SpaNavigation)."""
    with sync_playwright() as p:
        options = {"executable_path": os.environ["MODULUS_CHROMIUM"]} if os.environ.get("MODULUS_CHROMIUM") else {}
        browser = p.chromium.launch(args=["--no-sandbox"], **options)
        page = browser.new_page(viewport={"width": 1280, "height": 900})
        page.goto(base + "/", wait_until="networkidle")
        page.evaluate("() => { window.__marker = 'same-document'; }")
        page.mouse.move(0, 0)  # a chart tooltip left under the pointer by the page load would cover the link
        page.locator("a[href='/pages/faq']:visible").first.click()
        page.wait_for_url("**/pages/faq", timeout=15000)
        if page.evaluate("() => window.__marker") != "same-document":
            fail(f"{name}: a menu click reloaded the whole document")
        browser.close()


def main():
    apps = base_urls()
    if not apps:
        print("Set MODULUS_MVC, MODULUS_RAZOR, MODULUS_BLAZOR and/or MODULUS_FLUID.")
        return 2
    for name, base in apps.items():
        health(base, name)
        headers(base, name)
        if name == "fluid":
            continue
        check_pages(base, name, enforced=name in ("mvc", "razor"))
    if "mvc" in apps:
        spa(apps["mvc"], "mvc")
    if "razor" in apps:
        spa(apps["razor"], "razor")
    print("smoke: " + ("passed" if not failures else f"{len(failures)} failure(s)"))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
