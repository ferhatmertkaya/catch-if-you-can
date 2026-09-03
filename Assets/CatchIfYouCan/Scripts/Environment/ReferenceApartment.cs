using CatchIfYouCan.Procedural;
using UnityEngine;

namespace CatchIfYouCan.Environment
{
    /// <summary>
    /// One fixed, walkable two-storey apartment, built from this transform.
    ///
    /// <para>
    /// <b>It is a reference, not a replacement for generation.</b> Nothing here touches the
    /// deterministic generator, its seeds, its layout hash or <c>GenerationVersion</c> - the
    /// apartment is hand-authored in code the way the lobby is, and it exists so that lighting,
    /// scale, room sizes, stair pitch, ghost navigation and equipment have a real interior to
    /// be judged against before a generator is asked to produce one. See
    /// <c>Docs/TWO_FLOOR_GENERATION.md</c> for how the generator reaches the same shapes later.
    /// </para>
    ///
    /// <para>
    /// Built at an offset from the lobby, in the same scene, because the portal shows it
    /// <em>live</em>. A view through a portal is a second camera rendering real geometry; a
    /// scene that is not loaded has no geometry to render, so the apartment has to exist while
    /// the player is still standing in the lobby.
    /// </para>
    ///
    /// <para>
    /// Room rectangles are in metres, measured from this transform, and every room is tagged
    /// with its <see cref="RoomCategory"/> so that ghost behaviour and prop spawning can read
    /// the same categories they read from a generated house.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Reference Apartment")]
    public sealed class ReferenceApartment : MonoBehaviour
    {
        [Header("Surfaces")]
        [Tooltip("The authored Victorian wall material. Left empty a flat runtime colour is " +
                 "used, which is what makes a blockout look like a blockout - assign the real " +
                 "one with Catch If You Can > Environment > Dress Reference Apartment.")]
        [SerializeField] private Material wallMaterial;

        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material ceilingMaterial;
        [SerializeField] private Material trimMaterial;

        [Header("Lighting")]
        [Tooltip("A dim warm lamp per room. The apartment is looked into through a portal from " +
                 "a lit lobby, so a pitch-black interior reads as a wall rather than a room.")]
        [SerializeField] private bool buildRoomLights = true;

        [SerializeField] private Color lampColour = new Color(1f, 0.86f, 0.68f);
        [SerializeField, Min(0f)] private float lampIntensity = 1.6f;
        [SerializeField, Min(0.5f)] private float lampRange = 6.5f;

        [Header("Arrival")]
        [Tooltip("Where a player arriving through the portal stands, relative to this " +
                 "transform. Inside the entrance hall, facing into the flat.")]
        [SerializeField] private Vector3 arrivalLocalPosition = new Vector3(0f, 0.05f, 1.2f);

        [SerializeField] private float arrivalYaw;

        private bool _built;

        /// <summary>Where the portal delivers a player. Read by the portal, not by gameplay.</summary>
        public Vector3 ArrivalPosition => transform.TransformPoint(arrivalLocalPosition);

        public Quaternion ArrivalRotation =>
            transform.rotation * Quaternion.Euler(0f, arrivalYaw, 0f);

        /// <summary>
        /// What the portal camera should look at when the player is still in the lobby: the
        /// entrance hall, straight ahead. Not a fixed shot - the portal renders from the
        /// player's own transformed eye - but it is the anchor the portal pair is built around.
        /// </summary>
        public Transform ViewAnchor { get; private set; }

        private ApartmentShell.Surfaces _surfaces;

        private void Awake()
        {
            Build();
        }

        /// <summary>
        /// Builds the whole flat. Safe to call twice; the second call does nothing.
        /// </summary>
        public void Build()
        {
            if (_built)
                return;
            _built = true;

            _surfaces = new ApartmentShell.Surfaces
            {
                Wall = wallMaterial != null ? wallMaterial : Fallback(new Color(0.62f, 0.58f, 0.52f)),
                Floor = floorMaterial != null ? floorMaterial : Fallback(new Color(0.29f, 0.22f, 0.17f)),
                Ceiling = ceilingMaterial != null ? ceilingMaterial : Fallback(new Color(0.80f, 0.78f, 0.74f)),
                Trim = trimMaterial != null ? trimMaterial : Fallback(new Color(0.34f, 0.28f, 0.23f)),
            };

            BuildGroundFloor();
            BuildUpperFloor();
            BuildStairwell();

            var anchor = new GameObject("ApartmentViewAnchor");
            anchor.transform.SetParent(transform, false);
            anchor.transform.localPosition = arrivalLocalPosition + new Vector3(0f, 1.6f, 2.0f);
            ViewAnchor = anchor.transform;
        }

        // ---- ground floor -------------------------------------------------------------------
        //
        // Footprint is 11 x 9 m. The hall runs north from the entrance with the living room and
        // kitchen off it, which is the shape of a narrow terraced house rather than an open plan
        // flat - corners to look round, and doorways a ghost can stand in.

        private const float GroundY = 0f;

        private void BuildGroundFloor()
        {
            Transform floor = Section("Floor_0");

            Room(floor, "Hall", RoomCategory.Hallway, new Rect(-1.4f, 0f, 2.8f, 9f), GroundY);
            Room(floor, "LivingRoom", RoomCategory.LivingRoom, new Rect(-6.4f, 0f, 5f, 5.4f), GroundY);
            Room(floor, "Kitchen", RoomCategory.Kitchen, new Rect(1.4f, 0f, 4.2f, 4.2f), GroundY);
            Room(floor, "DiningRoom", RoomCategory.DiningRoom, new Rect(1.4f, 4.2f, 4.2f, 4.8f), GroundY);
            Room(floor, "Cloakroom", RoomCategory.Bathroom, new Rect(-6.4f, 5.4f, 5f, 3.6f), GroundY);

            // The entrance wall carries the portal opening. It is left as a hole rather than a
            // door leaf: the portal IS the door, and a leaf in it would be the thing the player
            // is looking at instead of the flat.
            ApartmentShell.Wall(floor, "Wall_Entrance", new Vector2(-1.4f, 0f), new Vector2(1.4f, 0f),
                GroundY, _surfaces, new ApartmentShell.Opening(1.4f, 1.2f, 2.4f));

            // Hall to living room, hall to kitchen, kitchen to dining.
            ApartmentShell.Wall(floor, "Wall_HallW", new Vector2(-1.4f, 0f), new Vector2(-1.4f, 9f),
                GroundY, _surfaces, ApartmentShell.Opening.Door(2.4f), ApartmentShell.Opening.Door(7.0f));
            ApartmentShell.Wall(floor, "Wall_HallE", new Vector2(1.4f, 0f), new Vector2(1.4f, 9f),
                GroundY, _surfaces, ApartmentShell.Opening.Door(2.0f), ApartmentShell.Opening.Door(6.4f));
            ApartmentShell.Wall(floor, "Wall_KitchenDining", new Vector2(1.4f, 4.2f), new Vector2(5.6f, 4.2f),
                GroundY, _surfaces, new ApartmentShell.Opening(2.1f, 1.6f, 2.2f));

            // Outer envelope.
            ApartmentShell.Wall(floor, "Wall_West", new Vector2(-6.4f, 0f), new Vector2(-6.4f, 9f), GroundY, _surfaces);
            ApartmentShell.Wall(floor, "Wall_East", new Vector2(5.6f, 0f), new Vector2(5.6f, 9f), GroundY, _surfaces);
            ApartmentShell.Wall(floor, "Wall_North", new Vector2(-6.4f, 9f), new Vector2(5.6f, 9f), GroundY, _surfaces);
            ApartmentShell.Wall(floor, "Wall_SouthW", new Vector2(-6.4f, 0f), new Vector2(-1.4f, 0f), GroundY, _surfaces);
            ApartmentShell.Wall(floor, "Wall_SouthE", new Vector2(1.4f, 0f), new Vector2(5.6f, 0f), GroundY, _surfaces);
        }

        // ---- upper floor --------------------------------------------------------------------

        private static readonly float UpperY = ApartmentShell.StoreyPitch;

        private void BuildUpperFloor()
        {
            Transform floor = Section("Floor_1");

            Room(floor, "Landing", RoomCategory.Hallway, new Rect(-1.4f, 0f, 2.8f, 9f), UpperY);
            Room(floor, "MasterBedroom", RoomCategory.Bedroom, new Rect(-6.4f, 0f, 5f, 5.4f), UpperY);
            Room(floor, "SecondBedroom", RoomCategory.KidsRoom, new Rect(1.4f, 0f, 4.2f, 4.2f), UpperY);
            Room(floor, "Bathroom", RoomCategory.Bathroom, new Rect(1.4f, 4.2f, 4.2f, 4.8f), UpperY);
            Room(floor, "Study", RoomCategory.Office, new Rect(-6.4f, 5.4f, 5f, 3.6f), UpperY);

            ApartmentShell.Wall(floor, "Wall_LandingW", new Vector2(-1.4f, 0f), new Vector2(-1.4f, 9f),
                UpperY, _surfaces, ApartmentShell.Opening.Door(2.4f), ApartmentShell.Opening.Door(7.0f));
            ApartmentShell.Wall(floor, "Wall_LandingE", new Vector2(1.4f, 0f), new Vector2(1.4f, 9f),
                UpperY, _surfaces, ApartmentShell.Opening.Door(2.0f), ApartmentShell.Opening.Door(6.4f));

            ApartmentShell.Wall(floor, "Wall_West", new Vector2(-6.4f, 0f), new Vector2(-6.4f, 9f), UpperY, _surfaces);
            ApartmentShell.Wall(floor, "Wall_East", new Vector2(5.6f, 0f), new Vector2(5.6f, 9f), UpperY, _surfaces);
            ApartmentShell.Wall(floor, "Wall_North", new Vector2(-6.4f, 9f), new Vector2(5.6f, 9f), UpperY, _surfaces);
            ApartmentShell.Wall(floor, "Wall_South", new Vector2(-6.4f, 0f), new Vector2(5.6f, 0f), UpperY, _surfaces);
        }

        // ---- the stairwell ------------------------------------------------------------------

        private void BuildStairwell()
        {
            Transform section = Section("Stairwell");

            // The flight climbs the north end of the hall. The hole in the upper slab is made by
            // simply not building a ceiling over that stretch of hall - see Room().
            ApartmentShell.Stairs(section, "Stairs_Ground",
                new Vector3(0f, GroundY, 5.4f), 1.9f, 3.4f, 14, 0f, _surfaces);
        }

        // ---- helpers -------------------------------------------------------------------------

        private Transform Section(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        /// <summary>
        /// A floor slab, a ceiling and a category tag. The ceiling is skipped over the top of
        /// the stair flight so the two storeys are one connected space rather than two boxes.
        /// </summary>
        private void Room(Transform parent, string name, RoomCategory category, Rect footprint,
                          float floorY)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.AddComponent<ApartmentRoom>().Configure(category, footprint, floorY);

            ApartmentShell.Floor(root.transform, "Floor", footprint, floorY, _surfaces);

            bool overStairwell = Mathf.Approximately(floorY, UpperY) &&
                                 footprint.Overlaps(new Rect(-1.4f, 5.0f, 2.8f, 4.0f));
            if (!overStairwell)
                ApartmentShell.Ceiling(root.transform, "Ceiling", footprint, floorY, _surfaces);

            if (buildRoomLights)
                Lamp(root.transform, footprint, floorY);
        }

        private void Lamp(Transform parent, Rect footprint, float floorY)
        {
            var go = new GameObject("RoomLamp");
            go.transform.SetParent(parent, false);
            go.transform.localPosition =
                new Vector3(footprint.center.x, floorY + ApartmentShell.StoreyHeight - 0.45f,
                            footprint.center.y);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = lampColour;
            light.intensity = lampIntensity;
            light.range = lampRange;
            // Additional lights cast no shadows in this project's URP asset, so asking for them
            // costs the setting and buys nothing.
            light.shadows = LightShadows.None;
        }

        private static Material Fallback(Color colour)
        {
            Shader lit = Art.CiycShaders.FindLit();
            if (lit == null)
                return null;

            var material = new Material(lit) { name = "Apartment_Blockout_Runtime" };
            material.color = colour;
            return material;
        }
    }
}
