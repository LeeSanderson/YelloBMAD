using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;
using Yello.Contracts;
using Yello.Contracts.Accounts;
using Yello.Contracts.ApiTokens;
using Yello.Contracts.Labels;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;
using Yello.Contracts.Tasks;

namespace Yello.Tests.Client;

/// <summary>
/// The Host's API, faked at the browser's network edge with <c>page.RouteAsync</c>, so a browser case
/// can drive a signed-in flow with no Host, no database and no container. Story 1.7, open question 5.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written by the story 1.7 ATDD run on 2026-10-24.</b> Story 1.7 recommends this shape (Q5 option
/// a) because the machine has no memory for another SQL Server engine and a real Host inside this
/// suite would be a ring change. What it does <b>not</b> prove, stated so nobody reads more into a
/// green case than is there: the real Session cookie crossing origins, the real CORS preflight the
/// Host answers, and anything the Host decides. Those are re-owned by story 1.9.
/// </para>
/// <para>
/// <b>The answers are the Host's shapes.</b> Every body is a <c>Yello.Contracts</c> type
/// (<see cref="AntiforgeryTokenResponse"/>, <see cref="ProblemResponse"/>,
/// <see cref="SpaceIdentityResponse"/>, <see cref="ActiveSpaceResponse"/>) and every path is a
/// contract route, so a contract change breaks the fake as well.
/// </para>
/// <para>
/// <b>Every request is recorded</b>, so a case can assert that a request was never made - AC10's
/// "without issuing any Space-scoped request for it".
/// </para>
/// </remarks>
internal sealed class FakeYelloApi
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The status <c>DecideAsync</c> answers when the connection must be reset rather than answered. Story 2.3.
    /// </summary>
    private const int AbortConnection = -1;

    /// <summary>
    /// The longest Label name the Host accepts (<c>LabelName.MaxLength</c>, story 2.4 Q2), which the fake mirrors.
    /// </summary>
    private const int FakeLabelNameMaxLength = 50;

    /// <summary>
    /// Story 2.6 Q3 (a)'s recommended refusal for a neighbour that is no longer a neighbour (and Q10's exhaustion), stated
    /// here because <c>ProblemTypes</c> gains it only in the dev phase. Retarget to the contract constant on activation.
    /// </summary>
    public const string TaskPositionUnavailableType = "urn:yello:problem:task-position-unavailable";

    private readonly ConcurrentQueue<FakeRequest> _requests = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _held = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _arrived = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _heldProjects = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _arrivedProjects = new();
    private readonly ConcurrentDictionary<string, string> _createdByKey = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _heldBoards = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _arrivedBoards = new();
    private readonly ConcurrentDictionary<string, string> _createdTaskByKey = new(StringComparer.Ordinal);
    private TaskCompletionSource? _heldRename;
    private TaskEditHold? _taskEditHold;
    private TaskEditHold? _lastTaskEditHold;
    private TaskCompletionSource? _arrivedRename;
    private TaskCompletionSource? _heldLabelCreate;
    private TaskCompletionSource? _arrivedLabelCreate;
    private DeleteHold? _taskDeleteHold;
    private DeleteHold? _lastTaskDeleteHold;
    private DeleteHold? _projectDeleteHold;
    private DeleteHold? _lastProjectDeleteHold;
    private readonly ConcurrentDictionary<string, string> _deletedByKey = new(StringComparer.Ordinal);
    private TaskEditHold? _taskMoveHold;
    private TaskEditHold? _lastTaskMoveHold;
    private readonly ConcurrentDictionary<string, (string Path, string Body, string Answer)> _movedByKey = new(StringComparer.Ordinal);
    private readonly TaskCompletionSource<FakeRequest> _signInSeen = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<FakeRequest> _signOutSeen = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The anti-forgery request token this fake issues, unique per instance.
    /// </summary>
    public string IssuedToken { get; } = "fake-antiforgery-" + Guid.NewGuid().ToString("N");

    /// <summary>
    /// Whether <c>GET /api/v1/sessions/current</c> answers 204. A successful sign-in sets it; a
    /// sign-out clears it.
    /// </summary>
    public bool SignedIn { get; set; }

    /// <summary>
    /// Whether the first <c>DELETE /api/v1/sessions</c> is refused for its anti-forgery token, as
    /// the Host refuses a token minted to a Session that has since been ended on another device.
    /// </summary>
    public bool RefuseFirstSignOut { get; set; }

    /// <summary>
    /// The switcher's answer, in the order the Host serves it.
    /// </summary>
    public IList<(Guid Id, string Name)> Switcher { get; } = new List<(Guid Id, string Name)>();

    /// <summary>
    /// The Role, as its stable code, the Account holds in each Space it may be served.
    /// </summary>
    public IDictionary<Guid, string> Roles { get; } = new Dictionary<Guid, string>();

    /// <summary>
    /// The Account's API Tokens as the Account-settings list serves them. Story 1.8.
    /// </summary>
    public IList<FakeApiToken> ApiTokens { get; } = new List<FakeApiToken>();

    /// <summary>
    /// The plaintext the next issuance answers with - Q9's format, unique per instance, so a case can
    /// look for exactly this string in the DOM and in storage. Story 1.8.
    /// </summary>
    public string IssuedSecret { get; } = "yello_pat_" + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Each Space's Projects, as the list route serves them. Story 2.1; the routes are
    /// <c>Yello.Contracts.Projects</c>' ones, and the bodies are <see cref="FakeProject"/>, shaped as
    /// <c>ProjectResponse</c>.
    /// </summary>
    public IDictionary<Guid, List<FakeProject>> Projects { get; } = new Dictionary<Guid, List<FakeProject>>();

    /// <summary>
    /// Whether a Project creation is refused <c>409 bound-reached</c> for <c>projects-per-space</c>, limit
    /// 50, as the Host refuses the 51st. Story 2.1.
    /// </summary>
    public bool ProjectBoundReached { get; set; }

    /// <summary>
    /// Whether the next Project creation is applied and its answer then lost - the connection dropped
    /// after the server committed. A creation repeating an <c>Idempotency-Key</c> already applied is
    /// answered with the first answer, as the Host replays it. Story 2.1.
    /// </summary>
    public bool LoseNextProjectCreateAnswer { get; set; }

    /// <summary>
    /// Each Project's Board, keyed by (Space, Project), as <c>GET …/projects/{projectId}/board</c> serves it.
    /// Story 2.2; the routes are <c>BoardRoutes</c>' and <c>TaskRoutes</c>',
    /// and the bodies are <see cref="FakeBoard"/>, shaped as story 2.2 Task 3's <c>BoardResponse</c>.
    /// </summary>
    public IDictionary<(Guid SpaceId, Guid ProjectId), FakeBoard> Boards { get; } = new Dictionary<(Guid SpaceId, Guid ProjectId), FakeBoard>();

    /// <summary>
    /// Whether a Task creation is refused <c>409 bound-reached</c> for <c>tasks-per-project</c>, limit 5000, as
    /// the Host refuses the 5,001st. Story 2.2.
    /// </summary>
    public bool TaskBoundReached { get; set; }

    /// <summary>
    /// A refusal the next Task creation is answered with instead of being applied - an idempotency refusal,
    /// say (AC13). Cleared once used. Story 2.2.
    /// </summary>
    public (int Status, string ProblemType)? NextTaskCreateRefusal { get; set; }

    /// <summary>
    /// A refusal the next Task edit is answered with instead of being applied - <c>(status, type, capability)</c>.
    /// Cleared once used. Story 2.3 (written by its ATDD run).
    /// </summary>
    public (int Status, string ProblemType, string? Capability)? NextTaskEditRefusal { get; set; }

    /// <summary>
    /// Each Space's Labels, as <c>GET …/labels</c> serves them, by name then id (story 2.4 Q12). Story 2.4 (written by
    /// its ATDD run); the routes are <see cref="LabelRoutes"/>'s, and the bodies are <see cref="FakeLabel"/>, shaped as
    /// <c>LabelResponse</c>.
    /// </summary>
    public IDictionary<Guid, List<FakeLabel>> Labels { get; } = new Dictionary<Guid, List<FakeLabel>>();

    /// <summary>
    /// Whether the Project listing states each Project's true Task count (<c>taskCount</c>, the sum of its Board's column
    /// totals, 0 without a Board) - story 2.5 Q2 (a)'s appended field. Off by default, so every case written before 2.5 is
    /// served the bytes it always was. Story 2.5 (written by its ATDD run).
    /// </summary>
    public bool ServeProjectTaskCounts { get; set; }

    /// <summary>
    /// A refusal the next Task deletion is answered with instead of being applied - <c>(status, type, capability)</c>.
    /// Cleared once used. Story 2.5.
    /// </summary>
    public (int Status, string ProblemType, string? Capability)? NextTaskDeleteRefusal { get; set; }

    /// <summary>
    /// A refusal the next Project deletion is answered with instead of being applied. Cleared once used. Story 2.5.
    /// </summary>
    public (int Status, string ProblemType, string? Capability)? NextProjectDeleteRefusal { get; set; }

    /// <summary>
    /// A refusal the next Task move is answered with instead of being applied - <c>(status, type, capability)</c>.
    /// Cleared once used. Story 2.6 (written by its ATDD run).
    /// </summary>
    public (int Status, string ProblemType, string? Capability)? NextTaskMoveRefusal { get; set; }

    /// <summary>
    /// Every request seen, in order.
    /// </summary>
    public IReadOnlyCollection<FakeRequest> Requests => _requests;

    /// <summary>
    /// Completes with the first sign-in submission seen.
    /// </summary>
    public Task<FakeRequest> SignInSeen => _signInSeen.Task;

    /// <summary>
    /// Completes with the first sign-out seen.
    /// </summary>
    public Task<FakeRequest> SignOutSeen => _signOutSeen.Task;

    /// <summary>
    /// Holds the next answer about <paramref name="spaceId"/> until <see cref="Release"/>.
    /// </summary>
    /// <returns>A task that completes when the held request has arrived and is waiting.</returns>
    public Task HoldAsync(Guid spaceId)
    {
        _held[spaceId] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _arrived[spaceId] = arrived;

        return arrived.Task;
    }

    /// <summary>
    /// Lets a held answer go.
    /// </summary>
    public void Release(Guid spaceId)
    {
        if (_held.TryRemove(spaceId, out var held))
        {
            held.TrySetResult();
        }
    }

    /// <summary>
    /// Holds the next Project list answer for <paramref name="spaceId"/> until
    /// <see cref="ReleaseProjects"/>. Story 2.1.
    /// </summary>
    /// <returns>A task that completes when the held request has arrived and is waiting.</returns>
    public Task HoldProjectsAsync(Guid spaceId)
    {
        _heldProjects[spaceId] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _arrivedProjects[spaceId] = arrived;

        return arrived.Task;
    }

    /// <summary>
    /// Lets a held Project list answer go.
    /// </summary>
    public void ReleaseProjects(Guid spaceId)
    {
        if (_heldProjects.TryRemove(spaceId, out var held))
        {
            held.TrySetResult();
        }
    }

    /// <summary>
    /// Holds the next Project rename answer until <see cref="ReleaseRename"/>. Story 2.1.
    /// </summary>
    /// <returns>A task that completes when the held rename has arrived and is waiting.</returns>
    public Task HoldRenameAsync()
    {
        _heldRename = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _arrivedRename = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        return _arrivedRename.Task;
    }

    /// <summary>
    /// Lets a held rename answer go.
    /// </summary>
    public void ReleaseRename() => _heldRename?.TrySetResult();

    /// <summary>
    /// Holds the next Label definition (<c>POST …/labels</c>) until <see cref="ReleaseLabelCreate"/>, so a case can
    /// assert the surface waits for the server (it needs the id). Story 2.4.
    /// </summary>
    /// <returns>A task that completes when the held definition has arrived and is waiting.</returns>
    public Task HoldLabelCreateAsync()
    {
        _heldLabelCreate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _arrivedLabelCreate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        return _arrivedLabelCreate.Task;
    }

    /// <summary>
    /// Lets a held Label definition go, applied as the Host applies it.
    /// </summary>
    public void ReleaseLabelCreate() => _heldLabelCreate?.TrySetResult();

    /// <summary>
    /// Holds the next Board answer for <paramref name="projectId"/> until <see cref="ReleaseBoard"/>. Story 2.2.
    /// </summary>
    /// <returns>A task that completes when the held request has arrived and is waiting.</returns>
    public Task HoldBoardAsync(Guid projectId)
    {
        _heldBoards[projectId] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _arrivedBoards[projectId] = arrived;

        return arrived.Task;
    }

    /// <summary>
    /// Lets a held Board answer go.
    /// </summary>
    public void ReleaseBoard(Guid projectId)
    {
        if (_heldBoards.TryRemove(projectId, out var held))
        {
            held.TrySetResult();
        }
    }

    /// <summary>
    /// Holds the <b>next</b> Task edit (<c>PATCH …/tasks/{taskId}</c>) until it is released; later edits are
    /// answered at once, so a case can hold one attribute's edit while another's is admitted (Q8). Story 2.3.
    /// </summary>
    /// <returns>A task that completes when the held edit has arrived and is waiting.</returns>
    public Task HoldTaskEditAsync()
    {
        var hold = new TaskEditHold();

        _taskEditHold = hold;
        _lastTaskEditHold = hold;

        return hold.Arrived.Task;
    }

    /// <summary>
    /// Lets the held Task edit go and applies it, as the Host admits it.
    /// </summary>
    public void ReleaseTaskEdit() => ReleaseHeldEdit(TaskEditOutcome.Admitted);

    /// <summary>
    /// Lets the held Task edit go with a refusal instead: nothing is applied.
    /// </summary>
    /// <param name="status">The status code.</param>
    /// <param name="problemType">The problem type.</param>
    /// <param name="capability">The capability a <c>403</c> names, or <c>null</c>.</param>
    public void ReleaseTaskEditRefused(int status, string problemType, string? capability = null) =>
        ReleaseHeldEdit(new TaskEditOutcome(status, problemType, capability, Abort: false));

    /// <summary>
    /// Lets the held Task edit go with no answer at all - the connection reset, as a timeout or a dropped
    /// network reaches the browser. Nothing is applied.
    /// </summary>
    public void ReleaseTaskEditUnanswered() => ReleaseHeldEdit(new TaskEditOutcome(0, string.Empty, Capability: null, Abort: true));

    /// <summary>
    /// Holds the <b>next</b> Task deletion (<c>DELETE …/tasks/{taskId}</c>) until it is released, so a case can assert
    /// the deletion is not optimistic: the card and the panel stay while it is out. Story 2.5.
    /// </summary>
    /// <returns>A task that completes when the held deletion has arrived and is waiting.</returns>
    public Task HoldTaskDeleteAsync()
    {
        var hold = new DeleteHold();

        _taskDeleteHold = hold;
        _lastTaskDeleteHold = hold;

        return hold.Arrived.Task;
    }

    /// <summary>
    /// Lets the held Task deletion go and applies it, as the Host does: <c>204</c>.
    /// </summary>
    public void ReleaseTaskDelete() => _lastTaskDeleteHold?.Released.TrySetResult(TaskEditOutcome.Deleted);

    /// <summary>
    /// Lets the held Task deletion go with a refusal instead: nothing is deleted.
    /// </summary>
    public void ReleaseTaskDeleteRefused(int status, string problemType, string? capability = null) =>
        _lastTaskDeleteHold?.Released.TrySetResult(new TaskEditOutcome(status, problemType, capability, Abort: false));

    /// <summary>
    /// Lets the held Task deletion go with no answer - the connection reset. Nothing is deleted, so an identical resend
    /// under the same key is applied.
    /// </summary>
    public void ReleaseTaskDeleteUnanswered() =>
        _lastTaskDeleteHold?.Released.TrySetResult(new TaskEditOutcome(0, string.Empty, Capability: null, Abort: true));

    /// <summary>
    /// Holds the <b>next</b> Project deletion (<c>DELETE …/projects/{projectId}</c>) until it is released. Story 2.5.
    /// </summary>
    /// <returns>A task that completes when the held deletion has arrived and is waiting.</returns>
    public Task HoldProjectDeleteAsync()
    {
        var hold = new DeleteHold();

        _projectDeleteHold = hold;
        _lastProjectDeleteHold = hold;

        return hold.Arrived.Task;
    }

    /// <summary>
    /// Lets the held Project deletion go and applies it: the Project and its Board are gone. <c>204</c>.
    /// </summary>
    public void ReleaseProjectDelete() => _lastProjectDeleteHold?.Released.TrySetResult(TaskEditOutcome.Deleted);

    /// <summary>
    /// Lets the held Project deletion go with a refusal instead: nothing is deleted.
    /// </summary>
    public void ReleaseProjectDeleteRefused(int status, string problemType, string? capability = null) =>
        _lastProjectDeleteHold?.Released.TrySetResult(new TaskEditOutcome(status, problemType, capability, Abort: false));

    /// <summary>
    /// Lets the held Project deletion go with no answer - the connection reset. Nothing is deleted, so an identical
    /// resend under the same key is applied. Story 2.5 review.
    /// </summary>
    public void ReleaseProjectDeleteUnanswered() =>
        _lastProjectDeleteHold?.Released.TrySetResult(new TaskEditOutcome(0, string.Empty, Capability: null, Abort: true));

    /// <summary>
    /// Holds the <b>next</b> Task move (<c>POST …/tasks/{taskId}/move</c>) until it is released, so a case can assert the
    /// move is optimistic: the card is already in its chosen slot and both totals have moved while it is out. Later moves
    /// are answered at once. Story 2.6 (written by its ATDD run).
    /// </summary>
    /// <returns>A task that completes when the held move has arrived and is waiting.</returns>
    public Task HoldTaskMoveAsync()
    {
        var hold = new TaskEditHold();

        _taskMoveHold = hold;
        _lastTaskMoveHold = hold;

        return hold.Arrived.Task;
    }

    /// <summary>
    /// Lets the held Task move go and applies it, as the Host admits it: <c>200</c> with the moved Task.
    /// </summary>
    public void ReleaseTaskMove() => _lastTaskMoveHold?.Released.TrySetResult(TaskEditOutcome.Admitted);

    /// <summary>
    /// Lets the held Task move go with a refusal instead: nothing is moved.
    /// </summary>
    /// <param name="status">The status code.</param>
    /// <param name="problemType">The problem type.</param>
    /// <param name="capability">The capability a <c>403</c> names, or <c>null</c>.</param>
    public void ReleaseTaskMoveRefused(int status, string problemType, string? capability = null) =>
        _lastTaskMoveHold?.Released.TrySetResult(new TaskEditOutcome(status, problemType, capability, Abort: false));

    /// <summary>
    /// Lets the held Task move go with no answer - the connection reset. Nothing is moved, so an identical resend under the
    /// same key is applied.
    /// </summary>
    public void ReleaseTaskMoveUnanswered() =>
        _lastTaskMoveHold?.Released.TrySetResult(new TaskEditOutcome(0, string.Empty, Capability: null, Abort: true));

    /// <summary>
    /// The move path for one Task: <c>TaskRoutes.TaskOf</c> then <c>/move</c> - story 2.6 Q1 (a)'s recommended route,
    /// stated here because <c>TaskRoutes.MoveOf</c> does not exist until the dev phase adds it. Retarget to the contract
    /// helper on activation.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="taskId">The Task.</param>
    /// <returns>The path.</returns>
    public static string MovePathOf(Guid spaceId, Guid taskId) => TaskRoutes.TaskOf(spaceId, taskId) + "/move";

    /// <summary>
    /// The Task with <paramref name="taskId"/> as the fake holds it now, with its column, in whichever Board.
    /// </summary>
    /// <param name="taskId">The Task.</param>
    /// <returns>Its column and the Task, or <c>null</c>.</returns>
    public (FakeColumn Column, FakeTask Task)? FindTask(Guid taskId)
    {
        foreach (var column in Boards.Values.SelectMany(board => board.Columns))
        {
            var task = column.Tasks.Find(candidate => candidate.Id == taskId);

            if (task is not null)
            {
                return (column, task);
            }
        }

        return null;
    }

    /// <summary>
    /// Whether any request named <paramref name="spaceId"/> in its path.
    /// </summary>
    public bool WasRequested(Guid spaceId) =>
        _requests.Any(request => request.Path.Contains(spaceId.ToString("D"), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Routes every API request the page makes, on any origin, to this fake.
    /// </summary>
    public Task InstallAsync(IPage page) => page.RouteAsync("**/api/v1/**", AnswerAsync);

    private async Task AnswerAsync(IRoute route)
    {
        var request = route.Request;
        var headers = await request.AllHeadersAsync().ConfigureAwait(false);
        var path = new Uri(request.Url).AbsolutePath;
        var seen = new FakeRequest(request.Method, path, headers, request.PostData);

        _requests.Enqueue(seen);

        var origin = headers.TryGetValue("origin", out var value) ? value : "*";

        var (status, body) = await DecideAsync(seen).ConfigureAwait(false);

        if (status == AbortConnection)
        {
            await route.AbortAsync("connectionreset").ConfigureAwait(false);
            return;
        }

        if (LoseNextProjectCreateAnswer && seen.Method == "POST" && TryProjects(seen.Path, out _))
        {
            LoseNextProjectCreateAnswer = false;
            await route.AbortAsync("connectionreset").ConfigureAwait(false);
            return;
        }

        await route.FulfillAsync(new RouteFulfillOptions
        {
            Status = status,
            ContentType = status >= 400 ? "application/problem+json" : "application/json",
            Body = body,
            Headers = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Access-Control-Allow-Origin"] = origin,
                ["Access-Control-Allow-Credentials"] = "true",
                ["Access-Control-Allow-Headers"] = $"content-type, {Antiforgery.HeaderName}, {Idempotency.HeaderName}",
                ["Access-Control-Allow-Methods"] = "GET, POST, PATCH, DELETE",
                ["Vary"] = "Origin",
            },
        }).ConfigureAwait(false);
    }

    private async Task<(int Status, string Body)> DecideAsync(FakeRequest request)
    {
        const int noContent = 204;

        switch (request.Method, request.Path)
        {
            case ("OPTIONS", _):
                return (noContent, string.Empty);

            case ("GET", var path) when path == SessionRoutes.AntiforgeryToken:
                return (200, JsonSerializer.Serialize(new AntiforgeryTokenResponse(IssuedToken), JsonOptions));

            case ("GET", var path) when path == SessionRoutes.Current:
                return SignedIn ? (noContent, string.Empty) : (401, Problem(ProblemTypes.AuthenticationFailed));

            case ("POST", var path) when path == SessionRoutes.Authenticate:
                SignedIn = true;
                _signInSeen.TrySetResult(request);
                return (noContent, string.Empty);

            case ("DELETE", var path) when path == SessionRoutes.Authenticate && RefuseFirstSignOut:
                RefuseFirstSignOut = false;
                return (400, Problem(ProblemTypes.AntiforgeryTokenMissing));

            case ("DELETE", var path) when path == SessionRoutes.Authenticate:
                SignedIn = false;
                _signOutSeen.TrySetResult(request);
                return (noContent, string.Empty);

            case ("GET", var path) when path == SpaceRoutes.Directory:
                return (200, JsonSerializer.Serialize(Switcher.Select(space => new SpaceIdentityResponse(space.Id, space.Name)), JsonOptions));

            // Story 1.8 - the Account-settings Token routes, answered with the contract types.
            case ("GET", var path) when path == ApiTokenRoutes.List:
                return (200, JsonSerializer.Serialize(ApiTokens.Select(TokenRow), JsonOptions));

            case ("POST", var path) when TryIssue(path, out var issueSpace):
                return Issue(issueSpace, request.Body);

            case ("DELETE", var path) when path.StartsWith(ApiTokenRoutes.List + "/", StringComparison.Ordinal)
                && Guid.TryParse(path[(ApiTokenRoutes.List.Length + 1)..], out var revoked):
                _ = ApiTokens.Remove(ApiTokens.FirstOrDefault(token => token.Id == revoked) ?? FakeApiToken.None);
                return (noContent, string.Empty);

            // Story 2.4 - the Space's Labels: list, define, delete.
            case ("GET", var path) when TryLabels(path, out var labelSpace):
                return (200, JsonSerializer.Serialize(LabelsIn(labelSpace), JsonOptions));

            case ("POST", var path) when TryLabels(path, out var defineSpace):
                return await DefineLabelAsync(defineSpace, request.Body).ConfigureAwait(false);

            case ("DELETE", var path) when TryLabel(path, out var deleteSpace, out var deleted):
                return DeleteLabel(deleteSpace, deleted);

            // Story 2.3 - one Task, read and edited by id.
            case ("GET", var path) when TryTask(path, out var readSpace, out var readTask):
                return ViewTask(readSpace, readTask);

            case ("PATCH", var path) when TryTask(path, out var editSpace, out var editTask):
                return await EditTaskAsync(editSpace, editTask, request).ConfigureAwait(false);

            // Story 2.5 - a Task and a Project deleted by id, not optimistically: held, refused or applied.
            case ("DELETE", var path) when TryTask(path, out var deleteTaskSpace, out var deletedTask):
                return await DeleteTaskAsync(deleteTaskSpace, deletedTask, request).ConfigureAwait(false);

            case ("DELETE", var path) when TryProject(path, out var deleteProjectSpace, out var deletedProject):
                return await DeleteProjectAsync(deleteProjectSpace, deletedProject, request).ConfigureAwait(false);

            // Story 2.6 - a Task moved to a Status and a position by its neighbour, optimistically: held, refused or applied.
            case ("POST", var path) when TryTaskMove(path, out var moveSpace, out var movedTask):
                return await MoveTaskAsync(moveSpace, movedTask, request).ConfigureAwait(false);

            // Story 2.2 - the Board and Task routes.
            case ("GET", var path) when TryUnderProject(path, "board", out var boardSpace, out var boardProject):
                return await BoardAsync(boardSpace, boardProject).ConfigureAwait(false);

            case ("POST", var path) when TryUnderProject(path, "tasks", out var taskSpace, out var taskProject):
                return CreateTask(taskSpace, taskProject, request);

            // Story 2.1 - the Project routes.
            case ("GET", var path) when TryProjects(path, out var listSpace):
                return await ListProjectsAsync(listSpace).ConfigureAwait(false);

            case ("POST", var path) when TryProjects(path, out var createSpace):
                return CreateProject(createSpace, request);

            case ("PATCH", var path) when TryProject(path, out var renameSpace, out var renamed):
                if (_heldRename is { } heldRename)
                {
                    _arrivedRename?.TrySetResult();
                    await heldRename.Task.ConfigureAwait(false);
                    _heldRename = null;
                }

                return RenameProject(renameSpace, renamed, request.Body);

            case ("GET", var path) when TryActiveSpace(path, out var spaceId):
                if (_held.TryGetValue(spaceId, out var held))
                {
                    if (_arrived.TryRemove(spaceId, out var arrived))
                    {
                        arrived.TrySetResult();
                    }

                    await held.Task.ConfigureAwait(false);
                }

                var listed = Switcher.FirstOrDefault(space => space.Id == spaceId);

                return Roles.TryGetValue(spaceId, out var role) && listed.Id == spaceId
                    ? (200, JsonSerializer.Serialize(new ActiveSpaceResponse(spaceId, listed.Name, role), JsonOptions))
                    : (404, Problem(ProblemTypes.NotAvailable));

            default:
                return (404, Problem(ProblemTypes.NotAvailable));
        }
    }

    private (int Status, string Body) Issue(Guid spaceId, string? body)
    {
        var listed = Switcher.FirstOrDefault(space => space.Id == spaceId);

        if (listed.Id != spaceId)
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        var name = (JsonSerializer.Deserialize<IssueApiTokenRequest>(body ?? "{}", JsonOptions)?.Name ?? string.Empty).Trim();

        // The Host's Q8 rule, as the Host answers it.
        if (name.Length is < 1 or > 100)
        {
            return (400, Problem(ProblemTypes.InvalidApiToken));
        }

        var token = new FakeApiToken(Guid.NewGuid(), name, spaceId, listed.Name, DateTimeOffset.UtcNow);
        ApiTokens.Add(token);

        var issued = new IssuedApiTokenResponse(
            token.Id,
            token.Name,
            new SpaceIdentityResponse(token.SpaceId, token.SpaceName),
            token.CreatedAt,
            IssuedSecret);

        return (201, JsonSerializer.Serialize(issued, JsonOptions));
    }

    private async Task<(int Status, string Body)> ListProjectsAsync(Guid spaceId)
    {
        if (_heldProjects.TryGetValue(spaceId, out var held))
        {
            if (_arrivedProjects.TryRemove(spaceId, out var arrived))
            {
                arrived.TrySetResult();
            }

            await held.Task.ConfigureAwait(false);
        }

        return Projects.TryGetValue(spaceId, out var projects)
            ? (200, JsonSerializer.Serialize(projects.OrderBy(project => project.Name, StringComparer.Ordinal).ThenBy(project => project.Id).Select(project => Counted(spaceId, project)), JsonOptions))
            : (200, "[]");
    }

    /// <summary>
    /// The Project as the listing serves it: with its true Task count when <see cref="ServeProjectTaskCounts"/> is on
    /// (story 2.5 Q2 (a)), otherwise exactly as stored.
    /// </summary>
    private FakeProject Counted(Guid spaceId, FakeProject project)
    {
        if (!ServeProjectTaskCounts)
        {
            return project;
        }

        var count = Boards.TryGetValue((spaceId, project.Id), out var board) ? board.Columns.Sum(column => column.Total) : 0;

        return project with { TaskCount = count };
    }

    /// <summary>
    /// The Host's Task deletion as story 2.5 specifies it: a key already applied replays <c>204</c>; a held deletion
    /// waits for its release (applied, refused or unanswered); a queued refusal applies nothing; otherwise an absent Task
    /// is the not-found and a present one leaves its column, whose true total drops by one.
    /// </summary>
    private async Task<(int Status, string Body)> DeleteTaskAsync(Guid spaceId, Guid taskId, FakeRequest request)
    {
        var key = request.Header(Idempotency.HeaderName);

        if (key is not null && _deletedByKey.ContainsKey(key))
        {
            return (204, string.Empty);
        }

        var outcome = await DeleteOutcomeAsync(() => Interlocked.Exchange(ref _taskDeleteHold, null), () =>
        {
            var refusal = NextTaskDeleteRefusal;

            NextTaskDeleteRefusal = null;
            return refusal;
        }).ConfigureAwait(false);

        if (outcome.Abort)
        {
            return (AbortConnection, string.Empty);
        }

        if (outcome.Status != 204)
        {
            return (outcome.Status, JsonSerializer.Serialize(new ProblemResponse(outcome.ProblemType, Errors: null, Capability: outcome.Capability), JsonOptions));
        }

        if (FindTaskIn(spaceId, taskId) is not { } found)
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        found.Column.Tasks.RemoveAll(task => task.Id == taskId);
        found.Column.Total--;

        if (key is not null)
        {
            _deletedByKey[key] = request.Path;
        }

        return (204, string.Empty);
    }

    /// <summary>
    /// The Host's Project deletion as story 2.5 specifies it: as <see cref="DeleteTaskAsync"/>, and a present Project
    /// leaves the listing and takes its Board - every Task in it - with it.
    /// </summary>
    private async Task<(int Status, string Body)> DeleteProjectAsync(Guid spaceId, Guid projectId, FakeRequest request)
    {
        var key = request.Header(Idempotency.HeaderName);

        if (key is not null && _deletedByKey.ContainsKey(key))
        {
            return (204, string.Empty);
        }

        var outcome = await DeleteOutcomeAsync(() => Interlocked.Exchange(ref _projectDeleteHold, null), () =>
        {
            var refusal = NextProjectDeleteRefusal;

            NextProjectDeleteRefusal = null;
            return refusal;
        }).ConfigureAwait(false);

        if (outcome.Abort)
        {
            return (AbortConnection, string.Empty);
        }

        if (outcome.Status != 204)
        {
            return (outcome.Status, JsonSerializer.Serialize(new ProblemResponse(outcome.ProblemType, Errors: null, Capability: outcome.Capability), JsonOptions));
        }

        var projects = Projects.TryGetValue(spaceId, out var held) ? held : [];

        if (projects.RemoveAll(project => project.Id == projectId) == 0)
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        _ = Boards.Remove((spaceId, projectId));

        if (key is not null)
        {
            _deletedByKey[key] = request.Path;
        }

        return (204, string.Empty);
    }

    /// <summary>
    /// How a deletion is answered: the armed hold's release if one is armed, else a queued refusal, else applied.
    /// </summary>
    private static async Task<TaskEditOutcome> DeleteOutcomeAsync(Func<DeleteHold?> takeHold, Func<(int Status, string ProblemType, string? Capability)?> takeRefusal)
    {
        if (takeHold() is { } hold)
        {
            hold.Arrived.TrySetResult();

            return await hold.Released.Task.ConfigureAwait(false);
        }

        return takeRefusal() is { } refusal
            ? new TaskEditOutcome(refusal.Status, refusal.ProblemType, refusal.Capability, Abort: false)
            : TaskEditOutcome.Deleted;
    }

    /// <summary>
    /// The Host's Task move as story 2.6 recommends it (Q1 (a) a Session-only <c>POST …/move</c>, Q2 (a)
    /// <c>{statusId, afterTaskId}</c> with <c>null</c> for the top, Q3 (a) <c>409 task-position-unavailable</c>, Q18 a move
    /// to where the Task already is answers <c>200</c> unchanged): a key already applied replays its answer, and the same
    /// key on another body is <c>422 idempotency-key-reused</c>; a held move waits for its release; a queued refusal moves
    /// nothing; otherwise an absent Task is the not-found, a Status outside the Board is <c>400 invalid-task-status</c>, a
    /// <c>positionKey</c> member is <c>400 invalid-task-edit</c>, a neighbour that is not a Task of the destination column
    /// (or is the moved Task) is the Q3 refusal, and a present Task leaves its column for the slot after its neighbour,
    /// both totals following. Only the moved Task's key is written - a key strictly between its new neighbours - so every
    /// sibling's key is unchanged, as AC3 requires of the Host.
    /// </summary>
    private async Task<(int Status, string Body)> MoveTaskAsync(Guid spaceId, Guid taskId, FakeRequest request)
    {
        var key = request.Header(Idempotency.HeaderName);
        var body = request.Body ?? string.Empty;

        if (key is not null && _movedByKey.TryGetValue(key, out var replayed))
        {
            return string.Equals(replayed.Path, request.Path, StringComparison.Ordinal) && string.Equals(replayed.Body, body, StringComparison.Ordinal)
                ? (200, replayed.Answer)
                : (422, Problem(ProblemTypes.IdempotencyKeyReused));
        }

        var outcome = TaskEditOutcome.Admitted;

        if (Interlocked.Exchange(ref _taskMoveHold, null) is { } hold)
        {
            hold.Arrived.TrySetResult();
            outcome = await hold.Released.Task.ConfigureAwait(false);
        }
        else if (NextTaskMoveRefusal is { } refusal)
        {
            NextTaskMoveRefusal = null;
            outcome = new TaskEditOutcome(refusal.Status, refusal.ProblemType, refusal.Capability, Abort: false);
        }
        else
        {
            // Unheld and unrefused: applied below.
        }

        if (outcome.Abort)
        {
            return (AbortConnection, string.Empty);
        }

        if (outcome.Status != 200)
        {
            return (outcome.Status, JsonSerializer.Serialize(new ProblemResponse(outcome.ProblemType, Errors: null, Capability: outcome.Capability), JsonOptions));
        }

        if (FindTaskIn(spaceId, taskId) is not { } found)
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        var answer = Move(found.Board, found.Column, found.Task, body);

        if (answer.Status == 200 && key is not null)
        {
            _movedByKey[key] = (request.Path, body, answer.Body);
        }

        return answer;
    }

    private static (int Status, string Body) Move(FakeBoard board, FakeColumn column, FakeTask task, string body)
    {
        var (refusal, destination, afterTaskId) = ReadMove(board, body);

        if (refusal is not null || destination is null)
        {
            return (400, Problem(refusal ?? ProblemTypes.InvalidTaskEdit));
        }

        if (afterTaskId == task.Id || (afterTaskId is { } neighbour && !destination.Tasks.Exists(candidate => candidate.Id == neighbour)))
        {
            return (409, Problem(TaskPositionUnavailableType));
        }

        var current = column.Tasks.FindIndex(candidate => candidate.Id == task.Id);
        Guid? predecessor = current > 0 ? column.Tasks[current - 1].Id : null;

        // Q18: already there - nothing written, the Task answered unchanged.
        if (destination == column && predecessor == afterTaskId)
        {
            return (200, JsonSerializer.Serialize(task, JsonOptions));
        }

        return (200, JsonSerializer.Serialize(Place(column, current, destination, task, afterTaskId), JsonOptions));
    }

    /// <summary>
    /// A move body read as the Host reads it: unreadable, not an object, carrying a <c>positionKey</c> member, or an
    /// <c>afterTaskId</c> that is not a GUID is <c>invalid-task-edit</c>; a <c>statusId</c> outside the Board is
    /// <c>invalid-task-status</c>; <c>afterTaskId</c> absent or <c>null</c> is the top.
    /// </summary>
    private static (string? Refusal, FakeColumn? Destination, Guid? AfterTaskId) ReadMove(FakeBoard board, string body)
    {
        JsonElement members;

        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrEmpty(body) ? "{}" : body);

            members = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return (ProblemTypes.InvalidTaskEdit, null, null);
        }

        if (members.ValueKind != JsonValueKind.Object || members.TryGetProperty("positionKey", out _))
        {
            return (ProblemTypes.InvalidTaskEdit, null, null);
        }

        if (!members.TryGetProperty("statusId", out var statusValue) || ColumnOf(board, statusValue) is not { } destination)
        {
            return (ProblemTypes.InvalidTaskStatus, null, null);
        }

        if (!members.TryGetProperty("afterTaskId", out var afterValue) || afterValue.ValueKind == JsonValueKind.Null)
        {
            return (null, destination, null);
        }

        if (afterValue.ValueKind == JsonValueKind.String && afterValue.TryGetGuid(out var after))
        {
            return (null, destination, after);
        }

        return (ProblemTypes.InvalidTaskEdit, null, null);
    }

    /// <summary>
    /// Takes the Task out of its column and places it after <paramref name="afterTaskId"/> (or at the top) in
    /// <paramref name="destination"/>, with a key strictly between its new neighbours; both totals follow.
    /// </summary>
    private static FakeTask Place(FakeColumn column, int current, FakeColumn destination, FakeTask task, Guid? afterTaskId)
    {
        column.Tasks.RemoveAt(current);
        column.Total--;

        var index = afterTaskId is { } placedAfter ? destination.Tasks.FindIndex(candidate => candidate.Id == placedAfter) + 1 : 0;
        var lower = index > 0 ? destination.Tasks[index - 1].PositionKey : null;
        var upper = index < destination.Tasks.Count ? destination.Tasks[index].PositionKey : null;
        var moved = task with { StatusId = destination.StatusId, PositionKey = KeyBetween(lower, upper) };

        destination.Tasks.Insert(index, moved);
        destination.Total++;

        return moved;
    }

    /// <summary>
    /// A key that sorts strictly between <paramref name="lower"/> and <paramref name="upper"/> under
    /// <see cref="StringComparer.Ordinal"/>, in the product's base-62 alphabet - a midpoint, with no jitter. The fake's own
    /// stand-in for <c>PositionKey.Between</c>, which this suite cannot see (it references <c>Yello.Contracts</c> only). An
    /// out-of-order pair (a Status edit's appended <c>z</c> key) is treated as "after the last".
    /// </summary>
    private static string KeyBetween(string? lower, string? upper)
    {
        const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        var low = lower ?? string.Empty;
        var free = upper is null || (lower is not null && string.CompareOrdinal(lower, upper) >= 0);
        var high = upper ?? string.Empty;
        var key = new StringBuilder();

        var position = 0;

        while (true)
        {
            var below = position < low.Length ? alphabet.IndexOf(low[position], StringComparison.Ordinal) : 0;
            var bounded = position < high.Length ? alphabet.IndexOf(high[position], StringComparison.Ordinal) : 0;
            var above = free ? alphabet.Length : bounded;

            if (above - below > 1)
            {
                key.Append(alphabet[(below + above) / 2]);
                return key.ToString();
            }

            key.Append(alphabet[below]);

            if (above - below == 1)
            {
                free = true;
            }

            position++;
        }
    }

    /// <summary>
    /// Whether <paramref name="path"/> is <c>/api/v1/spaces/{s}/tasks/{t}/move</c> - <see cref="MovePathOf"/>.
    /// </summary>
    private static bool TryTaskMove(string path, out Guid spaceId, out Guid taskId)
    {
        const string leaf = "/move";

        spaceId = Guid.Empty;
        taskId = Guid.Empty;

        return path.EndsWith(leaf, StringComparison.Ordinal)
            && TryTask(path[..^leaf.Length], out spaceId, out taskId)
            && path == MovePathOf(spaceId, taskId);
    }

    private (int Status, string Body) CreateProject(Guid spaceId, FakeRequest request)
    {
        var key = request.Header(Idempotency.HeaderName);

        if (key is not null && _createdByKey.TryGetValue(key, out var replayed))
        {
            return (201, replayed);
        }

        var body = request.Body;

        if (ProjectBoundReached)
        {
            return (409, JsonSerializer.Serialize(new ProblemResponse(ProblemTypes.BoundReached, Errors: null, Bound: "projects-per-space", Limit: 50), JsonOptions));
        }

        if (!TryProjectName(body, out var name))
        {
            return (400, Problem(ProblemTypes.InvalidProjectName));
        }

        var project = new FakeProject(Guid.NewGuid(), name, DateTimeOffset.UtcNow);

        if (!Projects.TryGetValue(spaceId, out var projects))
        {
            projects = [];
            Projects[spaceId] = projects;
        }

        projects.Add(project);

        var created = JsonSerializer.Serialize(project, JsonOptions);

        if (key is not null)
        {
            _createdByKey[key] = created;
        }

        return (201, created);
    }

    private (int Status, string Body) RenameProject(Guid spaceId, Guid projectId, string? body)
    {
        var projects = Projects.TryGetValue(spaceId, out var held) ? held : [];
        var index = projects.FindIndex(project => project.Id == projectId);

        if (index < 0)
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        if (!TryProjectName(body, out var name))
        {
            return (400, Problem(ProblemTypes.InvalidProjectName));
        }

        projects[index] = projects[index] with { Name = name };

        return (200, JsonSerializer.Serialize(projects[index], JsonOptions));
    }

    private async Task<(int Status, string Body)> BoardAsync(Guid spaceId, Guid projectId)
    {
        if (_heldBoards.TryGetValue(projectId, out var held))
        {
            if (_arrivedBoards.TryRemove(projectId, out var arrived))
            {
                arrived.TrySetResult();
            }

            await held.Task.ConfigureAwait(false);
        }

        return Boards.TryGetValue((spaceId, projectId), out var board)
            ? (200, JsonSerializer.Serialize(board, JsonOptions))
            : (404, Problem(ProblemTypes.NotAvailable));
    }

    /// <summary>
    /// The Host's Task creation as story 2.2 specifies it: the key replays first, then the bound, then the
    /// title rule (Q3 (a): trimmed, 1-200), then the Task is appended to the first column, whose total grows
    /// by one.
    /// </summary>
    private (int Status, string Body) CreateTask(Guid spaceId, Guid projectId, FakeRequest request)
    {
        var key = request.Header(Idempotency.HeaderName);

        if (key is not null && _createdTaskByKey.TryGetValue(key, out var replayed))
        {
            return (201, replayed);
        }

        if (!Boards.TryGetValue((spaceId, projectId), out var board))
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        if (NextTaskCreateRefusal is { } refusal)
        {
            NextTaskCreateRefusal = null;
            return (refusal.Status, Problem(refusal.ProblemType));
        }

        if (TaskBoundReached)
        {
            return (409, JsonSerializer.Serialize(new ProblemResponse(ProblemTypes.BoundReached, Errors: null, Bound: "tasks-per-project", Limit: 5000), JsonOptions));
        }

        using var document = JsonDocument.Parse(string.IsNullOrEmpty(request.Body) ? "{}" : request.Body);
        var title = (document.RootElement.TryGetProperty("title", out var value) ? value.GetString() ?? string.Empty : string.Empty).Trim();

        if (title.Length is < 1 or > 200)
        {
            return (400, Problem(ProblemTypes.InvalidTaskTitle));
        }

        var first = board.Columns[0];
        var task = new FakeTask(Guid.NewGuid(), title, first.StatusId, $"z{first.Tasks.Count:D5}", DateTimeOffset.UtcNow);

        first.Tasks.Add(task);
        first.Total++;

        var created = JsonSerializer.Serialize(task, JsonOptions);

        if (key is not null)
        {
            _createdTaskByKey[key] = created;
        }

        return (201, created);
    }

    private (int Status, string Body) ViewTask(Guid spaceId, Guid taskId) =>
        FindTaskIn(spaceId, taskId) is { } found
            ? (200, JsonSerializer.Serialize(found.Task, JsonOptions))
            : (404, Problem(ProblemTypes.NotAvailable));

    /// <summary>
    /// The Host's Task edit as story 2.3 recommends it (Q1 (a) merge-patch, Q3 (a) append to the new column,
    /// Q4 (a) UTC): a held edit waits for its release; a refusal applies nothing; otherwise each member sent is
    /// applied - the title trimmed and held to 1-200, the Status held to the Board's columns (the effective set),
    /// the due date normalised to UTC or cleared by <c>null</c> - and a Status change moves the Task to the end
    /// of its new column, both totals following.
    /// </summary>
    private async Task<(int Status, string Body)> EditTaskAsync(Guid spaceId, Guid taskId, FakeRequest request)
    {
        var outcome = TaskEditOutcome.Admitted;

        if (Interlocked.Exchange(ref _taskEditHold, null) is { } hold)
        {
            hold.Arrived.TrySetResult();
            outcome = await hold.Released.Task.ConfigureAwait(false);
        }
        else if (NextTaskEditRefusal is { } refusal)
        {
            NextTaskEditRefusal = null;
            outcome = new TaskEditOutcome(refusal.Status, refusal.ProblemType, refusal.Capability, Abort: false);
        }
        else
        {
            // Unheld and unrefused: applied below.
        }

        if (outcome.Abort)
        {
            return (AbortConnection, string.Empty);
        }

        if (outcome.Status != 200)
        {
            return (outcome.Status, JsonSerializer.Serialize(new ProblemResponse(outcome.ProblemType, Errors: null, Capability: outcome.Capability), JsonOptions));
        }

        if (FindTaskIn(spaceId, taskId) is not { } found)
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        return Apply(found.Board, found.Column, found.Task, request.Body, LabelsIn(spaceId));
    }

    private static (int Status, string Body) Apply(FakeBoard board, FakeColumn column, FakeTask task, string? body, IReadOnlyList<FakeLabel> spaceLabels)
    {
        using var document = JsonDocument.Parse(string.IsNullOrEmpty(body) ? "{}" : body);
        var members = document.RootElement;
        var edited = task;

        if (members.TryGetProperty("title", out var titleValue))
        {
            if (!TryTitle(titleValue, out var title))
            {
                return (400, Problem(ProblemTypes.InvalidTaskTitle));
            }

            edited = edited with { Title = title };
        }

        if (members.TryGetProperty("dueDate", out var dueValue))
        {
            edited = edited with { DueDate = dueValue.ValueKind == JsonValueKind.Null ? null : dueValue.GetDateTimeOffset().ToUniversalTime() };
        }

        // Story 2.4 Q1 (a): labelIds is the whole set; not an array is the edit's own refusal, and an id that is not a
        // GUID, not one of the Space's Labels, or repeated is invalid-task-labels. Served in server order (Q12).
        if (members.TryGetProperty("labelIds", out var labelsValue))
        {
            if (LabelsRefusal(labelsValue, spaceLabels) is { } refusal)
            {
                return (400, Problem(refusal));
            }

            var sent = labelsValue.EnumerateArray().Select(id => id.GetGuid()).ToList();

            edited = edited with { LabelIds = [.. spaceLabels.Select(label => label.Id).Where(sent.Contains)] };
        }

        var destination = column;

        if (members.TryGetProperty("statusId", out var statusValue))
        {
            if (ColumnOf(board, statusValue) is not { } target)
            {
                return (400, Problem(ProblemTypes.InvalidTaskStatus));
            }

            destination = target;
        }

        var index = column.Tasks.FindIndex(candidate => candidate.Id == task.Id);

        if (destination == column)
        {
            column.Tasks[index] = edited;
        }
        else
        {
            column.Tasks.RemoveAt(index);
            column.Total--;
            edited = edited with { StatusId = destination.StatusId, PositionKey = $"z{destination.Tasks.Count:D5}" };
            destination.Tasks.Add(edited);
            destination.Total++;
        }

        return (200, JsonSerializer.Serialize(edited, JsonOptions));
    }

    /// <summary>
    /// The refusal a <c>labelIds</c> member earns, or <c>null</c>: not an array is the edit's own
    /// <c>invalid-task-edit</c>; an element that is not a GUID, not one of the Space's Labels, or repeated is
    /// <c>invalid-task-labels</c>. Story 2.4.
    /// </summary>
    private static string? LabelsRefusal(JsonElement value, IReadOnlyList<FakeLabel> spaceLabels)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return ProblemTypes.InvalidTaskEdit;
        }

        var sent = value.EnumerateArray()
            .Select(id => id.ValueKind == JsonValueKind.String && id.TryGetGuid(out var labelId) ? labelId : Guid.Empty)
            .ToList();

        var valid = !sent.Contains(Guid.Empty)
            && sent.Distinct().Count() == sent.Count
            && sent.TrueForAll(id => spaceLabels.Any(label => label.Id == id));

        return valid ? null : ProblemTypes.InvalidTaskLabels;
    }

    /// <summary>
    /// The Host's title rule for an edit: a string, trimmed, 1-200 characters; <c>null</c> is refused (Q1 (a)).
    /// </summary>
    private static bool TryTitle(JsonElement value, out string title)
    {
        title = (value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty).Trim();

        return title.Length is >= 1 and <= 200;
    }

    /// <summary>
    /// The Board column for a sent Status id - the effective set - or <c>null</c> for anything else.
    /// </summary>
    private static FakeColumn? ColumnOf(FakeBoard board, JsonElement value) =>
        value.ValueKind == JsonValueKind.String && value.TryGetGuid(out var statusId)
            ? board.Columns.FirstOrDefault(candidate => candidate.StatusId == statusId)
            : null;

    private (FakeBoard Board, FakeColumn Column, FakeTask Task)? FindTaskIn(Guid spaceId, Guid taskId)
    {
        foreach (var board in Boards.Where(entry => entry.Key.SpaceId == spaceId).Select(entry => entry.Value))
        {
            foreach (var column in board.Columns)
            {
                var task = column.Tasks.Find(candidate => candidate.Id == taskId);

                if (task is not null)
                {
                    return (board, column, task);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The Space's Labels in server order: name (case-insensitively, as the database's collation compares), then id.
    /// </summary>
    private List<FakeLabel> LabelsIn(Guid spaceId) =>
        Labels.TryGetValue(spaceId, out var labels)
            ? [.. labels.OrderBy(label => label.Name, StringComparer.OrdinalIgnoreCase).ThenBy(label => label.Id)]
            : [];

    /// <summary>
    /// The Host's Label definition as story 2.4 recommends it: a held definition waits for its release; then the name
    /// rule (Q2: trimmed, 1-50), the palette (Q3) and uniqueness per Space ignoring case (Q2); then the Label is added.
    /// </summary>
    private async Task<(int Status, string Body)> DefineLabelAsync(Guid spaceId, string? body)
    {
        if (_heldLabelCreate is { } held)
        {
            _arrivedLabelCreate?.TrySetResult();
            await held.Task.ConfigureAwait(false);
            _heldLabelCreate = null;
        }

        using var document = JsonDocument.Parse(string.IsNullOrEmpty(body) ? "{}" : body);
        var root = document.RootElement;
        var name = (root.TryGetProperty("name", out var nameValue) && nameValue.ValueKind == JsonValueKind.String ? nameValue.GetString() ?? string.Empty : string.Empty).Trim();
        var colour = root.TryGetProperty("colour", out var colourValue) && colourValue.ValueKind == JsonValueKind.String ? colourValue.GetString() ?? string.Empty : string.Empty;

        if (name.Length < 1 || name.Length > FakeLabelNameMaxLength)
        {
            return (400, Problem(ProblemTypes.InvalidLabelName));
        }

        if (!LabelColours.Keys.Contains(colour, StringComparer.Ordinal))
        {
            return (400, Problem(ProblemTypes.InvalidLabelColour));
        }

        if (!Labels.TryGetValue(spaceId, out var labels))
        {
            labels = [];
            Labels[spaceId] = labels;
        }

        if (labels.Exists(label => string.Equals(label.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return (409, Problem(ProblemTypes.LabelNameTaken));
        }

        var created = new FakeLabel(Guid.NewGuid(), name, colour);

        labels.Add(created);

        return (201, JsonSerializer.Serialize(created, JsonOptions));
    }

    /// <summary>
    /// The Host's Label deletion (story 2.4 AC3, Q11): absent is the not-found; applied to any Task in the Space is
    /// <c>409 label-in-use</c>; otherwise gone.
    /// </summary>
    private (int Status, string Body) DeleteLabel(Guid spaceId, Guid labelId)
    {
        var labels = Labels.TryGetValue(spaceId, out var held) ? held : [];
        var index = labels.FindIndex(label => label.Id == labelId);

        if (index < 0)
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        var inUse = Boards
            .Where(entry => entry.Key.SpaceId == spaceId)
            .SelectMany(entry => entry.Value.Columns)
            .SelectMany(column => column.Tasks)
            .Any(task => task.LabelIds?.Contains(labelId) == true);

        if (inUse)
        {
            return (409, Problem(ProblemTypes.LabelInUse));
        }

        labels.RemoveAt(index);

        return (204, string.Empty);
    }

    /// <summary>
    /// Whether <paramref name="path"/> is <c>/api/v1/spaces/{s}/labels</c> - <see cref="LabelRoutes.LabelsOf"/>.
    /// </summary>
    private static bool TryLabels(string path, out Guid spaceId)
    {
        var prefix = SpaceRoutes.Directory + "/";

        spaceId = Guid.Empty;

        return path.StartsWith(prefix, StringComparison.Ordinal)
            && Guid.TryParse(path[prefix.Length..].Split('/')[0], out spaceId)
            && path == LabelRoutes.LabelsOf(spaceId);
    }

    /// <summary>
    /// Whether <paramref name="path"/> is <c>/api/v1/spaces/{s}/labels/{l}</c> - <see cref="LabelRoutes.LabelOf"/>.
    /// </summary>
    private static bool TryLabel(string path, out Guid spaceId, out Guid labelId)
    {
        var segments = path.Split('/');

        labelId = Guid.Empty;

        return TryLabels(string.Join('/', segments[..^1]), out spaceId)
            && Guid.TryParse(segments[^1], out labelId)
            && path == LabelRoutes.LabelOf(spaceId, labelId);
    }

    private void ReleaseHeldEdit(TaskEditOutcome outcome) => _lastTaskEditHold?.Released.TrySetResult(outcome);

    /// <summary>
    /// Whether <paramref name="path"/> is <c>/api/v1/spaces/{s}/tasks/{t}</c> - <c>TaskRoutes.TaskOf</c>.
    /// </summary>
    private static bool TryTask(string path, out Guid spaceId, out Guid taskId)
    {
        var prefix = SpaceRoutes.Directory + "/";
        var segments = path.StartsWith(prefix, StringComparison.Ordinal) ? path[prefix.Length..].Split('/') : [];

        spaceId = Guid.Empty;
        taskId = Guid.Empty;

        return segments is [var space, "tasks", var task]
            && Guid.TryParse(space, out spaceId)
            && Guid.TryParse(task, out taskId)
            && path == TaskRoutes.TaskOf(spaceId, taskId);
    }

    /// <summary>
    /// Whether <paramref name="path"/> is <c>/api/v1/spaces/{s}/projects/{p}/{leaf}</c>.
    /// </summary>
    private static bool TryUnderProject(string path, string leaf, out Guid spaceId, out Guid projectId)
    {
        var segments = path.Split('/');

        spaceId = Guid.Empty;
        projectId = Guid.Empty;

        return segments.Length >= 2
            && string.Equals(segments[^1], leaf, StringComparison.Ordinal)
            && TryProject(string.Join('/', segments[..^1]), out spaceId, out projectId);
    }

    /// <summary>
    /// The Host's Q3 rule, as the Host answers it: trimmed, 1-100 characters.
    /// </summary>
    private static bool TryProjectName(string? body, out string name)
    {
        using var document = JsonDocument.Parse(string.IsNullOrEmpty(body) ? "{}" : body);

        name = (document.RootElement.TryGetProperty("name", out var value) ? value.GetString() ?? string.Empty : string.Empty).Trim();

        return name.Length is >= 1 and <= 100;
    }

    private static bool TryProjects(string path, out Guid spaceId)
    {
        var prefix = SpaceRoutes.Directory + "/";

        spaceId = Guid.Empty;

        return path.StartsWith(prefix, StringComparison.Ordinal)
            && Guid.TryParse(path[prefix.Length..].Split('/')[0], out spaceId)
            && path == ProjectRoutes.ListIn(spaceId);
    }

    private static bool TryProject(string path, out Guid spaceId, out Guid projectId)
    {
        var segments = path.Split('/');

        projectId = Guid.Empty;

        return TryProjects(string.Join('/', segments[..^1]), out spaceId)
            && Guid.TryParse(segments[^1], out projectId)
            && path == ProjectRoutes.ProjectOf(spaceId, projectId);
    }

    private static ApiTokenResponse TokenRow(FakeApiToken token) =>
        new(token.Id, token.Name, new SpaceIdentityResponse(token.SpaceId, token.SpaceName), token.CreatedAt);

    private static bool TryIssue(string path, out Guid spaceId)
    {
        var prefix = SpaceRoutes.Directory + "/";

        spaceId = Guid.Empty;

        return path.StartsWith(prefix, StringComparison.Ordinal)
            && Guid.TryParse(path[prefix.Length..].Split('/')[0], out spaceId)
            && path == ApiTokenRoutes.IssueIn(spaceId);
    }

    private static bool TryActiveSpace(string path, out Guid spaceId)
    {
        var prefix = SpaceRoutes.Directory + "/";

        spaceId = Guid.Empty;

        return path.StartsWith(prefix, StringComparison.Ordinal)
            && Guid.TryParse(path[prefix.Length..], out spaceId)
            && path == SpaceRoutes.ActiveOf(spaceId);
    }

    private static string Problem(string type) =>
        JsonSerializer.Serialize(new ProblemResponse(type, Errors: null), JsonOptions);
}

/// <summary>
/// How a held Task edit is answered. Story 2.3.
/// </summary>
/// <param name="Status">The status code: <c>200</c> applies the edit.</param>
/// <param name="ProblemType">A refusal's problem type.</param>
/// <param name="Capability">A <c>403</c>'s capability.</param>
/// <param name="Abort">Whether the connection is reset instead of answered.</param>
internal sealed record TaskEditOutcome(int Status, string ProblemType, string? Capability, bool Abort)
{
    /// <summary>
    /// Applied, as the Host admits it.
    /// </summary>
    public static TaskEditOutcome Admitted { get; } = new(200, string.Empty, Capability: null, Abort: false);

    /// <summary>
    /// A deletion applied, as the Host answers it: <c>204</c>. Story 2.5.
    /// </summary>
    public static TaskEditOutcome Deleted { get; } = new(204, string.Empty, Capability: null, Abort: false);
}

/// <summary>
/// One armed hold on the next Task or Project deletion. Story 2.5.
/// </summary>
internal sealed class DeleteHold
{
    /// <summary>
    /// Completes when the held deletion has arrived.
    /// </summary>
    public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completes with the answer the held deletion is given.
    /// </summary>
    public TaskCompletionSource<TaskEditOutcome> Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>
/// One armed hold on the next Task edit. Story 2.3.
/// </summary>
internal sealed class TaskEditHold
{
    /// <summary>
    /// Completes when the held edit has arrived.
    /// </summary>
    public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completes with the answer the held edit is given.
    /// </summary>
    public TaskCompletionSource<TaskEditOutcome> Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>
/// One request the fake answered.
/// </summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The path, without the query.</param>
/// <param name="Headers">Every header, lower-cased by the browser.</param>
/// <param name="Body">The request body, or <c>null</c>.</param>
internal sealed record FakeRequest(string Method, string Path, IReadOnlyDictionary<string, string> Headers, string? Body = null)
{
    /// <summary>
    /// A header's value by name, ignoring case, or <c>null</c>.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <returns>The value.</returns>
    public string? Header(string name) =>
        Headers.FirstOrDefault(header => string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
}

/// <summary>
/// One API Token as the fake's Account-settings list holds it. Story 1.8.
/// </summary>
/// <param name="Id">The Token's id.</param>
/// <param name="Name">Its name.</param>
/// <param name="SpaceId">The Space it is bound to.</param>
/// <param name="SpaceName">That Space's name.</param>
/// <param name="CreatedAt">When it was issued.</param>
internal sealed record FakeApiToken(Guid Id, string Name, Guid SpaceId, string SpaceName, DateTimeOffset CreatedAt)
{
    /// <summary>
    /// Matches no Token, for a revoke of an id that is not held.
    /// </summary>
    public static FakeApiToken None { get; } = new(Guid.Empty, string.Empty, Guid.Empty, string.Empty, DateTimeOffset.MinValue);
}

/// <summary>
/// One Project as the fake serves it - the shape of story 2.1 Task 4's <c>ProjectResponse</c>
/// (<c>{id, name, createdAt}</c>). Replace with the contract type on activation.
/// </summary>
/// <param name="Id">The Project's id.</param>
/// <param name="Name">Its name.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="TaskCount">
/// Its true Task count - story 2.5 Q2 (a)'s appended field (AD-19), filled by the listing only when
/// <see cref="FakeYelloApi.ServeProjectTaskCounts"/> is on. <c>null</c> is left out of the body entirely, so every case
/// written before 2.5 is served the bytes it always was.
/// </param>
internal sealed record FakeProject(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? TaskCount = null);

/// <summary>
/// One Project's Board as the fake serves it - the shape of story 2.2 Task 3's <c>BoardResponse</c>
/// (<c>{project, columns}</c>). Replace with the contract type on activation.
/// </summary>
/// <param name="Project">The Project.</param>
/// <param name="Columns">One column per Status of the effective set, in effective order.</param>
internal sealed record FakeBoard(FakeBoardProject Project, IReadOnlyList<FakeColumn> Columns);

/// <summary>
/// The Board's Project - <c>BoardProjectResponse</c> (<c>{id, name}</c>).
/// </summary>
/// <param name="Id">The Project's id.</param>
/// <param name="Name">Its name, as this Board answer states it.</param>
internal sealed record FakeBoardProject(Guid Id, string Name);

/// <summary>
/// One column - <c>BoardColumnResponse</c> (<c>{statusId, name, total, tasks}</c>). <see cref="Total"/> is set
/// independently of <see cref="Tasks"/>, as the Host counts it (AC5), so a case can serve fewer rows than the
/// total.
/// </summary>
internal sealed class FakeColumn(Guid statusId, string name, int total, List<FakeTask> tasks)
{
    /// <summary>
    /// The Status.
    /// </summary>
    public Guid StatusId { get; } = statusId;

    /// <summary>
    /// The Status name.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// The true total for the Status.
    /// </summary>
    public int Total { get; set; } = total;

    /// <summary>
    /// The Tasks served, by position key.
    /// </summary>
    public List<FakeTask> Tasks { get; } = tasks;
}

/// <summary>
/// One Task - <c>TaskResponse</c> (<c>{id, title, statusId, positionKey, createdAt}</c>).
/// </summary>
/// <param name="Id">The Task's id.</param>
/// <param name="Title">Its title.</param>
/// <param name="StatusId">Its Status.</param>
/// <param name="PositionKey">Its position key, readable and never writable.</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="DueDate">Its due date in UTC, or <c>null</c> - story 2.3 Task 4's added field (AD-19).</param>
/// <param name="LabelIds">
/// Its Labels in server order - story 2.4 Task 4's added field (AD-19). <c>null</c> is left out of the body entirely, so
/// every case written before 2.4 is served the bytes it always was; a Labels edit always sets it (<c>[]</c> when
/// cleared).
/// </param>
internal sealed record FakeTask(
    Guid Id,
    string Title,
    Guid StatusId,
    string PositionKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueDate = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<Guid>? LabelIds = null);

/// <summary>
/// One Label as the fake serves it - the shape of story 2.4 Task 4's <c>LabelResponse</c> (<c>{id, name, colour}</c>).
/// Replace with the contract type on activation.
/// </summary>
/// <param name="Id">The Label's id.</param>
/// <param name="Name">Its name.</param>
/// <param name="Colour">Its palette key (Q3).</param>
internal sealed record FakeLabel(Guid Id, string Name, string Colour);
