using System.Collections.Generic;
using System.Linq;
using CruceChaos.Core.Cards;
using CruceChaos.Core.Game;

namespace CruceChaos.Core.Players.AI.GreedyHeuristic
{
    /// <summary>
    /// A greedy, single-trick heuristic AI. "Decent, not optimal": no lookahead,
    /// no probability, no memory — every decision is the best choice for the
    /// current trick given what is on the table.
    ///
    /// The behaviour is specified in docs/ai/greedy-heuristic.md; the rule
    /// letters in the comments below (A.1, A.3, B.1, ...) refer to that doc,
    /// which is the source of truth. If the two ever disagree, the doc wins and
    /// this file is the bug.
    ///
    /// It never consults trick history. It only reads its own hand, the current
    /// trick, and the trump suit (all public info from the context).
    /// </summary>
    public sealed class GreedyHeuristicController : IPlayerController
    {
        // --- Section F: bidding constants (tunable) ------------------------

        /// <summary>Flat value of each trump beyond the first (long trump = control).</summary>
        private const int TrumpLengthValue = 14;

        /// <summary>Fraction (percent) of a guarded non-trump honor's points that count.</summary>
        private const int GuardedHonorPercent = 75;

        /// <summary>Safety margin (points) required above 33×S before bidding S.</summary>
        private const int SafetyMargin = 3;

        /// <summary>
        /// Section F: estimate hand strength as if the longest suit were trump
        /// (which lead rule B.0 then realizes), and bid the highest contract the
        /// estimate clears by <see cref="SafetyMargin"/>; otherwise pass. See
        /// docs/ai/greedy-heuristic.md for the rationale.
        /// </summary>
        public Bid PlaceBid(RoundParticipant self, BiddingContext context)
        {
            IReadOnlyList<Card> hand = self.Hand.Cards;
            if (hand.Count == 0)
            {
                return Bid.Pass;
            }

            // F.1: candidate trump = longest suit (tie-break most points).
            Suit candidateTrump = hand
                .GroupBy(c => c.Suit)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Sum(c => c.Points))
                .First()
                .Key;

            int score = ScoreHand(hand, candidateTrump);

            // F.3: highest S whose 33*S is cleared by the safety margin.
            int bestS = 0;
            for (int s = 1; s <= context.MaxBid; s++)
            {
                if (score >= (33 * s) + SafetyMargin)
                {
                    bestS = s;
                }
            }

            // F.4: respect the current high bid; pass if we can't justify a
            // legal raise.
            if (bestS < context.MinLegalBid)
            {
                return Bid.Pass;
            }

            return Bid.Of(bestS);
        }

        /// <summary>F.2: a crude static expected-capture estimate, in points.</summary>
        private static int ScoreHand(IReadOnlyList<Card> hand, Suit trump)
        {
            int score = 0;

            int trumpCount = hand.Count(c => c.Suit == trump);
            if (trumpCount > 1)
            {
                score += (trumpCount - 1) * TrumpLengthValue;
            }

            foreach (Card c in hand)
            {
                bool isHonor = c.Rank == Rank.Ace || c.Rank == Rank.Ten;
                if (!isHonor)
                {
                    continue;
                }

                if (c.Suit == trump)
                {
                    score += c.Points; // trump honor: counts fully
                }
                else
                {
                    // Non-trump honor counts partially, and only if guarded
                    // (at least one other card of its suit to back it).
                    bool guarded = hand.Count(o => o.Suit == c.Suit) >= 2;
                    if (guarded)
                    {
                        score += (c.Points * GuardedHonorPercent) / 100;
                    }
                }
            }

            return score;
        }

        public Card ChooseCard(RoundParticipant self, IReadOnlyList<Card> legalMoves, ITrickContext context)
        {
            // Leading: no card has been led, so we pick the suit (section B).
            if (context.IsEmpty)
            {
                return ChooseLead(legalMoves, context.Trump, context.DrawPileCount);
            }

            // Following: a card has been led, decide whether we can win (A).
            return ChooseFollow(legalMoves, context);
        }

        // --- Section A: following ------------------------------------------

        private static Card ChooseFollow(IReadOnlyList<Card> legalMoves, ITrickContext context)
        {
            Suit? trump = context.Trump;

            // Strength of the winning card so far, within the contested suit.
            // Contested suit = trump if any trump is on the table, else led suit.
            bool trumpOnTable = trump.HasValue && context.Plays.Any(p => p.Card.Suit == trump.Value);
            Suit contested = trumpOnTable ? trump.Value : context.LedSuit;
            int bestOnTable = context.Plays
                .Where(p => p.Card.Suit == contested)
                .Select(p => p.Card.Strength)
                .DefaultIfEmpty(-1)
                .Max();

            // A.1: which of my legal moves would actually win the trick? A card
            // wins if it is of the contested suit and beats the best on table,
            // OR (when no trump is on the table yet) it is a trump — any trump
            // beats a non-trump led suit.
            List<Card> winners = legalMoves
                .Where(c => WouldWin(c, contested, bestOnTable, trump, trumpOnTable))
                .ToList();

            if (winners.Count > 0)
            {
                // A.2: win as cheaply as possible — lowest strength that still
                // wins (tie-break lowest points for determinism, section E).
                return winners
                    .OrderBy(c => c.Strength)
                    .ThenBy(c => c.Points)
                    .First();
            }

            // A.3: cannot win — dump the least valuable card. Order by lowest
            // points, then non-trump before trump, then lowest strength.
            return legalMoves
                .OrderBy(c => c.Points)
                .ThenBy(c => IsTrump(c, trump) ? 1 : 0)
                .ThenBy(c => c.Strength)
                .First();
        }

        /// <summary>
        /// Would playing <paramref name="candidate"/> become the new winning
        /// card, given the best card of the contested suit currently on the
        /// table?
        /// </summary>
        private static bool WouldWin(Card candidate, Suit contested, int bestOnTable, Suit? trump, bool trumpOnTable)
        {
            // If a trump is already down, only a higher trump can win.
            if (trumpOnTable)
            {
                return IsTrump(candidate, trump) && candidate.Strength > bestOnTable;
            }

            // No trump down yet: a trump wins outright (beats the led suit)...
            if (IsTrump(candidate, trump))
            {
                return true;
            }

            // ...otherwise only a stronger card of the contested (led) suit wins.
            return candidate.Suit == contested && candidate.Strength > bestOnTable;
        }

        // --- Section B: leading --------------------------------------------

        private static Card ChooseLead(IReadOnlyList<Card> legalMoves, Suit? trump, int drawPileCount)
        {
            // B.0: FORCED opening lead. Trump is unset only on the round's very
            // first play, so this reliably detects the opening lead. Deliberately
            // set trump to our longest suit by leading it — lowest card of it, so
            // we establish trump cheaply without burning a high trump. Tie-break
            // longest suits by most total points (strongest long suit = best
            // trump), matching the bid heuristic's own trump pick.
            if (trump == null)
            {
                Suit chosen = legalMoves
                    .GroupBy(c => c.Suit)
                    .OrderByDescending(g => g.Count())
                    .ThenByDescending(g => g.Sum(c => c.Points))
                    .First()
                    .Key;

                return legalMoves
                    .Where(c => c.Suit == chosen)
                    .OrderBy(c => c.Strength)
                    .ThenBy(c => c.Points)
                    .First();
            }

            List<Card> nonTrump = legalMoves.Where(c => !IsTrump(c, trump)).ToList();

            // B.4: hand is all trump — no non-trump to lead. Lead the
            // lowest-points trump (then lowest strength); least damaging.
            if (nonTrump.Count == 0)
            {
                return legalMoves
                    .OrderBy(c => c.Points)
                    .ThenBy(c => c.Strength)
                    .First();
            }

            // B.1: draw-phase lead AFTER the opening (pile not empty, no
            // follow-suit obligation) — bank value early by leading the
            // highest-points non-trump card (tie-break highest strength).
            if (drawPileCount > 0)
            {
                return nonTrump
                    .OrderByDescending(c => c.Points)
                    .ThenByDescending(c => c.Strength)
                    .First();
            }

            // B.2: normal lead (pile empty, follow-suit in force) — cheap probe:
            // lowest-points non-trump (tie-break lowest strength).
            return nonTrump
                .OrderBy(c => c.Points)
                .ThenBy(c => c.Strength)
                .First();
        }

        // --- helpers -------------------------------------------------------

        private static bool IsTrump(Card card, Suit? trump) => trump.HasValue && card.Suit == trump.Value;
    }
}
