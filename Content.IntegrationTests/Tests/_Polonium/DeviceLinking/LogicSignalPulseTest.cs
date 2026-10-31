using Content.IntegrationTests.Fixtures;
using Content.Shared._Polonium.DeviceLinking;
using Content.Shared.Conveyor;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Components;
using Content.Shared.DeviceLinking.Systems;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Polonium.DeviceLinking;

/// <summary>
/// Since Wizden #44990 a signal with a logic state skipped devices that only react to plain pulses,
/// so an air alarm or a logic gate could not drive a conveyor anymore.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedLogicSignalPulseSystem))]
public sealed class LogicSignalPulseTest : GameTest
{
    private const string SourceProtoId = "PoloniumLogicSignalTestSource";
    private const string OutputPortId = "Output";

    private static readonly ProtoId<SourcePortPrototype> OutputPort = OutputPortId;
    private static readonly EntProtoId ConveyorProtoId = "ConveyorBelt";
    private static readonly EntProtoId LogicGateProtoId = "LogicGateOr";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  id: {SourceProtoId}
  components:
  - type: DeviceNetwork
    deviceNetId: Wireless
  - type: DeviceLinkSource
    ports:
    - {OutputPortId}
";

    [Test]
    public async Task HighSignalPulsesPlainDevicesOnly()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var link = server.System<DeviceLinkSystem>();
        var map = await pair.CreateTestMap();

        EntityUid source = default;
        EntityUid conveyor = default;
        EntityUid gate = default;
        await server.WaitPost(() =>
        {
            source = entMan.SpawnEntity(SourceProtoId, map.GridCoords);
            conveyor = entMan.SpawnEntity(ConveyorProtoId, map.GridCoords);
            gate = entMan.SpawnEntity(LogicGateProtoId, map.GridCoords);

            var forward = entMan.GetComponent<ConveyorComponent>(conveyor).ForwardPort;
            var inputA = entMan.GetComponent<LogicGateComponent>(gate).InputPortA;
            link.SaveLinks(null, source, conveyor, [(OutputPort, forward)]);
            link.SaveLinks(null, source, gate, [(OutputPort, inputA)]);

            link.SendSignal(source, OutputPort, false);
        });
        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.GetComponent<ConveyorComponent>(conveyor).State, Is.EqualTo(ConveyorState.Off),
                "A low signal must not pulse a plain device");
        });

        await server.WaitPost(() => link.SendSignal(source, OutputPort, true));
        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(entMan.GetComponent<ConveyorComponent>(conveyor).State, Is.EqualTo(ConveyorState.Forward),
                    "A high signal must reach a device that only handles plain pulses");
                Assert.That(entMan.GetComponent<LogicGateComponent>(gate).StateA, Is.EqualTo(SignalState.High),
                    "A device with its own logic state handling must not get an extra pulse");
            }
        });
    }
}
