namespace CruceChaos.Core.Cards
{
    /// <summary>
    /// The six ranks of a Cruce suit.
    ///
    /// The integer value assigned to each member encodes its STRENGTH
    /// (trick-taking power), from weakest to strongest:
    ///
    ///     Nine (0) &lt; Two (1) &lt; Three (2) &lt; Four (3) &lt; Ten (4) &lt; Ace (5)
    ///
    /// i.e. the in-game ranking  A > 10 > 4 > 3 > 2 > 9.
    ///
    /// NOTE: these ordinal values are deliberately NOT the card's point
    /// value. Point values (A=11, 10=10, 4=4, 3=3, 2=2, 9=0) live in
    /// <see cref="CardInfo"/> so that "how strong is it" and "how much is
    /// it worth" stay independent concepts.
    /// </summary>
    public enum Rank
    {
        Nine = 0,
        Two = 1,
        Three = 2,
        Four = 3,
        Ten = 4,
        Ace = 5
    }
}
