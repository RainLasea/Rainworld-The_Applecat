using applecat.Graphics.Sprout;
using UnityEngine;

namespace applecat.Gameplay
{
    internal static class PlayerGraphicsPatches
    {
        private const int FaceSpriteIndex = 9;

        private static bool _applied;

        public static void Apply()
        {
            if (_applied)
            {
                return;
            }

            try
            {
                On.PlayerGraphics.Update += PlayerGraphics_Update;
                On.PlayerGraphics.Reset += PlayerGraphics_Reset;
                On.PlayerGraphics.SuckedIntoShortCut += PlayerGraphics_SuckedIntoShortCut;
                On.PlayerGraphics.InitiateSprites += PlayerGraphics_InitiateSprites;
                On.PlayerGraphics.AddToContainer += PlayerGraphics_AddToContainer;
                On.PlayerGraphics.DrawSprites += PlayerGraphics_DrawSprites;
                On.PlayerGraphics.ApplyPalette += PlayerGraphics_ApplyPalette;

                On.Player.Update += Player_Update;
                On.Player.ctor += Player_ctor;
                On.Lizard.ctor += Lizard_ctor;
            }
            catch
            {
                Unapply();
                throw;
            }

            _applied = true;
        }

        public static void Unapply()
        {
            _applied = false;

            On.PlayerGraphics.Update -= PlayerGraphics_Update;
            On.PlayerGraphics.Reset -= PlayerGraphics_Reset;
            On.PlayerGraphics.SuckedIntoShortCut -= PlayerGraphics_SuckedIntoShortCut;
            On.PlayerGraphics.InitiateSprites -= PlayerGraphics_InitiateSprites;
            On.PlayerGraphics.AddToContainer -= PlayerGraphics_AddToContainer;
            On.PlayerGraphics.DrawSprites -= PlayerGraphics_DrawSprites;
            On.PlayerGraphics.ApplyPalette -= PlayerGraphics_ApplyPalette;

            On.Player.Update -= Player_Update;
            On.Player.ctor -= Player_ctor;
            On.Lizard.ctor -= Lizard_ctor;
        }

        private static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            orig(self, eu);

            self.AppleCatModule()?.Update(self, eu);
        }

        private static void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
        {
            orig(self, abstractCreature, world);
            self.Attach();
        }

        private static void Lizard_ctor(On.Lizard.orig_ctor orig, Lizard self, AbstractCreature abstractCreature, World world)
        {
            orig(self, abstractCreature, world);

            if (world != null && Features.MeanLizards.TryGet(world.game, out float ceiling))
            {
                self.spawnDataEvil = Mathf.Min(self.spawnDataEvil, ceiling);
            }
        }

        private static void PlayerGraphics_Update(On.PlayerGraphics.orig_Update orig, PlayerGraphics self)
        {
            orig(self);
            self.EnsureSprout()?.Update();
        }

        private static void PlayerGraphics_Reset(On.PlayerGraphics.orig_Reset orig, PlayerGraphics self)
        {
            orig(self);
            self.SproutModule()?.Reset();
        }

        private static void PlayerGraphics_SuckedIntoShortCut(On.PlayerGraphics.orig_SuckedIntoShortCut orig, PlayerGraphics self, Vector2 shortCutPosition)
        {
            orig(self, shortCutPosition);
            (self.owner as Player).SproutModule()?.SuckedIntoShortCut(shortCutPosition);
        }

        private static void PlayerGraphics_InitiateSprites(On.PlayerGraphics.orig_InitiateSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            orig(self, sLeaser, rCam);

            Sprout sprout = self.EnsureSprout();
            if (sprout == null)
            {
                return;
            }

            sprout.InitiateSprites(sLeaser, rCam);
        }

        private static void PlayerGraphics_AddToContainer(On.PlayerGraphics.orig_AddToContainer orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            orig(self, sLeaser, rCam, newContatiner);

            Sprout sprout = self.SproutModule();
            if (sprout == null)
            {
                return;
            }

            sprout.AddToContainer(sLeaser, rCam, newContatiner);
        }

        private static void PlayerGraphics_DrawSprites(On.PlayerGraphics.orig_DrawSprites orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);

            Player player = self.owner as Player;
            ApplyDiscoloration(self, sLeaser, player);

            Sprout sprout = self.SproutModule();
            if (sprout == null)
            {
                return;
            }

            sprout.DrawSprites(sLeaser, rCam, timeStacker, camPos);
        }

        private static void PlayerGraphics_ApplyPalette(On.PlayerGraphics.orig_ApplyPalette orig, PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            orig(self, sLeaser, rCam, palette);
            self.SproutModule()?.ApplyPalette(sLeaser, rCam, palette);
        }

        private static void ApplyDiscoloration(PlayerGraphics self, RoomCamera.SpriteLeaser sLeaser, Player player)
        {
            if (player == null || !Features.ShouldDiscolor(player))
            {
                return;
            }

            float satiation = player.MaxFoodInStomach <= 0
                ? 0f
                : Mathf.InverseLerp(0f, player.MaxFoodInStomach, player.FoodInStomach);
            Color target = Color.Lerp(Features.LeafGreen, Features.AppleRed, satiation);

            int limit = Mathf.Min(player.SproutModule()?.FirstSpriteIndex ?? int.MaxValue, sLeaser.sprites.Length);

            for (int i = 0; i < limit; i++)
            {
                if (i == FaceSpriteIndex)
                {
                    continue;
                }

                FSprite sprite = sLeaser.sprites[i];
                if (sprite == null)
                {
                    continue;
                }

                Color blended = Color.Lerp(sprite.color, target, 0.05f);
                sprite.color = blended;

                if (sprite is TriangleMesh mesh && mesh.customColor)
                {
                    for (int v = 0; v < mesh.verticeColors.Length; v++)
                    {
                        mesh.verticeColors[v] = blended;
                    }
                }
            }
        }
    }
}