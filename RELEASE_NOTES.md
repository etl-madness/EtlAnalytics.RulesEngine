# Release Notes - EtlAnalytics.RulesEngine

## [2.4.0] - 2026-08-01

### 🚀 New Features
- **Authorization Architecture Guidance (App/Package Split)**:
    - Added a formal guidance model where policy authority remains in consuming applications and the package provides reusable enforcement hooks.
    - Defined Hybrid RBAC + Group + per-resource ACL evaluation order with explicit deny precedence.
- **Optional Authorization Enforcement Hooks (Code)**:
    - Added `IRuleAuthorizationService` contract for host-provided policy checks.
    - Added optional authorization callback support in `BusinessRuleEngine` for resource-level checks (`Bundle`, `Rule`, `Connection`).
    - Added DI extension helpers for registering default or custom authorization services.
    - Added configuration-driven fail-closed mode using `RulesEngine:Authorization:FailClosed` (aliases: `RulesEngine:Authorization:RequirePolicyService`, `RulesEngine:RequireAuthorizationService`).

- **Execution Actor Metadata (Code)**:
    - Added `ExecutionActorContext` and `AuthorizationRequest` models.
    - Extended execution tracking state to include actor identity and decision correlation metadata.
    - Added additive lifecycle audit properties to core rule, bundle, connection, and history models.

### 🛠️ Improvements
- **New RBAC Processing Guide**: Added [RBAC.md](docs/RBAC.md) documenting processing flow, ownership semantics, and integration patterns for API, worker, and admin surfaces.
- **New Multi-Database Schema Draft**: Added [RBAC_SCHEMA_DRAFT.md](docs/RBAC_SCHEMA_DRAFT.md) with additive SQL Server, PostgreSQL, and MySQL schema examples for roles, permissions, group mappings, ACLs, and decision auditing.
- **Documentation Harmonization**: Updated README and all existing docs markdown guides to consistently reference the application-side authorization authority model and actor-level auditing.
- **Persistent Tracking Documentation**: Updated [PERSISTENT_EXECUTION_TRACKING.md](docs/PERSISTENT_EXECUTION_TRACKING.md) schema and sample code to include actor metadata columns and mapping (`ExecutedBy`, `ExecutedByName`, `ActorType`, `AuthMethod`, `DecisionCorrelationId`).
    - Added an idempotent migration appendix (`ALTER TABLE`) for SQL Server, PostgreSQL, and MySQL to retrofit existing `BundleExecutionLogs` tables.
- **Secure Defaults Guidance**: Added a production hardening checklist in [DEVELOPERS_GUIDE.md](docs/DEVELOPERS_GUIDE.md) covering fail-closed authorization, actor metadata, and operational verification.
- **Automated Validation**: Added authorization integration tests covering deny behavior and actor context propagation through tracker state.
    - Added tests covering fail-closed behavior with and without registered authorization providers.
    - Added tests confirming authorization callback delegate precedence over registered `IRuleAuthorizationService` implementations.

### ⚠️ Backward Compatibility
- **Backwards-Compatible Additions**: Existing runtime APIs remain supported. New authorization and actor-context functionality is optional and additive.

## [2.3.0] - 2026-07-31

### 🚀 New Features
- **Multi-Category & Multi-Tag Support**:
    - **Domain Models**: Added `Categories` (`List<string>`) and `Tags` (`List<string>`) properties to `BusinessRule`, `BusinessRuleBundle`, and `DbConnectionDefinition`.
    - **Execution Tracking Integration**: Updated `RuleExecutionState` and `BundleExecutionState` to capture and propagate `Categories` and `Tags` into execution state snapshots.
    - **Store Search & Filtering**: Added default interface search contracts to `IBusinessRuleStore`:
        - `GetRulesByCategoryAsync(category)`
        - `GetRulesByTagAsync(tag)`
        - `GetBundlesByCategoryAsync(category)`
        - `GetBundlesByTagAsync(tag)`
    - **Full CRUD Persistence & Auto-Migrations**:
        - Updated `SqlDatabaseService` with non-breaking `ALTER TABLE` auto-migration statements adding `Categories NVARCHAR(MAX) NULL` and `Tags NVARCHAR(MAX) NULL` columns if missing.
        - `INSERT` and `UPDATE` operations serialize `Categories` and `Tags` to JSON arrays.
        - `SELECT` queries deserialize JSON arrays back into `List<string>`.

### 🛠️ Improvements
- **Schema Upgrade Guide**: Created [SCHEMA_UPGRADE.md](docs/SCHEMA_UPGRADE.md) containing idempotent SQL migration scripts for SQL Server, PostgreSQL, and MySQL.
- **Documentation**: Updated `DEVELOPERS_GUIDE.md` with Section 7 detailing Categories and Tags model usage, database persistence strategies, and search methods.
- **Automated Tests**: Added `CategoryAndTagTests` unit test suite covering multi-category/tag modeling, execution tracker state propagation, and store search methods.

### ⚠️ Backward Compatibility
- **100% Backward Compatible**: Existing queries and model instantiations remain fully functional. Missing database columns are automatically created or safely default to empty lists (`[]`). For manual database migrations, see the [Schema Upgrade Guide](docs/SCHEMA_UPGRADE.md).

---

## [2.2.0] - 2026-07-30

### 🚀 New Features
- **Asynchronous Execution & Granular Status Tracking**:
    - **Sequence & Rule Level Status Lifecycle**: Every sequence group and rule item (including parallel execution steps) transitions through granular status states: `Pending`, `Starting` (`InProgress`), `Completed`, `Failed`, or `Skipped`.
    - **Thread-Safe Execution Tracker**: Introduced `IBundleExecutionTracker` and `InMemoryBundleExecutionTracker` to maintain real-time execution state snapshots.
    - **Pre-Populated Execution Tree**: `tracker.CreateExecutionAsync` initializes all sequences and parallel rules as `Pending` prior to execution start, enabling accurate progress calculations (e.g. `% completed`).
    - **Real-Time Progress Events**: Added `OnStatusChanged` event on `IBundleExecutionTracker` for live status change notifications.
    - **Dependency Injection Extension**: Added `services.AddBusinessRulesEngineTracking()` extension method for easy registration.

### 🛠️ Improvements
- **Public Rule Executors**: Promoted `CSharpRuleExecutor` and `TsqlRuleExecutor` to `public` protection level to allow custom DI registration and direct host referencing.
- **Architectural Diagrams & Documentation**:
    - Added [ARCHITECTURE_OVERVIEW.md](docs/ARCHITECTURE_OVERVIEW.md) featuring Mermaid diagrams for system architecture, sequence orchestration, async tracking flow, ER schema, and security sandboxing.
    - Added [PERSISTENT_EXECUTION_TRACKING.md](docs/PERSISTENT_EXECUTION_TRACKING.md) guide with SQL Server, PostgreSQL, and MySQL table schemas, Dapper `SqlBundleExecutionTracker` implementation, DI setup, and cleanup scripts.
    - Added [EXECUTION_TRACKING.md](docs/EXECUTION_TRACKING.md) guide and updated [README.md](README.md) with non-blocking API patterns and parallel rule targeting examples (`[0]` vs `[1]`, rule lookup by name or ID).
- **Automated Tests**: Added `ExecutionTrackingTests` suite covering pre-population, sequential and parallel status transitions, and failure state handling.

### ⚠️ Backward Compatibility
- **100% Backward Compatible**: Existing `ExecuteBundleAsync` method signatures remain intact. All state tracking parameters are optional.

---

## [2.1.0] - 2026-07-27

### 🚀 New Features
- **Parallel Execution Support**: You can now execute multiple rules within a bundle concurrently. 
    - Rules sharing the same `SequenceOrder` are automatically grouped into a "Sequence Group".
    - Groups with multiple items are executed in parallel using `Task.WhenAll`.
    - The engine synchronizes at the end of each sequence group before proceeding to the next.
- **Result Aggregation**: 
    - When a step is executed in parallel, its results are aggregated into a `List<object?>`.
    - Downstream rules receive this list in the `PreviousResult` property of the context.
    - Historical results in `StepResults[sequenceOrder]` also store the aggregated list for parallel groups.

### 🛠️ Improvements
- **Enhanced Documentation**: Full update to all `docs/` files, including a new High-Performance Data Enrichment use case.
- **Unit Tests**: Added comprehensive tests for parallel orchestration and error handling.

### ⚠️ Backward Compatibility
- **100% Backward Compatible**: Existing bundles with unique `SequenceOrder` values will continue to execute sequentially as before.
- **No Schema Changes Required**: The feature utilizes the existing `SequenceOrder` column. No database migrations are necessary.

### ⬆️ Upgrading
1. Update your project reference to version **2.1.0**.
2. **Result Handling Note**: If you modify an existing sequential bundle to be parallel, ensure that the rules *following* the parallel group are updated to handle a `List<object?>` in their `PreviousResult` instead of a single object.
3. No changes are required to the `IBusinessRuleStore` or `IRuleExecutor` implementations.

---

## [2.0.2] - Previous Release
- Initial multi-targeting support for .NET 8 and .NET 10.
- Decoupled SQL execution logic from core engine.
- Added support for cross-database rules via `ConnectionId`.
- Hardened C# and SQL security sandboxes.
