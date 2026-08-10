namespace Enigma.Backup.Core.Profiles;

/// <summary>
/// An input that is worked out again on every run: a base folder, and a glob matched against what is
/// inside it. Where a plain input names one file or folder, this names a rule — <c>C:\Logs</c> plus
/// <c>*.log</c> archives whatever the logs happen to be when the backup runs.
/// </summary>
/// <remarks>
/// <para>
/// The pattern is relative to <see cref="BaseDirectory"/> and uses <c>/</c> separators, with <c>\</c>
/// accepted and normalized as in a profile's exclude globs. The glob language is the exclude field's:
/// <c>*</c> matches within one path segment and <c>**</c> at any depth, so <c>*.log</c> matches the
/// logs directly under the base folder and <c>**/*.log</c> matches them at any depth.
/// </para>
/// <para>
/// One deliberate extension over strict file globbing: a pattern that matches a *directory's* name
/// takes that directory with everything in it, which is what a pattern such as <c>DirPrefix*</c> is
/// asking for. Exclude globs keep the strict semantics.
/// </para>
/// </remarks>
/// <param name="BaseDirectory">The absolute path of the folder the pattern is resolved against.</param>
/// <param name="Pattern">The glob, relative to the base folder.</param>
public sealed record PatternInput(string BaseDirectory, string Pattern);
