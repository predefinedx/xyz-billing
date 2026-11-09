# Home Task - XYZ Billing

A small full-stack billing application for "XYZ Inc.". 
Users submit orders through a web UI and the backend processes them through mocked payment gateways and returns a receipt or error.

## Running the backend (API)

```bash
cd api/XYZ.Billing.Api
dotnet run
```

The API starts on **http://localhost:5050**.

- Swagger UI: http://localhost:5050/swagger
- OpenAPI document: http://localhost:5050/openapi/v1.json

The SQLite database (billing.db) is created automatically in the API project folder on first start — no setup or migrations needed.

## Running the frontend (UI)

```bash
cd web
npm install
npm start
```

The UI is served on **http://localhost:4200** and talks to the API at localhost:5050 (CORS is pre-configured for this origin).

## Configuration

Everything works out of the box — no environment variables or API keys are required. Ports and allowed CORS origin are defined in:

- api/XYZ.Billing.Api/Properties/launchSettings.json (API port)
- api/XYZ.Billing.Api/Program.cs (CORS origin)
- web/src/app/core/services/billing.service.ts (API base URL used by the UI)

## Payment gateways

Payments are processed through mocked gateways, selectable in the UI:

- mock-success - Always approves the payment and returns a confirmation
- mock-failure - Always declines the payment 

New gateways can be added by implementing IPaymentGateway and registering the implementation in Program.cs — the order service resolves gateways by their Id at runtime.

## Idempotency

Submitting the same order (same order number and identical details) twice doesn't charge twice — the original receipt is returned instead. 
Reusing an order number with different details is rejected with 409 Conflict. This is enforced at the database level with a unique index on the order number, so concurrent duplicate submissions are also safe.

## Known limitation: orders stuck in Processing

An order is persisted with status Processing before the payment gateway is called, and is updated to Paid or Declined once the gateway responds. 
If the application crashes (or the gateway call fails unexpectedly) between these two steps, the order remains in Processing indefinitely, and resubmitting it returns 409 Conflict.

There is intentionally no automatic recovery for this state: with mocked payment gateways there is no way to verify whether the payment was actually made (no status-lookup API or webhooks at this task's scope). 
Guessing in either direction would risk a double charge or a lost payment. 
In a real integration this would be resolved by reconciling against the provider — querying the payment status by an idempotency key, or receiving webhook notifications — and then completing or failing the stuck order accordingly.
