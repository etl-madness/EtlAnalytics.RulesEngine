# Release Notes - EtlAnalytics.RulesEngine

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
- **Developer Documentation**: Added [EXECUTION_TRACKING.md](docs/EXECUTION_TRACKING.md) guide and updated [README.md](README.md) with non-blocking API patterns and JSON state schemas.
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
