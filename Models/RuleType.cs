namespace EtlAnalytics.RulesEngine.Models;

/// <summary>
/// Specifies the type of logic contained within a business rule.
/// </summary>
/// <summary>
/// Contains constants for well-known rule types.
/// </summary>
public static class RuleConstants
{
    /// <summary>Transact-SQL query.</summary>
    public const string TSQL = "TSQL";
    /// <summary>C# script.</summary>
    public const string CSharp = "CSharp";
}
