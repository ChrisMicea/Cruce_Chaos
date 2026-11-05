using System.Collections.Generic;
using UnityEngine;
using CruceChaos.Core.Cards;

namespace CruceChaos.Unity
{
    /// <summary>
    /// ScriptableObject that maps every (Suit, Rank) pair to its sprite asset.
    ///
    /// HOW TO SET UP IN EDITOR:
    ///   1. Right-click in Project window → Create → Cruce Chaos → Card Sprite Registry
    ///   2. In the Inspector, click "Auto-Fill from Cards/ folder" button (calls AutoFill in editor)
    ///      OR drag sprites manually into each entry.
    ///   3. Assign the asset to the GameManager's "Card Sprite Registry" field.
    ///
    /// Sprite naming convention (matches Assets/Cards/):
    ///   Rank prefix:  as | doi | trei | patru | noua | zece
    ///   Suit suffix:  verde | red | ghinda | duba
    ///   e.g.  as_verde.png  →  Ace of Green
    ///         zece_duba.png →  Ten of Bell
    /// </summary>
    [CreateAssetMenu(menuName = "Cruce Chaos/Card Sprite Registry", fileName = "CardSpriteRegistry")]
    public class CardSpriteRegistry : ScriptableObject
    {
        [System.Serializable]
        public struct CardSpriteEntry
        {
            public Suit suit;
            public Rank rank;
            public Sprite sprite;
        }

        [SerializeField] private CardSpriteEntry[] _entries;

        // Runtime lookup built on first use.
        private Dictionary<(Suit, Rank), Sprite> _lookup;

        /// <summary>Returns the sprite for the given card, or null if not found.</summary>
        public Sprite GetSprite(Card card) => GetSprite(card.Suit, card.Rank);

        public Sprite GetSprite(Suit suit, Rank rank)
        {
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue((suit, rank), out Sprite s) ? s : null;
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<(Suit, Rank), Sprite>();
            if (_entries == null) return;
            foreach (var entry in _entries)
            {
                _lookup[(entry.suit, entry.rank)] = entry.sprite;
            }
        }

        // --------------- Editor-time convenience ---------------
        // Kept in the runtime assembly but guarded so it only runs in editor.
        // Call this from a custom inspector button to fill entries automatically.

        /// <summary>
        /// Returns the expected sprite filename (without extension) for a card,
        /// using the Romanian naming convention used in Assets/Cards/.
        /// </summary>
        public static string ExpectedSpriteName(Suit suit, Rank rank)
        {
            string rankStr = rank switch
            {
                Rank.Ace   => "as",
                Rank.Two   => "doi",
                Rank.Three => "trei",
                Rank.Four  => "patru",
                Rank.Nine  => "noua",
                Rank.Ten   => "zece",
                _          => rank.ToString().ToLower()
            };

            string suitStr = suit switch
            {
                Suit.Green => "verde",
                Suit.Red   => "red",
                Suit.Acorn => "ghinda",
                Suit.Bell  => "duba",
                _          => suit.ToString().ToLower()
            };

            return $"{rankStr}_{suitStr}";
        }

#if UNITY_EDITOR
        /// <summary>
        /// Scans Assets/Cards/ for sprites matching the naming convention and
        /// populates _entries automatically. Call from a custom Editor script.
        /// </summary>
        public void AutoFillFromCardsFolder()
        {
            var suits = System.Enum.GetValues(typeof(Suit)) as Suit[];
            var ranks = System.Enum.GetValues(typeof(Rank)) as Rank[];

            var list = new List<CardSpriteEntry>();

            foreach (Suit suit in suits)
            {
                foreach (Rank rank in ranks)
                {
                    string name = ExpectedSpriteName(suit, rank);
                    // Search for the sprite asset anywhere under Assets/
                    string[] guids = UnityEditor.AssetDatabase.FindAssets($"{name} t:Sprite");
                    if (guids.Length > 0)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                        Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                        list.Add(new CardSpriteEntry { suit = suit, rank = rank, sprite = sprite });
                    }
                    else
                    {
                        Debug.LogWarning($"[CardSpriteRegistry] No sprite found for {name}");
                        list.Add(new CardSpriteEntry { suit = suit, rank = rank, sprite = null });
                    }
                }
            }

            _entries = list.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[CardSpriteRegistry] Auto-filled {_entries.Length} entries.");
        }
#endif
    }
}
