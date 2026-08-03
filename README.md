# EtlAnalytics.RulesEngine

`EtlAnalytics.RulesEngine` is a reusable business rules package for teams that need to centralize, secure, and operationalize decision logic across ETL and analytics workflows.

It helps organizations reduce hard-coded logic in pipelines, improve governance, and accelerate change delivery by separating rules from application code.

## Business Value

- Improves agility by allowing business logic updates without full pipeline rewrites.
- Strengthens governance through rule authorization, execution controls, and traceability.
- Reduces risk by standardizing rule execution behavior across teams and data products.
- Supports compliance and audit requirements with execution history and role-based access patterns.

## What This Package Enables

- Rule-driven orchestration for analytics and ETL scenarios.
- Multi-executor support (for example C# and T-SQL rule execution paths).
- Rule bundle management for grouping and sequencing logic.
- Execution tracking (in-memory and persistent patterns).
- Security capabilities such as authorization services and encryption utilities.

## Typical Business Scenarios

- Data quality enforcement before loading curated datasets.
- Dynamic enrichment or transformation logic that changes by product line or market.
- Policy-driven controls for high-impact operations (such as customer segmentation or pricing inputs).
- Governed execution where only approved actors can run or modify specific rule sets.
- Complex Dataflow and ETL orchestration where multiple rules must be executed in a specific order, with some rules running in parallel.

## Showcase OpenSource Application (DataForge)

The DataForge application demonstrates how to use this package to implement a governed ETL and analytics workflow. It is a reference implementation that shows how to integrate the rules engine into a real-world scenario.

In addition to the rules engine, DataForge includes a web-based UI for managing rule bundles, executing rules, and tracking execution history. This version also includes a management system for user roles and permissions, allowing organizations to enforce access controls on rule execution and modification.

- [DataForge](https://github.com/etl-madness/DataForge)

## Documentation Index

The following documents provide functional, technical, and operational guidance:

- [AI Implementation Guide](docs/ai_implementation_guide.md)
- [Architecture Overview](docs/ARCHITECTURE_OVERVIEW.md)
- [Business Rules](docs/BUSINESS_RULES.md)
- [Business Use Cases](docs/BUSINESS_USE_CASES.md)
- [Developers Guide](docs/DEVELOPERS_GUIDE.md)
- [Example](docs/Example.md)
- [Example Simple](docs/ExampleSimple.md)
- [Example Simple XML](docs/ExampleSimpleXML.md)
- [Execution Tracking](docs/EXECUTION_TRACKING.md)
- [Forbidden Keywords Modification](docs/forbidden_keywords_modification.md)
- [Persistent Execution Tracking](docs/PERSISTENT_EXECUTION_TRACKING.md)
- [RBAC](docs/RBAC.md)
- [RBAC Schema Draft](docs/RBAC_SCHEMA_DRAFT.md)
- [Schema Upgrade](docs/SCHEMA_UPGRADE.md)

## Audience

- Product owners and business analysts defining rule outcomes.
- Data platform teams implementing governed ETL and analytics processes.
- Engineering teams integrating reusable rule execution into enterprise systems.

## Versioning and Packaging

This repository builds a distributable .NET package that can be versioned and consumed by internal services and pipelines. Refer to solution and project metadata for current target frameworks and package versions.
