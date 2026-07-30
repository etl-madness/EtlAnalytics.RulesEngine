# Business Rules - EtlAnalytics.RulesEngine

This document outlines the core business rules and logic implemented within the `EtlAnalytics.RulesEngine` project.

## 1. Rule Execution Framework

### 1.1 Supported Rule Types
- **T-SQL**: SQL scripts executed against a database.
- **C#**: Dynamic C# scripts executed within a restricted runtime environment.
- **Javascript**: Logic scripts executed via the Jint engine (requires `EtlAnalytics.RulesEngine.Javascript` extension).

### 1.2 Execution Lifecycle
- Rules can be executed individually or as part of a **Bundle**.
- **Result Piping**: In a bundle, the output of each rule is passed as the `PreviousResult` to the subsequent rule.
- **Parallel Execution**: Rules sharing the same `SequenceOrder` within a bundle are executed concurrently. The engine waits for all rules in the group to complete before proceeding.
- **Result Aggregation**: Results from parallel rules are aggregated into a `List<object?>`.
- **State Management**: The execution context maintains a history of all step results within a bundle, indexed by their sequence order. For parallel groups, the value stored is the aggregated list of results.
- **Bundle Abort Policy**: If any step (sequential or parallel) within a bundle fails (throws an exception), the entire bundle execution is terminated immediately.

### 1.3 Asynchronous Execution & Real-Time Tracking
- **Granular Status Lifecycle**: Sequence groups and rule items transition through defined status states:
  - `Pending`: Pre-populated for all sequences and rule items prior to run start.
  - `Starting` / `InProgress`: Step is currently executing.
  - `Completed`: Step finished successfully.
  - `Failed`: Step encountered an exception during execution.
  - `Skipped`: Step bypassed due to a preceding failure or bundle termination.
- **Parallel Step Tracking**: Multi-rule sequence groups executing concurrently update individual rule statuses independently in a thread-safe manner.
- **State Store & Event Observer**: The `IBundleExecutionTracker` interface provides real-time state snapshots (`GetExecutionAsync`) and event callbacks (`OnStatusChanged`) for caller notifications.

## 2. SQL Rule Constraints & Security

### 2.1 Forbidden Keywords
To prevent unauthorized database modifications, SQL rules are scanned for forbidden keywords. Any rule containing these keywords is blocked from execution:
- `DROP`, `TRUNCATE`, `DELETE`, `UPDATE`, `INSERT`
- `GRANT`, `REVOKE`, `ALTER`, `CREATE`
- `xp_cmdshell`
- `sys.`, `information_schema`

### 2.2 Connection Management
- Rules can specify a target connection via `ConnectionId`.
- If no `ConnectionId` is provided, the engine falls back to the default system connection string.
- Connection strings retrieved from the database or configuration are expected to be encrypted.

### 2.3 SQL Parameters
Every SQL rule is automatically provided with the following JSON parameters:
- `PreviousResultJson`: The JSON-serialized result of the previous rule in the bundle.
- `StepResultsJson`: A JSON object containing results from all previous steps in the bundle.

### 2.4 SQL Timeouts
- The default timeout for SQL execution is **30 seconds**.

## 3. C# Scripting Constraints & Security

### 3.1 Restricted Sandbox
C# scripts are executed with a limited set of allowed assemblies and namespaces to ensure system stability:
- **Allowed Assemblies**: `mscorlib`, `System.Linq`, `System.Collections.Generic`, and the `EtlAnalytics.RulesEngine` core assembly.
- **Allowed Imports**: `System`, `System.Collections.Generic`, `System.Linq`, `System.Text`, `System.Threading.Tasks`, and `EtlAnalytics.RulesEngine.Models`.

### 3.2 C# Timeouts
- The default timeout for C# script execution is **10 seconds**.

## 4. Security & Encryption

### 4.1 Sensitive Data Protection
- All connection strings and potentially sensitive configuration values must be encrypted at rest.
- The engine uses **AES-256** encryption.
- Encryption keys are derived using **PBKDF2** with 100,000 iterations and a SHA256 hash.
- A fixed salt (`EtlAnalytics.Salt.RulesEngine`) is used for key derivation consistency across the library.

### 4.2 Key Management
- The encryption key is prioritized from the `DB_ENCRYPTION_KEY` environment variable, falling back to the `Security:EncryptionKey` app configuration setting.

## 5. Versioning and Metadata
- Each `BusinessRule` tracks its own version number (defaulting to 1).
- Rules track `CreatedAt` and `UpdatedAt` timestamps for auditability.
- Rules include an `IsActive` flag to allow for soft-disabling without deletion.

## 6. Usage Examples

### 6.1 Accessing Previous Step Data (C#)
In a C# rule, you can access the result of the immediately preceding rule using `PreviousResult`, or any specific step using the `StepResults` dictionary.

```csharp
// Example: Validate that the previous step returned at least 5 rows
var previousRows = (List<dynamic>)PreviousResult;
if (previousRows.Count < 5) {
    return "Failure: Insufficient data from previous step.";
}

// Example: Access data from the first step in the bundle (SequenceOrder 1)
var step1Data = StepResults[1];
return $"Processed {previousRows.Count} rows using configuration from step 1.";
```

### 6.2 Accessing Previous Step Data (T-SQL)
The syntax for accessing the `@PreviousResultJson` parameter varies depending on the target database provider.

| Database | Parameter Prefix | JSON Extraction Example |
| :--- | :---: | :--- |
| **SQL Server** | `@` | `CROSS APPLY OPENJSON(@PreviousResultJson) WITH (Status INT '$.Status')` |
| **PostgreSQL** | `:` | `SELECT * FROM table WHERE data ->> 'Status' = :PreviousResultJson` |
| **MySQL** | `?` | `SELECT * FROM table WHERE JSON_EXTRACT(?PreviousResultJson, '$.Status') = 1` |

#### **SQL Server Example**
```sql
-- Using OPENJSON to parse the previous result
SELECT TOP 1 * FROM Discounts 
CROSS APPLY OPENJSON(@PreviousResultJson) WITH (CustomerType NVARCHAR(50)) p
WHERE p.CustomerType = 'VIP';

-- Accessing specific historical steps from StepResultsJson
DECLARE @Step1Results NVARCHAR(MAX) = JSON_QUERY(@StepResultsJson, '$."1"');
SELECT * FROM OPENJSON(@Step1Results) WITH (ConfigValue INT '$.Value');
```

#### **PostgreSQL Example**
```sql
-- Using the native JSONB operators
SELECT * FROM "Discounts" 
WHERE "CustomerType" = CAST(:PreviousResultJson->>'CustomerType' AS TEXT)
LIMIT 1;

-- Accessing specific historical steps
SELECT * FROM "Configuration"
WHERE "Key" = CAST(:StepResultsJson->'1'->>'ConfigKey' AS TEXT);
```

#### **MySQL Example**
```sql
-- Using JSON_EXTRACT and JSON_UNQUOTE
SELECT * FROM Discounts 
WHERE CustomerType = JSON_UNQUOTE(JSON_EXTRACT(?PreviousResultJson, '$.CustomerType'))
LIMIT 1;

-- Accessing specific historical steps
SELECT * FROM Configuration
WHERE ConfigKey = JSON_UNQUOTE(JSON_EXTRACT(?StepResultsJson, '$."1".ConfigKey'));
```

### 6.3 Cross-Language Workflow (SQL to C#)
This example demonstrates a common pattern: fetching data from a database and then processing it using C# logic (e.g., calling an external API).

> [!NOTE]
> Since the C# sandbox blocks `System.Net.Http`, networking operations should be exposed via methods on your custom `RuleExecutionContext`.

#### **Step 1: SQL Rule (Fetch Data)**
```sql
-- Name: GetPendingNotifications
SELECT TOP 10 
    NotificationId, 
    TargetUrl, 
    MessagePayload 
FROM dbo.Outbox 
WHERE Processed = 0;
```

#### **Step 2: C# Rule (Process & Post)**
```csharp
// Name: ProcessOutbox
// PreviousResult contains the list of dynamic objects from the SQL rule
var notifications = (List<dynamic>)PreviousResult;
int successCount = 0;

foreach (var item in notifications) {
    // We assume 'HttpClientWrapper' is a property on your custom Context class
    var response = await HttpClientWrapper.PostAsync(item.TargetUrl, item.MessagePayload);
    
    if (response.IsSuccessStatusCode) {
        successCount++;
    }
}

return $"Successfully processed {successCount} out of {notifications.Count} notifications.";
```

### 6.4 Single Record Workflow (SQL to C#)
When a SQL rule is intended to return a single record, the C# rule should access the first element of the result collection.

#### **Step 1: SQL Rule (Fetch Single Record)**
```sql
-- Name: GetPriorityOrder
SELECT TOP 1 
    OrderId, 
    CustomerEmail, 
    PriorityLevel
FROM dbo.Orders 
WHERE Status = 'Pending' 
ORDER BY PriorityLevel DESC;
```

#### **Step 2: C# Rule (Process Single Item)**
```csharp
// Name: ProcessPriorityOrder
// SQL results are returned as a list; use Linq to get the single record
var order = ((List<dynamic>)PreviousResult).FirstOrDefault();

if (order == null) {
    return "No pending orders found.";
}

// Perform action with the single record
var response = await NotificationService.SendOrderAlertAsync(order.CustomerEmail, order.OrderId);

return $"Alert sent to {order.CustomerEmail} for Order #{order.OrderId}. Status: {response.Status}";
```

### 6.5 Multi-Step Integration Workflow (SQL + SQL -> C#)
This example shows how a final C# rule can combine data from multiple distinct SQL steps using the `StepResults` history.

#### **Step 1: SQL Rule (Sequence 1 - Get Customer)**
```sql
-- Name: GetCustomerInfo
SELECT TOP 1 Name, Email, LoyaltyTier FROM dbo.Customers WHERE CustomerId = @TargetId;
```

#### **Step 2: SQL Rule (Sequence 2 - Get Recent Order)**
```sql
-- Name: GetLastOrder
SELECT TOP 1 OrderId, TotalAmount FROM dbo.Orders WHERE CustomerId = @TargetId ORDER BY OrderDate DESC;
```

#### **Step 3: C# Rule (Sequence 3 - Combine & POST)**
```csharp
// Name: NotifyCustomerOfOrder
// Access results from specific steps using their sequence order
var customer = ((List<dynamic>)StepResults[1]).FirstOrDefault();
var lastOrder = ((List<dynamic>)StepResults[2]).FirstOrDefault();

if (customer == null || lastOrder == null) {
    return "Required data missing from previous steps. Aborting notification.";
}

// Combine data into a single payload
var payload = new {
    CustomerName = customer.Name,
    Email = customer.Email,
    Tier = customer.LoyaltyTier,
    OrderId = lastOrder.OrderId,
    Amount = lastOrder.TotalAmount
};

// Send combined data to a shipping/notification service
var response = await NotificationService.PostCombinedDataAsync("https://api.shipping.com/notify", payload);

return $"Notification sent to {customer.Name} for Order #{lastOrder.OrderId}. Response: {response.StatusCode}";
```

### 6.6 Complex Five-Rule Bundle (GET -> SQL -> SQL -> POST -> SQL)
This advanced example demonstrates a full lifecycle workflow: fetching remote config, querying multiple local tables, triggering a remote action, and logging the final outcome.

#### **Step 1: C# Rule (Sequence 1 - Get Threshold)**
```csharp
// Name: GetStockThreshold
// Fetch a configuration value from a remote service
var threshold = await ConfigService.GetFloatAsync("https://api.config.local/inventory/threshold");
return threshold; // e.g., 50.0
```

#### **Step 2: SQL Rule (Sequence 2 - Get Actual Stock)**
```sql
-- Name: GetCurrentStock
-- Use the threshold from Step 1 to find items below the limit
SELECT ItemId, Quantity 
FROM dbo.Inventory 
WHERE Quantity < CAST(JSON_VALUE(@StepResultsJson, '$."1"') AS FLOAT);
```

#### **Step 3: SQL Rule (Sequence 3 - Get Pending Orders)**
```sql
-- Name: GetPendingOrderCount
-- Also uses the value from Step 1 for some business logic
SELECT COUNT(*) as PendingCount 
FROM dbo.Orders 
WHERE Status = 'Pending' AND Priority > CAST(JSON_VALUE(@StepResultsJson, '$."1"') AS FLOAT);
```

#### **Step 4: C# Rule (Sequence 4 - Trigger Alert)**
```csharp
// Name: TriggerInventoryAlert
var lowStockItems = (List<dynamic>)StepResults[2];
var pendingCount = ((List<dynamic>)StepResults[3]).FirstOrDefault()?.PendingCount ?? 0;
var threshold = (float)StepResults[1];

if (lowStockItems.Count > 0) {
    var payload = new {
        ThresholdUsed = threshold,
        ItemCount = lowStockItems.Count,
        PendingOrders = pendingCount
    };
    // Post to an alerting system
    var alertResponse = await AlertService.PostAlertAsync("https://api.alerts.local/inventory", payload);
    return alertResponse.AlertId; // Return the generated Alert ID
}
return "NO_ALERT";
```

#### **Step 5: SQL Rule (Sequence 5 - Final Log)**
```sql
-- Name: LogWorkflowSummary
-- Accesses Step 1 threshold and Step 4 AlertId
-- NOTE: We use a Stored Procedure because the 'INSERT' keyword is blocked by the engine's security sandbox.
-- (Ensure 'EXEC' is whitelisted for the specific logging connection if necessary)

EXEC dbo.sp_LogRuleWorkflow 
    @Threshold = @Step1_Value, 
    @AlertId = @Step4_Value;

/* 
Inside the Proc:
CREATE PROCEDURE dbo.sp_LogRuleWorkflow @Threshold FLOAT, @AlertId NVARCHAR(100)
AS 
BEGIN
    INSERT INTO dbo.WorkflowLogs (Threshold, AlertId, LogDate) 
    VALUES (@Threshold, @AlertId, GETUTCDATE());
END
*/
```

> [!IMPORTANT]
> **Authorized Write Operations**: While the engine blocks direct `INSERT`/`UPDATE` keywords to prevent arbitrary data modification, using a Stored Procedure via `EXEC` is the recommended pattern for authorized write operations. This ensures that logic remains encapsulated within the database schema.
### 6.7 Parallel Execution & Result Aggregation (C#)
This example shows how to perform multiple independent tasks in parallel and then process their combined results in a final step.

#### **Step 1: SQL Rule (Sequence 1 - Fetch Products)**
```sql
SELECT ProductId, Name FROM Products WHERE Category = 'Electronics';
```

#### **Step 2: SQL Rule (Sequence 1 - Fetch Stock)**
```sql
SELECT ProductId, Quantity FROM Inventory WHERE WarehouseId = 10;
```

#### **Step 3: C# Rule (Sequence 2 - Join & Process)**
```csharp
// Name: ProcessParallelResults
// PreviousResult contains a List<object?> with results from Step 1 and Step 2.
// Positional Indexing: Index [0] corresponds to Step 1 ('Fetch Products'), Index [1] corresponds to Step 2 ('Fetch Stock')
var parallelResults = (List<object?>)PreviousResult;
var products = (List<dynamic>)parallelResults[0]; // 1st parallel rule in Sequence 1
var stock = (List<dynamic>)parallelResults[1];    // 2nd parallel rule in Sequence 1

Log($"Processing {products.Count} products with corresponding stock data.");

// Example join logic
foreach(var p in products) {
    var s = stock.FirstOrDefault(x => x.ProductId == p.ProductId);
    if (s != null && s.Quantity < 5) {
        await AlertService.TriggerLowStockAsync(p.Name, s.Quantity);
    }
}
return "Parallel processing complete.";
```

> [!TIP]
> **Targeting Parallel Rules by Name or ID**: When using `IBundleExecutionTracker`, you can also retrieve results directly by `RuleName` or `RuleId` instead of array index:
> ```csharp
> var state = await tracker.GetExecutionAsync(executionId);
> var seq1 = state.Sequences.First(s => s.SequenceOrder == 1);
> var products = seq1.Rules.First(r => r.RuleName == "Fetch Products").Result;
> var stock = seq1.Rules.First(r => r.RuleName == "Fetch Stock").Result;
> ```
