# Engine: First legal move (baseline)

**Class:** `CruceChaos.Core.Players.AI.FirstLegalMoveController`
**Memory:** none
**Status:** done

## Purpose

The stupidest AI that still plays legally. It exists as:

- a **difficulty floor** (the weakest possible opponent),
- a **development opponent** while building the engine, and
- a **control** to measure smarter engines against (any real AI should beat this
  convincingly over many rounds).

## Logic

- Play `legalMoves[0]` — the first card in the already-filtered legal set.

That is the entire strategy. It makes no evaluation of the trick, trump, points,
or history. Correctness (never playing an illegal card) is guaranteed entirely
by the rules pipeline upstream, not by the controller.

## Notes

Because the order of `legalMoves` reflects hand order, this engine's play is
deterministic for a given deal but not meaningfully strategic. Do not read
anything into *which* card it picks.
