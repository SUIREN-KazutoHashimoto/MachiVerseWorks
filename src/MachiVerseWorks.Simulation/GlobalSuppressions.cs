using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "Population planner methods intentionally remain instance members to keep the SimulationWorld planning seam cohesive for upcoming policy extensions.",
    Scope = "member",
    Target = "~M:MachiVerseWorks.Simulation.SimulationWorld.SelectDesiredActivity(MachiVerseWorks.Simulation.Internal.PersonState,System.Int32)")]
[assembly: SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "Activity validation intentionally remains on the SimulationWorld population seam for policy extension without changing public construction flow.",
    Scope = "member",
    Target = "~M:MachiVerseWorks.Simulation.SimulationWorld.ValidateActivityWindow(MachiVerseWorks.Simulation.DailyActivityWindow,System.String)")]
