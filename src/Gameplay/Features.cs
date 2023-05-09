using applecat.Core;
using SlugBase.Features;
using UnityEngine;

namespace applecat.Gameplay
{
    public static class Features
    {
        public const string SlugcatId = "applecat";

        public static readonly PlayerFeature<bool> IsAppleCatFlag = FeatureTypes.PlayerBool("applecat/is_applecat");

        public static readonly PlayerFeature<bool> Discoloration = FeatureTypes.PlayerBool("applecat/discoloration");

        public static readonly PlayerFeature<bool> HasSprout = FeatureTypes.PlayerBool("applecat/sprout");

        public static readonly GameFeature<float> MeanLizards = FeatureTypes.GameFloat("applecat/mean_lizards");

        public static readonly Color LeafGreen = new Color(0.52f, 0.67f, 0.34f, 1f);

        public static readonly Color AppleRed = new Color(0.78f, 0.40f, 0.36f, 1f);

        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _ = IsAppleCatFlag;
            _ = Discoloration;
            _ = HasSprout;
            _ = MeanLizards;
        }

        public static bool Matches(Player player)
        {
            if (player == null)
            {
                return false;
            }

            if (IsAppleCatFlag.TryGet(player, out bool flagged) && flagged)
            {
                return true;
            }

            return Slugcat.IsId(player.SlugCatClass, SlugcatId);
        }

        public static bool ShouldDiscolor(Player player)
        {
            return player != null && Discoloration.TryGet(player, out bool value) && value;
        }

        public static bool ShouldGrowSprout(Player player)
        {
            if (player == null)
            {
                return false;
            }

            if (HasSprout.TryGet(player, out bool value))
            {
                return value;
            }

            return Matches(player);
        }
    }
}