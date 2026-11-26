using System;
using System.Collections.Generic;
using System.Linq;
using CruceChaos.Core.Cards;
using CruceChaos.Core.Game;
using CruceChaos.Core.Players;

namespace CruceChaos.PlayConsole
{
    /// <summary>
    /// An <see cref="IPlayerController"/> driven by a human at the terminal.
    ///
    /// This is the interactive counterpart to the AI controllers: it implements
    /// the exact same five decision points (<see cref="PlaceBid"/>,
    /// <see cref="DeclareTrump"/>, <see cref="ChooseDraftCard"/>,
    /// <see cref="AnnounceMarriage"/>, <see cref="ChooseCard"/>) by showing the
    /// relevant state and reading a numbered choice from stdin. Because it plugs
    /// into the same seam the engine already uses, a human can occupy any seat
    /// while AI controllers fill the rest — no engine change required.
    ///
    /// All prompts go to <see cref="Console.Error"/> and choices are read from
    /// <see cref="Console.In"/>, leaving stdout free to carry the engine's clean
    /// play-by-play log. Every prompt re-asks on invalid input rather than
    /// throwing, so a fat-fingered entry never aborts a game.
    /// </summary>
    public sealed class HumanConsoleController : IPlayerController
    {
        private readonly string _name;

        public HumanConsoleController(string name)
        {
            _name = name;
        }

        // --- bidding --------------------------------------------------------

        public Bid PlaceBid(RoundParticipant self, BiddingContext context)
        {
            Header($"{_name} — your bid");
            ShowHand(self);

            string high = context.CurrentHighBid.HasValue
                ? context.CurrentHighBid.Value.ToString()
                : "none yet";
            Console.Error.WriteLine($"  Current high bid: {high}   (legal: {context.MinLegalBid}..{context.MaxBid})");

            // Options: pass, or any legal numeric contract.
            var choices = new List<(string label, Bid bid)> { ("Pass", Bid.Pass) };
            for (int v = context.MinLegalBid; v <= context.MaxBid; v++)
            {
                choices.Add(($"Bid {v}", Bid.Of(v)));
            }

            int idx = AskChoice(choices.Select(c => c.label).ToList());
            return choices[idx].bid;
        }

        // --- trump declaration ---------------------------------------------

        public Suit DeclareTrump(RoundParticipant self)
        {
            Header($"{_name} — declare trump");
            ShowHand(self);

            var suits = EnumValues<Suit>();
            int idx = AskChoice(suits.Select(s => s.ToString()).ToList());
            return suits[idx];
        }

        // --- reveal-and-draft ----------------------------------------------

        public Card ChooseDraftCard(RoundParticipant self, IReadOnlyList<Card> available, Suit trump)
        {
            Header($"{_name} — draft a card  (trump: {trump})");
            ShowHand(self);
            Console.Error.WriteLine("  Revealed cards to draft from:");

            int idx = AskChoice(available.Select(c => Describe(c, trump)).ToList());
            return available[idx];
        }

        // --- marriage announcement -----------------------------------------

        public Suit? AnnounceMarriage(RoundParticipant self, IReadOnlyList<Suit> available, Suit? trump)
        {
            Header($"{_name} — announce a marriage?");
            ShowHand(self);
            Console.Error.WriteLine(trump.HasValue
                ? $"  (trump is {trump}; a trump marriage is worth 40, otherwise 20)"
                : "  (no trump this round; a marriage is worth 20)");

            // "Do not announce" is option 0; each available suit follows.
            var labels = new List<string> { "Do not announce" };
            labels.AddRange(available.Select(s =>
            {
                int worth = (trump.HasValue && s == trump.Value) ? 40 : 20;
                return $"Announce {s} (+{worth}) — must then lead its 3 or 4";
            }));

            int idx = AskChoice(labels);
            return idx == 0 ? (Suit?)null : available[idx - 1];
        }

        // --- card play ------------------------------------------------------

        public Card ChooseCard(RoundParticipant self, IReadOnlyList<Card> legalMoves, ITrickContext context)
        {
            Header($"{_name} — play a card");
            ShowTrick(context);
            ShowHand(self, context.Trump);

            if (legalMoves.Count == 1)
            {
                Console.Error.WriteLine($"  Only one legal move: {Describe(legalMoves[0], context.Trump)} — auto-playing.");
                return legalMoves[0];
            }

            Console.Error.WriteLine("  Your legal moves:");
            int idx = AskChoice(legalMoves.Select(c => Describe(c, context.Trump)).ToList());
            return legalMoves[idx];
        }

        // --- presentation helpers ------------------------------------------

        private static void Header(string title)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"==================== {title} ====================");
        }

        private static void ShowHand(RoundParticipant self, Suit? trump = null)
        {
            IReadOnlyList<Card> cards = self.Hand.Cards;
            if (cards.Count == 0)
            {
                Console.Error.WriteLine("  Hand: (empty)");
                return;
            }

            // Group by suit for readability, strongest card first within a suit.
            string shown = string.Join("   ", cards
                .OrderBy(c => c.Suit)
                .ThenByDescending(c => c.Strength)
                .Select(c => Describe(c, trump)));
            Console.Error.WriteLine($"  Hand: {shown}");
        }

        private static void ShowTrick(ITrickContext context)
        {
            if (context.Trump.HasValue)
            {
                Console.Error.WriteLine($"  Trump: {context.Trump}   Draw pile: {context.DrawPileCount}");
            }
            else
            {
                Console.Error.WriteLine($"  Trump: none   Draw pile: {context.DrawPileCount}");
            }

            if (context.IsEmpty)
            {
                Console.Error.WriteLine("  Trick: you are leading.");
                return;
            }

            string onTable = string.Join("   ", context.Plays
                .Select(p => $"{p.Participant.Player.Name}:{Describe(p.Card, context.Trump)}"));
            Console.Error.WriteLine($"  Trick so far (led {context.LedSuit}): {onTable}");
        }

        /// <summary>Card label with points, marking trump with a <c>*</c>.</summary>
        private static string Describe(Card card, Suit? trump)
        {
            bool isTrump = trump.HasValue && card.Suit == trump.Value;
            string star = isTrump ? "*" : string.Empty;
            return $"{card}{star} [{card.Points}p]";
        }

        /// <summary>
        /// Print a numbered menu to stderr and return the chosen 0-based index.
        /// Re-asks until the input is a number in range. On end-of-input
        /// (piped stdin exhausted) it defaults to the first option so a scripted
        /// run can never hang.
        /// </summary>
        private static int AskChoice(IReadOnlyList<string> options)
        {
            for (int i = 0; i < options.Count; i++)
            {
                Console.Error.WriteLine($"    [{i}] {options[i]}");
            }

            while (true)
            {
                Console.Error.Write("  > ");
                string line = Console.In.ReadLine();

                if (line == null)
                {
                    // stdin closed (e.g. piped input ran out). Pick the first
                    // option deterministically instead of looping forever.
                    Console.Error.WriteLine($"(no input — defaulting to [0] {options[0]})");
                    return 0;
                }

                line = line.Trim();
                if (int.TryParse(line, out int idx) && idx >= 0 && idx < options.Count)
                {
                    return idx;
                }

                Console.Error.WriteLine($"  Please enter a number between 0 and {options.Count - 1}.");
            }
        }

        private static T[] EnumValues<T>() => (T[])Enum.GetValues(typeof(T));
    }
}
