# Integration Testing

The `test/Paymob.IntegrationTests` project runs the SDK against Paymob's real
API (`https://accept.paymob.com`). These tests exist to prove the SDK's happy
path works end-to-end, beyond what mocked unit tests can show.

## Environment variables

| Variable | Required | Used by | Description |
|---|---|---|---|
| `PAYMOB_API_KEY` | Yes | All tests | API key from the Paymob dashboard (Developers → API keys). |
| `PAYMOB_TEST_INTEGRATION_ID` | Yes (except auth test) | Order, checkout, intention | A **test-mode** payment integration id (e.g. card) from the dashboard. |
| `PAYMOB_TEST_IFRAME_ID` | Yes for checkout test | Checkout test | The iframe id that renders the checkout page. |
| `PAYMOB_SECRET_KEY` | Yes for intention test | Intention test | Secret key from the dashboard (Developers). |
| `PAYMOB_PUBLIC_KEY` | Optional | Intention test | Public key; when set, the unified-checkout URL is also verified. |

If any required variable is missing, the affected test calls
`Assert.Ignore()` naming the missing variable(s) — the suite reports
"skipped", never fails, and no secrets are ever hardcoded.

## Where to get sandbox credentials

1. Sign in to the Paymob dashboard: https://accept.paymob.com
2. Go to **Developers → API keys** for the API key, secret key, public key,
   and HMAC secret.
3. Use a **test** integration id (card in test mode) — the tests create orders
   and payment keys with a 1 EGP (100 piasters) amount and never submit card
   details, so no real charge is possible. Do not point these tests at a
   production integration id with real money.

## Running

```bash
dotnet test test/Paymob.IntegrationTests -c Release
```

The project is tagged `[Category("Integration")]`. To exclude it from a full
test run:

```bash
dotnet test --filter "TestCategory!=Integration"
```
