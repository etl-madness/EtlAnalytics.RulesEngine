# Asynchronous Business Rule Bundle Execution & Granular Status Tracking

`EtlAnalytics.RulesEngine` provides built-in, thread-safe execution tracking for business rule bundles. This feature enables hosts (such as Web APIs, background workers, or CLI services) to trigger long-running rule bundles asynchronously, track step-by-step sequence and parallel rule progress (`Pending` $\rightarrow$ `Starting` $\rightarrow$ `Completed` / `Failed`), and query progress without blocking client requests.

---

## Key Features

- **Granular Status Lifecycle**: Every sequence group and rule item transitions through well-defined lifecycle states: `Pending`, `Starting` (`InProgress`), `Completed`, `Failed`, or `Skipped`.
- **Parallel Step Awareness**: Multi-rule sequence groups executing via `Task.WhenAll` update individual rule states concurrently in a thread-safe manner.
- **Pre-populated Execution Tree**: Initializing a bundle tracking run pre-populates all sequences and rule items as `Pending`, making progress calculations predictable (e.g. `% completed`).
- **Real-Time Event Observer**: Subscribe to `OnStatusChanged` events to receive immediate notifications when state transitions occur.
- **Host Agnostic**: Works cleanly across ASP.NET Core Web APIs, Azure Functions, Worker Services, or Console applications.

---

## 1. Registering the Tracker in Dependency Injection

Use the `AddBusinessRulesEngineTracking()` extension method in your application startup:

```csharp
using EtlAnalytics.RulesEngine;

var builder = WebApplication.CreateBuilder(args);

// Register Business Rules Engine execution tracking service
builder.Services.AddBusinessRulesEngineTracking();
```

This registers `IBundleExecutionTracker` with the thread-safe `InMemoryBundleExecutionTracker` implementation as a Singleton.

---

## 2. Triggering Non-Blocking Asynchronous Execution in an API

To allow callers to initiate bundle execution without blocking for completion:

```csharp
[ApiController]
[Route("api/rules")]
public class RulesController : ControllerBase
{
    private readonly BusinessRuleEngine<MyExecutionContext> _engine;
    private readonly IBundleExecutionTracker _tracker;
    private readonly IBusinessRuleStore _ruleStore;

    public RulesController(
        BusinessRuleEngine<MyExecutionContext> engine,
        IBundleExecutionTracker tracker,
        IBusinessRuleStore ruleStore)
    {
        _engine = engine;
        _tracker = tracker;
        _ruleStore = ruleStore;
    }

    [HttpPost("bundle/execute-async")]
    public async Task<IActionResult> ExecuteBundleAsync([FromQuery] string name)
    {
        var bundle = await _ruleStore.GetBusinessRuleBundleByNameAsync(name);
        if (bundle == null) return NotFound($"Bundle '{name}' not found.");

        // 1. Pre-initialize execution tree with all steps set to 'Pending'
        var executionState = await _tracker.CreateExecutionAsync(bundle);
        Guid executionId = executionState.ExecutionId;

        // 2. Fire-and-forget background execution
        _ = Task.Run(async () =>
        {
            var context = new MyExecutionContext();
            await _engine.ExecuteBundleAsync(
                bundle,
                context,
                appendLog: null,
                tracker: _tracker,
                executionId: executionId);
        });

        // 3. Immediately return 202 Accepted with status URL
        return Accepted(new
        {
            ExecutionId = executionId,
            BundleName = bundle.Name,
            Status = executionState.Status.ToString(),
            StatusUrl = $"/api/rules/bundle/status/{executionId}"
        });
    }

    [HttpGet("bundle/status/{executionId:guid}")]
    public async Task<IActionResult> GetStatus(Guid executionId)
    {
        var state = await _tracker.GetExecutionAsync(executionId);
        if (state == null) return NotFound($"Execution ID '{executionId}' not found.");

        return Ok(state);
    }
}
```

---

## 3. Subscribing to Real-Time Event Notifications

You can observe execution state changes in real-time by attaching a handler to `OnStatusChanged`:

```csharp
tracker.OnStatusChanged += (sender, args) =>
{
    Console.WriteLine($"[{args.Status}] Execution {args.ExecutionId} | Sequence {args.SequenceOrder} | Rule '{args.RuleName}': {args.Message}");
};
```

---

## 4. Execution State DTO Schema

Calling `GetExecutionAsync(executionId)` returns a `BundleExecutionState` object containing complete execution details:

```json
{
  "executionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "bundleId": 10,
  "bundleName": "Daily Security Audit",
  "status": "Starting",
  "startTime": "2026-07-30T11:25:00Z",
  "endTime": null,
  "sequences": [
    {
      "sequenceOrder": 1,
      "status": "Completed",
      "rules": [
        {
          "ruleId": 101,
          "ruleName": "Fetch Logs",
          "ruleType": "TSQL",
          "status": "Completed"
        }
      ]
    },
    {
      "sequenceOrder": 2,
      "status": "Starting",
      "rules": [
        {
          "ruleId": 102,
          "ruleName": "Filter Threat IPs",
          "ruleType": "CSharp",
          "status": "Starting"
        },
        {
          "ruleId": 103,
          "ruleName": "Scan CVE Database",
          "ruleType": "CSharp",
          "status": "Starting"
        }
      ]
    }
  ]
}
```

---

## 5. Targeting Specific Rules in Parallel Execution

When rules share the same `SequenceOrder`, they run concurrently in parallel, and their results are aggregated into a `List<object?>`. Here are the three ways to identify and target specific rules within a parallel group.

### Method 1: Target by Rule Name or Rule ID via `IBundleExecutionTracker` (Recommended for API/UI Status)

When querying execution state from `IBundleExecutionTracker` or an API endpoint:

```csharp
var state = await tracker.GetExecutionAsync(executionId);

// Find the parallel sequence group (e.g. SequenceOrder #2)
var sequence2 = state?.Sequences.FirstOrDefault(s => s.SequenceOrder == 2);

// Target specific rule by Rule ID (e.g. Rule ID 102)
var rule102 = sequence2?.Rules.FirstOrDefault(r => r.RuleId == 102);
Console.WriteLine($"Rule 102 Status: {rule102?.Status}, Result: {rule102?.Result}");

// Target specific rule by Rule Name (e.g. 'Filter Threat IPs')
var threatRule = sequence2?.Rules.FirstOrDefault(r => r.RuleName == "Filter Threat IPs");
Console.WriteLine($"Filter Threat IPs Status: {threatRule?.Status}, Result: {threatRule?.Result}");
```

### Method 2: Position-Based Indexing in Downstream Rules (`PreviousResult` / `StepResults`)

In downstream rules, `PreviousResult` or `StepResults[sequenceOrder]` holds the `List<object?>` of results in the order defined by `bundle.Items`:

```csharp
// Inside a downstream C# rule:
var parallelResults = (List<object?>)PreviousResult;

// Index [0] matches the 1st configured item in SequenceOrder 2
var filterThreatsData = parallelResults[0];

// Index [1] matches the 2nd configured item in SequenceOrder 2
var scanCveData = parallelResults[1];
```

### Method 3: Self-Describing Named Dictionary / Anonymous Wrappers

Rules in a parallel group can return named dictionary wrappers so downstream steps can target them dynamically without relying on array positions:

**Parallel Rule A (FilterThreats):**
```csharp
return new Dictionary<string, object?> {
    { "RuleKey", "FilterThreats" },
    { "Data", threatList }
};
```

**Parallel Rule B (ScanCVEs):**
```csharp
return new Dictionary<string, object?> {
    { "RuleKey", "ScanCVEs" },
    { "Data", cveList }
};
```

**Downstream Rule (Sequence 3):**
```csharp
var parallelResults = (List<IDictionary<string, object?>>)PreviousResult;

var threatData = parallelResults.FirstOrDefault(r => r["RuleKey"]?.ToString() == "FilterThreats")?["Data"];
var cveData = parallelResults.FirstOrDefault(r => r["RuleKey"]?.ToString() == "ScanCVEs")?["Data"];
```

