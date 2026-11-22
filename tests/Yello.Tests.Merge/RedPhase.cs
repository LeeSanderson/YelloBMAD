namespace Yello.Tests.Merge;

/// <summary>
/// Skip reasons for the cases that wait on a candidate. Each names the criterion the case proves.
/// </summary>
internal static class RedPhase
{
    public const string NoCandidate =
        "red: AC4 - no candidate behind Candidate.Strategy yet; the port is a stand-in (AR-40a, Q5)";

    public const string NoCandidateBudget =
        "red: AC4 - no candidate behind Candidate.Strategy yet; budgets are Q6 (a): 16 ms per remote change, 1 s per reconnection exchange, native";

    public const string NoCandidateProperty =
        "red: AC7 - no candidate behind Candidate.Strategy yet; budgets are Q10 (a): 200 sessions per PR run, 5,000 nightly";
}
