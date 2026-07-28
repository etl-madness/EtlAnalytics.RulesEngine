# Release Notes - EtlAnalytics.RulesEngine

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
