# Business Use Cases - EtlAnalytics.RulesEngine

This document outlines key real-world business use cases and architectural patterns solved by `EtlAnalytics.RulesEngine`. By decoupling business logic from application code, organizations can dynamically alter system behavior, enforce security policies, and update business rules without code changes or redeployments.

## Authorization Governance Use Case

In regulated environments, applications commonly apply Hybrid RBAC + Group + ACL policies to control who can create, edit, delete, and execute rules and bundles.

Recommended pattern:
- Use application authentication and claims mapping for identity.
- Use role and group grants for baseline access.
- Use per-resource ACL entries for exceptions.
- Keep explicit deny precedence to reduce risk.

See `RBAC.md` and `RBAC_SCHEMA_DRAFT.md` for implementation details.

---

## 1. Zero-Day Exploit & Vulnerability Hot-Remediation 🛡️

### Problem Statement
When a new **Zero-Day vulnerability** or malicious payload pattern is discovered in production, conventional patch management requires building, testing, and deploying updated code binaries across environments. In high-stakes enterprise environments, CI/CD pipeline deployments can take hours or days—leaving systems vulnerable to active exploitation.

### Solution
With `EtlAnalytics.RulesEngine`, security and engineering teams can instantly publish C# or T-SQL remediation rules into the rule store. The application evaluates these rules during request processing without requiring application restarts or code redeployments.

### Workflow Example

#### A. In-Line Payload Interception
1. **Threat Detected**: Security monitoring detects a Zero-Day vulnerability targeting an application endpoint.
2. **Dynamic Rule Deployment**: A remediation C# rule is published to the `IBusinessRuleStore` with high priority.
3. **Hot Enforcement**: The application runs the rule, intercepting incoming payloads and aborting suspicious execution flows immediately.

```csharp
// Example Zero-Day In-Line Interception Rule (C# Script)
string payload = PreviousResult as string ?? string.Empty;

// Check for known Zero-Day exploit signature / payload pattern
if (payload.Contains("${jndi:") || payload.Contains("eval(base64_decode"))
{
    // Log security incident and block request immediately
    return new { Blocked = true, Reason = "Zero-Day Exploit Remediation Triggered" };
}

return new { Blocked = false, Payload = payload };
```

#### B. Automated KEV / CVE Lookup & BigFix Remediation Rule Bundle
Beyond inline payload blocking, a multi-step **Rule Bundle** can query a vulnerability database to retrieve an active CVE, query an asset inventory database for impacted servers, verify CISA Known Exploited Vulnerabilities (KEV), and trigger an HCL BigFix remediation action via `HttpClient`.

##### Step 1: SQL Rule (Database 1 - Fetch Active CVE)
```sql
-- Query Threat Intelligence DB for top unpatched critical Zero-Day CVE
SELECT TOP 1 CveId, Severity, ThreatScore
FROM VulnerabilityDb.dbo.ActiveThreatAlerts
WHERE Status = 'CRITICAL' AND IsRemediated = 0
ORDER BY CreatedDate DESC;
```

##### Step 2: SQL Rule (Database 2 - Fetch Impacted Servers)
```sql
-- Query CMDB Asset Inventory DB using PreviousResultJson from Step 1
SELECT Hostname, IPAddress, Environment
FROM CmdbDb.dbo.ServerAssets
WHERE IsActive = 1 
  AND Environment = 'Production'
  AND InstalledPatches NOT LIKE '%' + JSON_VALUE(@PreviousResultJson, '$[0].CveId') + '%';
```

##### Step 3: C# Rule (SOAR Orchestration & BigFix Trigger)
```csharp
// Example Zero-Day KEV Verification & BigFix Orchestration Rule (C# Script)
// StepResults contains:
// StepResults[1] -> Result of Step 1 (CVE info)
// StepResults[2] -> Result of Step 2 (List of impacted servers)

var step1Result = StepResults.ContainsKey(1) ? StepResults[1] as dynamic : null;
var step2Result = StepResults.ContainsKey(2) ? StepResults[2] as System.Collections.IEnumerable : null;

// Extract CVE ID from Step 1 Database Query
string cveId = step1Result?[0]?.CveId ?? "CVE-2024-3094";

// Extract Server List from Step 2 Database Query
var impactedServers = new List<string>();
if (step2Result != null)
{
    foreach (dynamic item in step2Result)
    {
        if (item?.Hostname != null) impactedServers.Add((string)item.Hostname);
    }
}

if (impactedServers.Count == 0)
{
    return new { Status = "Complete", Message = $"No unpatched servers found for {cveId}." };
}

// 1. Fetch CISA Known Exploited Vulnerabilities (KEV) Feed
using var client = new HttpClient();
client.DefaultRequestHeaders.Add("User-Agent", "RulesEngine-SecurityAutomation/1.0");

string kevUrl = "https://www.cisa.gov/sites/default/files/feeds/known_exploited_vulnerabilities.json";
var kevResponse = await client.GetAsync(kevUrl);
if (!kevResponse.IsSuccessStatusCode)
{
    return new { Status = "Failed", Error = "Unable to reach CISA KEV feed." };
}

string jsonFeed = await kevResponse.Content.ReadAsStringAsync();
bool isExploitedInWild = jsonFeed.Contains(cveId);

if (!isExploitedInWild)
{
    return new { Status = "Skipped", Message = $"{cveId} not currently flagged in active KEV catalog." };
}

// 2. Resolve BigFix Remediation URI & Post Fixlet Action
string bigFixServer = "https://bigfix.internal:52311/api";
string fixletActionUri = $"{bigFixServer}/action/remediate/cve/{cveId}";

var actionPayload = new 
{ 
    CveId = cveId,
    Action = "ApplyEmergencyFixlet",
    Targets = impactedServers 
};

var requestMessage = new HttpRequestMessage(HttpMethod.Post, fixletActionUri)
{
    Content = new StringContent(
        System.Text.Json.JsonSerializer.Serialize(actionPayload),
        System.Text.Encoding.UTF8,
        "application/json")
};

 
  // Add BigFix API Authentication
     client.DefaultRequestHeaders.Authorization = 
         new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes("<username>:<password>")));

var bigFixResponse = await client.SendAsync(requestMessage);
string resultMessage = await bigFixResponse.Content.ReadAsStringAsync();

return new 
{ 
    Status = "Triggered", 
    CveId = cveId, 
    KevStatus = "Active Exploitation Verified", 
    ImpactedServerCount = impactedServers.Count, 
    ImpactedServers = impactedServers,
    BigFixResponseCode = (int)bigFixResponse.StatusCode,
    BigFixResult = resultMessage 
};
```

---

## 2. Real-Time Fraud Detection & Risk Scoring 💳

### Problem Statement
E-commerce and financial platforms face continuously changing fraud vectors. Static fraud thresholds baked into code binaries make it impossible to adapt quickly when fraudulent transaction surges occur during peak sales events.

### Solution
A rule bundle executes sequential SQL and C# validation steps:
1. **SQL Rule**: Queries historical transaction velocity and flags high-frequency buyers over the past hour.
2. **C# Rule**: Combines historical velocity with request metadata (IP geolocation, device fingerprint, transaction value) to compute a dynamic risk score.

```csharp
// C# Risk Calculation Step in a Rule Bundle
dynamic velocityData = PreviousResult;
int txCountLastHour = velocityData?.TxCount ?? 0;
double txAmount = ContextData.Amount;

if (txCountLastHour > 10 && txAmount > 500.00)
{
    return new { Action = "RequireMFA", RiskScore = 85 };
}

return new { Action = "Allow", RiskScore = 15 };
```

---

## 3. Data Quality & ETL Ingestion Pipelines 📊

### Problem Statement
ETL (Extract, Transform, Load) pipelines process data from heterogeneous third-party vendors with unpredictable formatting, missing fields, or invalid schemas. Hardcoding transformations for every vendor leads to brittle codebase maintenance.

### Solution
Data engineers register configurable business rules for data cleansing, validation, and transformation per vendor:
- **Null Value Imputation**: Replaces missing values with domain defaults.
- **Outlier Filtering**: Flags records exceeding standard deviation thresholds.
- **XML/JSON Schema Validation**: Parses and normalizes incoming XML/JSON documents dynamically.

---

## 4. Dynamic Pricing, Discounts & Tiered Promotions 🏷️

### Problem Statement
Marketing and sales operations frequently launch targeted campaigns, tier-based discounts, and custom enterprise pricing rules. Hardcoding complex conditional promotion trees bloats domain services.

### Solution
The engine evaluates pricing rule bundles where each rule applies cumulative discounts based on customer tier, cart total, order history, and active promo codes:
- **Rule 1 (Tier Discount)**: Applies 10% discount for Gold tier customers.
- **Rule 2 (Volume Discount)**: Applies extra 5% discount if cart total > $1,000.
- **Rule 3 (Bundle Promo)**: Adds complimentary support items for specific SKU combinations.

---

## 5. Regulatory Compliance & Data Masking (GDPR / HIPAA) 🔒

### Problem Statement
Data privacy legislation requires strict data handling based on user jurisdiction and consent status. Exporting or logging raw PII (Personally Identifiable Information) in non-compliant regions risks severe legal penalties.

### Solution
Compliance teams configure rule bundles that inspect data payloads before logging or storage:
- **PII Redaction**: Automatically masks SSNs, credit card numbers, and email addresses for users residing in GDPR jurisdictions.
- **Consent Gatekeeping**: Verifies user consent flags before allowing data downstream.

```csharp
// Dynamic PII Redaction Rule
string userRegion = ContextData.Region;
string rawEmail = ContextData.Email;

if (userRegion == "EU" || userRegion == "CA")
{
    // Redact email for compliance
    var parts = rawEmail.Split('@');
    string maskedEmail = parts[0][0] + "***@" + parts[1];
    return new { MaskedEmail = maskedEmail, Redacted = true };
}

return new { MaskedEmail = rawEmail, Redacted = false };
```

---

## 6. High-Performance Multi-Source Data Enrichment ⚡

### Problem Statement
In complex decision-making workflows, the engine often needs to gather data from multiple independent sources (e.g., internal databases, external APIs, and cache layers) before applying business logic. Executing these requests sequentially introduces unnecessary latency.

### Solution
By using **Parallel Execution** (assigning the same `SequenceOrder` to independent rules), the engine can fetch all required data concurrently. The final processing rule then receives an aggregated list of results, significantly reducing the total execution time of the bundle.

### Workflow Example
1.  **Rule 1 (SQL - Sequence 1)**: Fetch Customer Profile from Production DB.
2.  **Rule 2 (SQL - Sequence 1)**: Fetch Recent Orders from Audit DB.
3.  **Rule 3 (C# - Sequence 1)**: Fetch Credit Score from an external Web API.
4.  **Rule 4 (C# - Sequence 2)**: Processes the aggregated `List<object?>` containing the profile, orders, and credit score to decide on a loan approval.

---

## Summary of Benefits

| Benefit | Description |
| :--- | :--- |
| **Zero Downtime Updates** | Modify, enable, or disable business rules instantly without redeploying binaries. |
| **Instant Vulnerability Shielding** | Rapidly mitigate Zero-Day exploits and malicious traffic vectors at runtime. |
| **Auditability & Traceability** | Execution logging and step history provide complete transparency for compliance. |
| **Sandboxed Security** | Restricted C# assemblies and SQL keyword blacklisting prevent untrusted script execution. |
