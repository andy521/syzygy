using System.Linq;
using amulware.Graphics;
using Lidgren.Network;
using Syzygy.Game;
using Syzygy.Game.Behaviours;
using Syzygy.GameGeneration;

namespace Syzygy.GameManagement.Server
{
    sealed class BuildGameHandler : GenericBuildGameHandler<NetServer>
    {
        private readonly StateContainer.Builder stateBuilder;

        public BuildGameHandler(NetServer server, PlayerLookup players, PlayerConnectionLookup connections, Id<Player> ownID)
            : base(server, players, ownID, new ServerBehaviourProvider(server, connections))
        {
            this.stateBuilder = new StateContainer.Builder
            {
                Server = server,
                Players = players,
                Connections = connections,
            };
            var generator = new SimpleGenerator();

            var instructions = generator.Generate(players.Select(p => p.ID).ToList());

            // instructions may want to be spaced out in time in the future,
            // to prevent packet loss, and thus longer building time
            foreach (var instruction in instructions)
            {
                // build own game
                this.executeInstruction(instruction);

                if (connections.Count > 0)
                {
                    // instruct clients how to build game
                    var message = server.CreateMessage();
                    instruction.WriteMessage(message);
                    server.SendMessage(message, connections, NetDeliveryMethod.ReliableOrdered, 0);
                }
            }

            // send finish message and finish
            this.finish(this.stateBuilder);

            if (connections.Count > 0)
            {
                var finishMessage = server.CreateMessage();
                finishMessage.Write((byte)GenerationMessageType.FinishGenerating);
                server.SendMessage(finishMessage, connections, NetDeliveryMethod.ReliableOrdered, 0);
            }
        }

        public override void Update(UpdateEventArgs e)
        {
            this.stop(new ReadyGameHandler(this.stateBuilder.Build()));
        }
    }
}
