using System.Runtime.CompilerServices;
using applecat.Core;
using applecat.Graphics.Sprout;
using UnityEngine;

namespace applecat.Gameplay
{
    public sealed class AppleCat
    {
        private const int SunlightPerUpdate = 10;

        private const int AmbientLightPerUpdate = 2;

        private const int BaseLightPerFood = 1600;

        private const float HungryLightDemandScale = 0.55f;

        private const int SparkInterval = 34;

        private const int DarkDecay = 15;

        private float _lightEnergy;

        private int _sparkTimer;

        public void Update(Player player, bool evenUpdate)
        {
            if (!evenUpdate || player == null || player.room == null)
            {
                return;
            }

            bool canGrow = player.Consious && !player.dead;
            if (!canGrow)
            {
                _lightEnergy = Mathf.Max(0f, _lightEnergy - DarkDecay);
                return;
            }

            bool inSunlight = IsLit(player);
            _lightEnergy += inSunlight ? SunlightPerUpdate : AmbientLightPerUpdate;

            _sparkTimer++;
            if (inSunlight && _sparkTimer % SparkInterval == 0 && player.room.readyForAI)
            {
                SpawnLightSpark(player);
            }

            if (player.FoodInStomach >= player.MaxFoodInStomach && player.playerState.quarterFoodPoints >= 3)
            {
                _lightEnergy = Mathf.Min(_lightEnergy, DemandFor(player));
                return;
            }

            if (_lightEnergy < DemandFor(player))
            {
                return;
            }

            _lightEnergy = 0f;
            player.AddQuarterFood();
            player.AddQuarterFood();

            if (player.playerState.quarterFoodPoints >= 3)
            {
                SpawnMatureFeedback(player);
            }
        }

        private static float DemandFor(Player player)
        {
            float satiation = player.MaxFoodInStomach <= 0
                ? 1f
                : Mathf.Clamp01(player.FoodInStomach / (float)player.MaxFoodInStomach);
            return BaseLightPerFood * Mathf.Lerp(HungryLightDemandScale, 1f, satiation);
        }

        private static bool IsLit(Player player)
        {
            Room room = player.room;
            if (room == null)
            {
                return false;
            }

            Vector2 pos = player.mainBodyChunk.pos;
            if (!room.VisualContact(pos, pos + new Vector2(0f, 2000f)))
            {
                return false;
            }

            RoomCamera camera = CameraForPlayer(player);
            if (camera == null)
            {
                return false;
            }

            return room.DarknessOfPoint(camera, pos) <= 0.02f;
        }

        private static RoomCamera CameraForPlayer(Player player)
        {
            RainWorldGame game = player.room.game;
            if (game.cameras.Length == 0)
            {
                return null;
            }

            int cam = player.room.cameraPositions.Length > 0
                ? player.room.CameraViewingPoint(player.mainBodyChunk.pos)
                : -1;

            return cam >= 0 && cam < game.cameras.Length ? game.cameras[cam] : game.cameras[0];
        }

        private static void SpawnLightSpark(Player player)
        {
            Vector2 origin = player.mainBodyChunk.pos + new Vector2(Random.Range(-6f, 6f), Random.Range(2f, 14f));
            Vector2 velocity = new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.4f, 1.4f));
            player.room.AddObject(new Spark(origin, velocity, new Color(0.55f, 0.95f, 0.3f), null, 14, 28));
        }

        private static void SpawnMatureFeedback(Player player)
        {
            Vector2 origin = player.mainBodyChunk.pos + Vector2.up * 12f;
            player.room.AddObject(new Spark(origin, Vector2.up * 0.8f, new Color(0.9f, 0.25f, 0.2f), null, 18, 30));
        }
    }

    public static class PlayerFeatures
    {
        private static readonly ConditionalWeakTable<Player, AppleCat> Modules = new ConditionalWeakTable<Player, AppleCat>();

        private static readonly ConditionalWeakTable<Player, Sprout> Sprouts = new ConditionalWeakTable<Player, Sprout>();

        public static AppleCat AppleCatModule(this Player player)
        {
            if (player == null)
            {
                return null;
            }

            return Modules.TryGetValue(player, out AppleCat module) ? module : null;
        }

        public static Sprout SproutModule(this Player player)
        {
            if (player == null)
            {
                return null;
            }

            return Sprouts.TryGetValue(player, out Sprout sprout) ? sprout : null;
        }

        public static Sprout SproutModule(this PlayerGraphics graphics)
        {
            return (graphics?.owner as Player).SproutModule();
        }

        public static Sprout EnsureSprout(this PlayerGraphics graphics)
        {
            if (graphics == null)
            {
                return null;
            }

            Player player = graphics.owner as Player;
            if (player == null || !Features.Matches(player))
            {
                return null;
            }

            if (Sprouts.TryGetValue(player, out Sprout existing))
            {
                return existing;
            }

            Sprout created = new Sprout(graphics);
            Sprouts.Add(player, created);
            return created;
        }

        public static AppleCat Attach(this Player player)
        {
            if (player == null || !Features.Matches(player))
            {
                return null;
            }

            if (Modules.TryGetValue(player, out AppleCat existing))
            {
                return existing;
            }

            AppleCat created = new AppleCat();
            Modules.Add(player, created);
            return created;
        }
    }
}