namespace MachiVerseWorks.Protocol;

public static class PersistentRegionalEvolutionProtocolChunker
{
    private const int MaximumItemsPerChunk = 128;

    public static IReadOnlyList<PersistentRegionalEvolutionSnapshotMessage> Split(
        PersistentRegionalEvolutionSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Settlements);
        ArgumentNullException.ThrowIfNull(message.Parcels);
        ArgumentNullException.ThrowIfNull(message.Buildings);
        ArgumentNullException.ThrowIfNull(message.ServiceCatchments);
        ArgumentNullException.ThrowIfNull(message.InfrastructureDemands);
        ArgumentNullException.ThrowIfNull(message.Relations);
        ArgumentNullException.ThrowIfNull(message.Events);
        ArgumentNullException.ThrowIfNull(message.CommutingFlows);
        ArgumentNullException.ThrowIfNull(message.FreightFlows);

        var chunks = new List<PersistentRegionalEvolutionSnapshotMessage>();
        var builder = new ChunkBuilder(message.CurrentYear, message.TickCount, message.IsFullSnapshot);
        foreach (var item in message.Settlements) builder.AddSettlement(item, chunks);
        foreach (var item in message.Parcels) builder.AddParcel(item, chunks);
        foreach (var item in message.Buildings) builder.AddBuilding(item, chunks);
        foreach (var item in message.ServiceCatchments) builder.AddServiceCatchment(item, chunks);
        foreach (var item in message.InfrastructureDemands) builder.AddInfrastructureDemand(item, chunks);
        foreach (var item in message.Relations) builder.AddRelation(item, chunks);
        foreach (var item in message.Events) builder.AddEvent(item, chunks);
        foreach (var item in message.CommutingFlows) builder.AddCommutingFlow(item, chunks);
        foreach (var item in message.FreightFlows) builder.AddFreightFlow(item, chunks);
        builder.Flush(chunks, allowEmpty: true);
        return chunks;
    }

    private sealed class ChunkBuilder(int currentYear, ulong tickCount, bool firstChunkIsFullSnapshot)
    {
        private readonly List<ProtocolSettlementEvolution> _settlements = [];
        private readonly List<ProtocolParcelEvolution> _parcels = [];
        private readonly List<ProtocolBuildingLifecycle> _buildings = [];
        private readonly List<ProtocolServiceCatchment> _serviceCatchments = [];
        private readonly List<ProtocolInfrastructureDemand> _infrastructureDemands = [];
        private readonly List<ProtocolRegionalRelation> _relations = [];
        private readonly List<ProtocolRegionalEvolutionEvent> _events = [];
        private readonly List<ProtocolRegionalCommutingFlow> _commutingFlows = [];
        private readonly List<ProtocolRegionalFreightFlow> _freightFlows = [];
        private int _itemCount;
        private bool _isFirstChunk = true;

        public void AddSettlement(ProtocolSettlementEvolution item, List<PersistentRegionalEvolutionSnapshotMessage> chunks)
        { EnsureCapacity(chunks); _settlements.Add(item); _itemCount++; }
        public void AddParcel(ProtocolParcelEvolution item, List<PersistentRegionalEvolutionSnapshotMessage> chunks)
        { EnsureCapacity(chunks); _parcels.Add(item); _itemCount++; }
        public void AddBuilding(ProtocolBuildingLifecycle item, List<PersistentRegionalEvolutionSnapshotMessage> chunks)
        { EnsureCapacity(chunks); _buildings.Add(item); _itemCount++; }
        public void AddServiceCatchment(ProtocolServiceCatchment item, List<PersistentRegionalEvolutionSnapshotMessage> chunks)
        { EnsureCapacity(chunks); _serviceCatchments.Add(item); _itemCount++; }
        public void AddInfrastructureDemand(ProtocolInfrastructureDemand item, List<PersistentRegionalEvolutionSnapshotMessage> chunks)
        { EnsureCapacity(chunks); _infrastructureDemands.Add(item); _itemCount++; }
        public void AddRelation(ProtocolRegionalRelation item, List<PersistentRegionalEvolutionSnapshotMessage> chunks)
        { EnsureCapacity(chunks); _relations.Add(item); _itemCount++; }
        public void AddEvent(ProtocolRegionalEvolutionEvent item, List<PersistentRegionalEvolutionSnapshotMessage> chunks)
        { EnsureCapacity(chunks); _events.Add(item); _itemCount++; }
        public void AddCommutingFlow(ProtocolRegionalCommutingFlow item, List<PersistentRegionalEvolutionSnapshotMessage> chunks)
        { EnsureCapacity(chunks); _commutingFlows.Add(item); _itemCount++; }
        public void AddFreightFlow(ProtocolRegionalFreightFlow item, List<PersistentRegionalEvolutionSnapshotMessage> chunks)
        { EnsureCapacity(chunks); _freightFlows.Add(item); _itemCount++; }

        public void Flush(List<PersistentRegionalEvolutionSnapshotMessage> chunks, bool allowEmpty = false)
        {
            if (_itemCount == 0 && (!allowEmpty || chunks.Count > 0)) return;
            chunks.Add(new PersistentRegionalEvolutionSnapshotMessage(
                currentYear,
                tickCount,
                _settlements.ToArray(),
                _parcels.ToArray(),
                _buildings.ToArray(),
                _serviceCatchments.ToArray(),
                _infrastructureDemands.ToArray(),
                _relations.ToArray(),
                _events.ToArray(),
                _commutingFlows.ToArray(),
                _freightFlows.ToArray(),
                _isFirstChunk && firstChunkIsFullSnapshot));
            _isFirstChunk = false;
            _settlements.Clear();
            _parcels.Clear();
            _buildings.Clear();
            _serviceCatchments.Clear();
            _infrastructureDemands.Clear();
            _relations.Clear();
            _events.Clear();
            _commutingFlows.Clear();
            _freightFlows.Clear();
            _itemCount = 0;
        }

        private void EnsureCapacity(List<PersistentRegionalEvolutionSnapshotMessage> chunks)
        {
            if (_itemCount < MaximumItemsPerChunk) return;
            Flush(chunks);
        }
    }
}
