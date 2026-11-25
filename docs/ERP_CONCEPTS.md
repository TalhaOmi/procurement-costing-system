# ERP concepts and how this project maps to them

This project is a small imitation of ideas found in ERP platforms such as Acumatica. It does not run on or connect to any ERP product. The terms below come from Acumatica's public documentation.

| ERP term | What it means | Where it shows up here |
|---|---|---|
| **Customization project** | A package of changes (custom fields, forms, inquiries, business events, code) published to an instance without reinstalling, and movable between instances. | `docs/CHANGES.md` entry CHG-002 with `db/changes` and `db/rollback` scripts: a change you can apply and undo. |
| **Generic inquiry** | A no-code, configurable query over ERP data. | The four report SQL files in `src/Erp.Api/Sql`. |
| **Business event** | A rule that watches data and triggers a notification or action when a condition is met. | `BudgetThresholdCrossed`: the "project reached 90% of budget" email. |
| **Graph (business logic controller)** | The C# class behind a form that holds its business logic, actions and events. | `ProcurementService` plus the rules in `Erp.Core`. |
| **BQL (Business Query Language)** | Acumatica's compile-time-checked C# query language, translated to SQL. | The raw SQL in `/Sql` is the nearest analogue. |
| **DAC (data access class)** | A C# class mapped to a database table. | The entity classes in `Erp.Core/Entities.cs`. |
| **Custom field** | A field added to an existing table or form through a customization. | `Project.AlertEmail`, `AlertThresholdPct`, `AlertSent`. |

## Trying the real thing

- Acumatica Open University has free developer courses, for example T190 Quick Start in Customization and the no-code W140 Customization Projects. Downloading course files needs a sign-in.
- I found no public self-serve free trial. The practice instance is one you install yourself with the Configuration Wizard, which needs Windows with IIS and SQL Server (the free Developer edition works). Nothing in this project was built inside Acumatica.

Sources: https://openuni.acumatica.com/courses/development/ , https://www.acumatica.com/blog/setting-up-local-developer-instances-of-acumatica/ , https://help.myob.com.au/advanced/Docs/Published/StudioDeveloperGuide/GLO__mng_Glossary.html
