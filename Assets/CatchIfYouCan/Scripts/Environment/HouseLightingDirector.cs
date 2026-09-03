using CatchIfYouCan.Core;
using CatchIfYouCan.Interaction;
using CatchIfYouCan.Procedural;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatchIfYouCan.Environment
{
    /// <summary>
    /// Turns a generated house from a lit blockout into a dark one worth carrying a torch
    /// through.
    ///
    /// <para>
    /// <b>It changes no geometry and no layout.</b> Every light it touches was already placed by
    /// <c>PrimitiveRoomFactory</c> at the room's light socket; what changes is colour, warmth,
    /// range, shadowing and - mostly - whether it is switched on at all. Nothing here reads or
    /// advances a generation stream, so the layout, its hash and <c>GenerationVersion</c> are
    /// untouched by construction rather than by promise.
    /// </para>
    ///
    /// <para>
    /// <b>The flashlight has to matter.</b> A house where every room light is on is a house
    /// where the torch is decoration and the dark is somewhere else. Most practicals start off;
    /// which ones are on is derived from the mission seed, so two players given the same case
    /// walk into the same house lit the same way, and the breaker box and the light switches
    /// the generator already installs remain the way to change that.
    /// </para>
    ///
    /// <para>
    /// <b>Dark is not black.</b> A room with nothing in it but a torch beam is unreadable and
    /// unnavigable, which is tedious rather than frightening. A cold, very low ambient and a
    /// thin fog keep silhouettes and doorways legible at the edge of the beam; the horror is in
    /// what the beam is not pointing at, and that only works if the player can tell there is a
    /// room there.
    /// </para>
    /// </summary>
    public static class HouseLightingDirector
    {
        private const string LogTag = "[CIYC][Lighting] ";

        // ---- the night --------------------------------------------------------------------

        /// <summary>Ambient. Cold, and barely there.</summary>
        private static readonly Color NightAmbient = new Color(0.055f, 0.062f, 0.082f);

        /// <summary>Fog. The same cold, so distance reads as depth rather than as haze.</summary>
        private static readonly Color NightFog = new Color(0.045f, 0.05f, 0.068f);

        private const float FogDensity = 0.022f;

        /// <summary>Moonlight through the windows. Blue, weak, and the only thing outside.</summary>
        private static readonly Color Moonlight = new Color(0.62f, 0.71f, 1f);

        private const float MoonIntensity = 0.28f;

        /// <summary>
        /// Roughly one room in four keeps a light on. Enough to give the house landmarks and
        /// somewhere for a flicker event to be seen; not enough to replace the torch.
        /// </summary>
        private const int LitRoomInverseFrequency = 4;

        /// <summary>
        /// Dresses the whole house. Called once, when the mission goes live - not while it is
        /// only being looked at through a portal, because ambient and fog belong to the active
        /// scene and applying them early would repaint the lobby.
        /// </summary>
        public static void Apply(GeneratedHouse house, int seed)
        {
            if (house == null)
            {
                CIYCLog.Warn(LogTag + "No house to light.");
                return;
            }

            ApplyEnvironment();
            ApplyMoon(house);

            int lit = 0, total = 0;
            foreach (GeneratedRoomInstance room in house.Rooms)
            {
                if (room?.Root == null)
                    continue;

                foreach (Light light in room.Root.GetComponentsInChildren<Light>(true))
                {
                    if (light == null || light.type == LightType.Directional)
                        continue;

                    total++;
                    DressPractical(light, room.Category);

                    bool on = ShouldStartLit(seed, room.NodeId);
                    if (on)
                        lit++;
                    SetRoomLightOn(light, on);
                }
            }

            CIYCLog.Info(LogTag + "Lit the house: " + lit + " of " + total +
                         " practical(s) start on (seed " + seed + ").");
        }

        // ---- the scene ---------------------------------------------------------------------

        /// <summary>
        /// Ambient and fog for the mission scene.
        ///
        /// <para>
        /// Written to <c>RenderSettings</c>, which belongs to whichever scene is active - so
        /// this must run after the mission scene has become the active one, and it deliberately
        /// does not touch the lobby's.
        /// </para>
        /// </summary>
        private static void ApplyEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = NightAmbient;
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = NightFog;
            RenderSettings.fogDensity = FogDensity;
        }

        /// <summary>
        /// The moon. One cold directional light, low, angled so it rakes across the house
        /// rather than lighting it flat, and casting soft shadows so window frames read.
        /// </summary>
        private static void ApplyMoon(GeneratedHouse house)
        {
            Light moon = FindDirectional(house);

            if (moon == null)
            {
                var go = new GameObject("Moonlight");
                if (house.Root != null)
                    go.transform.SetParent(house.Root, false);
                moon = go.AddComponent<Light>();
                moon.type = LightType.Directional;
            }

            moon.color = Moonlight;
            moon.intensity = MoonIntensity;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.85f;

            // Low and off to one side: a moon overhead lights floors, and what wants lighting
            // is the wall opposite each window.
            moon.transform.rotation = Quaternion.Euler(24f, 138f, 0f);
        }

        private static Light FindDirectional(GeneratedHouse house)
        {
            if (house.Root == null)
                return null;

            foreach (Light light in house.Root.GetComponentsInChildren<Light>(true))
            {
                if (light != null && light.type == LightType.Directional)
                    return light;
            }

            return null;
        }

        // ---- one practical -------------------------------------------------------------------

        /// <summary>
        /// Colour, warmth, reach and shadowing for one room light.
        ///
        /// <para>
        /// The temperature is what separates the rooms: a kitchen and a bathroom are lit by
        /// something fluorescent and unkind, a living room and a bedroom by a shaded bulb, a
        /// basement by whatever was cheapest. Same fixture, different room, and the house stops
        /// reading as one repeated space.
        /// </para>
        /// </summary>
        private static void DressPractical(Light light, RoomCategory category)
        {
            light.useColorTemperature = true;
            light.colorTemperature = TemperatureFor(category);
            light.color = Color.white;
            light.intensity = IntensityFor(category);
            light.range = RangeFor(category);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.9f;
        }

        /// <summary>Kelvin. Lower is warmer.</summary>
        private static float TemperatureFor(RoomCategory category)
        {
            switch (category)
            {
                case RoomCategory.Kitchen:
                case RoomCategory.Bathroom:
                case RoomCategory.UtilityRoom:
                    return 4600f;      // fluorescent, unflattering
                case RoomCategory.Basement:
                case RoomCategory.Garage:
                    return 3600f;      // bare bulb
                case RoomCategory.Attic:
                    return 3200f;
                case RoomCategory.Hallway:
                    return 3000f;
                default:
                    return 2650f;      // a shaded lamp in a lived-in room
            }
        }

        /// <summary>
        /// Deliberately low across the board. These are single bulbs in dark rooms at night,
        /// not the lighting rig of a level that wants to be seen.
        /// </summary>
        private static float IntensityFor(RoomCategory category)
        {
            switch (category)
            {
                case RoomCategory.Kitchen:
                case RoomCategory.Bathroom:
                    return 0.85f;
                case RoomCategory.Basement:
                case RoomCategory.Attic:
                case RoomCategory.Garage:
                    return 0.35f;
                default:
                    return 0.6f;
            }
        }

        private static float RangeFor(RoomCategory category)
        {
            switch (category)
            {
                case RoomCategory.Hallway:
                    return 6.5f;
                case RoomCategory.Basement:
                case RoomCategory.Attic:
                    return 4.5f;
                default:
                    return 5.5f;
            }
        }

        private static void SetRoomLightOn(Light light, bool on)
        {
            // Through the controller where there is one, so the switch on the wall and the
            // breaker box agree with what the room is actually doing. Setting light.enabled
            // behind the controller's back is how a switch ends up reporting "on" for a dark
            // room.
            var controller = light.GetComponentInParent<LightController>();
            if (controller != null)
            {
                controller.SetOn(on, invokeEvents: false);
                return;
            }

            light.enabled = on;
        }

        // ---- which rooms are lit -----------------------------------------------------------

        /// <summary>
        /// Stable per (seed, room), and computed here rather than drawn from a generation
        /// stream.
        ///
        /// <para>
        /// <b>This must not touch <c>CiycRandom</c>.</b> Lighting is presentation: if it drew
        /// from a layout stream it would either move the layout or grow a stream that the layout
        /// hash then has to account for, and either one turns a lighting tweak into a
        /// determinism change. A local hash of the seed and the room's node id gives the same
        /// answer on every machine while being invisible to generation.
        /// </para>
        /// </summary>
        private static bool ShouldStartLit(int seed, int nodeId)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u;
                h ^= (uint)nodeId * 2246822519u;
                h ^= h >> 15;
                h *= 2654435761u;
                h ^= h >> 13;
                return h % LitRoomInverseFrequency == 0u;
            }
        }
    }
}
