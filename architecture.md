# Architecture Decision Record

## 1. Why the Outbox Pattern instead of in-memory queue

**Rejected alternative:** Call the ERP adapter directly from the webhook controller, or push to an in-memory `Channel<T>`.

**Problem with direct call:** Shopify expects a response in under 5 seconds. Real ERP calls (especially REST-based cloud ERPs) regularly take 2-10 seconds and can time out. A slow ERP causes Shopify to retry the webhook, which causes duplicates.

**Problem with in-memory queue:** The queue is lost on process restart. If the API pod crashes mid-deployment, any queued orders are silently dropped.

**Why outbox wins:** The webhook controller does one thing — writes a row to `OutboxMessages` and returns 200. This is a local database write, takes < 5ms, and never fails due to ERP issues. The background worker processes the queue independently. If the process restarts, the queue survives in the database. If the ERP is down, messages stay pending until it recovers.

**Trade-off:** Introduces eventual consistency — orders appear in the ERP seconds after the webhook, not milliseconds. Acceptable for all real-world use cases (no business needs sub-second ERP sync).

---

## 2. Why HMAC verification matters

Shopify webhooks arrive over the public internet. Without verification, any attacker who discovers the endpoint can POST fake orders — creating inventory movements, invoices, and customer records that don't correspond to real purchases.

Shopify signs every webhook with HMAC-SHA256 using the shared secret from the Partner Dashboard. The middleware recomputes the HMAC from the raw body and compares using `CryptographicOperations.FixedTimeEquals` (constant-time comparison). A standard `==` comparison is vulnerable to timing attacks — an attacker can probe byte by byte to guess the signature. Constant-time comparison eliminates this.

The middleware runs before routing, so invalid requests never reach the controller. The body is buffered (`EnableBuffering`) so it can be read by the middleware and re-read by the controller without stream rewinding issues.

---

## 3. How the adapter pattern enables ERP portability

The `IErpAdapter` interface defines exactly what the sync pipeline needs from any ERP: get or create a customer, create an order, decrement stock, generate an invoice. The pipeline (`OrderSyncService`) depends only on this interface — it has no knowledge of whether the ERP is a local SQL database, a REST API, or a legacy SOAP service.

Two implementations ship with the project:

**`SqlErpAdapter`** — for ERPs that expose a SQL database or ORM (Logo Tiger, Mikro, custom in-house systems). Uses Entity Framework directly. Fastest path; no HTTP overhead.

**`RestApiErpAdapter`** — for cloud ERPs (NetSuite, SAP Business One, Odoo, Microsoft Dynamics). Demonstrates the HTTP call pattern with realistic latency simulation. In production, replace the mock HTTP calls with real Refit or HttpClient calls to the ERP's REST API.

Switching between them requires one config change and zero code changes in the sync pipeline.

**What a real NetSuite adapter would look like:** Replace `SimulateApiCallAsync` with actual `HttpClient.PostAsJsonAsync` calls to the NetSuite REST API, handle OAuth 2.0 token refresh, map Shopify fields to NetSuite's record schema, handle NetSuite-specific error codes. The `OrderSyncService` doesn't change at all.

---

## 4. Trade-offs and what changes at higher scale

### Current design suits: single-tenant, moderate volume (up to ~500 orders/hour)

**Outbox polling interval (5 seconds):** Good for demos and low-medium volume. At high volume, reduce to 1s or switch to database `LISTEN/NOTIFY` (PostgreSQL) or SQL Server Service Broker to eliminate polling latency entirely.

**Batch size (10 messages):** Conservative. Safe to increase to 50-100 for higher throughput. Each message is independent so batches parallelise trivially.

**Single background worker:** Fine for one API instance. For horizontal scaling (multiple API pods), the `MarkProcessingAsync` call acts as an optimistic lock — only one worker processes each message. At very high scale, add a distributed lock (Redis `SETNX`) around batch fetch to prevent duplicate processing across pods.

**Stock decrement in-process:** Current approach loads product entity, decrements, saves within the order transaction. At high concurrency, two orders for the same SKU processed simultaneously can both read `stock=10`, both write `stock=9`, resulting in stock=9 instead of stock=8. Fix with `UPDATE Products SET StockQuantity = StockQuantity - @qty WHERE Sku = @sku` (raw SQL with SQL Server's row-level locking) or an optimistic concurrency token on `StockQuantity`.

**No dead-letter queue UI:** Failed messages are visible in the Outbox page but require manual DB intervention to retry. Production addition: add a "Retry" button on the Outbox page that resets `Status='pending'` and `AttemptCount=0`.

**No webhook deduplication at receipt:** The idempotency check happens at sync time (by `ShopifyOrderId`), not at webhook receipt. Two rapid identical webhook deliveries both create `OutboxMessage` rows; only one gets through to the ERP. Acceptable — the extra row just gets marked `DuplicateOrderSkipped` in the audit log.
