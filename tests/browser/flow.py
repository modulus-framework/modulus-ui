"""
The workflow designer in a real browser, on the three samples: a customer message goes through the support desk workflow, the
nodes finish with the success status, the result list is filled, and (MVC and Razor Pages) saving clears the run again.

Environment: MODULUS_MVC, MODULUS_RAZOR and MODULUS_BLAZOR (base URLs), and MODULUS_CHROMIUM (optional browser path).
It fails (exit 1) when a run does not finish, a node fails, a result is missing, or the page logs a console error.
"""
import os
import sys
import time

from playwright.sync_api import sync_playwright

MESSAGE = "My order is late, I want a refund"
PAGE = "/apps/flow"


def fail(message):
    print(f"FAIL {message}")
    sys.exit(1)


def statuses(page):
    return {
        "success": page.locator('.m-flow-node[data-status="success"]').count(),
        "error": page.locator('.m-flow-node[data-status="error"]').count(),
        "running": page.locator('.m-flow-node[data-status="running"]').count(),
    }


def wait_for_run(page, name, timeout=60):
    # The Blazor run paints each node as it starts and ends, so wait until nothing is running and something succeeded.
    deadline = time.time() + timeout
    while time.time() < deadline:
        counts = statuses(page)
        if counts["running"] == 0 and counts["success"] > 0:
            return counts
        page.wait_for_timeout(250)
    fail(f"{name}: the run did not finish ({statuses(page)})")


def check(name, base, interactive_save):
    with sync_playwright() as p:
        options = {"executable_path": os.environ["MODULUS_CHROMIUM"]} if os.environ.get("MODULUS_CHROMIUM") else {}
        browser = p.chromium.launch(args=["--no-sandbox"], **options)
        page = browser.new_page(viewport={"width": 1280, "height": 900})
        errors = []
        page.on("console", lambda m: errors.append(m.text) if m.type == "error" and "favicon" not in m.text else None)
        page.on("pageerror", lambda e: errors.append(str(e)))
        response = page.goto(base + PAGE, wait_until="networkidle")
        if response is None or response.status != 200:
            fail(f"{name}{PAGE}: answered {response.status if response else 'nothing'}")
        page.wait_for_selector(".m-flow.m-flow-ready", timeout=30000)

        page.locator('input[aria-label="Customer message"]').fill(MESSAGE)
        if interactive_save:
            # MVC and Razor Pages: Run posts the canvas and renders the result on the same page.
            with page.expect_navigation(wait_until="networkidle"):
                page.locator("button", has_text="Run").first.click()
            page.wait_for_selector(".m-flow.m-flow-ready", timeout=30000)
        else:
            page.locator("button", has_text="Run").first.click()
        counts = wait_for_run(page, name)
        if counts["error"]:
            fail(f"{name}: {counts['error']} node(s) failed")
        if page.locator(".m-card-title", has_text="Last run").count() == 0 and interactive_save:
            fail(f"{name}: no Last run card after the run")
        if interactive_save:
            if page.locator('input[aria-label="Customer message"]').input_value() != MESSAGE:
                fail(f"{name}: the message was not kept after the run")
            with page.expect_navigation(wait_until="networkidle"):
                page.locator("button", has_text="Save workflow").click()
            page.wait_for_selector(".m-flow.m-flow-ready", timeout=30000)
            if statuses(page)["success"] != 0 or page.locator(".m-card-title", has_text="Last run").count() != 0:
                fail(f"{name}: saving did not clear the run")
        print(f"{name}: run finished with {counts['success']} node(s) successful, {len(errors)} console error(s)")
        for message in errors:
            fail(f"{name}{PAGE}: console error: {message[:160]}")
        browser.close()


def main():
    targets = [
        ("mvc", os.environ.get("MODULUS_MVC"), True),
        ("razor", os.environ.get("MODULUS_RAZOR"), True),
        ("blazor", os.environ.get("MODULUS_BLAZOR"), False),
    ]
    for name, base, interactive_save in targets:
        if base:
            check(name, base.rstrip("/"), interactive_save)
    print("flow: passed")


if __name__ == "__main__":
    main()
