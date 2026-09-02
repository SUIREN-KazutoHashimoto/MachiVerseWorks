using System.Reflection;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ObservationGatewayBoundaryTests
{
    [TestMethod]
    public void GatewayRuntimeServicesDependOnObservationSourceInsteadOfSimulationRuntime()
    {
        Type[] gatewayTypes =
        [
            typeof(WebSocketSessionHandler),
            typeof(SnapshotPublishService),
            typeof(PopulationPublishService),
            typeof(EconomyPublishService),
            typeof(LogisticsPublishService),
            typeof(PowerPublishService),
            typeof(WaterSewerPublishService),
            typeof(GasPublishService),
            typeof(OpticalPublishService),
            typeof(RadioPublishService),
            typeof(WorldEnvironmentPublishService),
        ];

        foreach (var type in gatewayTypes)
        {
            var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsTrue(constructors.Length > 0, $"{type.Name} must have a constructor.");
            Assert.IsFalse(
                constructors.SelectMany(static constructor => constructor.GetParameters())
                    .Any(static parameter => parameter.ParameterType == typeof(SimulationRuntime)),
                $"{type.Name} must use IObservationSource rather than SimulationRuntime.");
            Assert.IsFalse(
                constructors.SelectMany(static constructor => constructor.GetParameters())
                    .Any(static parameter => parameter.ParameterType == typeof(AdminCommandQueue) || parameter.ParameterType == typeof(AdminCommandExecutorV2)),
                $"{type.Name} must not depend on the Administration mutation boundary.");
        }
    }

    [TestMethod]
    public void OnlySimulationObservationSourceBridgesGatewayToSimulationRuntime()
    {
        var constructor = typeof(SimulationObservationSource)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single();

        CollectionAssert.AreEqual(
            new[] { typeof(SimulationRuntime) },
            constructor.GetParameters().Select(static parameter => parameter.ParameterType).ToArray());

        foreach (var method in typeof(IObservationSource).GetMethods())
        {
            Assert.AreNotEqual(typeof(SimulationWorld), method.ReturnType);
            Assert.IsFalse(method.GetParameters().Any(static parameter => parameter.ParameterType == typeof(SimulationWorld)));
        }
    }

    [TestMethod]
    public void GatewayRegistrationDoesNotRegisterAdministrationServices()
    {
        var services = new ServiceCollection();
        services.AddObservationGateway();

        Assert.IsTrue(services.Any(static descriptor => descriptor.ServiceType == typeof(IObservationSource)));
        Assert.IsTrue(services.Any(static descriptor => descriptor.ServiceType == typeof(WebSocketSessionHandler)));
        Assert.IsFalse(services.Any(static descriptor => descriptor.ServiceType == typeof(AdminCommandQueue)));
        Assert.IsFalse(services.Any(static descriptor => descriptor.ServiceType == typeof(AdminCommandExecutorV2)));
    }

    [TestMethod]
    public void ConnectionObservationStateDoesNotOwnSimulationWorld()
    {
        var connectionFields = typeof(ClientConnection).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsFalse(connectionFields.Any(static field => field.FieldType == typeof(SimulationWorld)));

        var subscriptionProperties = typeof(ClientSubscriptionState).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        Assert.IsFalse(subscriptionProperties.Any(static property => property.PropertyType == typeof(SimulationWorld)));
    }

    [TestMethod]
    public void ObservationProtocolAdapterPreservesExistingWireEncoding()
    {
        var version = ProtocolVersion.Current;
        var spawn = new AgentSpawnMessage(7, 1, 2, 3, 4, 5, 6, 8);
        CollectionAssert.AreEqual(ProtocolCodec.Serialize(spawn, version), ObservationProtocolAdapter.Serialize(spawn, version));

        var inspect = new InspectPersonMessage(42);
        CollectionAssert.AreEqual(PopulationProtocolCodec.Serialize(inspect, version), ObservationProtocolAdapter.Serialize(inspect, version));

        var inspectFrame = PopulationProtocolCodec.Serialize(inspect, version);
        Assert.IsTrue(ObservationProtocolAdapter.TryDeserialize(inspectFrame, out var envelope, out var error), error.ToString());
        Assert.IsNotNull(envelope);
        Assert.IsInstanceOfType<InspectPersonMessage>(envelope.Message);
        Assert.AreEqual(42UL, ((InspectPersonMessage)envelope.Message).PersonId);
    }

    [TestMethod]
    public async Task WebSocketObservationRouteRejectsNonObservationProtocolMessages()
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 1, tickRate: 30, snapshotRate: 5);
        using var socket = await host.ConnectWebSocketAsync();
        await ServerTestHost.HandshakeAsync(socket);

        await ServerTestHost.SendAsync(socket, new HelloMessage());
        var response = await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3));

        var error = response.Message as ProtocolErrorMessage;
        Assert.IsNotNull(error);
        Assert.AreEqual(ProtocolErrorCode.InvalidRequest, error.Code);
    }
}
