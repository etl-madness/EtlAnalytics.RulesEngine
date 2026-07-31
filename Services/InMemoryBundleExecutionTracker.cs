using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EtlAnalytics.RulesEngine.Interfaces;
using EtlAnalytics.RulesEngine.Models;

namespace EtlAnalytics.RulesEngine.Services;

/// <summary>
/// An in-memory, thread-safe implementation of <see cref="IBundleExecutionTracker"/> for tracking real-time execution states.
/// </summary>
public class InMemoryBundleExecutionTracker : IBundleExecutionTracker
{
    private readonly ConcurrentDictionary<Guid, BundleExecutionState> _executions = new();

    /// <inheritdoc />
    public event EventHandler<BundleProgressEventArgs>? OnStatusChanged;

    /// <inheritdoc />
    public Task<BundleExecutionState> CreateExecutionAsync(BusinessRuleBundle bundle, Guid? executionId = null)
    {
        var id = executionId ?? Guid.NewGuid();

        var state = new BundleExecutionState
        {
            ExecutionId = id,
            BundleId = bundle.Id,
            BundleName = bundle.Name,
            Categories = new List<string>(bundle.Categories),
            Tags = new List<string>(bundle.Tags),
            Status = ExecutionStatus.Pending,
            StartTime = null,
            EndTime = null
        };

        var groupedItems = bundle.Items
            .GroupBy(i => i.SequenceOrder)
            .OrderBy(g => g.Key);

        foreach (var group in groupedItems)
        {
            var seqState = new SequenceExecutionState
            {
                SequenceOrder = group.Key,
                Status = ExecutionStatus.Pending,
                StartTime = null,
                EndTime = null,
                Message = $"Step sequence {group.Key} pending execution"
            };

            foreach (var item in group)
            {
                seqState.Rules.Add(new RuleExecutionState
                {
                    RuleId = item.RuleId,
                    RuleName = !string.IsNullOrWhiteSpace(item.RuleName) ? item.RuleName : $"Rule #{item.RuleId}",
                    RuleType = item.RuleType ?? "Unknown",
                    SequenceOrder = group.Key,
                    Status = ExecutionStatus.Pending,
                    StartTime = null,
                    EndTime = null
                });
            }

            state.Sequences.Add(seqState);
        }

        _executions[id] = state;

        RaiseStatusChanged(new BundleProgressEventArgs(
            id,
            bundle.Name,
            ExecutionStatus.Pending,
            $"Bundle '{bundle.Name}' execution initialized and pending",
            CloneState(state)));

        return Task.FromResult(CloneState(state));
    }

    /// <inheritdoc />
    public Task UpdateBundleStatusAsync(Guid executionId, ExecutionStatus status, string? message = null)
    {
        if (!_executions.TryGetValue(executionId, out var state))
        {
            return Task.CompletedTask;
        }

        lock (state)
        {
            state.Status = status;
            if (status == ExecutionStatus.Starting && state.StartTime == null)
            {
                state.StartTime = DateTime.UtcNow;
            }
            if (message != null)
            {
                state.Logs.Add($"[{DateTime.UtcNow:HH:mm:ss}] [BUNDLE] [{status}] {message}");
            }
        }

        RaiseStatusChanged(new BundleProgressEventArgs(
            executionId,
            state.BundleName,
            status,
            message ?? $"Bundle status updated to {status}",
            CloneState(state)));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateSequenceStatusAsync(Guid executionId, int sequenceOrder, ExecutionStatus status, string? message = null)
    {
        if (!_executions.TryGetValue(executionId, out var state))
        {
            return Task.CompletedTask;
        }

        lock (state)
        {
            var sequence = state.Sequences.FirstOrDefault(s => s.SequenceOrder == sequenceOrder);
            if (sequence != null)
            {
                sequence.Status = status;
                if (status == ExecutionStatus.Starting && sequence.StartTime == null)
                {
                    sequence.StartTime = DateTime.UtcNow;
                }
                else if (status == ExecutionStatus.Completed || status == ExecutionStatus.Failed || status == ExecutionStatus.Skipped)
                {
                    sequence.EndTime = DateTime.UtcNow;
                }

                if (!string.IsNullOrWhiteSpace(message))
                {
                    sequence.Message = message;
                }
            }

            // Keep parent bundle status updated if starting
            if (status == ExecutionStatus.Starting && state.Status == ExecutionStatus.Pending)
            {
                state.Status = ExecutionStatus.Starting;
                state.StartTime ??= DateTime.UtcNow;
            }

            state.Logs.Add($"[{DateTime.UtcNow:HH:mm:ss}] [SEQUENCE #{sequenceOrder}] [{status}] {message ?? status.ToString()}");
        }

        RaiseStatusChanged(new BundleProgressEventArgs(
            executionId,
            state.BundleName,
            status,
            message ?? $"Sequence #{sequenceOrder} status updated to {status}",
            CloneState(state),
            sequenceOrder: sequenceOrder));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateRuleStatusAsync(Guid executionId, int ruleId, int sequenceOrder, ExecutionStatus status, object? result = null, Exception? error = null)
    {
        if (!_executions.TryGetValue(executionId, out var state))
        {
            return Task.CompletedTask;
        }

        string ruleName = $"Rule #{ruleId}";

        lock (state)
        {
            var sequence = state.Sequences.FirstOrDefault(s => s.SequenceOrder == sequenceOrder);
            if (sequence != null)
            {
                var rule = sequence.Rules.FirstOrDefault(r => r.RuleId == ruleId);
                if (rule != null)
                {
                    ruleName = rule.RuleName;
                    rule.Status = status;

                    if (status == ExecutionStatus.Starting && rule.StartTime == null)
                    {
                        rule.StartTime = DateTime.UtcNow;
                    }
                    else if (status == ExecutionStatus.Completed || status == ExecutionStatus.Failed || status == ExecutionStatus.Skipped)
                    {
                        rule.EndTime = DateTime.UtcNow;
                    }

                    if (result != null)
                    {
                        rule.Result = result;
                    }

                    if (error != null)
                    {
                        rule.ErrorMessage = error.Message;
                    }
                }
            }

            string logDetail = error != null ? $"Error: {error.Message}" : (status.ToString());
            state.Logs.Add($"[{DateTime.UtcNow:HH:mm:ss}] [RULE #{ruleId} ({ruleName})] [{status}] {logDetail}");
        }

        RaiseStatusChanged(new BundleProgressEventArgs(
            executionId,
            state.BundleName,
            status,
            $"Rule #{ruleId} ('{ruleName}') status updated to {status}",
            CloneState(state),
            sequenceOrder: sequenceOrder,
            ruleId: ruleId,
            ruleName: ruleName));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CompleteExecutionAsync(Guid executionId, ExecutionStatus status, object? finalResult = null, Exception? error = null)
    {
        if (!_executions.TryGetValue(executionId, out var state))
        {
            return Task.CompletedTask;
        }

        lock (state)
        {
            state.Status = status;
            state.EndTime = DateTime.UtcNow;
            state.FinalResult = finalResult;

            if (error != null)
            {
                state.ErrorMessage = error.Message;
            }

            // Mark any remaining pending sequences or rules as skipped
            foreach (var seq in state.Sequences)
            {
                if (seq.Status == ExecutionStatus.Pending)
                {
                    seq.Status = ExecutionStatus.Skipped;
                    seq.EndTime ??= DateTime.UtcNow;
                    seq.Message = "Skipped due to bundle termination or error";
                }

                foreach (var rule in seq.Rules)
                {
                    if (rule.Status == ExecutionStatus.Pending)
                    {
                        rule.Status = ExecutionStatus.Skipped;
                        rule.EndTime ??= DateTime.UtcNow;
                    }
                }
            }

            state.Logs.Add($"[{DateTime.UtcNow:HH:mm:ss}] [BUNDLE] [FINISHED] Status: {status}");
        }

        RaiseStatusChanged(new BundleProgressEventArgs(
            executionId,
            state.BundleName,
            status,
            $"Bundle execution completed with status '{status}'",
            CloneState(state)));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<BundleExecutionState?> GetExecutionAsync(Guid executionId)
    {
        if (_executions.TryGetValue(executionId, out var state))
        {
            lock (state)
            {
                return Task.FromResult<BundleExecutionState?>(CloneState(state));
            }
        }

        return Task.FromResult<BundleExecutionState?>(null);
    }

    /// <inheritdoc />
    public Task AppendLogAsync(Guid executionId, string logMessage)
    {
        if (_executions.TryGetValue(executionId, out var state))
        {
            lock (state)
            {
                state.Logs.Add($"[{DateTime.UtcNow:HH:mm:ss}] {logMessage}");
            }
        }

        return Task.CompletedTask;
    }

    private void RaiseStatusChanged(BundleProgressEventArgs args)
    {
        OnStatusChanged?.Invoke(this, args);
    }

    private static BundleExecutionState CloneState(BundleExecutionState original)
    {
        return new BundleExecutionState
        {
            ExecutionId = original.ExecutionId,
            BundleId = original.BundleId,
            BundleName = original.BundleName,
            Categories = new List<string>(original.Categories),
            Tags = new List<string>(original.Tags),
            Status = original.Status,
            StartTime = original.StartTime,
            EndTime = original.EndTime,
            FinalResult = original.FinalResult,
            ErrorMessage = original.ErrorMessage,
            Logs = new List<string>(original.Logs),
            Sequences = original.Sequences.Select(s => new SequenceExecutionState
            {
                SequenceOrder = s.SequenceOrder,
                Status = s.Status,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Message = s.Message,
                Rules = s.Rules.Select(r => new RuleExecutionState
                {
                    RuleId = r.RuleId,
                    RuleName = r.RuleName,
                    RuleType = r.RuleType,
                    SequenceOrder = r.SequenceOrder,
                    Categories = new List<string>(r.Categories),
                    Tags = new List<string>(r.Tags),
                    Status = r.Status,
                    StartTime = r.StartTime,
                    EndTime = r.EndTime,
                    Result = r.Result,
                    ErrorMessage = r.ErrorMessage
                }).ToList()
            }).ToList()
        };
    }
}
