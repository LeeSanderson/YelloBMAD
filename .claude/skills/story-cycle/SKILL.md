---
name: story-cycle
description: 'Drive the BMad story cycle unattended: for each story in sprint-status.yaml, run bmad-create-story, optionally bmad-testarch-atdd, then bmad-dev-story and bmad-code-review in fresh subagents, commit each phase, and repeat until every story is done. Use when the user says "run the story cycle", "build the remaining stories", or invokes /story-cycle.'
argument-hint: '[--atdd | --atdd=auto] [--max-stories N] [--stop-at-epic] [--no-commit] [--review-model sonnet|opus|haiku|fable]'
---

# Story Cycle

You are the **orchestrator**. You do not write stories, code or reviews yourself. Each BMad phase runs in a
**fresh subagent** that invokes the real BMad skill; you choose the next phase from `sprint-status.yaml`,
answer the skills' checkpoints on the user's behalf, check each phase's claims against the repo, commit, and
loop. Your context holds only the phase reports, never the file dumps.

Invoking this skill is the user's one-time consent for every subagent the run needs (create-story, ATDD and
code-review all ask for it once per run).

## Arguments

| Flag | Default | Effect |
|---|---|---|
| `--atdd` | auto | Run the **ATDD** phase (`bmad-testarch-atdd`) on every story, between Create and Develop. |
| `--atdd=auto` | auto | Run ATDD only on stories that meet the **ATDD trigger** (see the ATDD phase). |
| `--max-stories N` | unlimited | Stop after N stories reach `done` in this run. |
| `--stop-at-epic` | off | Stop when an epic's last story reaches `done`, so a retrospective can run. |
| `--no-commit` | off | Do not commit. Review then diffs `{baseline_commit}` against the working tree. |
| `--review-model M` | inherit | Model for the code-review agent. BMad recommends a different model from the one that wrote the code. |

## Paths

Read `_bmad/bmm/config.yaml` and resolve `{project-root}`:

- `{sprint_status}` = `{implementation_artifacts}/sprint-status.yaml`
- `{deferred_work}` = `{implementation_artifacts}/deferred-work.md`
- `{story_file}` = `{implementation_artifacts}/{story_key}.md`
- `{test_artifacts}` = `test_artifacts` from `_bmad/tea/config.yaml` (here, `_bmad-output/test-artifacts`)

**Planning inputs: find them, then pass them explicitly.** BMad producers write dated run folders
(`prds/prd-*/prd.md`, `architecture/architecture-*/ARCHITECTURE-SPINE.md`, `ux-designs/ux-*/DESIGN.md` and
`EXPERIENCE.md`), and create-story's discovery globs (`*prd*.md`, `*architecture*.md`) do not match them. The
miss is silent. At preflight, glob under `{planning_artifacts}` and `_bmad-output/` for the newest PRD,
architecture spine, UX `DESIGN.md` / `EXPERIENCE.md`, `epics.md`, the implementation-readiness report, any
`sprint-change-proposal-*.md`, `_bmad-output/specs/**/SPEC.md`, `_bmad-output/test-artifacts/test-design-*.md`,
`tests/TESTING-CONVENTIONS.md` and `**/project-context.md`. Store the paths that exist as `{planning_inputs}`.

## Preflight (once)

1. **Clean tree.** Run `git status --porcelain`. If anything is modified or untracked, HALT and ask the user
   whether to commit it, stash it, or stop. Never start a phase on top of someone else's uncommitted work: the
   review diff would absorb it, and the dev agent might revert it.
2. **Sprint status exists.** If `{sprint_status}` is missing, HALT and tell the user to run
   `bmad-sprint-planning` first.
3. **Stories already `done` but uncommitted** are covered by step 1. A story in `review` or `in-progress` is
   resumed by the loop, not restarted.
4. Resolve `{planning_inputs}` as above.
5. Report the starting position to the user in one line (for example: "story 1.6 backlog → creating; 4 stories
   left in epic 1, 48 in total") and begin. Do not ask for confirmation.

## The loop

Repeat until a stop condition applies.

### 1. Choose the next action

Re-read `{sprint_status}` in full every iteration. It is the source of truth; your memory of the last
iteration is not. Read `development_status` top to bottom and take the **first** story key
(`<epic>-<story>-<slug>`) whose status is not `done`:

| Status | Phase |
|---|---|
| `review` | **Review** |
| `in-progress` | **Develop** (continuation; dev-story resumes from the review follow-ups or the first unchecked task) |
| `ready-for-dev` | **ATDD** if it applies (see below), otherwise **Develop** |
| `backlog` | **Create** |

**ATDD applies** to a `ready-for-dev` story only when all of these hold:

- `--atdd` is set, or `--atdd=auto` is set and the story meets the trigger;
- the story file's frontmatter has no `baseline_commit`, so dev-story has not started;
- `{test_artifacts}/atdd-checklist-{story_key}.md` does not exist yet.

The last two conditions make a resumed run skip ATDD instead of repeating it.

**ATDD trigger (`--atdd=auto`).** Read the story's acceptance criteria and Dev Notes. The trigger is met when
they cite any of these: an isolation or security NFR; a timing or enumeration
invariant; a test the test design marks **P0**; or a security architecture rule. 
These are the stories where a wrong assumption costs a redesign, so testing it before any code
exists pays for itself. CRUD-shaped stories that cite none of them go straight to Develop. Tell the user in one
line which way you decided and why.

Before you take a story from a new epic: if every story of the previous epic is `done` and its `epic-N` is
still `in-progress`, set `epic-N: done` and update `last_updated`. That transition is manual by BMad's own
definition, so it is yours. Commit it as `Close epic N`. Stop here if `--stop-at-epic` is set. Leave
`epic-N-retrospective` alone.

If no story is left, go to **Finish**.

### 2. Run the phase in a fresh subagent

Spawn one `general-purpose` agent per phase, and run phases **sequentially**: each one depends on the files
the previous one wrote. Pass `model` only for the Review phase, and only when `--review-model` is set. Use the
prompt templates below, filling every `{placeholder}`, including the **Test-execution policy** and **Comment
rule** blocks the ATDD, Develop and Review templates name. Every prompt ends with the shared **Return contract**.

You are bound by the same policy: never start a `dotnet test` while a phase agent may be running one, and run
any full suite you need in the background.

While an agent runs, tell the user in one line what it is doing ("1.6: dev-story running"). Do not run the
same work yourself.

### 3. Verify what the phase claims

A phase report is a claim. Before committing, check it against the repo:

- **Status transition.** `{sprint_status}` and the story file's `Status:` line both show the expected value
  (Create → `ready-for-dev`, ATDD → still `ready-for-dev`, Develop → `review`, Review → `done` or
  `in-progress`). If they disagree, or the status did not move, treat the phase as **failed**.
- **ATDD output.** The checklist exists. The story's Dev Notes carry the **ATDD handoff** section (see the ATDD
  phase). The build is clean, and the agent's filtered run of the new scaffold classes shows them discovered
  with **zero failures**: red scaffolds skipped, any scaffold reported as green-today passing. A failing
  scaffold would break every gate after it, so it must be skipped, not left red. Do not run the full suite
  after ATDD: the scaffolds are skipped and add nothing a full run would see.
- **Build and tests**, after Develop and after every Review. Run `dotnet build Yello.slnx`
  yourself (incremental, so it is cheap) and require 0 warnings and 0 errors. Then read the agent's full-run
  log, `artifacts/test-runs/{story_key}-{phase}.log` (see the **Test-execution policy**). It must exist, be
  newer than every file `git status --porcelain` lists, show zero failures, and match the totals the agent
  reported. The Develop log has no browser-suite results by design (it runs with
  `-p:YelloSkipBrowserSuite=true`); the Review log must include them, so a story never reaches `done` without
  one passing browser run. Only when the log is missing, stale or disagrees do you run the full suite yourself, once, by the
  same policy (background, `--max-parallel-test-modules 1`). On any failure or mismatch, treat the phase as
  **failed**.
- **Scope.** `git status --porcelain` lists only files the phase should touch. A Create phase touches only the
  story file and `sprint-status.yaml`.

**Failed phase:** re-run it once in a new agent. Give that agent the failure evidence and the previous
agent's report. If it fails a second time, stop the loop and report.

### 4. Commit the phase

Unless `--no-commit` is set, commit straight to the current branch with **one commit per phase**, never one
per file:

- **Create:** `Add story {story_id}`, with the story file and `sprint-status.yaml` only.
- **ATDD:** `Add ATDD scaffolds for story {story_id}`. Include the scaffold test files, the checklist and the
  story file (for the handoff section). The body lists each scaffold with its AC, which ones are red and which
  are green today, and the activation order.
- **Develop:** a subject that states what the story makes true, in the voice of `git log` (for example
  "Resolve the active Space per request, enforced in the database"). The body comes from the agent's report:
  the requirement IDs, the dominant finding, the decisions taken, planted violations and whether each was
  caught, deferrals and their owners, and the exact build and test totals you verified.
- **Review:** `Close the code review: <the finding that mattered most>`. The body gives raw findings → triaged
  count, patches applied, deferrals with owners, decisions and who made them, and the verified totals.

End every commit with the attribution trailers your session requires. Never use `--no-verify`. If a hook
fails, the phase has failed (see step 3).

### 5. Route the outcome

- Create done → ATDD the same story next if ATDD applies, otherwise Develop.
- ATDD done → Develop the same story next.
- Develop done → Review the same story next.
- Review → `done`: count the story as completed and go back to step 1.
- Review → `in-progress` (patches left, or unresolved high/medium findings): Develop again, and dev-story
  picks up the `[Review]` items. **Allow at most two review rounds per story.** If a third would be needed,
  stop the loop and report.

### 6. Questions only the user can answer

Some outcomes need the user. Ask with `AskUserQuestion`, then pass the answer to **the same agent** with
`SendMessage` so its context survives. Do not start a new agent for this.

- **Develop HALT:** a new dependency needs approval, three consecutive failures, a task too ambiguous to
  implement, or missing configuration. Show the user the agent's exact blocker and its proposed options.
- **Review `decision-needed` findings:** the agent returns them without deciding (see the Review prompt).
  Batch them into one `AskUserQuestion` call of up to four questions; if there are more, use several calls.
  Each question carries the finding and the options, with the agent's recommendation listed first.

If the user declines or chooses to stop, stop the loop.

## Phase prompts

### Shared standing facts (paste into every prompt)

```
You are running one phase of an unattended BMad story cycle. An orchestrator spawned you; there is no human
in this conversation. Rules:
- Invoke the named BMad skill with the Skill tool and follow it exactly. Do not imitate it from memory.
- When the skill tells you to ask the user or to HALT at a checkpoint, use the standing answer given below.
  If no standing answer covers the question, stop and return it as a blocker. Never invent the user's
  answer to a question that has none.
- Do not commit, push, branch or stash. The orchestrator commits.
- Never use `git checkout --`, `git restore` or `git reset` to undo a temporary change, such as a planted
  violation that tests a gate. On a tracked file it destroys uncommitted story work, and on an untracked file
  it silently leaves the plant in place. Save the original content first, and restore it in a `finally`.
- Planning inputs (load these; the skill's own discovery globs miss the dated run folders):
  {planning_inputs}
- Story: {story_key}. Story file: {story_file}. Sprint status: {sprint_status}.
```

### Test-execution policy (paste into the ATDD, Develop and Review prompts)

A full `dotnet test Yello.slnx --max-parallel-test-modules 1` takes about 16 minutes (Slices about 7, Client about 4,
Isolation about 4), and building and testing is over half of each story's time. Earlier runs averaged 20-28
test invocations per develop phase, ran the full suite in ATDD although every scaffold was skipped, re-ran it
after each review patch, and hit the Bash tool's 10-minute timeout with `until grep ...; do sleep` loops. This
policy replaces that. Fill `{phase}` with `atdd`, `develop` or `review`.

```
Test execution policy. It overrides any step of the BMad skill that says "run all tests", "run the full test
suite" or "run the full regression suite", except the one full run this policy allows for your phase.
- Rebuild before every test run: `dotnet build Yello.slnx` (0 warnings, 0 errors). `--no-build` runs whatever
  was last built. The browser suite publishes `Yello.Client` itself, so it needs no separate client build.
- One `dotnet test` process at a time, never two (CLAUDE.md, "Test memory budget").
- Iterate on classes, not suites: one project, filtered to the classes you changed or that cover the code you
  changed, for example
    dotnet test --project tests/Yello.Tests.Slices/Yello.Tests.Slices.csproj --no-build -- --filter-class "Yello.Tests.Slices.Memberships.MembershipHandlerTests"
  (`--filter-namespace` and `--filter-method` work the same way, and accept `*` wildcards; repeat the option
  to select several). A bare class name matches nothing and exits 8: use the full name or `"*ClassName"`. A large partial class such as `ActiveSpaceEndpointTests` is one class, so narrow it with
  `--filter-method "*ChangeMembershipRole*"` or similar. The Architecture suite takes seconds; run it whole
  whenever you touch production code or test conventions.
- A full run is started with `run_in_background: true`, its output saved to the phase log, and then you wait
  for the completion notification:
    New-Item -ItemType Directory -Force artifacts/test-runs | Out-Null; dotnet test Yello.slnx --no-build --max-parallel-test-modules 1 2>&1 | Tee-Object artifacts/test-runs/{story_key}-{phase}.log
  Never run a full run in the foreground: the tool times out at 10 minutes and the run carries on unseen.
  Never poll with `until grep ...; do sleep`, `Start-Sleep` or `while` loops. A filtered run that may take
  more than a few minutes (any Client class, a large Slices class) also goes in the background.
- If the full run fails: fix, re-run the failing classes until green, then make one more full run. Report how
  many full runs you made and why.
```

Phase-specific lines, appended under that block:

- **ATDD:** `No full run. Build, then confirm each new scaffold class is discovered and reports zero failures: one
  filtered run per project with --filter-class for the new classes (skipped scaffolds start no SQL Server), or
  --list-tests. Run the Architecture suite whole, because it holds the convention gates new test code can
  break. Report the discovered, skipped and passing counts per class.`
- **Develop:** `While iterating, run only the affected classes (and the Architecture suite). That replaces
  dev-story's per-task "run all existing tests" (step 7) and "run full test suite" (step 8). Exactly one full
  run, at step 9 ("run the full regression suite"), before you set Status to review, logged as
  artifacts/test-runs/{story_key}-develop.log. That full run leaves out the browser suite: add
  -p:YelloSkipBrowserSuite=true after --no-build (the switch ci.yml uses). The browser suite is about a quarter
  of the full run, and Review's full run includes it. If the story touches Yello.Client, Yello.Contracts or
  tests/Yello.Tests.Client, still run the affected Client classes filtered (in the background).`
- **Review:** `Apply every accepted patch first. Then run the classes the patches touch (and the Architecture
  suite), then one final full run logged as artifacts/test-runs/{story_key}-review.log. Never a full run per
  patch. Your full run includes the browser suite (no YelloSkipBrowserSuite), because Develop's left it out.
  Make this one full run even when no patch was applied.`

### Comment rule (paste into the Develop and Review prompts)

```
Comment rule for code you write or change (CLAUDE.md, "Code comments"):
- A comment says why non-obvious code is the way it is: the constraint, the trap, the reason for the odd
  choice. If the code says it, do not comment it.
- No story numbers, AC or requirement ids, review dates, Q&A references or narrative history ("added by",
  "corrected at code review", "until ..."). That belongs in the commit message and the story file.
- No XML doc comment that restates the member's name or signature. Keep <remarks> to a few lines.
- Do not rewrite existing comments you are not otherwise changing.
```

### Create

```
{shared standing facts}

Invoke skill `bmad-create-story` with args `{story_key}`.
Standing answers:
- "Any other documents to include?" → every path in the planning inputs above.
- Questions the skill saves for the end → write them into the story file's Dev Notes as numbered open
  questions, each with a recommended decision. The dev phase decides them as reversible, recorded decisions.
  Do not wait for answers.
When you finish, confirm that the story file exists, that its Status is ready-for-dev and that
sprint-status.yaml shows {story_key}: ready-for-dev.
```

### ATDD

```
{shared standing facts}

Invoke skill `bmad-testarch-atdd` with args `{story_file}`.
Standing answers:
- Create / Edit / Validate menu → Create. If a progress file exists for this story, stop and return blocked;
  do not resume or overwrite it.
- Generation mode → AI generation from the story and documentation. No recording mode.
- Test levels → follow the levels and project bindings in tests/TESTING-CONVENTIONS.md and the test design.
  Do not add a new test project or a new package. If one seems necessary, return that as a blocker.
- Step 4 workers (API / E2E): if you cannot launch subagents, generate those tests yourself, in sequence.
- Red phase: every scaffold whose AC is not yet implemented must be committed SKIPPED, with the skip reason
  naming its AC (in xUnit, `[Fact(Skip = "red: AC<n> - <why>")]`), so the build and the suite stay green. A
  scaffold that passes today is a standing regression gate: leave it active and say so in the checklist.
- Every scaffold must compile. Seed data and fixtures must satisfy the real schema, including foreign keys.
  Cite the constraint you checked. Story 1.5's seed helpers failed exactly here.
- Container-backed tests: reuse existing fixtures and classes where you can. Each new container-backed class
  starts another database engine on a full run, and this machine is memory-bound.
- Do not write product code.
After the skill completes, add this section to the story file's Dev Notes, and change nothing else in it:
  ### ATDD artifacts - red-phase scaffolds already in the tree
  The checklist path; each scaffold file with its case count and ACs; which are green today; the activation
  order, with the scaffold that tests the riskiest assumption first; and the instruction "Activate one at a
  time, confirm it fails for the stated reason, then implement. A scaffold that is wrong is corrected and the
  correction recorded in the Debug Log, not deleted."

{test-execution policy, phase = atdd, with the ATDD line}
Report the build result and, per new scaffold class, the discovered, skipped and passing counts.
```

### Develop

```
{shared standing facts}

Invoke skill `bmad-dev-story` with args `{story_file}`.
Standing answers:
- An "ATDD artifacts" section in Dev Notes → follow its activation order. Activating a scaffold, confirming
  that it fails for the stated reason and then making it pass counts as the red-green step for that test.
  Record each scaffold you had to correct, and why, in the Debug Log.
- Open questions in the story file → decide each one as a reversible decision. Record it in Completion Notes
  with its reasoning, phrased as a line the user can cut later.
- Step 10's "do you need explanations?" → no. Skip straight to completion.
- HALT conditions (new dependency, three consecutive failures, ambiguous task, missing configuration) → do
  not work around them. Stop and return the blocker with the options you see and your recommendation.
- Anything you cannot finish in this story → add an entry to {deferred_work} that names the story which owns it.

{test-execution policy, phase = develop, with the Develop line}
{comment rule}
The build and the one full run must pass before you set Status to review. Report the full run's totals and
its log path.
```

### Review

```
{shared standing facts}

Invoke skill `bmad-code-review` with args:
  `{story_file}` commit range {baseline_commit}..HEAD
(With --no-commit, use `{story_file}` uncommitted changes against {baseline_commit}.)
Standing answers:
- Review target → the story file as the spec and the commit range above as the diff; review_mode = full.
- Step 1 checkpoint ("confirm to proceed") → proceed.
- Diff over ~3000 lines, offer to chunk → decline. Review it whole.
- Step 2: if you cannot launch subagents, the skill writes reviewer prompt files and HALTs. Return
  `RESULT: needs-reviewers` with the prompt file paths. The orchestrator runs the reviewers and sends their
  findings back to you.
- Step 3 routing → patch only what you rate `high` or `medium`. A finding you rate `low` (cosmetic, a nitpick,
  naming, style, a comment's wording, a test that could be tidier) goes to `defer`, even when the fix is
  unambiguous. One exception: a comment this diff added that breaks the comment rule below is a `patch`,
  because deleting or shortening it is mechanical. Severity does not change `decision_needed`: those still
  go to the user.
- Step 4 writing low-severity deferrals → in the story file, `- [x] [Review][Defer] <Title> [<file>:<line>] —
  deferred, low severity` (not "pre-existing"); in {deferred_work}, under the step's `## Deferred from: code
  review of ...` heading, one line each: what and where, `Severity: low`, and an owner (the story or epic
  whose code it is, or `sweep` when nothing owns it). With no low patch left open, step 6 sets the story
  `done` as usual.
- Step 4 decision-needed findings → do NOT decide them. Return `RESULT: needs-decisions` with each finding's
  title, detail, options and your recommendation. Your answers will come back by message; then continue.
- Step 4 patch handling → option 1, apply every patch. Then test by the policy below: affected classes first,
  then one full run.
- Step 4 deferral reasons → name the story or epic that owns each one.
- Step 7 "what next?" → 3, done.

{test-execution policy, phase = review, with the Review line}
{comment rule}
Report the raw finding count, the triaged count (patched / deferred as low / deferred otherwise / decisions /
dismissed), the finding that mattered most, and the full run's totals and log path.
```

`{baseline_commit}` is the commit **before the story's first test or code change**:

- If an ATDD commit exists for this story (`git log --grep "Add ATDD scaffolds for story {story_id}"`), use its
  parent. The scaffolds are then part of the reviewed diff. That matters: story 1.5's ATDD run left 3 of 9
  scaffolds wrong, so scaffolds need review as much as product code does.
- Otherwise, use the `baseline_commit` that dev-story wrote to the story file's frontmatter.
- If neither exists, use the parent of the Create commit for this story.

**`needs-reviewers` fallback.** Launch one fresh agent per prompt file **in parallel**, with no conversation
context: the layers are blind by design. Pass `--review-model` to them if it is set. Concatenate their
findings and `SendMessage` them to the waiting review agent.

### Return contract (end every prompt with this)

```
Finish with this block and nothing after it, at most 300 words:
RESULT: complete | blocked | needs-decisions | needs-reviewers | failed
STORY: {story_key}  STATUS-NOW: <sprint-status value>
SUMMARY: <what changed, the requirement IDs, the dominant finding>
DECISIONS: <decisions taken, one line each>
DEFERRED: <entries added to deferred-work.md, with owners>
TESTS: <build result; test totals passed/failed/skipped; number of full runs and the log path>
BLOCKER: <only when blocked, needs-decisions or needs-reviewers: exact question or paths, options, recommendation>
```

## Stop conditions

Stop the loop and go to **Finish** when any of these applies:

- No story is left that is not `done`.
- `--max-stories` is reached, or `--stop-at-epic` fires at an epic boundary.
- A phase fails twice in a row.
- A story needs a third review round.
- The user declines or stops at a question.
- A phase reports a requirement contradiction: the story, epic and PRD disagree, or an AC cannot be met as
  written. Recommend `bmad-correct-course` and stop. Do not let the cycle paper over it.

## Finish

Report to the user:

- Stories completed this run, each with its commit SHAs (create / ATDD / develop / review rounds). For every
  ATDD story, also give the number of scaffolds the dev phase had to correct. That number tells you whether
  ATDD is earning its place.
- Where the loop stopped and why, quoting the blocker if there was one.
- Decisions the agents took on the user's behalf, collected from every `DECISIONS:` line. The user should
  check these.
- New `deferred-work.md` entries and the stories that own them.
- The final verified test totals.
- Suggested next step: `bmad-retrospective` at an epic boundary, `bmad-correct-course` for a contradiction,
  or re-invoke `/story-cycle` to continue.
