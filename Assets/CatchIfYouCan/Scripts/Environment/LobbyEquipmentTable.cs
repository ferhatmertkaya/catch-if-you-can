using System.Collections;
using System.Collections.Generic;
using CatchIfYouCan.Core;
using CatchIfYouCan.Equipment;
using CatchIfYouCan.Interaction;
using CatchIfYouCan.Player;
using UnityEngine;

namespace CatchIfYouCan.Environment
{
    /// <summary>
    /// Lays the whole kit out on the lobby's table, and takes the torch out of the player's hand
    /// so it is one of them.
    ///
    /// <para>
    /// <b>The list is read, never written.</b> Every item comes from the equipment catalog the
    /// game already has - <see cref="EquipmentCatalog"/>, filled from
    /// <see cref="EquipmentDefinitionFactory"/> - and every object is built by
    /// <see cref="MissionEquipmentInstaller.BuildItem"/>, the same call the mission uses to put
    /// something in a hand. A second list here would be a second answer to "what equipment
    /// exists", and the two would diverge the first time somebody added a twelfth item.
    /// </para>
    ///
    /// <para>
    /// <b>Nothing new is invented.</b> An id with no runtime implementation still gets an object,
    /// because <c>EquipmentRuntimeFactory</c> answers with a DEV_PLACEHOLDER for anything its
    /// switch does not handle - that placeholder carries the real definition, so it reads as the
    /// item it stands for and cannot be mistaken for content. Which items those are is reported
    /// rather than hidden.
    /// </para>
    ///
    /// <para>
    /// <b>The torch is moved, not removed.</b> <see cref="PlayerFactory"/> builds one into the
    /// player's hand and that is not this component's business to change - so the lobby takes
    /// the one it finds out of the torch slot and leaves the table's flashlight to be picked up
    /// instead. The mission path is untouched: this component only exists in the lobby.
    /// </para>
    ///
    /// <para>
    /// <b>Measured, not assumed.</b> The table's top is read from its own collider and renderer
    /// bounds and the grid is derived from that. The lobby's table is built at runtime by its
    /// prop component, so any size written here as a constant would be a guess about an object
    /// that does not exist yet.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Lobby Equipment Table")]
    public sealed class LobbyEquipmentTable : MonoBehaviour
    {
        private const string LogTag = "[CIYC][LobbyTable] ";

        [Header("Where the kit goes")]
        [Tooltip("A surface to lay the kit out on. LEFT EMPTY the kit lies on the floor in " +
                 "front of the player's arrival point, which is what you want while the room " +
                 "is still being built.")]
        [SerializeField] private Transform table;

        [Tooltip("Where the player arrives. Left empty, it is taken from the LobbyAtmosphere " +
                 "on this same object, which already has it wired.")]
        [SerializeField] private Transform spawnAnchor;

        [Tooltip("How far in front of the spawn the kit lies, in metres. Far enough that it is " +
                 "not inside the player's own capsule, near enough to see on the first frame.")]
        [SerializeField, Min(0.5f)] private float floorDistance = 1.6f;

        [Tooltip("How much floor the kit is spread over, in metres.")]
        [SerializeField] private Vector2 floorArea = new Vector2(2.6f, 1.1f);

        [Header("Layout")]
        [Tooltip("Minimum gap between two items, edge to edge. Below this they read as a pile.")]
        [SerializeField, Min(0.01f)] private float minimumGap = 0.06f;

        [Tooltip("How far above the measured top an item is placed, so nothing sinks into it.")]
        [SerializeField, Min(0f)] private float surfaceClearance = 0.005f;

        [Header("The torch")]
        [Tooltip("Take the torch out of the lobby player's hands, so the one on the table is " +
                 "the one they carry in. Off, they start holding it as before.")]
        [SerializeField] private bool takeTorchOutOfHands = true;

        private readonly List<EquipmentBase> _placed = new List<EquipmentBase>();
        private bool _torchTaken;

        /// <summary>What is lying on the table right now.</summary>
        public IReadOnlyList<EquipmentBase> Placed => _placed;

        private void Start()
        {
            if (!TryResolveSurface(out Bounds top))
                return;

            StartCoroutine(BuildRoutine(top));
        }

        /// <summary>
        /// One frame late, because the table builds its own geometry in <c>Start</c> and the
        /// order of two <c>Start</c> calls in the same scene is not defined. Measuring first
        /// would measure the authored placeholder collider rather than the table that ends up
        /// standing there.
        /// </summary>
        private IEnumerator BuildRoutine(Bounds fallbackTop)
        {
            yield return null;

            Bounds top = TryResolveSurface(out Bounds measured) ? measured : fallbackTop;

            PlaceAll(top);

            if (takeTorchOutOfHands)
                StartCoroutine(TakeTorchWhenPlayerArrives());
        }

        // ---- the table ------------------------------------------------------------------------

        /// <summary>
        /// Where the kit goes: an assigned surface if there is one, otherwise the FLOOR in front
        /// of where the player arrives.
        ///
        /// <para>
        /// It used to hunt the room for a table by shape, and that hunt is gone. It tested the
        /// centre of a prop's measured box against a height window, so a 0.30 m table - centre at
        /// 0.15 - fell under a 0.25 m floor and was rejected for being a table. Nothing was
        /// placed, and eleven invisible items look exactly like eleven items that were never
        /// built. A guessing search that silently answers "nothing" is worse than no search.
        /// </para>
        /// <para>
        /// So: assign <c>table</c> and the kit goes on it. Assign nothing and it lies on the
        /// floor where the player can see it on the first frame, which is what the room needs
        /// while it is being built.
        /// </para>
        /// </summary>
        private bool TryResolveSurface(out Bounds top)
        {
            top = default;

            if (table != null)
            {
                if (TryMeasureTop(table, out top))
                {
                    CIYCLog.Info(LogTag + "laying the kit out on '" + table.name + "', top at " +
                                 top.max.y.ToString("F2") + " m.");
                    return true;
                }

                CIYCLog.Warn(LogTag + "'" + table.name + "' has neither a collider nor a " +
                             "renderer, so its top cannot be measured. Falling back to the floor.");
            }

            return TryResolveFloor(out top);
        }

        /// <summary>
        /// A patch of floor in front of the player's arrival point, found by looking DOWN with
        /// real physics rather than by assuming y = 0. The lobby floor is an authored object at
        /// its own height, and an item laid at a guessed height is an item in the floor or
        /// hovering over it.
        /// </summary>
        private bool TryResolveFloor(out Bounds top)
        {
            top = default;

            Transform spawn = ResolveSpawn();
            if (spawn == null)
            {
                CIYCLog.Error(LogTag + "no player spawn to lay the kit out in front of, and no " +
                              "'table' assigned either, so there is nowhere to put it. Wire " +
                              "'playerSpawn' on the LobbyAtmosphere beside this component.");
                return false;
            }

            // In FRONT of the spawn, not on it: the player arrives inside their own capsule, and
            // eleven items at the spawn point are eleven items inside the player.
            Vector3 centre = spawn.position + spawn.forward * floorDistance;

            Physics.SyncTransforms();

            float y;
            if (Physics.Raycast(centre + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit,
                                4f, ~0, QueryTriggerInteraction.Ignore))
            {
                y = hit.point.y;
            }
            else
            {
                y = spawn.position.y;
                CIYCLog.Warn(LogTag + "no floor under " + centre.ToString("F1") +
                             ", so the kit is laid at the spawn's own height instead.");
            }

            centre.y = y;
            top = new Bounds(centre, new Vector3(floorArea.x, 0f, floorArea.y));

            CIYCLog.Info(LogTag + "no table assigned, so the kit lies on the floor at " +
                         centre.ToString("F1") + " over " + floorArea.x.ToString("F1") + " x " +
                         floorArea.y.ToString("F1") + " m.");
            return true;
        }

        /// <summary>
        /// The arrival point, asked of the sibling that already has it wired rather than looked
        /// up by name - a hard-coded object name that stops resolving fails silently and forever.
        /// </summary>
        private Transform ResolveSpawn()
        {
            if (spawnAnchor != null)
                return spawnAnchor;

            var atmosphere = GetComponent<LobbyAtmosphere>();
            return atmosphere != null ? atmosphere.PlayerSpawn : null;
        }

        /// <summary>
        /// The world box of everything under this transform, from colliders first and renderers
        /// second. Colliders first because that is what the player bumps into and what an item
        /// has to rest on; renderers as the fallback for a prop whose visual is bigger than its
        /// collision.
        /// </summary>
        private static bool TryMeasureTop(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;

            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                if (c == null || c.isTrigger)
                    continue;
                Bounds b = c.bounds;
                if (b.size == Vector3.zero)
                    continue;
                if (!any) { bounds = b; any = true; } else bounds.Encapsulate(b);
            }

            if (any)
                return true;

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null)
                    continue;
                Bounds b = r.bounds;
                if (b.size == Vector3.zero)
                    continue;
                if (!any) { bounds = b; any = true; } else bounds.Encapsulate(b);
            }

            return any;
        }

        // ---- the kit ---------------------------------------------------------------------------

        private void PlaceAll(Bounds top)
        {
            // The ONE source: the authored catalog when there is one, the code-built definitions
            // when there is not. Asking for the catalog directly would be right in the editor and
            // empty in a build that has not had its assets written yet, and an empty table is
            // indistinguishable from a broken one.
            EquipmentDefinition[] all = EquipmentDefinitionFactory.All();

            var definitions = new List<EquipmentDefinition>();
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    EquipmentDefinition d = all[i];
                    if (d != null && !string.IsNullOrEmpty(d.Id))
                        definitions.Add(d);
                }
            }

            if (definitions.Count == 0)
            {
                CIYCLog.Error(LogTag + "No equipment definitions at all, so there is nothing to " +
                              "lay out. This component reads the existing definitions rather " +
                              "than carrying its own list, so an empty table here means the " +
                              "catalog and the code fallback are both empty.");
                return;
            }

            // A grid that fits the measured top, not a spacing somebody typed. Columns along the
            // longer horizontal axis so a narrow table reads as rows rather than as a queue.
            bool xIsLong = top.size.x >= top.size.z;
            float across = xIsLong ? top.size.x : top.size.z;
            float deep = xIsLong ? top.size.z : top.size.x;

            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(definitions.Count * across /
                                                                  Mathf.Max(0.01f, deep))));
            columns = Mathf.Min(columns, definitions.Count);
            int rows = Mathf.CeilToInt(definitions.Count / (float)columns);

            float pitchAcross = across / columns;
            float pitchDeep = deep / Mathf.Max(1, rows);

            if (pitchAcross < minimumGap || pitchDeep < minimumGap)
            {
                CIYCLog.Warn(LogTag + definitions.Count + " items on a " +
                             top.size.x.ToString("F2") + " x " + top.size.z.ToString("F2") +
                             " m top leaves " + Mathf.Min(pitchAcross, pitchDeep).ToString("F3") +
                             " m per item, under the " + minimumGap.ToString("F2") +
                             " m minimum. They are spread past the table's edge rather than " +
                             "stacked inside each other - a bigger table is the fix.");
                pitchAcross = Mathf.Max(pitchAcross, minimumGap);
                pitchDeep = Mathf.Max(pitchDeep, minimumGap);
            }

            var placeholders = new List<string>();
            float topY = top.max.y + surfaceClearance;

            for (int i = 0; i < definitions.Count; i++)
            {
                EquipmentDefinition definition = definitions[i];

                int column = i % columns;
                int row = i / columns;

                float a = (column + 0.5f - columns * 0.5f) * pitchAcross;
                float d = (row + 0.5f - rows * 0.5f) * pitchDeep;

                var position = new Vector3(
                    top.center.x + (xIsLong ? a : d),
                    topY,
                    top.center.z + (xIsLong ? d : a));

                EquipmentBase item = Place(definition, position);
                if (item == null)
                    continue;

                if (!EquipmentRuntimeFactory.HasRuntimePath(definition.Id))
                    placeholders.Add(definition.Id);

                _placed.Add(item);
            }

            CIYCLog.Info(LogTag + _placed.Count + " of " + definitions.Count +
                         " item(s) on the table in " + rows + " x " + columns + " at " +
                         pitchAcross.ToString("F2") + " x " + pitchDeep.ToString("F2") + " m" +
                         (placeholders.Count > 0
                             ? "; DEBUG PLACEHOLDER for: " + string.Join(", ", placeholders)
                             : "; every item has a real runtime object") + ".");
        }

        /// <summary>
        /// One item, standing on the table and waiting to be taken.
        ///
        /// <para>
        /// It rests rather than falls: a Rigidbody left dynamic would have eleven items settle
        /// into each other on the first physics step and slide off. It becomes dynamic again the
        /// moment the player drops it, which is the inventory's job and already works.
        /// </para>
        /// </summary>
        private EquipmentBase Place(EquipmentDefinition definition, Vector3 position)
        {
            EquipmentBase item = MissionEquipmentInstaller.BuildItem(definition);
            if (item == null)
            {
                CIYCLog.Warn(LogTag + "'" + definition.Id + "' produced no object, so it is not " +
                             "on the table.");
                return null;
            }

            Transform t = item.transform;
            t.SetParent(transform, false);
            t.position = position;
            t.rotation = Quaternion.identity;

            // Sit the object ON the surface rather than through it: its own pivot is rarely at
            // its base, and this project's purchased pieces put pivots metres from their mesh.
            if (TryMeasureTop(t, out Bounds b))
                t.position += Vector3.up * Mathf.Max(0f, position.y - b.min.y);

            var body = item.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            var pickup = item.GetComponent<InteractivePickup>();
            if (pickup == null)
                pickup = item.gameObject.AddComponent<InteractivePickup>();

            pickup.Configure(item, "Take " + definition.DisplayName, false);

            return item;
        }

        // ---- the torch --------------------------------------------------------------------------

        /// <summary>
        /// Waits for the lobby player and empties their torch slot.
        ///
        /// <para>
        /// The player is spawned by the mode controller after this room is switched on, so there
        /// is nobody to take anything from in <c>Start</c>. Polled rather than event-driven
        /// because the alternative is a second subscription to the player registry for a debug
        /// convenience, and it stops the moment it succeeds.
        /// </para>
        /// </summary>
        private IEnumerator TakeTorchWhenPlayerArrives()
        {
            var wait = new WaitForSeconds(0.25f);

            while (!_torchTaken && isActiveAndEnabled)
            {
                foreach (PlayerInventory inventory in FindObjectsByType<PlayerInventory>(
                             FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (inventory == null || !inventory.HasTorch)
                        continue;

                    // Destroyed rather than dropped: the table already carries a flashlight built
                    // from the same definition, and dropping this one would leave two torches in
                    // one room - which is how this project ended up with two flashlights once
                    // already (CLAUDE.md mistake 1).
                    inventory.RemoveItemFromSlot(PlayerInventory.TorchSlotIndex, false);
                    _torchTaken = true;

                    CIYCLog.Info(LogTag + "The torch is on the table rather than in the player's " +
                                 "hand. Mission play is unaffected: this runs in the lobby only.");
                    break;
                }

                yield return wait;
            }
        }
    }
}
