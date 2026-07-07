# XML Documentation Design — ArturRios.Data

**Date:** 2026-06-20
**Scope:** Add XML documentation comments to all public types and members in `src/`
**Approach:** Option A — `<summary>` + `<returns>` + `<typeparam>` only

## Goal

Equip the NuGet package with XML docs so IntelliSense surfaces descriptions for every public type and member consumers
interact with.

## Files in Scope

| File                                        | Types / Members                                                             |
|---------------------------------------------|-----------------------------------------------------------------------------|
| `src/Entity.cs`                             | `Entity` class, `Id` property (already documented — keep as-is)             |
| `src/Interfaces/IReadOnlyRepository.cs`     | Interface, `<typeparam>`, `GetAll`, `GetById`                               |
| `src/Interfaces/ICrudRepository.cs`         | Interface, `<typeparam>`, `Create`, `GetAll`, `GetById`, `Update`, `Delete` |
| `src/Interfaces/IRangeRepository.cs`        | Interface, `<typeparam>`, `UpdateRange`, `DeleteRange`                      |
| `src/Configuration/BaseDbContextOptions.cs` | Class, `ConnectionString`                                                   |

## Documentation Rules

### Tags used

- `<summary>` — on every public type and member
- `<returns>` — on every non-void method
- `<typeparam name="T">` — on every generic interface

### Tags NOT used

- `<param>` — parameter names (`entity`, `id`, `ids`) are self-explanatory
- `<remarks>` — library is intentionally simple; no usage guidance needed
- `<exception>` — not documenting throw behavior at interface level

## Return Value Semantics

| Method        | Returns                                     |
|---------------|---------------------------------------------|
| `Create`      | ID of the created entity                    |
| `Update`      | The updated entity                          |
| `Delete`      | ID of the deleted entity                    |
| `GetAll`      | Queryable of all entities of type `T`       |
| `GetById`     | The matching entity, or `null` if not found |
| `UpdateRange` | The updated entities                        |
| `DeleteRange` | IDs of the deleted entities                 |

## Out of Scope

- Test project — no XML docs needed
- README changes
- New public API surface
