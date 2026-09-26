import { expect, type APIRequestContext, type BrowserContext } from "@playwright/test";

const API_BASE = process.env.PRISMEDIA_E2E_API_URL ?? "http://127.0.0.1:8008/api";
const USERNAME = process.env.PRISMEDIA_E2E_USERNAME ?? "e2e-admin";
const PASSWORD = process.env.PRISMEDIA_E2E_PASSWORD ?? "e2e-admin-password-1";
/** Session cookie name, from SessionCookieName in Prismedia.Api/Security/PrismediaAuthentication.cs. */
const SESSION_COOKIE = "prismedia-session";

/** Creates the first admin on a fresh server if needed, then logs in and returns the session token. */
export async function loginAsE2eUser(request: APIRequestContext): Promise<string> {
  const status = await request.get(`${API_BASE}/auth/setup-status`);
  expect(status.ok()).toBeTruthy();
  if ((await status.json()).needsSetup) {
    const setup = await request.post(`${API_BASE}/auth/setup`, {
      data: { username: USERNAME, password: PASSWORD, displayName: "E2E Admin" },
    });
    expect(setup.ok(), await setup.text()).toBeTruthy();
  }

  const login = await request.post(`${API_BASE}/auth/login`, {
    data: { username: USERNAME, password: PASSWORD, client: "e2e", deviceName: "playwright" },
  });
  expect(login.ok(), await login.text()).toBeTruthy();
  const cookie = login
    .headersArray()
    .filter((header) => header.name.toLowerCase() === "set-cookie")
    .map((header) => header.value)
    .find((value) => value.startsWith(`${SESSION_COOKIE}=`));
  const token = cookie?.split(";")[0]?.slice(SESSION_COOKIE.length + 1) ?? (await login.json()).accessToken;
  expect(token, "login returned no session token").toBeTruthy();
  return token as string;
}

/** Signs a browser context in as the e2e user (same account on every context = "another device"). */
export async function signInContext(context: BrowserContext, request: APIRequestContext, baseURL: string) {
  const token = await loginAsE2eUser(request);
  const url = new URL(baseURL);
  await context.addCookies([
    { name: SESSION_COOKIE, value: token, domain: url.hostname, path: "/", httpOnly: true, sameSite: "Lax" },
  ]);
  return token;
}
