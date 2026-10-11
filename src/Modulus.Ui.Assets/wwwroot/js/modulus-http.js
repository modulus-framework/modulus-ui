/*
 * Modulus same-origin JSON requests for Blazor pages.
 *
 * A Blazor Server circuit runs on the server, so an injected HttpClient never carries the user's cookies. These calls
 * run in the browser instead: fetch sends the sign-in cookie on same-origin requests, and the antiforgery token
 * (rendered by <ModulusHead /> as <meta name="csrf-token">) rides along in the RequestVerificationToken header.
 *
 * Imported by ModulusHttp (Modulus.Blazor); resolves to { status, ok, body } and never throws.
 */
export async function send(method, url, body) {
    const headers = { Accept: 'application/json' };
    const token = document.querySelector('meta[name="csrf-token"]')?.content;
    if (token) headers.RequestVerificationToken = token;

    const init = { method, headers, credentials: 'same-origin' };
    if (body !== null && body !== undefined) {
        headers['Content-Type'] = 'application/json';
        init.body = JSON.stringify(body);
    }

    try {
        const response = await fetch(url, init);
        const text = await response.text();
        let data = null;
        if (text) {
            try { data = JSON.parse(text); } catch { /* not JSON: leave the body empty */ }
        }
        return { status: response.status, ok: response.ok, body: data };
    } catch {
        return { status: 0, ok: false, body: null };
    }
}
