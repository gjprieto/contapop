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

A User represents an individual who interacts with the system. Users are associated with a specific tenant and have roles and permissions that determine their access to resources and actions within the system.

**Attributes:**
- `id`: Unique identifier for the user.
- `tenant_id`: Identifier of the tenant the user belongs to.
- `name`: Name of the user.
- `email`: Email address of the user.
- `created_at`: Timestamp when the user was created.
- `updated_at`: Timestamp when the user was last updated.

### Project

A Project represents a collection of resources and activities within the system. Projects are owned by tenants and can have multiple users collaborating on them. They provide a way to organize and manage related entities and workflows.

**Attributes:**
- `id`: Unique identifier for the project.
- `tenant_id`: Identifier of the tenant the project belongs to.
- `name`: Name of the project.
- `created_at`: Timestamp when the project was created.
- `updated_at`: Timestamp when the project was last updated.

### Bank Account

A Bank Account represents a financial account associated with a tenant or project. It is used to manage and track financial transactions, balances, and other related information within the system.

**Attributes:**
- `id`: Unique identifier for the bank account.
- `tenant_id`: Identifier of the tenant the bank account belongs to.
- `project_id`: Identifier of the project the bank account belongs to (if applicable).
- `account_number`: Bank account number.
- `bank_name`: Name of the bank.
- `created_at`: Timestamp when the bank account was created.
- `updated_at`: Timestamp when the bank account was last updated.

### Credit or Debit Card

A Credit or Debit Card represents a payment card associated with a tenant or project. It is used to manage and track financial transactions, payments, and other related information within the system.

**Attributes:**
- `id`: Unique identifier for the credit or debit card.
- `tenant_id`: Identifier of the tenant the card belongs to.
- `project_id`: Identifier of the project the card belongs to (if applicable).
- `card_number`: Card number.
- `cardholder_name`: Name of the cardholder.
- `expiration_date`: Expiration date of the card.
- `created_at`: Timestamp when the card was created.
- `updated_at`: Timestamp when the card was last updated.

### Transaction

A Transaction represents a financial operation involving a bank account. It records details such as the amount, date, type (e.g., income, expense), and associated entities within the system.

**Attributes:**
- `id`: Unique identifier for the transaction.
- `bank_account_id`: Identifier of the bank account associated with the transaction.
- `amount`: Amount of the transaction.
- `date`: Date of the transaction.
- `type`: Type of the transaction (e.g., income, expense).
- `created_at`: Timestamp when the transaction was created.
- `updated_at`: Timestamp when the transaction was last updated.

### Invoice or Ticket

An Invoice or Ticket represents a billing or payment document associated with a tenant or project. It records details such as the amount, date, due date, and associated transactions within the system.

**Attributes:**
- `id`: Unique identifier for the invoice or ticket.
- `tenant_id`: Identifier of the tenant the invoice or ticket belongs to.
- `project_id`: Identifier of the project the invoice or ticket belongs to (if applicable).
- `amount`: Amount of the invoice or ticket.
- `date`: Date of the invoice or ticket.
- `due_date`: Due date of the invoice or ticket.
- `created_at`: Timestamp when the invoice or ticket was created.
- `updated_at`: Timestamp when the invoice or ticket was last updated.

### Payment

A Payment represents a financial transaction related to an invoice or ticket. It records details such as the amount, date, payment method, and associated entities within the system.

**Attributes:**
- `id`: Unique identifier for the payment.
- `invoice_or_ticket_id`: Identifier of the invoice or ticket associated with the payment.
- `amount`: Amount of the payment.
- `date`: Date of the payment.
- `payment_method`: Method of the payment (e.g., bank transfer, credit card).
- `created_at`: Timestamp when the payment was created.
- `updated_at`: Timestamp when the payment was last updated.

### Budget

A Budget represents a financial plan associated with a tenant or project. It records details such as the allocated amount, time period, and associated entities within the system.

**Attributes:**
- `id`: Unique identifier for the budget.
- `tenant_id`: Identifier of the tenant the budget belongs to.
- `project_id`: Identifier of the project the budget belongs to (if applicable).
- `allocated_amount`: Allocated amount for the budget.
- `start_date`: Start date of the budget period.
- `end_date`: End date of the budget period.
- `created_at`: Timestamp when the budget was created.
- `updated_at`: Timestamp when the budget was last updated.

### Report

A Report represents a summary or analysis of financial data within the system. It is associated with a tenant or project and provides insights into financial performance, trends, and other relevant metrics.

**Attributes:**
- `id`: Unique identifier for the report.
- `tenant_id`: Identifier of the tenant the report belongs to.
- `project_id`: Identifier of the project the report belongs to (if applicable).
- `title`: Title of the report.
- `content`: Content or body of the report.
- `created_at`: Timestamp when the report was created.
- `updated_at`: Timestamp when the report was last updated.

### Expense

An Expense represents a financial outflow associated with a tenant or project. It records details such as the amount, date, category, and associated entities within the system. It can be recurring or one-time, providing a way to track and manage expenditures effectively.

**Attributes:**
- `id`: Unique identifier for the expense.
- `tenant_id`: Identifier of the tenant the expense belongs to.
- `project_id`: Identifier of the project the expense belongs to (if applicable).
- `amount`: Amount of the expense.
- `date`: Date of the expense.
- `category`: Category of the expense (e.g., utilities, salaries).
- `recurring`: Indicates if the expense is recurring.
- `recurring_interval`: Specifies the interval at which the expense recurs (e.g., monthly, yearly).
- `created_at`: Timestamp when the expense was created.
- `updated_at`: Timestamp when the expense was last updated.

### Revenue
 
A Revenue represents a financial inflow associated with a tenant or project. It records details such as the amount, date, category, and associated entities within the system. It can be recurring or one-time, providing a way to track and manage income effectively.

**Attributes:**
- `id`: Unique identifier for the revenue.
- `tenant_id`: Identifier of the tenant the revenue belongs to.
- `project_id`: Identifier of the project the revenue belongs to (if applicable).
- `amount`: Amount of the revenue.
- `date`: Date of the revenue.
- `category`: Category of the revenue (e.g., sales, investments).
- `recurring`: Indicates if the revenue is recurring.
- `recurring_interval`: Specifies the interval at which the revenue recurs (e.g., monthly, yearly).
- `created_at`: Timestamp when the revenue was created.
- `updated_at`: Timestamp when the revenue was last updated.

### Plan

A Plan represents a financial strategy or allocation associated with a tenant or project. It outlines the intended distribution of resources, budgetary goals, and financial targets within the system. It represents expected financial activities and helps in guiding decision-making and resource management.

**Attributes:**
- `id`: Unique identifier for the plan.
- `tenant_id`: Identifier of the tenant the plan belongs to.
- `project_id`: Identifier of the project the plan belongs to (if applicable).
- `title`: Title of the plan.
- `description`: Description or details of the plan.
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
- A Project belongs to a Tenant and can have multiple Users collaborating on it.
- A Bank Account belongs to a Tenant or Project and is used to manage financial transactions.
- A Credit or Debit Card belongs to a Tenant or Project and is used to manage financial transactions.
- A Transaction is associated with a Bank Account and records financial operations.
- An Invoice or Ticket belongs to a Tenant or Project and is associated with Transactions.
- A Payment is related to an Invoice or Ticket and records financial transactions.
- A Budget belongs to a Tenant or Project and represents a financial plan.
- A Report belongs to a Tenant or Project and provides financial analysis.
- An Expense belongs to a Tenant or Project and records financial outflows.
- A Revenue belongs to a Tenant or Project and records financial inflows.
- A Plan belongs to a Tenant or Project and can have multiple Planned Revenues and Planned Expenses associated with it, representing a financial strategy or forecast.
- A Planned Revenue belongs to a Tenant or Project and is associated with a Plan, representing anticipated financial inflows.
- A Planned Expense belongs to a Tenant or Project and is associated with a Plan, representing anticipated financial outflows.