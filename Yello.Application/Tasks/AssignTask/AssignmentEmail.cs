using Yello.Application.Invitations;

namespace Yello.Application.Tasks.AssignTask;

/// <summary>
/// The assignment email's template: the one notification an Assignee receives when someone else gives them a Task.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its parameter list is its content contract.</b> It takes the assigner's display name, the Space's name, the Project's
/// name, the Task's title and the client's address - strings and a URI, nothing else. No Domain type can be passed in, so
/// nothing of another Space, no other Member and no email address can reach the email. A gate holds the signature.
/// </para>
/// <para>
/// <b>The link carries no id</b>: it opens the client, where the recipient signs in and picks the Space. An id in a link
/// that can be forwarded would tell its reader that a Space or Task exists.
/// </para>
/// <para>
/// <b>Plain text, English only for now</b>, terse and factual, with no exclamation marks, as the Invitation's.
/// </para>
/// </remarks>
public static class AssignmentEmail
{
    /// <summary>
    /// Renders the subject and the body.
    /// </summary>
    /// <param name="assignerName">The assigner's display name, read when the assignment was made - never their address.</param>
    /// <param name="spaceName">The Space the Task is in.</param>
    /// <param name="projectName">The Project the Task is in.</param>
    /// <param name="taskTitle">The Task's title.</param>
    /// <param name="link">The client's address, with no path naming a Space or Task.</param>
    /// <returns>The rendered email.</returns>
    public static RenderedEmail Render(string assignerName, string spaceName, string projectName, string taskTitle, Uri link)
    {
        ArgumentNullException.ThrowIfNull(link);

        var subject = $"{assignerName} assigned you a Task in {spaceName}";
        var body =
            $"{assignerName} assigned you \"{taskTitle}\" in the Project {projectName} in the Space {spaceName} on Yello.\n\nOpen Yello to see it: {link.OriginalString}\n";

        return new RenderedEmail(subject, body);
    }
}
