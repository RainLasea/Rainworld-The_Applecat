using applecat.Gameplay;
using BepInEx;

namespace applecat
{
    [BepInPlugin(ModId, "The AppleCat", "0.2.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string ModId = "applecat";

        private bool _hooked;

        private void OnEnable()
        {
            Features.Initialize();
            PlayerGraphicsPatches.Apply();
            _hooked = true;
        }

        private void OnDisable()
        {
            if (_hooked)
            {
                PlayerGraphicsPatches.Unapply();
                _hooked = false;
            }
        }
    }
}