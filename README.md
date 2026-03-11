Vieo Demo:
https://youtu.be/RKDg8MKEZvY

## SportsStore – CA1 (Full Stack Development)

This repository contains my upgraded and extended **SportsStore** application (originally .NET 6). The project has been modernised to **.NET 9** and includes **CI validation**, **structured logging (Serilog + Seq)**, and a **Stripe test payment checkout flow**.

### Key Features Delivered

* ✅ **Upgrade to .NET 9 (net9.0)** for both the web app and test project.
* ✅ **Serilog structured logging** configured via `appsettings.json`:

  * Logs to **Console**
  * Logs to **Rolling file** (`SportsStore/Logs/log-YYYYMMDD.txt`)
  * Logs to **Seq** (`http://localhost:5341`)
  * Includes traceability fields such as **CorrelationId**, **CartId**, **ProductIds**, **OrderId**, **StripeSessionId**, and **PaymentIntentId**.
* ✅ **Stripe payment integration (test mode)** using the official Stripe .NET SDK:

  * Checkout redirects to Stripe, processes payment, then confirms the order only after `PaymentStatus == "paid"`
  * Handles **Success**, **Cancel**, and **Failed** scenarios
  * Stores Stripe confirmation details with the order (SessionId, PaymentIntentId, Status, PaidAtUtc)
* ✅ **GitHub Actions CI pipeline**:

  * Runs on **push to main** and **pull requests to main**
  * Executes `dotnet restore`, `dotnet build`, and `dotnet test`
  * Uploads **TRX test results** as an artifact

---

## How to Run Locally

### Prerequisites

* .NET SDK 9.x installed
* SQL Server LocalDB (or your configured SQL Server)
* (Optional) Seq running locally on `http://localhost:5341`

### 1) Restore / Build / Test

From the repo root:

```bash
dotnet clean
dotnet restore
dotnet build
dotnet test
```

### 2) Configure Stripe keys (User Secrets – required for payments)

From the `SportsStore` project folder:

```bash
dotnet user-secrets init
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
```

> Keys are stored locally and are not committed to source control.

### 3) Run the application

```bash
dotnet run --project .\SportsStore\SportsStore.csproj
```

Open: `http://localhost:5000/`

---

## Observability (Serilog + Seq)

* Rolling file logs are written to: `SportsStore/Logs/`
* Seq (optional) should be available at: `http://localhost:5341`
* Important traceability properties included in logs:

  * `CorrelationId`, `CartId`, `ProductIds`
  * `StripeSessionId`, `PaymentIntentId`, `PaymentStatus`
  * `OrderId`, `Total`, `Items`

---

## Stripe Test Flow (End-to-End)

1. Add products to cart → Checkout
2. Submit checkout → redirected to Stripe Checkout
3. Use test card:

   * `4242 4242 4242 4242` (any future exp date, any CVC)
4. On success, app returns to `/Payment/Success` and then `/Completed?orderId=...`
5. Cancel returns to `/Payment/Cancel`
6. Invalid session id triggers the Failed path

---

## CI Pipeline

GitHub Actions workflow is located at:

* `.github/workflows/ci.yml`

It validates the solution by running restore/build/test on every:

* push to `main`
* pull request to `main`

---
