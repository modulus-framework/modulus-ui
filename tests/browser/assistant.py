"""
The AI assistant in a real browser, on the MVC, Razor Pages and Blazor samples: a style guide and a purchase order are uploaded;
the order waits for approval and is approved; a knowledge question answers from the style guide and cites it; the agent answers
a sales question with a table, a forecast with a table, and an order lookup from its tool.

Environment: MODULUS_MVC, MODULUS_RAZOR and MODULUS_BLAZOR (base URLs), and MODULUS_CHROMIUM (optional browser path).
It fails (exit 1) when an expected answer, card or table is missing, or the page logs a console error.
"""
import os
import sys
import tempfile
import time

from playwright.sync_api import sync_playwright

STYLE = "Brand style guide\n\nButtons use the primary colour. Headings are sentence case.\n"
ORDER = "PURCHASE ORDER\nPO-1001\nBuyer: Acme Retail Ltd\n\nSKU-100 2 x 49.90\nSKU-200 1 x 120.00\n"


def fail(message):
    print(f"FAIL {message}")
    sys.exit(1)


def turns(page):
    return page.locator(".m-chat-turn").count()


def ask(page, question, blazor):
    before = turns(page)
    page.locator("textarea.m-chat-input").fill(question)
    if blazor:
        page.locator(".m-chat-composer button").first.click()
        for _ in range(80):
            page.wait_for_timeout(250)
            if turns(page) > before + 1:
                break
    else:
        with page.expect_navigation(wait_until="networkidle"):
            page.locator(".m-chat-composer button").first.click()
    page.wait_for_timeout(300)


def check(name, base, blazor, files):
    with sync_playwright() as p:
        options = {"executable_path": os.environ["MODULUS_CHROMIUM"]} if os.environ.get("MODULUS_CHROMIUM") else {}
        browser = p.chromium.launch(args=["--no-sandbox"], **options)
        page = browser.new_page(viewport={"width": 1280, "height": 1000})
        errors = []
        page.on("console", lambda m: errors.append(m.text[:140]) if m.type == "error" and "favicon" not in m.text and "tile.openstreetmap" not in m.text and "ERR_TUNNEL" not in m.text else None)
        page.on("pageerror", lambda e: errors.append(str(e)[:140]))
        response = page.goto(base + "/apps/assistant", wait_until="networkidle")
        if response is None or response.status != 200:
            fail(f"{name}: /apps/assistant answered {response.status if response else 'nothing'}")

        # Upload: the style guide and the order (the order's approval is asked for at once).
        page.locator("input[type=file]").first.set_input_files(files)
        if blazor:
            page.wait_for_timeout(2500)
        else:
            with page.expect_navigation(wait_until="networkidle"):
                page.locator(".m-upload button", has_text="Upload").first.click()
        names = page.locator(".m-upload-name").all_inner_texts()
        if "style-guide.md" not in names or "po-1001.txt" not in names:
            fail(f"{name}: the uploaded documents are not listed ({names})")

        # Approval: the order's card is waiting; Approve creates the order.
        approve = page.get_by_role("button", name="Approve") if blazor else page.locator("button[value$=':approve']")
        if approve.count() == 0:
            fail(f"{name}: no approval card for the purchase order")
        if blazor:
            approve.first.click()
            page.wait_for_timeout(1500)
        else:
            with page.expect_navigation(wait_until="networkidle"):
                approve.first.click()
        if "approved, and the order is created" not in page.locator(".m-chat-log").inner_text():
            fail(f"{name}: approving the order did not create it")

        # Knowledge base: the answer comes from the style guide and cites it.
        if blazor:
            page.get_by_label("Knowledge base (sources cited)").check()
        else:
            page.locator("input[type=radio][name=mode][value=kb]").check()
        ask(page, "what colour do buttons use?", blazor)
        last = page.locator(".m-chat-turn").last
        if "primary colour" not in last.inner_text():
            fail(f"{name}: the knowledge answer does not use the style guide: {last.inner_text()[:120]!r}")
        if "style-guide.md" not in last.locator(".m-chat-sources").inner_text():
            fail(f"{name}: the knowledge answer does not cite style-guide.md")

        # Agent: analytics as a table, a forecast as a table, and a lookup through a tool.
        if blazor:
            page.get_by_label("Agent with tools").check()
        else:
            page.locator("input[type=radio][name=mode][value=agent]").check()
        ask(page, "sales by city", blazor)
        if page.locator(".m-chat-table").count() < 1:
            fail(f"{name}: the sales answer has no table")
        ask(page, "forecast sales for Paris", blazor)
        if "(forecast)" not in page.locator(".m-chat-log").inner_text():
            fail(f"{name}: the forecast has no forecast row")
        ask(page, "where is SHP-000123?", blazor)
        if "SHP-000123:" not in page.locator(".m-chat-turn").last.inner_text():
            fail(f"{name}: the order lookup did not answer")

        if errors:
            fail(f"{name}: console errors: {errors[:3]}")
        print(f"{name}: upload, approval, knowledge answer, agent analytics and lookup passed")
        browser.close()


def main():
    folder = tempfile.mkdtemp()
    style = os.path.join(folder, "style-guide.md")
    order = os.path.join(folder, "po-1001.txt")
    with open(style, "w", encoding="utf-8") as f:
        f.write(STYLE)
    with open(order, "w", encoding="utf-8") as f:
        f.write(ORDER)
    files = [style, order]
    for name, base, blazor in [("mvc", os.environ.get("MODULUS_MVC"), False), ("razor", os.environ.get("MODULUS_RAZOR"), False), ("blazor", os.environ.get("MODULUS_BLAZOR"), True)]:
        if base:
            check(name, base.rstrip("/"), blazor, files)
    print("assistant: passed")


if __name__ == "__main__":
    main()
