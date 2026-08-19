# Reviewer Gate — Web Verification lens

**Run:** 2026-08-19, Update mode (Gaps 1–3). Run **inline**, not as an independent subagent — subagents unavailable this session, so the independence property `references/reviewer-gate.md` relies on is absent. Findings below stand on cited sources rather than on reviewer independence.

**Verdict: 1 critical finding, corrected. Everything else this update asserts is either already-verified from the 2026-08-17 run or carries no external claim.**

---

## CRITICAL — `AD-26`'s single-statement ownership swap rested on a false claim about SQL Server

**As drafted in this update**, AD-26 said acceptance performs the Role swap as one `UPDATE` with a `CASE` over both Membership rows, justified by:

> *"SQL Server evaluates AD-5's filtered unique index at statement completion, so it can never transiently observe two Owners and statement order cannot matter."*

**That is false.** SQL Server has no deferred uniqueness enforcement of any kind:

- `SET CONSTRAINTS … DEFERRED` exists in PostgreSQL and Oracle. **SQL Server does not support it.** Practitioner guidance is consistent that SQL Server therefore requires a temporary-value workaround for unique-value swaps.
- Uniqueness is checked **per row as the index is maintained**, not at statement completion.
- Microsoft's own documentation describes only immediate enforcement — the engine "returns an error message that states the `UNIQUE` constraint was violated" — and documents no statement-level or transaction-level deferral option.

A single `CASE` update can therefore process the promote before the demote, transiently write a second `Owner` row for the Space, and fail on AD-5's filtered unique index. Plan-dependent, which is worse than reliably broken.

**Correction applied.** Two `UPDATE` statements in one transaction, in a fixed order: **demote the current Owner to `Admin` first, then promote the recipient.**

**Why this needs no temporary value** — worth recording, because it is the non-obvious part and an implementer following generic swap advice would add a pointless third statement. The index is **filtered** on `Role = Owner`. Demoting removes the row from the index *entirely*, leaving **zero** matching rows for that `SpaceId`, and zero never violates uniqueness. The classic temporary-value dance is only needed when both rows must continuously hold a constrained value. Here one leaves the constrained set before the other enters it.

Reverse order fails. Order is load-bearing, and AD-26 now says so and forbids tracked-entity `SaveChanges`, since EF Core selects its own statement order for two tracked rows.

**Sources:**
- [Unique constraints and check constraints — Microsoft Learn](https://learn.microsoft.com/en-us/sql/relational-databases/tables/unique-constraints-and-check-constraints?view=sql-server-ver17)
- [Create a unique index — Microsoft Learn](https://learn.microsoft.com/en-us/sql/relational-databases/indexes/create-unique-indexes?view=sql-server-ver16)
- [Deferrable SQL Constraints in Depth — begriffs.com](https://begriffs.com/posts/2017-08-27-deferrable-sql-constraints.html)
- [Violation of unique constraint while updating database — microsoft.public.dotnet.framework.adonet](https://groups.google.com/g/microsoft.public.dotnet.framework.adonet/c/MlVSKBraJis)
- [Atomicity of UPDATE, interchanging values in unique — PostgreSQL list](https://postgresql.org/message-id/23176.1047165697%40www5.gmx.net) (the canonical statement of the general problem)

---

## Pattern worth acting on

**This is the second plausible-but-wrong SQL Server claim in this spine.** The 2026-08-17 run recorded:

> *"Guid.CreateVersion7() is WRONG for SQL Server and was a defect in the first draft."*

Same shape: a confident assertion about SQL Server engine behaviour, produced from model priors, caught only by this lens. Two for two.

**Recommendation:** treat any assertion about SQL Server engine behaviour in this project as unverified until checked against Microsoft documentation. The two defects found so far were both in *index behaviour* specifically.

---

## Verified as needing no external check

| Claim in this update | Why no web check needed |
|---|---|
| Filtered unique index permits zero matching rows | Definitional — a unique index constrains duplicates, not existence. AD-5 already depends on it. |
| Filtered index on `WHERE State = Pending` is expressible | Same construct AD-5 already uses for `WHERE Role = Owner`. |
| `ExecuteUpdate` available in EF Core 10 | Present since EF Core 7; the Stack table already pins EF Core 10. Participates in the slice's explicit transaction, which the Mutation convention already mandates. |
| In-process push clears NFR-2's 1 s live-session clause | Architectural, not a version claim. NFR-2 is worded to fail a poller or cross-replica hop — AD-14 is neither. |
| AD-27 computed-on-read expiry | No external dependency. |

## Not re-verified

Every technology pinned in the 2026-08-17 run (.NET 10.0.11, Blazor WASM, EF Core 10, Azure SQL free offer, Container Apps ingress limits, xUnit v3, Testcontainers, ArchUnitNET) was verified then and is untouched by this update. This lens did not re-date them; at ~2 days old that is proportionate, but a full re-verification is due whenever the Stack table is next edited.
