# Release Notes

## v2.3.0

This release focuses on richer rule metadata and clearer governance guidance for applications that embed `EtlAnalytics.RulesEngine`.

### Highlights

- Added Category and Tag metadata support across core rule-engine entities, including rules, bundles, connections, and execution-tracking records.
- Expanded the governance story around RBAC, ACLs, and group-based authorization so the consuming application can own policy decisions while the package provides enforcement hooks.
- Clarified the recommended authorization flow: explicit deny ACL, explicit allow ACL, RBAC grants, owner fallback, then default deny.
- Updated the documentation trail for schema upgrades and authorization planning, including the RBAC schema draft and the v2.3.0 schema upgrade guide.
- Refreshed the DataForge showcase description to highlight the reference application for governed ETL and analytics workflows, including bundle management, execution history, and roles/permissions administration.

### Category and Tag Details

- Categories and Tags are stored as JSON array-style metadata on supported entities.
- The fields are nullable so existing databases can adopt them without breaking current records.
- The metadata is intended to improve discoverability, organization, and lifecycle management of rules and related assets.

### RBAC and ACL Details

- Authorization is intentionally application-owned and provider-agnostic.
- The package exposes the hooks needed to integrate a host policy engine for CRUD and execution checks.
- RBAC, group-role mappings, ACL entries, and decision auditing are documented as the preferred governance model for consuming applications.

### Documentation Updates

- `docs/SCHEMA_UPGRADE.md` now tracks the v2.3.0 Categories/Tags upgrade path and points to the RBAC schema draft for authorization planning.
- `docs/RBAC.md` documents the recommended authorization evaluation order and ownership semantics.
- `docs/RBAC_SCHEMA_DRAFT.md` outlines the additive tables and audit fields for application-side authorization.
