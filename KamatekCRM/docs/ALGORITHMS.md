# KamatekCRM Core Algorithms

This document explains the mathematical and logical algorithms powering the system's business intelligence.

## 1. SLA & Maintenance Automation

The `SlaService` runs as a background task to automate maintenance scheduling based on active contracts.

### Logic Flow:
1.  **Scan**: Retrieve all `MaintenanceContract` records where `IsActive = true` and `NextDueDate <= Today`.
2.  **Generate**: For each match, create a new `ServiceJob` with `WorkOrderType.Maintenance`.
3.  **Recalculate**: Update the `NextDueDate` using the contract's `FrequencyInMonths`.

### Algorithm (Next Date Calculation):
```csharp
var nextDate = contract.NextDueDate.AddMonths(contract.FrequencyInMonths);
if (nextDate < today) 
{
    // Ensure we don't schedule in the past if a contract was missed
    nextDate = today.AddMonths(contract.FrequencyInMonths);
}
```

---

## 2. Inventory Valuation (Weighted Average Cost - WAC)

To provide accurate financial reporting, the system uses the **Weighted Average Cost (WAC)** algorithm for stock entry.

### Formula:
$$NewAverageCost = \frac{(CurrentQty \times CurrentCost) + (InboundQty \times InboundCost)}{CurrentQty + InboundQty}$$

### Implementation Detail:
When a purchase occurs, the `InventoryDomainService` recalculates the average cost for that specific `ProductId` in the target `WarehouseId` before updating the total quantity. This ensures the inventory value is always current and accurately reflected in the financial balance sheet.

---

## 3. Pagination Algorithm

Large datasets are handled using a standardized `PagedResult<T>` structure to minimize memory footprint and network traffic.

### Structure:
*   **Items**: The current page of data.
*   **PageSize**: Number of records per page.
*   **CurrentPage**: The current index (1-based).
*   **TotalCount**: Total number of records in the database matching the criteria.
*   **PageCount**: `TotalCount / PageSize` (rounded up).

---

## 4. Soft Delete Filter Algorithm

EF Core applies a global expression tree modifier to every query targeting `ISoftDeletable` entities. This ensures that `Deleted` records are transparently excluded from all `SELECT` queries without requiring manual `Where` clauses in the repository layer.
