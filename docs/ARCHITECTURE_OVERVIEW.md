# Architecture Overview - EtlAnalytics.RulesEngine

`EtlAnalytics.RulesEngine` is a lightweight, database-agnostic, multi-targeted (.NET 8 & .NET 10) business rules engine supporting T-SQL, C# Scripting, and JavaScript.

This document provides a comprehensive architectural breakdown of the engine, its pluggable execution pipeline, sequence orchestration, asynchronous tracking framework, and security sandboxing.

---

## 1. System Architecture Diagram

The engine follows a modular, decoupled architecture where the core logic ("Brain") remains independent of concrete database providers ("Hands").

```mermaid
graph TD
    Client["Consuming App / Web API / Worker"]
    Engine["BusinessRuleEngine"]
    Store["IBundleExecutionTracker"]
    
    subgraph Core Library
        Engine
        Store
        Ctx["RuleExecutionContext"]
        ExecInterface["IRuleExecutor"]
        CsExec["CSharpRuleExecutor"]
        Tracker["InMemoryBundleExecutionTracker"]
    end

    subgraph Extension Packages
        DapperExec["DapperSqlRuleExecutor"]
        JsExec["JintJavascriptRuleExecutor"]
    end

    Client -->|Invokes| Engine
    Client -->|Registers| Tracker
    Engine -->|Loads Definitions| RuleStore["IBusinessRuleStore"]
    Engine -->|Delegates Step| ExecInterface
    ExecInterface <---|Implements| CsExec
    ExecInterface <---|Implements| DapperExec
    ExecInterface <---|Implements| JsExec
    Engine -->|Updates Status| Tracker
    Tracker -->|Emits Events| Client
```

---

## 2. Pluggable Package Hierarchy

To maintain zero unnecessary dependencies, the library is split into a core package and optional provider extensions:

| Package | Purpose | Dependencies |
| :--- | :--- | :--- |
| **`EtlAnalytics.RulesEngine`** | Core orchestrator, C# script executor, sandboxing, and in-memory/abstract status tracking. | `Microsoft.CodeAnalysis.CSharp.Scripting`, `Microsoft.Extensions.Configuration.Abstractions` |
| **`EtlAnalytics.RulesEngine.Dapper`** | Provides SQL Server, PostgreSQL, and MySQL T-SQL execution. | `Dapper`, `Microsoft.Data.SqlClient`, `Npgsql`, `MySqlData` |
| **`EtlAnalytics.RulesEngine.Javascript`** | Provides browser-like JavaScript rule execution. | `Jint` |

---

## 3. Bundle Orchestration & Parallel Execution Pipeline

When executing a `BusinessRuleBundle`, the engine groups items by `SequenceOrder`. Items sharing the same sequence number run concurrently in parallel via `Task.WhenAll`.

```mermaid
sequenceDiagram
    autonumber
    participant Host as Application / API
    participant Engine as BusinessRuleEngine
    participant Store as IBusinessRuleStore
    participant Tracker as IBundleExecutionTracker
    participant Executor as IRuleExecutor

    Host->>Tracker: CreateExecutionAsync(bundle)
    Tracker-->>Host: Returns ExecutionId (All steps "Pending")
    Host->>Engine: ExecuteBundleAsync(bundle, context, tracker, executionId)
    Engine->>Tracker: UpdateBundleStatusAsync(Starting)

    loop For Each SequenceGroup (Ordered by SequenceOrder)
        Engine->>Tracker: UpdateSequenceStatusAsync(Starting)
        
        alt Single Item Sequence
            Engine->>Store: GetBusinessRuleByIdAsync(RuleId)
            Engine->>Tracker: UpdateRuleStatusAsync(Starting)
            Engine->>Executor: ExecuteAsync(rule, context)
            Executor-->>Engine: Returns Rule Result
            Engine->>Tracker: UpdateRuleStatusAsync(Completed)
        else Parallel Multi-Item Sequence
            Engine->>Tracker: UpdateSequenceStatusAsync(Executing Parallel Rules)
            par Parallel Rule A
                Engine->>Executor: ExecuteAsync(Rule A)
                Executor-->>Engine: Returns Result A
                Engine->>Tracker: UpdateRuleStatusAsync(Rule A, Completed)
            and Parallel Rule B
                Engine->>Executor: ExecuteAsync(Rule B)
                Executor-->>Engine: Returns Result B
                Engine->>Tracker: UpdateRuleStatusAsync(Rule B, Completed)
            end
            Note over Engine: Aggregates [Result A, Result B] into List
        end

        Engine->>Tracker: UpdateSequenceStatusAsync(Completed)
    end

    Engine->>Tracker: CompleteExecutionAsync(Completed, FinalResult)
```

---

## 4. Asynchronous Execution & Real-Time Observer Pattern

To prevent long-running rule bundles from blocking API HTTP threads, execution tracking uses an asynchronous non-blocking observer model:

```mermaid
flowchart LR
    subgraph Client Layer
        Controller["Web API Controller"]
        Caller["API Caller / Frontend"]
    end

    subgraph Tracking Layer
        Tracker["IBundleExecutionTracker"]
        Event["OnStatusChanged Event"]
    end

    subgraph Execution Layer
        Worker["Task.Run / Background Worker"]
        Engine["BusinessRuleEngine"]
    end

    Caller -- "1. POST /api/rules/bundle/execute-async" --> Controller
    Controller -- "2. CreateExecutionAsync(bundle)" --> Tracker
    Controller -- "3. Enqueue / Task.Run" --> Worker
    Controller -- "4. Return 202 Accepted (ExecutionId)" --> Caller
    Worker -- "5. ExecuteBundleAsync(...)" --> Engine
    Engine -- "6. Status Transitions" --> Tracker
    Tracker -- "7. OnStatusChanged" --> Event
    Caller -- "8. GET /api/rules/status/{executionId}" --> Controller
    Controller -- "9. Snapshot Data" --> Tracker
```

---

## 5. Persistent Tracking Data Schema

For hosts using database persistence via `SqlBundleExecutionTracker`, the relational schema models parent bundle runs, sequence groups, and individual rules:

```mermaid
erDiagram
    BundleExecutionLogs ||--o{ SequenceExecutionLogs : "contains"
    BundleExecutionLogs ||--o{ RuleExecutionLogs : "contains"

    BundleExecutionLogs {
        Guid ExecutionId PK
        int BundleId
        string BundleName
        string Categories
        string Tags
        string Status
        datetime StartTime
        datetime EndTime
        string ErrorMessage
        string Logs
        datetime CreatedAt
    }

    SequenceExecutionLogs {
        int Id PK
        Guid ExecutionId FK
        int SequenceOrder
        string Status
        datetime StartTime
        datetime EndTime
        string Message
    }

    RuleExecutionLogs {
        int Id PK
        Guid ExecutionId FK
        int SequenceOrder
        int RuleId
        string RuleName
        string RuleType
        string Categories
        string Tags
        string Status
        datetime StartTime
        datetime EndTime
        string ErrorMessage
        string ResultJson
    }
```

---

## 6. Security & Sandboxing Architecture

To ensure execution safety when running dynamic user-defined logic, the engine enforces multi-layered sandboxing:

```mermaid
graph TD
    Rule["Incoming Rule Execution Request"]
    
    Rule --> CheckType{Rule Type?}
    
    CheckType -->|TSQL| SqlCheck["Keyword Filter Scan"]
    SqlCheck --> SqlValidation{"Contains Forbidden Keywords?"}
    SqlValidation -->|Yes| SqlBlock["Throw SecurityException & Block Run"]
    SqlValidation -->|No| SqlTimeout["Enforce 30s SQL Command Timeout"]
    SqlTimeout --> SqlExec["Execute via ISqlRuleExecutor"]

    CheckType -->|CSharp| CsCheck["Roslyn ScriptOptions Sandbox"]
    CsCheck --> CsWhitelist["Restrict Assembly References & Namespace Imports"]
    CsWhitelist --> CsTimeout["Enforce 10s CancellationToken Timeout"]
    CsTimeout --> CsExec["Evaluate via Roslyn Scripting Engine"]
```
