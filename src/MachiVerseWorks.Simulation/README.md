# MachiVerseWorks.Simulation

都市シミュレーションの正本となる C# class library です。

Phase 2 の最小 PoC では、固定 tick、deterministic seed、安定した Agent ID、cell-based spatial index、外部へ mutable state を露出しない snapshot 境界を提供します。

HTTP、WebSocket、ASP.NET Core などの通信層には依存させません。
