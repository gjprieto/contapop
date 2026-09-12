# New specifications after Task 3.7 completion

I found some new requirements and clarifications that emerged after completing Task 3.7. I provided the new specifications already divided into tasks.

## New Tasks & Specifications

- Task 3.7a: Change related document button:
    - On the invoice row there is a document icon representing the related invoice document.
    - Task 3.7j supersedes its original generated-PDF behavior: the icon opens the uploaded `invoice` attachment in another browser tab and is hidden when no invoice attachment exists.
- Task 3.7b: Filtering invoices:
    - Add a feature to filter the invoice list based on criteria such as status, date, or amount.
    - Use same filter format as you can find on Transaction list view.
    - Ensure that the filtered results are displayed correctly in the list view.
    - Provide a way to clear the filters and return to the full invoice list.
    - Implement any necessary backend support to handle filtering efficiently.
- Task 3.7c: Removing invoices:
    - Add a button to remove an invoice from the list.
    - The button to remove an invoice should only be visible when the invoice is not paid or attached to a payment.
    - The button should be placed in the same location as the "Issue", "Void" and other row buttons.
    - Add a confirmation dialog before removing an invoice to prevent accidental deletions.
    - Ensure that the removal of an invoice updates the list view and any related totals or summaries in the UI.
    - Update any related backend state or database records to reflect the removal of the invoice.
- Task 3.7d: Invoice details:
    - Add a button to view the details of an invoice.
    - The button should be labeled with the icon of an eye to indicate viewing details.
    - The button should be placed in the same location as the "Issue", "Void" and other row buttons for consistency.
    - Display all relevant details of an invoice, including line items, totals, and status. Details to be displayed in a dedicated details view.
    - Ensure that the invoice details view is updated when any changes occur (e.g., payment status, attached documents).
    - Provide a way to navigate back to the invoice list from the details view.
    - Implement any necessary backend support to fetch and update invoice details.
- Task 3.7e: Attaching document to invoice:
    - Add a button to attach a document (e.g., PDF, image) to an invoice.
    - The button should be labeled with an icon of a paperclip to indicate its purpose.
    - The button should be placed in the same location as the "Issue", "Void" and other row buttons for consistency.
    - Ensure that the attached document is displayed in the invoice details view.
    - Update any related backend state or database records to store the attachment.
    - Implement validation to restrict the types and sizes of files that can be attached.
    - Provide a way to remove or replace the attached document if needed. Task 3.7j supersedes the original single-attachment model with a typed attachment collection.
- Task 3.7h: Create and edit multi-line draft invoices:
    - Allow one or more item lines when creating an invoice.
    - Allow editing only while an invoice is a draft. Counterparty, direction, and type remain immutable; invoice date, due date, and item lines can change.
    - Provide an edit action from invoice details, a cancel path that persists nothing, and a save path that recalculates all line and invoice totals.
- Task 3.7j: Specify uploaded invoice documents and other attachments:
    - Stop generating invoice PDFs; registering or issuing an invoice does not produce a document.
    - Before a file is selected while no invoice document exists, ask whether it is an `Invoice` or `Other type of attachment`.
    - If an invoice document exists, the document-icon action opens that uploaded document; hide the action when no invoice document is attached.
    - Allow other attachments in addition to the one optional invoice document. Once an invoice document exists, later uploads are other attachments without another type choice. Removing the invoice document restores the type choice for the next upload.
