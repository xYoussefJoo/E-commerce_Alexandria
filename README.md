# Alexandria — Every story, shelved

A full-stack online bookstore built with ASP.NET Core MVC. Browse a categorized catalog, add books to a cart, check out, and track order history as a customer; manage the catalog and view sales analytics as an admin.

## Features

**Catalog & browsing**
- Books organized into a two-level category hierarchy (categories and sub-categories)
- Product detail pages with related-book suggestions and view-count tracking
- Paginated listings for books, categories, and order history

**Shopping & checkout**
- Cart with per-product quantity, clamped to available stock
- Checkout with shipping address and payment method (Cash on Delivery, Credit Card, PayPal)
- Stock is decremented atomically at checkout, so two simultaneous orders can't oversell the same copy
- Order history and per-order detail pages

**Accounts & roles**
- ASP.NET Core Identity authentication with `Customer` and `Admin` roles
- Admin-only catalog management (create/edit/delete books and categories, image upload with content validation)
- Admin dashboard with revenue, order-status breakdown, best sellers, top customers, and a 14-day revenue chart

**Frontend**
- Server-rendered Razor views styled with Tailwind CSS
- [htmx](https://htmx.org/)-boosted navigation for fast, app-like page transitions without a SPA framework

## Tech stack

- ASP.NET Core MVC (.NET 10)
- Entity Framework Core + SQL Server
- ASP.NET Core Identity
- Tailwind CSS, htmx
- xUnit tests running against a real SQLite in-memory database

## Getting started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (LocalDB or SQL Server Express both work — see the connection string in `appsettings.json`)

### Setup

```bash
git clone https://github.com/xYoussefJoo/E-commerce_Alexandria.git
cd E-commerce_Alexandria
dotnet tool install --global dotnet-ef   # skip if you already have it
dotnet ef database update
dotnet run
```

The app seeds an `Admin` and `Customer` role on first run and creates a default admin account. Its password isn't hardcoded — set it before running via [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets):

```bash
dotnet user-secrets init
dotnet user-secrets set "AdminSeed:Password" "YourStrongPassword1!"
```

If you skip this, a random password is generated and printed once to the console on startup.

### Rebuilding CSS

Tailwind output is checked in at `wwwroot/css/output.css`, so you don't need the Tailwind CLI just to run the app. To rebuild it after editing `Styles/tailwind.css`, download the [Tailwind CLI](https://github.com/tailwindlabs/tailwindcss/releases) for your platform and run:

```bash
./tailwindcss -i Styles/tailwind.css -o wwwroot/css/output.css --minify
```

### Running tests

```bash
dotnet test
```

## Project structure

```
Controllers/        MVC controllers (catalog, cart, orders, accounts)
Areas/Admin/         Admin dashboard
Models/              Domain models and view models
Data/                EF Core DbContext
Migrations/          EF Core migrations
Views/               Razor views
wwwroot/             Static assets, Tailwind output, uploaded product images
Tests/               xUnit test project
```

## License

No license specified — all rights reserved by the repository owner.
