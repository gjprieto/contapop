# Domain Analysis

This document provides an analysis of the domain, including key concepts, entities, and relationships relevant to the project. It aims to offer a comprehensive understanding of the domain to guide development and decision-making processes.

## Context

Contapop is an accountancy SaaS platform that provides tools and services for managing financial and accounting tasks for small organizations and individuals. It enables users to handle resources, projects, and other entities within a structured and secure environment. The main goal is to keep financial and accounting processes organized, efficient, and accessible.

## Key Concepts

- **Entity**: A distinct object or concept within the domain that has a unique identity.
- **Attribute**: A property or characteristic of an entity that provides additional information about it.
- **Relationship**: An association between two or more entities that defines how they interact or are connected within the domain.

## Key Entities (MVP)

### Tenant

A Tenant represents an organization or individual that owns and manages resources within the system. It serves as a container for users, projects, and other entities, providing isolation and administrative control.

**Attributes:**
- `id`: Unique identifier for the tenant.
- `name`: Name of the tenant.
- `created_at`: Timestamp when the tenant was created.
- `updated_at`: Timestamp when the tenant was last updated.

### User

A User represents an individual who interacts with the system. Users are associated with a specific tenant and have roles and permissions that determine their access to resources and actions within the system. **Decision (2026-09-06): preferences live directly on User** — `theme`, `language`, `notifications_enabled` are added here rather than a separate entity, since MVP has exactly one User per Tenant (see `docs/analysis/mvp/scope-decisions.md`'s single-owner decision).

**Attributes:**
- `id`: Unique identifier for the user.
- `tenant_id`: Identifier of the tenant the user belongs to.
- `name`: Name of the user.
- `email`: Email address of the user.
- `theme`: UI theme preference (e.g. `light`, `dark`).
- `language`: Preferred language/locale (e.g. `es`, `en`).
- `notifications_enabled`: Whether in-app notifications/reminders are enabled.
- `created_at`: Timestamp when the user was created.
- `updated_at`: Timestamp when the user was last updated.

### Project

A Project represents a collection of resources and activities within the system. Projects are owned by tenants and can have multiple users collaborating on them. They provide a way to organize and manage related entities and workflows. **Decision (2026-09-06): Project is an internal organizational grouping** — e.g. separating personal vs. business books, or tracking multiple business lines under one tenant — not a customer or client. Billing counterparties are modeled separately as Counterparty, below.

**Attributes:**
- `id`: Unique identifier for the project.
- `tenant_id`: Identifier of the tenant the project belongs to.
- `name`: Name of the project.
- `created_at`: Timestamp when the project was created.
- `updated_at`: Timestamp when the project was last updated.

### Bank Account

A Bank Account represents a financial account associated with a tenant or project. It is used to manage and track financial transactions, balances, and other related information within the system. **Decision (2026-09-06): soft-deleted like Project/Transaction** — `ArchiveBankAccount` (`docs/analysis/contracts.md`) needs a status to transition, so this gets the same `active`/`archived` pattern rather than a hard delete.

**Attributes:**
- `id`: Unique identifier for the bank account.
- `tenant_id`: Identifier of the tenant the bank account belongs to.
- `project_id`: Identifier of the project the bank account belongs to (if applicable).
- `account_number`: Bank account number.
- `bank_name`: Name of the bank.
- `status`: `active` or `archived`.
- `created_at`: Timestamp when the bank account was created.
- `updated_at`: Timestamp when the bank account was last updated.

### Credit or Debit Card

A Credit or Debit Card represents a manual, user-entered label for a payment card associated with a tenant or project, used so other records (such as an Expense) can note which card was used. **Decision (2026-09-06): metadata only for the MVP** — no real card number, CVV, or other PCI-scoped data is ever stored; this is a descriptive tag, not a payment method integration or a connection to a tokenization vendor.

**Attributes:**
- `id`: Unique identifier for the credit or debit card.
- `tenant_id`: Identifier of the tenant the card belongs to.
- `project_id`: Identifier of the project the card belongs to (if applicable).
- `label`: User-entered descriptive label (e.g. "Visa ending 1234"), never a real card number.
- `cardholder_name`: Name of the cardholder, as entered by the user.
- `expiration_date`: Optional, user-entered, informational only — not used for any payment processing.
- `created_at`: Timestamp when the card was created.
- `updated_at`: Timestamp when the card was last updated.

### Transaction

A Transaction represents a financial operation involving a bank account. It records details such as the amount, date, type (e.g., income, expense), and associated entities within the system. **Decision (2026-09-06): full reconciliation is in MVP scope** — an Expense, Revenue, or Payment (owned by other services) can reference a Transaction as the bank-side record it was reconciled against, via `reconciled_transaction_id` on those entities (see their sections below). Because Transaction can now be referenced cross-service, it gets a `status` field and is soft-deleted (archived) rather than hard-deleted, the same pattern already used for Project, so a reconciled reference never dangles.

**Attributes:**
- `id`: Unique identifier for the transaction.
- `bank_account_id`: Identifier of the bank account associated with the transaction.
- `amount`: Amount of the transaction.
- `date`: Date of the transaction.
- `type`: Type of the transaction (e.g., income, expense).
- `status`: `active` or `archived` — archived instead of hard-deleted once referenced by a reconciliation.
- `created_at`: Timestamp when the transaction was created.
- `updated_at`: Timestamp when the transaction was last updated.

### Counterparty

A Counterparty represents the customer or supplier on the other side of an Invoice or Ticket — the party being billed (for an outgoing invoice) or the party billing the tenant (for an incoming invoice/ticket). **Decision (2026-09-06): added as a first-class entity** to support the incoming/outgoing distinction and the search/filter behavior the Invoices screen requires.

**Attributes:**
- `id`: Unique identifier for the counterparty.
- `tenant_id`: Identifier of the tenant the counterparty belongs to.
- `type`: Whether the counterparty is a `customer` or a `supplier`.
- `name`: Name of the counterparty (person or organization).
- `tax_id`: Optional tax/fiscal identifier (e.g. NIF/CIF), useful for compliant invoicing.
- `email`: Optional contact email.
- `address`: Optional billing address.
- `status`: `active` or `archived` — set by `ArchiveCounterparty` (`docs/analysis/contracts.md`); soft-deleted rather than hard-deleted since a Counterparty is referenced by Invoices.
- `created_at`: Timestamp when the counterparty was created.
- `updated_at`: Timestamp when the counterparty was last updated.

### Invoice or Ticket

An Invoice or Ticket represents a billing or payment document associated with a tenant or project. It records details such as the amount, date, due date, and associated transactions within the system. **Decision (2026-09-06): added `counterparty_id` and `direction`** — the Invoices screen distinguishes incoming and outgoing invoices, which requires knowing which party (Counterparty) is on the other side and in which direction the money flows. **Decision (2026-09-06): basic VAT/IVA breakdown for MVP** — `amount` is replaced with a net/tax/total breakdown, the minimum needed for a usable Spanish invoice. Full Facturae/SII/Verifactu compliance is explicitly deferred past MVP (see `docs/analysis/mvp/scope-decisions.md`). **Decision (2026-09-06): added a stored `status`** — `IssueInvoice`, `VoidInvoice`, `RecordPayment`, and the `MarkInvoicesOverdue` background job (`docs/analysis/contracts.md`) all transition it, and the Invoices screen filters by it.

**Attributes:**
- `id`: Unique identifier for the invoice or ticket.
- `tenant_id`: Identifier of the tenant the invoice or ticket belongs to.
- `project_id`: Identifier of the project the invoice or ticket belongs to (if applicable).
- `counterparty_id`: Identifier of the Counterparty — the customer being billed (outgoing) or the supplier billing the tenant (incoming).
- `direction`: Whether the invoice is `outgoing` (tenant billing the counterparty) or `incoming` (counterparty billing the tenant).
- `status`: `draft`, `issued`, `paid`, `overdue`, or `void`.
- `net_amount`: Amount before tax.
- `tax_rate`: VAT/IVA rate applied (e.g. 21%, 10%, 4%, or 0% for exempt).
- `tax_amount`: Computed tax amount (`net_amount` × `tax_rate`).
- `total_amount`: Amount actually due (`net_amount` + `tax_amount`). Currency is implicitly EUR for MVP — see `docs/analysis/mvp/scope-decisions.md`.
- `date`: Date of the invoice or ticket.
- `due_date`: Due date of the invoice or ticket.
- `created_at`: Timestamp when the invoice or ticket was created.
- `updated_at`: Timestamp when the invoice or ticket was last updated.

### Payment

A Payment represents a financial transaction related to an invoice or ticket. It records details such as the amount, date, payment method, and associated entities within the system. **Decision (2026-09-06): can be reconciled against a Financial Accounts & Ledger Transaction** — optional, one-to-one for MVP (no split payments across multiple transactions); this is a cross-service reference, validated the same way Project references are (see `docs/analysis/services.md`'s Cross-Service Data Consistency Strategy).

**Attributes:**
- `id`: Unique identifier for the payment.
- `invoice_or_ticket_id`: Identifier of the invoice or ticket associated with the payment.
- `reconciled_transaction_id`: Optional identifier of the Transaction (Financial Accounts & Ledger) this payment was matched against.
- `amount`: Amount of the payment.
- `date`: Date of the payment.
- `payment_method`: Method of the payment (e.g., bank transfer, credit card).
- `created_at`: Timestamp when the payment was created.
- `updated_at`: Timestamp when the payment was last updated.

### Report

A Report represents a saved set of criteria for a financial analysis, not a frozen snapshot. **Decision (2026-09-06): stores `criteria`, not `content`** — per `docs/analysis/contracts.md`'s Reporting section, a saved report's financial content is always computed fresh from the Reporting service's projections at read time; only the title and the criteria used to generate it are persisted.

**Attributes:**
- `id`: Unique identifier for the report.
- `tenant_id`: Identifier of the tenant the report belongs to.
- `project_id`: Identifier of the project the report belongs to (if applicable).
- `title`: Title of the report.
- `criteria`: Structured filter criteria used to generate the report's content (date range, plus any other filters offered on the Reports screen).
- `created_at`: Timestamp when the report was created.
- `updated_at`: Timestamp when the report was last updated.

### Expense

An Expense represents a financial outflow associated with a tenant or project. It records details such as the amount, date, category, and associated entities within the system. It can be recurring or one-time, providing a way to track and manage expenditures effectively. **Decision (2026-09-06): can be reconciled against a Financial Accounts & Ledger Transaction** — optional, one-to-one for MVP; a cross-service reference validated the same way as a Project reference. **Decision (2026-09-06): can also be imported from a PDF via OCR** — see `import_source` below and `docs/analysis/contracts.md`. **Decision (2026-09-06): added `confirmed_at`** — makes the "draft until confirmed" rule from `contracts.md` concrete: null while a `pdf_ocr` import is awaiting `ConfirmImportedExpense`, set immediately for a `manual` record, set by confirmation for an OCR one. Any report-facing query filters on `confirmed_at IS NOT NULL`.

**Attributes:**
- `id`: Unique identifier for the expense.
- `tenant_id`: Identifier of the tenant the expense belongs to.
- `project_id`: Identifier of the project the expense belongs to (if applicable).
- `reconciled_transaction_id`: Optional identifier of the Transaction (Financial Accounts & Ledger) this expense was matched against.
- `amount`: Amount of the expense.
- `date`: Date of the expense.
- `category`: Category of the expense (e.g., utilities, salaries).
- `recurring`: Indicates if the expense is recurring.
- `recurring_interval`: Specifies the interval at which the expense recurs (e.g., monthly, yearly).
- `import_source`: How the record was created — `manual` or `pdf_ocr`. An OCR-imported expense is created as a draft pending user confirmation before it counts toward reports (see `contracts.md`).
- `confirmed_at`: Null while an OCR-imported draft awaits confirmation; set (immediately for manual, on confirmation for OCR) once the record counts toward reports.
- `created_at`: Timestamp when the expense was created.
- `updated_at`: Timestamp when the expense was last updated.

### Revenue
 
A Revenue represents a financial inflow associated with a tenant or project. It records details such as the amount, date, category, and associated entities within the system. It can be recurring or one-time, providing a way to track and manage income effectively. **Decision (2026-09-06): can be reconciled against a Financial Accounts & Ledger Transaction** — optional, one-to-one for MVP; a cross-service reference validated the same way as a Project reference. **Decision (2026-09-06): can also be imported from a PDF via OCR** — see `import_source` below and `docs/analysis/contracts.md`. **Decision (2026-09-06): added `confirmed_at`** — same purpose as Expense's field, above.

**Attributes:**
- `id`: Unique identifier for the revenue.
- `tenant_id`: Identifier of the tenant the revenue belongs to.
- `project_id`: Identifier of the project the revenue belongs to (if applicable).
- `reconciled_transaction_id`: Optional identifier of the Transaction (Financial Accounts & Ledger) this revenue was matched against.
- `amount`: Amount of the revenue.
- `date`: Date of the revenue.
- `category`: Category of the revenue (e.g., sales, investments).
- `recurring`: Indicates if the revenue is recurring.
- `recurring_interval`: Specifies the interval at which the revenue recurs (e.g., monthly, yearly).
- `import_source`: How the record was created — `manual` or `pdf_ocr`. An OCR-imported revenue is created as a draft pending user confirmation before it counts toward reports (see `contracts.md`).
- `confirmed_at`: Null while an OCR-imported draft awaits confirmation; set (immediately for manual, on confirmation for OCR) once the record counts toward reports.
- `created_at`: Timestamp when the revenue was created.
- `updated_at`: Timestamp when the revenue was last updated.

### Plan

A Plan represents a financial strategy or allocation associated with a tenant or project. It outlines the intended distribution of resources, budgetary goals, and financial targets within the system, and acts as the budget (allocation ceiling) for its period as well as a container for detailed forecast line items via Planned Revenue and Planned Expense. **Decision (2026-09-06): the earlier separate `Budget` entity is merged into `Plan`** — the MVP screens only expose a "Plans" screen, and Budget's shape (an allocated amount over a period) is fully covered here.

**Attributes:**
- `id`: Unique identifier for the plan.
- `tenant_id`: Identifier of the tenant the plan belongs to.
- `project_id`: Identifier of the project the plan belongs to (if applicable).
- `title`: Title of the plan.
- `description`: Description or details of the plan.
- `allocated_amount`: Optional overall allocation/budget ceiling for the plan's period (absorbed from the former Budget entity).
- `start_date`: Start date of the plan's period (absorbed from the former Budget entity).
- `end_date`: End date of the plan's period (absorbed from the former Budget entity).
- `status`: `active` or `archived` — set by `ArchivePlan` (`docs/analysis/contracts.md`).
- `created_at`: Timestamp when the plan was created.
- `updated_at`: Timestamp when the plan was last updated.

### Planned Revenue

A Planned Revenue represents an anticipated financial inflow associated with a tenant or project. It records details such as the expected amount, date, category, and associated entities within the system. It helps in forecasting and managing future income effectively.

**Attributes:**
- `id`: Unique identifier for the planned revenue.
- `tenant_id`: Identifier of the tenant the planned revenue belongs to.
- `project_id`: Identifier of the project the planned revenue belongs to (if applicable).
- `plan_id`: Identifier of the plan the planned revenue is associated with (if applicable).
- `amount`: Expected amount of the revenue.
- `date`: Expected date of the revenue.
- `category`: Category of the revenue (e.g., sales, investments).
- `recurring`: Indicates if the planned revenue is recurring.
- `recurring_interval`: Specifies the interval at which the planned revenue recurs (e.g., monthly, yearly).
- `created_at`: Timestamp when the planned revenue was created.
- `updated_at`: Timestamp when the planned revenue was last updated.

### Planned Expense

A Planned Expense represents an anticipated financial outflow associated with a tenant or project. It records details such as the expected amount, date, category, and associated entities within the system. It helps in forecasting and managing future expenses effectively.

**Attributes:**
- `id`: Unique identifier for the planned expense.
- `tenant_id`: Identifier of the tenant the planned expense belongs to.
- `project_id`: Identifier of the project the planned expense belongs to (if applicable).
- `plan_id`: Identifier of the plan the planned expense is associated with (if applicable).
- `amount`: Expected amount of the expense.
- `date`: Expected date of the expense.
- `category`: Category of the expense (e.g., operational, marketing).
- `recurring`: Indicates if the planned expense is recurring.
- `recurring_interval`: Specifies the interval at which the planned expense recurs (e.g., monthly, yearly).
- `created_at`: Timestamp when the planned expense was created.
- `updated_at`: Timestamp when the planned expense was last updated.

## Relationships

- A User belongs to a Tenant and can have multiple roles and permissions.
- A Project belongs to a Tenant and can have multiple Users collaborating on it. It is an internal organizational grouping, not a customer.
- A Bank Account belongs to a Tenant or Project and is used to manage financial transactions.
- A Credit or Debit Card belongs to a Tenant or Project and is used to manage financial transactions.
- A Transaction is associated with a Bank Account and records financial operations. It may be referenced by an Expense, a Revenue, or a Payment as the bank-side record they were reconciled against.
- An Invoice or Ticket belongs to a Tenant or Project, references a Counterparty (customer or supplier), has a direction (incoming or outgoing), and is associated with Transactions.
- A Counterparty belongs to a Tenant and is referenced by Invoices as the customer or supplier on the other side of the billing relationship.
- A Payment is related to an Invoice or Ticket and records financial transactions. It may optionally reference a reconciled Transaction.
- A Report belongs to a Tenant or Project and provides financial analysis.
- An Expense belongs to a Tenant or Project and records financial outflows. It may optionally reference a reconciled Transaction, and may be created manually or imported from a PDF via OCR.
- A Revenue belongs to a Tenant or Project and records financial inflows. It may optionally reference a reconciled Transaction, and may be created manually or imported from a PDF via OCR.
- A Plan belongs to a Tenant or Project and can have multiple Planned Revenues and Planned Expenses associated with it, representing a financial strategy, budget, and forecast (absorbing the former Budget entity).
- A Planned Revenue belongs to a Tenant or Project and is associated with a Plan, representing anticipated financial inflows.
- A Planned Expense belongs to a Tenant or Project and is associated with a Plan, representing anticipated financial outflows.