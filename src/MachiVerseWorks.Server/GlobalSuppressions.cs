using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Maintainability",
    "CA1512:Use ArgumentOutOfRangeException throw helper",
    Justification = "The explicit stable-ID guard keeps the Person inspection boundary aligned with other protocol ID validation paths.",
    Scope = "member",
    Target = "~M:MachiVerseWorks.Server.ClientConnection.SetInspectedPerson(System.UInt64)")]
