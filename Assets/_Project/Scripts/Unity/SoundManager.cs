using UnityEngine;

namespace CruceChaos.Unity
{
    /// <summary>
    /// Central one-shot sound-effects player for a single scene.
    ///
    /// Owns one <see cref="AudioSource"/> and exposes a named Play method per
    /// game event we actually hook. Scene controllers (GameManager, the menu
    /// controller) hold a serialized reference to this object and call the Play
    /// methods at the existing game-event points — no global state, the
    /// dependency is visible in each caller's Inspector.
    ///
    /// Every call is null-safe: a missing clip or a scene without a SoundManager
    /// simply stays silent instead of throwing, so wiring can be added
    /// incrementally.
    ///
    /// SCENE SETUP (per gameplay / menu scene):
    ///   1. Empty GameObject named "SoundManager".
    ///   2. Add an AudioSource component — Play On Awake OFF, Loop OFF.
    ///   3. Add this SoundManager component to the same object.
    ///   4. (Optional) drag the AudioSource into the Source field; if left
    ///      empty, Awake() grabs it via GetComponent.
    ///   5. Assign the AudioClip fields you need for that scene.
    ///   6. Drag this SoundManager object into the "Sound" slot of the scene's
    ///      GameManager / MainMenuController.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class SoundManager : MonoBehaviour
    {
        [Header("Audio Source")]
        [Tooltip("The AudioSource one-shots play through. Leave Play On Awake " +
                 "OFF. If unassigned, the AudioSource on this GameObject is used.")]
        [SerializeField] private AudioSource _source;

        [Header("Clips")]
        [Tooltip("The human declares a marriage.")]
        [SerializeField] private AudioClip _marriageDeclared;
        [Tooltip("A card is placed into the trick (any seat).")]
        [SerializeField] private AudioClip _cardPlaced;
        [Tooltip("The deck is shuffled at the start of a round.")]
        [SerializeField] private AudioClip _deckShuffle;
        [Tooltip("The player presses the Shuffle-hand button.")]
        [SerializeField] private AudioClip _handShuffle;
        [Tooltip("The player presses the Sort (auto-sort toggle) button.")]
        [SerializeField] private AudioClip _handSort;
        [Tooltip("A main-menu button is clicked.")]
        [SerializeField] private AudioClip _menuButton;
        [Tooltip("A card is drawn from the pile (1v1) or picked in the draft.")]
        [SerializeField] private AudioClip _cardDraw;
        [Tooltip("The player clicks a card that is not legal to play.")]
        [SerializeField] private AudioClip _illegalMove;
        [Tooltip("A trick resolves in the human's team's favour.")]
        [SerializeField] private AudioClip _trickWon;
        [Tooltip("A trick resolves against the human's team.")]
        [SerializeField] private AudioClip _trickLost;
        [Tooltip("A round's result favours the human's team (positive VP).")]
        [SerializeField] private AudioClip _roundWon;
        [Tooltip("A round's result goes against the human's team.")]
        [SerializeField] private AudioClip _roundLost;
        [Tooltip("The match is won by the human's team.")]
        [SerializeField] private AudioClip _matchWon;
        [Tooltip("The match is lost (another team wins it).")]
        [SerializeField] private AudioClip _matchLost;
        [Tooltip("Any seat (human or AI) places a valid bid.")]
        [SerializeField] private AudioClip _bidPlaced;
        [Tooltip("Any seat (human or AI) passes / is skipped during bidding.")]
        [SerializeField] private AudioClip _bidPassed;
        [Tooltip("Short tick as the player moves the bid slider.")]
        [SerializeField] private AudioClip _bidSlider;

        [Header("Volume")]
        [SerializeField] [Range(0f, 1f)] private float _sfxVolume = 1f;

        private void Awake()
        {
            if (_source == null)
                _source = GetComponent<AudioSource>();
        }

        /// <summary>Plays a one-shot clip at the configured SFX volume. No-op if
        /// the clip or the source is missing.</summary>
        private void Play(AudioClip clip)
        {
            if (clip == null || _source == null) return;
            _source.PlayOneShot(clip, _sfxVolume);
        }

        /// <summary>
        /// Plays a clip and returns how long (seconds) it will take to finish.
        /// Used by callers that must delay a scene load until the sound has been
        /// heard — they play, then wait the returned duration before loading.
        /// Returns 0 if nothing played, so a missing clip causes no delay.
        /// </summary>
        private float PlayAndGetLength(AudioClip clip)
        {
            if (clip == null || _source == null) return 0f;
            _source.PlayOneShot(clip, _sfxVolume);
            return clip.length;
        }

        // ── Named event hooks ─────────────────────────────────────────────────

        public void PlayMarriageDeclared() => Play(_marriageDeclared);
        public void PlayCardPlaced()       => Play(_cardPlaced);
        public void PlayDeckShuffle()      => Play(_deckShuffle);
        public void PlayHandShuffle()      => Play(_handShuffle);
        public void PlayHandSort()         => Play(_handSort);
        public void PlayCardDraw()         => Play(_cardDraw);
        public void PlayIllegalMove()      => Play(_illegalMove);
        public void PlayBidPlaced()        => Play(_bidPlaced);
        public void PlayBidPassed()        => Play(_bidPassed);
        public void PlayBidSlider()        => Play(_bidSlider);
        public void PlayRoundWon()         => Play(_roundWon);
        public void PlayRoundLost()        => Play(_roundLost);
        public void PlayMatchWon()         => Play(_matchWon);
        public void PlayMatchLost()        => Play(_matchLost);

        // Return their length so the trick→draw sequence can wait for the trick
        // sting to finish before the draw sound starts (avoids them overlapping
        // in the 1v1 draw-pile flow).
        public float PlayTrickWon()        => PlayAndGetLength(_trickWon);
        public float PlayTrickLost()       => PlayAndGetLength(_trickLost);

        /// <summary>
        /// Plays the menu-button click and returns its length in seconds. The
        /// menu fires this immediately before a scene load / app quit, which
        /// would cut a normal one-shot off, so the caller waits the returned
        /// duration before loading. Returns 0 (no wait) if no clip is assigned.
        /// </summary>
        public float PlayMenuButton()      => PlayAndGetLength(_menuButton);
    }
}
