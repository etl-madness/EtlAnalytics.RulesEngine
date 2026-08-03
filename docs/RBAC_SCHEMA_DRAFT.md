# RBAC and ACL Schema Draft (SQL Server, PostgreSQL, MySQL)

This draft provides additive schema guidance for application-side authorization while remaining compatible with EtlAnalytics.RulesEngine.

Important:
- Application owns these policy tables and writes.
- Package consumes authorization outcomes through application services.
- Scripts are examples and should be adapted to existing naming conventions.

---

## 1. Core Tables

### 1.1 Roles
- RoleId
- RoleName
- Description
- IsActive
- CreatedAtUtc
- CreatedBy
- ModifiedAtUtc
- ModifiedBy

### 1.2 RolePermissions
- RolePermissionId
- RoleId
- ResourceType
- Action
- IsAllowed
- CreatedAtUtc
- CreatedBy

### 1.3 UserRoles
- UserRoleId
- UserId
- RoleId
- IsActive
- CreatedAtUtc
- CreatedBy

### 1.4 GroupRoles
- GroupRoleId
- GroupId
- RoleId
- IsActive
- CreatedAtUtc
- CreatedBy

### 1.5 ResourceAcls
- ResourceAclId
- ResourceType
- ResourceId
- PrincipalType (User, Group, Role)
- PrincipalId
- Action
- Effect (Allow, Deny)
- Reason
- IsActive
- CreatedAtUtc
- CreatedBy
- ModifiedAtUtc
- ModifiedBy

### 1.6 AuthorizationDecisionAudit
- DecisionId
- CorrelationId
- PrincipalId
- PrincipalType
- ResourceType
- ResourceId
- Action
- Decision
- DecisionSource
- EvaluatedAtUtc
- MetadataJson

---

## 2. Entity Audit Field Additions

Recommended additive columns for application-managed data tables:

### BusinessRules
- CreatedBy
- CreatedByName
- ModifiedBy
- ModifiedByName
- ModifiedAtUtc
- OwnerUserId
- OwnershipRevoked

### BusinessRuleBundles
- CreatedBy
- CreatedByName
- ModifiedBy
- ModifiedByName
- ModifiedAtUtc
- OwnerUserId
- OwnershipRevoked

### DbConnections
- CreatedBy
- CreatedByName
- ModifiedBy
- ModifiedByName
- ModifiedAtUtc
- OwnerUserId
- OwnershipRevoked

### Execution Tables
- ExecutedBy
- ExecutedByName
- AuthMethod
- DecisionCorrelationId

---

## 3. SQL Server Draft DDL

```sql
CREATE TABLE dbo.Roles (
    RoleId INT IDENTITY(1,1) PRIMARY KEY,
    RoleName NVARCHAR(150) NOT NULL UNIQUE,
    Description NVARCHAR(500) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(255) NULL,
    ModifiedAtUtc DATETIME2 NULL,
    ModifiedBy NVARCHAR(255) NULL
);

CREATE TABLE dbo.RolePermissions (
    RolePermissionId INT IDENTITY(1,1) PRIMARY KEY,
    RoleId INT NOT NULL,
    ResourceType NVARCHAR(100) NOT NULL,
    Action NVARCHAR(50) NOT NULL,
    IsAllowed BIT NOT NULL,
    CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(255) NULL,
    CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId)
);

CREATE TABLE dbo.UserRoles (
    UserRoleId INT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(255) NOT NULL,
    RoleId INT NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(255) NULL,
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId)
);

CREATE TABLE dbo.GroupRoles (
    GroupRoleId INT IDENTITY(1,1) PRIMARY KEY,
    GroupId NVARCHAR(255) NOT NULL,
    RoleId INT NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(255) NULL,
    CONSTRAINT FK_GroupRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId)
);

CREATE TABLE dbo.ResourceAcls (
    ResourceAclId BIGINT IDENTITY(1,1) PRIMARY KEY,
    ResourceType NVARCHAR(100) NOT NULL,
    ResourceId NVARCHAR(255) NOT NULL,
    PrincipalType NVARCHAR(20) NOT NULL,
    PrincipalId NVARCHAR(255) NOT NULL,
    Action NVARCHAR(50) NOT NULL,
    Effect NVARCHAR(10) NOT NULL,
    Reason NVARCHAR(500) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(255) NULL,
    ModifiedAtUtc DATETIME2 NULL,
    ModifiedBy NVARCHAR(255) NULL
);

CREATE TABLE dbo.AuthorizationDecisionAudit (
    DecisionId BIGINT IDENTITY(1,1) PRIMARY KEY,
    CorrelationId UNIQUEIDENTIFIER NOT NULL,
    PrincipalId NVARCHAR(255) NOT NULL,
    PrincipalType NVARCHAR(20) NOT NULL,
    ResourceType NVARCHAR(100) NOT NULL,
    ResourceId NVARCHAR(255) NOT NULL,
    Action NVARCHAR(50) NOT NULL,
    Decision NVARCHAR(10) NOT NULL,
    DecisionSource NVARCHAR(50) NOT NULL,
    EvaluatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    MetadataJson NVARCHAR(MAX) NULL
);

CREATE INDEX IX_RolePermissions_Role_Resource_Action ON dbo.RolePermissions(RoleId, ResourceType, Action);
CREATE INDEX IX_UserRoles_UserId ON dbo.UserRoles(UserId);
CREATE INDEX IX_GroupRoles_GroupId ON dbo.GroupRoles(GroupId);
CREATE INDEX IX_ResourceAcls_Resource ON dbo.ResourceAcls(ResourceType, ResourceId, Action);
CREATE INDEX IX_ResourceAcls_Principal ON dbo.ResourceAcls(PrincipalType, PrincipalId, Action);
CREATE INDEX IX_AuthorizationDecisionAudit_Principal_Time ON dbo.AuthorizationDecisionAudit(PrincipalId, EvaluatedAtUtc);
```

---

## 4. PostgreSQL Draft DDL

```sql
CREATE TABLE IF NOT EXISTS roles (
    role_id SERIAL PRIMARY KEY,
    role_name VARCHAR(150) NOT NULL UNIQUE,
    description VARCHAR(500),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by VARCHAR(255),
    modified_at_utc TIMESTAMP,
    modified_by VARCHAR(255)
);

CREATE TABLE IF NOT EXISTS role_permissions (
    role_permission_id SERIAL PRIMARY KEY,
    role_id INT NOT NULL REFERENCES roles(role_id),
    resource_type VARCHAR(100) NOT NULL,
    action VARCHAR(50) NOT NULL,
    is_allowed BOOLEAN NOT NULL,
    created_at_utc TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by VARCHAR(255)
);

CREATE TABLE IF NOT EXISTS user_roles (
    user_role_id SERIAL PRIMARY KEY,
    user_id VARCHAR(255) NOT NULL,
    role_id INT NOT NULL REFERENCES roles(role_id),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by VARCHAR(255)
);

CREATE TABLE IF NOT EXISTS group_roles (
    group_role_id SERIAL PRIMARY KEY,
    group_id VARCHAR(255) NOT NULL,
    role_id INT NOT NULL REFERENCES roles(role_id),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by VARCHAR(255)
);

CREATE TABLE IF NOT EXISTS resource_acls (
    resource_acl_id BIGSERIAL PRIMARY KEY,
    resource_type VARCHAR(100) NOT NULL,
    resource_id VARCHAR(255) NOT NULL,
    principal_type VARCHAR(20) NOT NULL,
    principal_id VARCHAR(255) NOT NULL,
    action VARCHAR(50) NOT NULL,
    effect VARCHAR(10) NOT NULL,
    reason VARCHAR(500),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at_utc TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by VARCHAR(255),
    modified_at_utc TIMESTAMP,
    modified_by VARCHAR(255)
);

CREATE TABLE IF NOT EXISTS authorization_decision_audit (
    decision_id BIGSERIAL PRIMARY KEY,
    correlation_id UUID NOT NULL,
    principal_id VARCHAR(255) NOT NULL,
    principal_type VARCHAR(20) NOT NULL,
    resource_type VARCHAR(100) NOT NULL,
    resource_id VARCHAR(255) NOT NULL,
    action VARCHAR(50) NOT NULL,
    decision VARCHAR(10) NOT NULL,
    decision_source VARCHAR(50) NOT NULL,
    evaluated_at_utc TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    metadata_json TEXT
);

CREATE INDEX IF NOT EXISTS ix_role_permissions_role_resource_action ON role_permissions(role_id, resource_type, action);
CREATE INDEX IF NOT EXISTS ix_user_roles_user_id ON user_roles(user_id);
CREATE INDEX IF NOT EXISTS ix_group_roles_group_id ON group_roles(group_id);
CREATE INDEX IF NOT EXISTS ix_resource_acls_resource ON resource_acls(resource_type, resource_id, action);
CREATE INDEX IF NOT EXISTS ix_resource_acls_principal ON resource_acls(principal_type, principal_id, action);
CREATE INDEX IF NOT EXISTS ix_auth_decision_principal_time ON authorization_decision_audit(principal_id, evaluated_at_utc);
```

---

## 5. MySQL Draft DDL

```sql
CREATE TABLE IF NOT EXISTS Roles (
    RoleId INT AUTO_INCREMENT PRIMARY KEY,
    RoleName VARCHAR(150) NOT NULL UNIQUE,
    Description VARCHAR(500) NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CreatedBy VARCHAR(255) NULL,
    ModifiedAtUtc DATETIME NULL,
    ModifiedBy VARCHAR(255) NULL
);

CREATE TABLE IF NOT EXISTS RolePermissions (
    RolePermissionId INT AUTO_INCREMENT PRIMARY KEY,
    RoleId INT NOT NULL,
    ResourceType VARCHAR(100) NOT NULL,
    ActionName VARCHAR(50) NOT NULL,
    IsAllowed TINYINT(1) NOT NULL,
    CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CreatedBy VARCHAR(255) NULL,
    CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);

CREATE TABLE IF NOT EXISTS UserRoles (
    UserRoleId INT AUTO_INCREMENT PRIMARY KEY,
    UserId VARCHAR(255) NOT NULL,
    RoleId INT NOT NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CreatedBy VARCHAR(255) NULL,
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);

CREATE TABLE IF NOT EXISTS GroupRoles (
    GroupRoleId INT AUTO_INCREMENT PRIMARY KEY,
    GroupId VARCHAR(255) NOT NULL,
    RoleId INT NOT NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CreatedBy VARCHAR(255) NULL,
    CONSTRAINT FK_GroupRoles_Roles FOREIGN KEY (RoleId) REFERENCES Roles(RoleId)
);

CREATE TABLE IF NOT EXISTS ResourceAcls (
    ResourceAclId BIGINT AUTO_INCREMENT PRIMARY KEY,
    ResourceType VARCHAR(100) NOT NULL,
    ResourceId VARCHAR(255) NOT NULL,
    PrincipalType VARCHAR(20) NOT NULL,
    PrincipalId VARCHAR(255) NOT NULL,
    ActionName VARCHAR(50) NOT NULL,
    EffectType VARCHAR(10) NOT NULL,
    Reason VARCHAR(500) NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    CreatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CreatedBy VARCHAR(255) NULL,
    ModifiedAtUtc DATETIME NULL,
    ModifiedBy VARCHAR(255) NULL
);

CREATE TABLE IF NOT EXISTS AuthorizationDecisionAudit (
    DecisionId BIGINT AUTO_INCREMENT PRIMARY KEY,
    CorrelationId CHAR(36) NOT NULL,
    PrincipalId VARCHAR(255) NOT NULL,
    PrincipalType VARCHAR(20) NOT NULL,
    ResourceType VARCHAR(100) NOT NULL,
    ResourceId VARCHAR(255) NOT NULL,
    ActionName VARCHAR(50) NOT NULL,
    DecisionType VARCHAR(10) NOT NULL,
    DecisionSource VARCHAR(50) NOT NULL,
    EvaluatedAtUtc DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    MetadataJson LONGTEXT NULL
);

CREATE INDEX IX_RolePermissions_Role_Resource_Action ON RolePermissions(RoleId, ResourceType, ActionName);
CREATE INDEX IX_UserRoles_UserId ON UserRoles(UserId);
CREATE INDEX IX_GroupRoles_GroupId ON GroupRoles(GroupId);
CREATE INDEX IX_ResourceAcls_Resource ON ResourceAcls(ResourceType, ResourceId, ActionName);
CREATE INDEX IX_ResourceAcls_Principal ON ResourceAcls(PrincipalType, PrincipalId, ActionName);
CREATE INDEX IX_AuthDecision_Principal_Time ON AuthorizationDecisionAudit(PrincipalId, EvaluatedAtUtc);
```

---

## 6. Migration Order

1. Create RBAC and ACL tables.
2. Backfill roles and assignments.
3. Add ownership and actor audit columns to existing entity and execution tables.
4. Enable observe mode decision logging.
5. Enable enforce mode once policy outputs are validated.

---

## 7. Rollback-Safe Notes

- Keep schema additions additive and nullable when possible.
- Avoid dropping legacy columns in first rollout.
- Keep both old and new policy checks behind a feature flag during transition.

---

## 8. Related Documents

- docs/RBAC.md
- docs/PERSISTENT_EXECUTION_TRACKING.md
- docs/SCHEMA_UPGRADE.md
