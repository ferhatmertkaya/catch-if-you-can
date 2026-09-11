using System.Collections.Generic;
using UnityEngine;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// The touch HUD's sprite set, loaded once and kept.
    ///
    /// <para>
    /// The HUD is built in code rather than authored as a prefab, so there is no inspector slot
    /// to drop a sprite into and the art has to be reachable by path. That is why these live
    /// under <c>Resources</c> rather than beside the other icons in <c>Art/</c>: a sprite outside
    /// a Resources folder with nothing referencing it is stripped from the build, and the HUD
    /// would come up as white squares on device while looking correct in the editor.
    /// </para>
    ///
    /// <para>
    /// Every accessor returns null rather than throwing when the art is missing. An
    /// <see cref="UnityEngine.UI.Image"/> with a null sprite draws a plain rectangle, so a broken
    /// import costs the shape and never the controls.
    /// </para>
    /// </summary>
    public static class HudSprites
    {
        private const string Root = "UI/Controls/";

        /// <summary>
        /// Where the PLACEHOLDER equipment icons live, one per id from
        /// <see cref="Equipment.EquipmentIds"/>.
        /// </summary>
        private const string EquipmentRoot = "UI/Equipment/Icon_Equipment_";

        private static readonly Dictionary<string, Sprite> _equipment =
            new Dictionary<string, Sprite>();

        private static Sprite _disc, _ring, _glow, _chevron, _reticle;
        private static Sprite _flashlight, _sprint, _crouch, _interact, _pickup, _drop;
        private static bool _loaded;

        /// <summary>Filled circle. The glass body of every control.</summary>
        public static Sprite Disc { get { Load(); return _disc; } }

        /// <summary>Thin circular outline. The border of every control.</summary>
        public static Sprite Ring { get { Load(); return _ring; } }

        /// <summary>
        /// Disc with a soft radial falloff, for the drop shadow and the torch's lit glow. A
        /// hard-edged disc reads as a grey ring around the button wherever the scene behind is
        /// bright, and an edge is exactly what a shadow must not have.
        /// </summary>
        public static Sprite Glow { get { Load(); return _glow; } }

        /// <summary>Small ring with a centre dot.</summary>
        public static Sprite Reticle { get { Load(); return _reticle; } }

        /// <summary>Single chevron, pointing up. Rotated in the UI for the other three.</summary>
        public static Sprite Chevron { get { Load(); return _chevron; } }

        public static Sprite Flashlight { get { Load(); return _flashlight; } }
        public static Sprite Sprint { get { Load(); return _sprint; } }
        public static Sprite Crouch { get { Load(); return _crouch; } }

        /// <summary>Finger on a ring. Shown for anything in reach that is used rather than taken.</summary>
        public static Sprite Interact { get { Load(); return _interact; } }

        /// <summary>Reaching hand. Shown when there is something in reach to take.</summary>
        public static Sprite Pickup { get { Load(); return _pickup; } }

        /// <summary>The same hand with the item falling out of it.</summary>
        public static Sprite Drop { get { Load(); return _drop; } }

        /// <summary>
        /// A PLACEHOLDER icon for an equipment id, or null when there is none for it.
        ///
        /// <para>
        /// <b>This is not the icon architecture; it is the last resort behind it.</b>
        /// <see cref="Equipment.EquipmentDefinition.Icon"/> is the authored slot and it wins
        /// wherever it is filled in. All eleven of those slots are currently empty, and an
        /// <see cref="UnityEngine.UI.Image"/> with a null sprite draws NOTHING - so a slot
        /// holding the DOTS projector was pixel-for-pixel a slot holding nothing, which is the
        /// one distinction the inventory row exists to make. FINAL ART IS STILL NEEDED: filling
        /// in the definition's Icon retires the placeholder for that item with no code change.
        /// </para>
        ///
        /// <para>
        /// A failed load is cached as null, so a missing icon costs one <c>Resources.Load</c>
        /// per id for the life of the process rather than one per HUD refresh.
        /// </para>
        /// </summary>
        public static Sprite EquipmentPlaceholder(string equipmentId)
        {
            if (string.IsNullOrEmpty(equipmentId))
                return null;

            if (_equipment.TryGetValue(equipmentId, out Sprite cached))
                return cached;

            Sprite loaded = Resources.Load<Sprite>(EquipmentRoot + equipmentId);
            _equipment[equipmentId] = loaded;

            if (loaded == null)
            {
                Debug.LogWarning("[CIYC] No inventory icon for equipment id '" + equipmentId +
                                 "'. Fill in EquipmentDefinition.Icon, or add " +
                                 EquipmentRoot + equipmentId + ".png under Resources. Until " +
                                 "then that slot draws empty while holding something.");
            }

            return loaded;
        }

        private static void Load()
        {
            if (_loaded)
                return;

            _loaded = true;
            _disc = Resources.Load<Sprite>(Root + "UI_Disc");
            _ring = Resources.Load<Sprite>(Root + "UI_Ring");
            _glow = Resources.Load<Sprite>(Root + "UI_Glow");
            _chevron = Resources.Load<Sprite>(Root + "UI_Chevron");
            _reticle = Resources.Load<Sprite>(Root + "UI_Reticle");
            _flashlight = Resources.Load<Sprite>(Root + "Icon_Flashlight");
            _sprint = Resources.Load<Sprite>(Root + "Icon_Sprint");
            _crouch = Resources.Load<Sprite>(Root + "Icon_Crouch");
            _interact = Resources.Load<Sprite>(Root + "Icon_Interact");
            _pickup = Resources.Load<Sprite>(Root + "Icon_Pickup");
            _drop = Resources.Load<Sprite>(Root + "Icon_Drop");

            if (_disc == null || _ring == null)
            {
                Debug.LogWarning("[CIYC] HUD shape sprites missing from Resources/" + Root +
                                 ". The controls will draw as rectangles. Check that the PNGs " +
                                 "in Assets/CatchIfYouCan/Resources/UI/Controls imported as " +
                                 "Sprite (2D and UI).");
            }
        }

        /// <summary>Drops the cache. For editor tooling that reimports the sprites.</summary>
        public static void Invalidate()
        {
            _loaded = false;
            _equipment.Clear();
        }
    }
}
