using CatchIfYouCan.Content;
using CatchIfYouCan.Procedural.Deterministic;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    /// <summary>
    /// A room built BY HAND, from numbers in the inspector.
    ///
    /// <para>
    /// Put this on an empty GameObject, set the size, tick the walls that carry a door or a
    /// window, right-click the component and choose "Raum bauen". It calls the same
    /// <see cref="ModularRoomBuilder"/> the house generator calls, so what appears is the
    /// production path and not a mock-up - the same meshes, the same UVs in metres, the same HQ
    /// materials, the same colliders, and the same door and window inserts.
    /// </para>
    /// <para>
    /// It changes nothing about generation. The generator derives its rooms from the layout, as
    /// it always has; this hands the builder a room description written by a person instead.
    /// Nothing here is read by the mission, nothing is serialised into a scene the game loads,
    /// and no number here reaches the layout hash.
    /// </para>
    /// <para>
    /// The result is ordinary GameObjects. Once it is built you can move a wall, delete one,
    /// nudge the door, drop a light in - it is a normal hierarchy, not a live preview that
    /// overwrites your edits. Rebuilding replaces it, so build first and edit after.
    /// </para>
    /// </summary>
    [AddComponentMenu("Catch If You Can/HQ Room (hand-built)")]
    public class HQRoomAuthoring : MonoBehaviour
    {
        [Header("Groesse")]
        [Tooltip("Innenmass in Metern. Der Boden liegt bei y = 0, die Decke bei y = Hoehe.")]
        public Vector3 Size = new Vector3(6f, 3f, 6f);

        [Header("Tueren - eine echte Oeffnung, 1.25 x 2.60")]
        public bool DoorNorth;
        public bool DoorEast;
        public bool DoorSouth = true;
        public bool DoorWest;

        [Header("Fenster - Oeffnung 2.05 x 0.90 auf 1.55 m Bruestung")]
        [Tooltip("Eine Wand mit Tuer bekommt kein Fenster: die Tuer gewinnt.")]
        public bool WindowNorth = true;
        public bool WindowEast;
        public bool WindowSouth;
        public bool WindowWest;

        [Header("Inhalt")]
        [Tooltip("Woher Materialien und Tuer-/Fenstereinsaetze kommen. Leer lassen heisst: " +
                 "neutrale Grautoene, keine Einsaetze - nuetzlich, um die reine Form zu sehen.")]
        public ModularInteriorCatalog Catalog;

        [Tooltip("Nur zum Ansehen: ein Punktlicht in der Mitte, damit die Materialien lesbar " +
                 "sind. Das ist keine Spielbeleuchtung.")]
        public bool AddInspectionLight = true;

        /// <summary>
        /// The room category. It changes nothing structural here - the builder reads it only to
        /// pick category-specific module variants, and this pack supplies none - but it is what
        /// the room would be called in a real house.
        /// </summary>
        [Header("Beschriftung")]
        public RoomCategory Category = RoomCategory.Bedroom;

        private const string BuiltName = "HQ_Room_Built";

        [ContextMenu("Raum bauen")]
        public void Rebuild()
        {
            Clear();

            if (Size.x < 1f || Size.y < 2f || Size.z < 1f)
            {
                Core.CIYCLog.Error("[CIYC][HandRoom] " + Size.ToString("F2") + " ist zu klein " +
                                   "fuer einen begehbaren Raum. Mindestens 1 x 2 x 1 m.");
                return;
            }

            if (Size.y < ModularRoomBuilder.DoorHeight + 0.05f)
            {
                Core.CIYCLog.Warn("[CIYC][HandRoom] Die Decke steht auf " +
                                  Size.y.ToString("F2") + " m und die Tuer ist " +
                                  ModularRoomBuilder.DoorHeight.ToString("F2") + " m hoch - " +
                                  "es bleibt kein Sturz ueber der Oeffnung.");
            }

            if (Size.y < ModularRoomBuilder.WindowSill + ModularRoomBuilder.WindowHeight)
            {
                Core.CIYCLog.Warn("[CIYC][HandRoom] Das Fenster reicht bis " +
                                  (ModularRoomBuilder.WindowSill + ModularRoomBuilder.WindowHeight)
                                      .ToString("F2") +
                                  " m und die Decke steht auf " + Size.y.ToString("F2") +
                                  " m - die Oeffnung schneidet die Decke.");
            }

            var room = new LayoutRoom(
                roomId: 0,
                archetypeId: "HAND_BUILT",
                category: Category,
                cell: new GridCell(0, 0),
                rotationIndex: 0,
                positionMm: new Vec3i(0, 0, 0),
                sizeMm: new Vec3i(Millimetres(Size.x), Millimetres(Size.y), Millimetres(Size.z)),
                variantIndex: 0,
                doorMask: DoorMask(),
                openMask: 0);

            GameObject built = ModularRoomBuilder.Build(room, transform.position, transform,
                                                        Catalog, WindowMask(), out string error);

            if (built == null)
            {
                Core.CIYCLog.Error("[CIYC][HandRoom] Der Raum konnte nicht gebaut werden: " + error);
                return;
            }

            built.name = BuiltName;

            if (AddInspectionLight)
                AddLight(built.transform);

            Core.CIYCLog.Info("[CIYC][HandRoom] " + Size.ToString("F2") + " gebaut. " +
                              Report(built));
        }

        [ContextMenu("Raum loeschen")]
        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == null || child.name != BuiltName)
                    continue;

                // DestroyImmediate, because this only ever runs from the inspector: a deferred
                // Destroy would leave the old room standing for the rest of the frame and the
                // new one would be built inside it.
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        private int DoorMask()
        {
            int mask = 0;
            if (DoorNorth) mask |= LayoutRoom.DirectionMask(SocketDirection.North);
            if (DoorEast) mask |= LayoutRoom.DirectionMask(SocketDirection.East);
            if (DoorSouth) mask |= LayoutRoom.DirectionMask(SocketDirection.South);
            if (DoorWest) mask |= LayoutRoom.DirectionMask(SocketDirection.West);
            return mask;
        }

        private int WindowMask()
        {
            int mask = 0;
            if (WindowNorth) mask |= LayoutRoom.DirectionMask(SocketDirection.North);
            if (WindowEast) mask |= LayoutRoom.DirectionMask(SocketDirection.East);
            if (WindowSouth) mask |= LayoutRoom.DirectionMask(SocketDirection.South);
            if (WindowWest) mask |= LayoutRoom.DirectionMask(SocketDirection.West);
            return mask;
        }

        private static int Millimetres(float metres) => Mathf.RoundToInt(metres * 1000f);

        private void AddLight(Transform parent)
        {
            var go = new GameObject("InspectionLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, Size.y - 0.4f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = Mathf.Max(Size.x, Size.z) * 2f;
            light.intensity = 2f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.None;
        }

        private static string Report(GameObject built)
        {
            var renderers = built.GetComponentsInChildren<Renderer>(true);
            var colliders = built.GetComponentsInChildren<Collider>(true);
            var filters = built.GetComponentsInChildren<MeshFilter>(true);

            int visible = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].enabled)
                    visible++;
            }

            int triangles = 0;
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh != null)
                    triangles += filters[i].sharedMesh.triangles.Length / 3;
            }

            int activeColliders = 0;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i].enabled)
                    activeColliders++;
            }

            return "Renderer " + visible + "/" + renderers.Length +
                   ", Dreiecke " + triangles +
                   ", Collider " + activeColliders + " aktiv von " + colliders.Length + ".";
        }
    }
}
