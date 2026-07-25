using System.Security;
using System.Text.Encodings.Web;
using System.Text.Json;
using EtlAnalytics.RulesEngine.Interfaces;
using EtlAnalytics.RulesEngine.Models;
using Microsoft.Extensions.Configuration;

namespace EtlAnalytics.RulesEngine.Executors;

internal class TsqlRuleExecutor : IRuleExecutor
{
    private readonly IBusinessRuleStore _ruleStore;
    private readonly ISqlRuleExecutor _sqlExecutor;
    private readonly IEncryptionService _encryptionService;
    private readonly string _connectionString;
    private readonly string[] _forbiddenKeywords;
    private readonly int _sqlTimeoutSeconds;

    public string RuleType => RuleConstants.TSQL;

    public TsqlRuleExecutor(
        IConfiguration configuration,
        IBusinessRuleStore ruleStore,
        ISqlRuleExecutor sqlExecutor,
        IEncryptionService encryptionService,
        string connectionString,
        string[] forbiddenKeywords,
        int sqlTimeoutSeconds)
    {
        _ruleStore = ruleStore;
        _sqlExecutor = sqlExecutor;
        _encryptionService = encryptionService;
        _connectionString = connectionString;
        _forbiddenKeywords = forbiddenKeywords;
        _sqlTimeoutSeconds = sqlTimeoutSeconds;
    }

    public async Task<object?> ExecuteAsync(BusinessRule rule, RuleExecutionContext context, Type contextType, Action<string>? appendLog)
    {
        string connectionString = _connectionString;
        string providerType = "SqlServer"; // Default fallback

        if (rule.ConnectionId.HasValue)
        {
            var dbConn = await _ruleStore.GetDbConnectionByIdAsync(rule.ConnectionId.Value);
            if (dbConn != null)
            {
                appendLog?.Invoke($"[SQL] Using specific connection: {dbConn.Name} ({dbConn.ProviderType})");

                string decryptedConn;
                try
                {
                    decryptedConn = _encryptionService.Decrypt(dbConn.ConnectionString);
                }
                catch (Exception ex)
                {
                    appendLog?.Invoke($"[WARN] Failed to decrypt connection string for {dbConn.Name}. Error: {ex.Message}. Attempting to use as-is.");
                    decryptedConn = dbConn.ConnectionString;
                }

                connectionString = decryptedConn;
                providerType = dbConn.ProviderType;
            }
            else
            {
                appendLog?.Invoke($"[WARN] Connection ID {rule.ConnectionId} not found. Falling back to default connection.");
            }
        }

        appendLog?.Invoke("[SQL] Executing T-SQL script...");

        // Basic Query Validation
        var upperCode = rule.Code.ToUpperInvariant();
        foreach (var keyword in _forbiddenKeywords)
        {
            if (upperCode.Contains(keyword.ToUpperInvariant()))
            {
                appendLog?.Invoke($"[SECURITY ALERT] T-SQL rule '{rule.Name}' contains forbidden keyword: {keyword}. Execution blocked.");
                throw new SecurityException($"Forbidden SQL keyword detected: {keyword}");
            }
        }

        var parameters = new Dictionary<string, object>();
        var jsonOptions = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        string previousJson = context != null ? JsonSerializer.Serialize(context.PreviousResult, jsonOptions) : "[]";
        string stepResultsJson = context != null ? JsonSerializer.Serialize(context.StepResults, jsonOptions) : "{}";

        parameters.Add("PreviousResultJson", previousJson);
        parameters.Add("StepResultsJson", stepResultsJson);

        var results = await _sqlExecutor.ExecuteAsync(
            rule.Code,
            parameters,
            connectionString,
            providerType,
            _sqlTimeoutSeconds,
            context?.CancellationToken ?? CancellationToken.None);

        var resultList = results.ToList();

        appendLog?.Invoke($"[SQL] Execution completed. {resultList.Count} rows returned.");
        return resultList;
    }
}
