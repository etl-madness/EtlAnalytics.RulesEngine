namespace EtlAnalytics.RulesEngine.Models;

/// <summary>
/// Specifies the type of logic contained within a business rule.
/// </summary>
public enum RuleType
{
    /// <summary>Transact-SQL query.</summary>
    TSQL,
    /// <summary>C# script.</summary>
    CSharp
}
