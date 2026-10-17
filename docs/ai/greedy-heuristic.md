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

Leading depends on whether the draw pile still has cards (the "cu carti de jos"
phase, where there is no follow-suit obligation).

1. **Draw-phase lead (draw pile NOT empty):** play the highest-**points**
   NON-trump card (tie-break highest strength).
   Rationale: while cards remain to draw there is no follow-suit obligation, so
   a high card led is relatively safe, and you refill your hand anyway — so
   hoarding value is pointless. Bank points early instead.

   This rule also governs the **very first lead of the round**, which is the
   card that establishes trump. Trump therefore falls out as a *side effect*:
   it becomes the suit of whatever highest-value non-trump card gets led. The
   AI does NOT deliberately choose trump by suit length — a single draw-phase
   lead rule was chosen for simplicity (the exact trump suit barely moves the
   game at large).

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

> Known crude spot: on the opening lead, "lead highest-value non-trump" is
> slightly incoherent because the suit you lead becomes trump — you cannot avoid
> making trump out of the card you lead. Accepted as-is for this tier.

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

## Summary (the whole engine in one breath)

> Follow: win with the cheapest card that wins, 
> else throw the lowest-point card, preferring to
> keep trump. Lead: the cheapest non-trump card. Never waste high cards or trump
> on tricks that don't need them.
