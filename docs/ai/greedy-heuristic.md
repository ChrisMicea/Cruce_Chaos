# Engine: Greedy heuristic

**Class:** `CruceChaos.Core.Players.AI.GreedyHeuristic.GreedyHeuristicController`
**Memory:** low (does not actually consult history — see section D)
**Status:** implemented — matches this doc

## Measured performance (vs. the first-legal baseline, 50 seeds)

| Greedy seat        | Greedy wins | Baseline wins | Ties | Greedy avg pts | Baseline avg pts |
| ------------------ | ----------- | ------------- | ---- | -------------- | ---------------- |
| Second player      | 30          | 19            | 1    | 63             | 56               |
| First player (lead)| 40          | 10            | 0    | 75             | 44               |

The engine is far stronger when it holds the **opening lead every round** (seat
0): it establishes trump and gets first use of the draw-phase value-banking
(B.1), and its edge is concentrated in leading rather than following. This also
flags a fairness point for multi-round matches: whoever opens has a structural
advantage under these rules, so matches should **rotate the opening lead**
between rounds (as the real game rotates the dealer). The 120-point checksum
held across all 100 runs.

## Goal

"Decent, not optimal." Beat the first-legal baseline convincingly, play in a way
that looks sensible to a human, but do NOT attempt lookahead or probability. All
decisions are greedy: best choice for *this* trick, given what is on the table
right now.

## Vocabulary (so the rules are unambiguous)

- **Strength** — trick-taking power (A > 10 > 4 > 3 > 2 > 9). Decides what beats
  what *within a suit*.
- **Points** — score value (A=11, 10=10, 4=4, 3=3, 2=2, 9=0). Decides what is
  worth capturing / costly to give away.
- These are different axes. "Cheapest card that still wins" uses strength;
  "least valuable card to throw away" uses points.
- **Winning card so far** — the card currently taking the trick under the
  trump/led-suit rules (trump beats led suit; highest of the contested suit).

## A. When FOLLOWING (a card has already been led)

Decide in this order:

1. **Can I win the trick with any of my legal moves?**
   A legal move wins if, placed on the table now, it would become the winning
   card so far. (Because `legalMoves` is pre-filtered, this already respects
   follow-suit and trump obligations.)

2. **If I CAN win:**
   - Win as *cheaply* as possible: play the **lowest-strength** card
     that still wins. (Don't waste an Ace to win a trick a 10 would have won.)

3. **If I CANNOT win:**
   - **Dump** the least valuable card: lowest **points** among legal moves. A 9
     (0 pts) is the ideal throwaway.
   - **Tie-break among equal-point dumps?** prefer dumping the
     lowest-strength one, and prefer a non-trump over a trump (keep trump for
     later). So: lowest points, then non-trump, then lowest strength.

## B. When LEADING (I choose the suit)

Leading depends only on whether the draw pile still has cards. Trump is declared explicitly before
play (section G), and the "bid winner must lead trump on the opening" obligation
is enforced by the **legality pipeline** (`LeadTrumpOnOpeningRule`), which
pre-filters `legalMoves` to trump on that one lead. So on the opening lead the
rules below simply pick among the trump-only set the pipeline provides — no
engine-side AI logic needed.

1. **Draw-phase lead (draw pile NOT empty):** play the
   highest-**points** NON-trump card (tie-break highest strength).
   Rationale: while cards remain to draw there is no follow-suit obligation, so
   a high card led is relatively safe, and you refill your hand anyway — so
   hoarding value is pointless. Bank points early instead.

2. **Normal lead (draw pile empty):** play the lowest-**points** NON-trump card
   (tie-break lowest strength). Once follow-suit is in force, a cheap probe is
   safer; conserve trump and high cards for capturing.

3. **Never lead trump** in this engine — leading trump bleeds your own trump.
   "Non-trump" in B.1 and B.2 means exactly that.

4. **Fallback (hand is all trump):** if the player holds *only* trump cards,
   B.1/B.2 have no non-trump to pick. Lead the lowest-**points** trump (then
   lowest strength) — the least damaging forced trump lead.

> Gate note: B.1 keys on "draw pile not empty", not on player count, because the
> no-follow-suit phase that justifies it only exists while the draw pile has
> cards. Future modes without a draw pile simply never trigger B.1, which is
> correct.

> Opening-lead note: the opening lead is NOT special-cased in the AI. Trump is
> declared explicitly before play (section G), and the forced-trump opening is
> enforced by the legality pipeline, which filters this lead to trumps. The AI
> rules above then just pick among that filtered set.

## C. Trump handling (cross-cutting)

- Trump is known from the context (trump suit).
- Treat trump as "high value to keep": the dump tie-break (A.3) and both lead
  rules (B.1, B.2) avoid spending trump (B.4 is the all-trump fallback only).
- When following and unable to follow suit, the rules pipeline may *force* a
  trump (trump obligation). In that case the AI still applies "win cheaply if
  winning, else dump cheapest" **within** the forced set — it does not get to
  avoid trumping, but it picks the cheapest trump that satisfies the obligation.

## D. Memory usage (low)

- This engine consults only **the tricks it has won** (its own capture pile).
- Do we even use that for anything in the greedy engine? No —
  greedy decisions are about the current trick only, so the low-memory record is
  not actually needed here. It is listed for completeness and to keep the
  information model consistent.

## E. Determinism / tie-breaking

- Where two cards are equally good by all rules above, pick the **lowest-strength
  then lowest-points** one deterministically (no randomness), so games are
  reproducible for a given seed.

## F. Bidding

The bid estimates hand strength and commits to capturing ~33×S of the 120 card
points. Overbidding is punished, so the engine is deliberately **conservative**:
bid the floor of what the hand can likely make, lean toward passing.

Strength is evaluated **as if the hand's longest suit were trump** — which is
consistent with the trump declaration (section G), which actually declares that
same suit. So:

1. **Pick the candidate trump:** the suit with the most cards (tie-break: most
   total points). Call its cards the *trumps*.

2. **Score the hand** (a rough expected-capture estimate, in points):
   - Count trump length. Each trump beyond the first is worth a flat
     `TrumpLengthValue` (long trump = control).
   - Add the **points of high cards you are likely to keep**: trump Aces/Tens
     count fully; non-trump Aces/Tens count partially (they can be trumped), and
     only if backed by at least one other card of their suit (a *guarded*
     honor). Bare non-trump honors count little.
   - This is intentionally crude — a static hand score, no opponent modelling.

3. **Map score → bid:** bid the highest S whose `33 × S` the score clears by a
   safety margin `M`; if even S=1 is not cleared, **pass**. Numeric constants
   (`TrumpLengthValue`, the honor weights, `M`) live in the controller and are
   tuned empirically.

4. **Respect the context:** never bid below `MinLegalBid` or above `MaxBid`; if
   the next legal bid exceeds what the score justifies, pass.

### Known limitations (accepted for this tier)

- **Teams:** in 2v2 the TEAM must make 33×S, so own-hand-only scoring will
  *under-bid* with a strong partner. Not corrected yet.
- No announcements (40/20 marriages) factor into the bid.
- No opponent modelling or memory — purely static hand evaluation.

## G. Trump declaration & drafting

After winning the bid, the AI makes two further decisions (both are explicit
`IPlayerController` calls, happening before any card is played):

1. **Declare trump** (`DeclareTrump`): the **longest suit** (tie-break most
   total points) — the SAME suit the bid heuristic (F) scored on, so the bid and
   the declared trump always agree. The forced-trump opening lead (enforced by
   the pipeline) then means the AI opens with a trump it is long in.

2. **Draft** (`ChooseDraftCard`, only in reveal-and-draft modes): take the
   **highest trump** available; if none is revealed, take the **highest-points**
   card (tie-break highest strength). Trump beats everything, so grabbing the
   best trump is the greedy pick. Trump is already declared when drafting, so
   this is trump-aware.

   NOTE: the AI does NOT gamble — it will not overbid hoping the draft rescues a
   weak hand. That risk/reward depth is left for a smarter engine; here the bid
   is committed conservatively before the reveal and the draft is a plain greedy
   grab.

## Summary (the whole engine in one breath)

> Bid conservatively on longest-suit-as-trump hand strength, else pass. Declare
> that longest suit as trump; draft the best trump (else highest value). Opening
> lead is forced to trump by the rules (not AI logic). Other draw-phase leads:
> bank the highest-value non-trump. Normal leads: cheapest non-trump. Follow:
> win with the cheapest card that wins, else throw the lowest-point card, keeping
> trump. Never waste high cards or trump on tricks that don't need them, and
> never gamble.
