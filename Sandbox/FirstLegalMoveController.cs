using System.Collections.Generic;
using CruceChaos.Core.Cards;
using CruceChaos.Core.Game;
using CruceChaos.Core.Players;

namespace CruceChaos.Sandbox
{
    /// <summary>
    /// The simplest possible controller: always plays the first legal card.
    ///
    /// This is a placeholder test driver, not real AI. It exists only to let
    /// the sandbox run a full round end-to-end so we can verify the engine's
    /// flow.
    /// </summary>
    public sealed class FirstLegalMoveController : IPlayerController
    {
        public Card ChooseCard(RoundParticipant self, IReadOnlyList<Card> legalMoves, ITrickContext context)
        {
            // legalMoves is guaranteed non-empty by the engine's contract.
            return legalMoves[0];
        }
    }
}
