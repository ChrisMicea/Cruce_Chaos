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
    }
}
