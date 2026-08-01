using System.Collections.Generic;

namespace Enigma.Backup.Desktop.Services;

/// <summary>What to ask when a profile needs a name — for "Save as" and for "Rename" alike.</summary>
/// <param name="Title">The dialog's title.</param>
/// <param name="Prompt">The sentence above the text box.</param>
/// <param name="InitialName">The name the box starts with; may be empty.</param>
/// <param name="TakenNames">
/// Names the dialog must refuse, so a collision is caught while it can still be corrected rather
/// than after a profile has been overwritten. A rename leaves the profile's own current name out of
/// this, since renaming something to what it is already called is not a collision.
/// </param>
public sealed record ProfileNameRequest(
    string Title,
    string Prompt,
    string InitialName,
    IReadOnlyCollection<string> TakenNames);
