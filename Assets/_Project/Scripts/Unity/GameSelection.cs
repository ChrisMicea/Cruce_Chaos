using CruceChaos.Core.Game;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Carries the player's menu choice across a scene load.
    ///
    /// Serialized Inspector fields do NOT survive <c>SceneManager.LoadScene</c>,
    /// but a static field does — the type stays loaded for the lifetime of the
    /// app domain. So the Main Menu buttons set <see cref="SelectedMode"/> just
    /// before loading a game scene, and the scene's <c>GameManager</c> reads it
    /// in its round setup to decide which <see cref="MatchConfig"/> to run.
    ///
    /// Defaults to <see cref="GameMode.OneVsOne"/> so a scene entered directly
    /// (e.g. pressing Play on the game scene in the editor, with no menu step)
    /// still behaves exactly like the original 1v1 game.
    /// </summary>
    public static class GameSelection
    {
        /// <summary>
        /// The mode the next-loaded game scene should run. Set by the Main Menu
        /// buttons; read by <c>GameManager</c> at round setup.
        /// </summary>
        public static GameMode SelectedMode { get; set; } = GameMode.OneVsOne;

        /// <summary>
        /// Which seats a human controls, indexed by seat (true = human, false =
        /// AI). This is the contract a future seat-selection screen fills before
        /// loading a game scene; <c>GameManager</c> reads it at setup and uses it
        /// verbatim when its length matches the chosen mode's player count.
        ///
        /// <c>null</c> means "unspecified" — no seat-selection step ran, so
        /// <c>GameManager</c> falls back to <see cref="PassAndPlay"/> and then to
        /// the original single-human-at-seat-0 game. Default is <c>null</c> so a
        /// scene entered directly (editor Play) behaves exactly like the original.
        /// </summary>
        public static bool[] HumanSeats { get; set; } = null;

        /// <summary>
        /// 1v1 hotseat (pass-and-play) convenience flag. When <c>true</c> and
        /// <see cref="HumanSeats"/> is <c>null</c>, <c>GameManager</c> derives an
        /// all-seats-human layout for the chosen mode (1v1 ⇒ {0,1}), so both
        /// players share the device and the pass-the-device gate runs between
        /// turns. When both this and <see cref="HumanSeats"/> are unset/false the
        /// game runs the original single-human-at-seat-0 flow, so direct editor
        /// scene entry still works.
        ///
        /// RESET SEMANTICS: this is a static field that outlives a scene load, so
        /// <c>GameManager</c> reads it in <c>Start()</c> and then clears it (and
        /// <see cref="HumanSeats"/>) back to the defaults. That keeps a later
        /// direct scene entry from inheriting a stale hotseat choice — the menu
        /// sets it, the scene reads it once, and it resets itself.
        /// </summary>
        public static bool PassAndPlay { get; set; } = false;
    }
}
