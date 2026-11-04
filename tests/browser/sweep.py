"""
Sweep of the plugin and table pages that were written but not browser-checked (plan §7.4, C1), on the MVC, Razor Pages and
Blazor samples. Each page is opened at 1280 px, at 390 px (phone) and in the dark colour scheme, and fails on: a status other
than 200, a console error, a CSP violation (the Blazor import map is allowed, as in smoke.py), or a horizontal scroll on the phone.
Not part of the CI gates: it takes several minutes. Run it by hand against the samples:

    MODULUS_CHROMIUM=... python3 tests/browser/sweep.py
"""
from playwright.sync_api import sync_playwright
PAGES = ["/plugins/charts-advanced","/plugins/code-editor","/plugins/color-picker","/plugins/date-picker","/plugins/document",
 "/plugins/dropzone","/plugins/editor","/plugins/image-cropper","/plugins/lightbox","/plugins/map","/plugins/map-fullsize",
 "/plugins/map-vector","/plugins/player","/plugins/sortable","/plugins/spreadsheet","/plugins/tour","/plugins/pdf",
 "/tables/tree","/tables/advanced","/apps/calendar","/apps/users/grid","/pages/changelog","/ui/icons","/ui/typography","/ui/colors","/ui/charts","/tables/ask","/apps/assistant"]
import os
BASES = {"mvc": os.environ.get("MODULUS_MVC", "http://localhost:5100"), "razor": os.environ.get("MODULUS_RAZOR", "http://localhost:5101"), "blazor": os.environ.get("MODULUS_BLAZOR", "http://localhost:5102")}
import base64
BLANK = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=")
results = []
with sync_playwright() as p:
    b = p.chromium.launch(args=["--no-sandbox"], **({"executable_path": os.environ["MODULUS_CHROMIUM"]} if os.environ.get("MODULUS_CHROMIUM") else {}))
    for name, base in BASES.items():
        for mode in ["desktop", "phone", "dark"]:
            ctx = b.new_context(viewport={"width": 390 if mode == "phone" else 1280, "height": 844 if mode == "phone" else 900},
                                color_scheme="dark" if mode == "dark" else "light")
            ctx.route("https://tile.openstreetmap.org/**", lambda r: r.fulfill(status=200, content_type="image/png", body=BLANK))
            for path in PAGES:
                page = ctx.new_page(); problems = []
                page.add_init_script("""document.addEventListener('securitypolicyviolation', e => {
                    window.__csp = (window.__csp || []).concat([e.violatedDirective + ' ' + (e.sample||'').slice(0,40)]); });""")
                page.on("console", lambda m: problems.append("console: " + m.text[:120]) if m.type == "error" and "favicon" not in m.text and "tile.openstreetmap" not in m.text and "ERR_TUNNEL" not in m.text else None)
                page.on("pageerror", lambda e: problems.append("pageerror: " + str(e)[:120]))
                try:
                    r = page.goto(base + path, wait_until="networkidle", timeout=45000)
                    status = r.status if r else 0
                    page.wait_for_timeout(400)
                    if status != 200: problems.append(f"status {status}")
                    for v in page.evaluate("() => window.__csp || []"):
                        if not (name == "blazor" and "script-src-elem" in v): problems.append("csp: " + v)
                    if mode == "phone":
                        sw = page.evaluate("() => document.documentElement.scrollWidth - document.documentElement.clientWidth")
                        if sw > 1: problems.append(f"horizontal overflow {sw}px")
                except Exception as e:
                    problems.append("error: " + str(e)[:120])
                if problems: results.append((name, mode, path, problems[:3]))
                page.close()
            ctx.close()
    b.close()
for r in results: print(r[0], r[1], r[2], "|", " ; ".join(r[3]))
print("pages checked:", len(PAGES) * 9, "pages with problems:", len(results))
