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
        [SerializeField, Min(0.5f)] private float floorDistance = 1.2f;

        [Tooltip("How far TO THE SIDE, in metres. The spawn faces the way out, so a kit laid " +
                 "straight ahead is a kit laid in the doorway - and eleven solid objects across " +
                 "a doorway is a wall. Off to one side it is visible and out of the way.")]
        [SerializeField] private float floorSideOffset = 1.9f;

        [Tooltip("How much floor the kit is spread over, in metres.")]
        [SerializeField] private Vector2 floorArea = new Vector2(1.8f, 0.9f);

        [Tooltip("EXACT spot for the kit. Drop an empty where you want the items to lie and " +
                 "wire it here, and the search below is not used at all - the floor is still " +
                 "measured under it, so the items rest on the floor rather than at the " +
                 "empty's own height. Left empty, the spot is searched for.")]
        [SerializeField] private Transform layoutAnchor;

        [Tooltip("How much headroom the kit needs for a spot to count as clear, in metres. " +
                 "The search rejects a spot whose box is already occupied by anything solid.")]
        [SerializeField, Min(0.1f)] private float clearanceHeight = 0.5f;

        [Header("Layout")]
        [Tooltip("Minimum gap between two items, edge to edge. Below this they read as a pile.")]
        [SerializeField, Min(0.01f)] private float minimumGap = 0.06f;

        [Tooltip("How far above the measured top an item is placed, so nothing sinks into it.")]
        [SerializeField, Min(0f)] private float surfaceClearance = 0.005f;

        [Header("Test item at the spawn")]
        [Tooltip("One item laid directly at the player's feet, separately from the kit, so it " +
                 "is there to pick up and mount without having to find it. LEFT EMPTY nothing " +
                 "is laid at the spawn and every item goes on the kit grid as before.")]
        [SerializeField] private string spawnTestItemId = EquipmentIds.SpectralGrid;

        [Tooltip("How far in front of the spawn the test item lies, in metres. Far enough to " +
                 "be outside the player's own capsule, near enough to be underfoot.")]
        [SerializeField, Min(0f)] private float spawnTestDistance = 0.8f;

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

            Physics.SyncTransforms();

            // An explicit anchor wins outright. The search below is a guess that measures
            // itself; a wired anchor is a decision, and a decision beats a good guess.
            if (layoutAnchor != null)
            {
                if (TryFloorAt(layoutAnchor.position, out Bounds anchored, out string why))
                {
                    top = anchored;
                    CIYCLog.Info(LogTag + "kit laid at the wired layoutAnchor, " +
                                 top.center.ToString("F2") + ".");
                    return true;
                }

                CIYCLog.Warn(LogTag + "layoutAnchor is wired at " +
                             layoutAnchor.position.ToString("F2") + " but " + why +
                             ". Laying the kit there anyway, because a wired anchor is a " +
                             "decision and overriding it silently would be worse than an " +
                             "item in an awkward spot.");
                top = new Bounds(layoutAnchor.position,
                                 new Vector3(floorArea.x, 0f, floorArea.y));
                return true;
            }

            Transform spawn = ResolveSpawn();
            if (spawn == null)
            {
                CIYCLog.Error(LogTag + "no player spawn to lay the kit out in front of, and no " +
                              "'table' or 'layoutAnchor' assigned either, so there is nowhere " +
                              "to put it. Wire 'playerSpawn' on the LobbyAtmosphere beside " +
                              "this component, or drop an empty and wire 'layoutAnchor'.");
                return false;
            }

            // MEASURED, not assumed.
            //
            // This used to be one hard-coded step forward and one fixed step to the right, and
            // that is exactly how the kit ended up inside a wall: "to the right of the spawn"
            // is only clear floor in the room somebody had in mind while typing it. The room
            // has since been rebuilt out of modules and the right-hand side of the spawn is a
            // wall, so eleven items were laid inside it - visible from one side, unreachable,
            // and indistinguishable from items that failed to spawn at all.
            //
            // Straight ahead is not the answer either: the spawn faces the way out, so the
            // kit would be in the doorway. So candidates are TRIED, in preference order, and
            // each one has to prove it is standing on floor with nothing solid already in it.
            var candidates = new List<Vector3>();
            var labels = new List<string>();

            float[] sides = { floorSideOffset, -floorSideOffset,
                              floorSideOffset * 1.5f, -floorSideOffset * 1.5f,
                              floorSideOffset * 0.6f, -floorSideOffset * 0.6f };
            float[] aheads = { floorDistance, floorDistance * 1.7f, floorDistance * 0.6f };

            for (int a = 0; a < aheads.Length; a++)
            {
                for (int i = 0; i < sides.Length; i++)
                {
                    candidates.Add(spawn.position + spawn.forward * aheads[a] +
                                   spawn.right * sides[i]);
                    labels.Add(aheads[a].ToString("F1") + " m ahead, " +
                               sides[i].ToString("F1") + " m to the side");
                }
            }

            // Last resorts: behind the player, where there is no doorway to block.
            candidates.Add(spawn.position - spawn.forward * floorDistance);
            labels.Add(floorDistance.ToString("F1") + " m behind");
            candidates.Add(spawn.position - spawn.forward * floorDistance * 1.8f);
            labels.Add((floorDistance * 1.8f).ToString("F1") + " m behind");

            var rejected = new List<string>();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (TryFloorAt(candidates[i], out Bounds found, out string why))
                {
                    top = found;
                    CIYCLog.Info(LogTag + "kit lies on clear floor " + labels[i] + ", at " +
                                 top.center.ToString("F2") + " over " +
                                 floorArea.x.ToString("F1") + " x " +
                                 floorArea.y.ToString("F1") + " m" +
                                 (rejected.Count > 0
                                      ? " (" + rejected.Count + " nearer spot(s) rejected: " +
                                        string.Join("; ", rejected.ToArray()) + ")"
                                      : " (first spot tried)") + ".");
                    return true;
                }

                if (rejected.Count < 4)
                    rejected.Add(labels[i] + " - " + why);
            }

            // Nothing passed. Say so with what was tried, and lay the kit at the spawn's own
            // feet rather than nowhere: an item in an awkward place can be picked up, and an
            // item that was never placed cannot.
            CIYCLog.Warn(LogTag + "no clear floor found in " + candidates.Count +
                         " spot(s) around the spawn, so the kit is laid at the spawn itself. " +
                         "Tried, and rejected: " + string.Join("; ", rejected.ToArray()) +
                         ". Drop an empty where the kit should lie and wire it to " +
                         "'layoutAnchor' to settle this.");

            top = new Bounds(spawn.position, new Vector3(floorArea.x, 0f, floorArea.y));
            return true;
        }

        /// <summary>
        /// Whether the kit fits at <paramref name="probe"/>: real floor under it, and nothing
        /// solid already standing in the space it would occupy.
        ///
        /// <para>
        /// Both halves matter and they fail differently. No floor is an item that falls; an
        /// occupied box is an item inside a wall. The second is the one that actually happened,
        /// and it is the one a downward ray on its own cannot see - a ray cast inside a wall
        /// still finds the floor underneath perfectly well.
        /// </para>
        /// </summary>
        private bool TryFloorAt(Vector3 probe, out Bounds top, out string why)
        {
            top = default;

            if (!Physics.Raycast(probe + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit,
                                 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                why = "no floor under it";
                return false;
            }

            if (hit.normal.y < 0.7f)
            {
                why = "the surface under it is a slope, not a floor";
                return false;
            }

            Vector3 centre = probe;
            centre.y = hit.point.y;

            // A box standing ON the floor, not in it: lifted clear so the floor itself is not
            // what the overlap finds.
            var halfExtents = new Vector3(floorArea.x * 0.5f, clearanceHeight * 0.5f,
                                          floorArea.y * 0.5f);
            Vector3 boxCentre = centre + Vector3.up * (clearanceHeight * 0.5f + 0.05f);

            Collider[] hits = Physics.OverlapBox(boxCentre, halfExtents, Quaternion.identity,
                                                 ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i];
                if (c == null || c == hit.collider)
                    continue;

                // The player stands at the spawn and is not an obstruction to lay a kit near.
                if (c.GetComponentInParent<CharacterController>() != null)
                    continue;

                // Nor is anything this component has already put down.
                if (c.GetComponentInParent<EquipmentBase>() != null)
                    continue;

                why = "\"" + c.name + "\" is already standing in it";
                return false;
            }

            top = new Bounds(centre, new Vector3(floorArea.x, 0f, floorArea.y));
            why = null;
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
            EquipmentDefinition testItem = null;
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    EquipmentDefinition d = all[i];
                    if (d == null || string.IsNullOrEmpty(d.Id))
                        continue;

                    // The test item is laid at the spawn instead of on the grid, not as well as.
                    // Two of the same device a few metres apart is the state where "did it
                    // spawn?" stops having one answer, and that is the question this is for.
                    if (!string.IsNullOrEmpty(spawnTestItemId) &&
                        string.Equals(d.Id, spawnTestItemId, System.StringComparison.Ordinal))
                    {
                        testItem = d;
                        continue;
                    }

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

            PlaceTestItemAtSpawn(testItem);
        }

        /// <summary>
        /// Lays one item directly at the player's feet.
        ///
        /// <para>
        /// The kit goes wherever there is clear floor, which is correct and is not the same as
        /// FINDABLE: the projector is a 0.22 m object lying among ten others somewhere off to
        /// one side, and "I cannot see it" and "it was never built" look identical from the
        /// spawn. This one is where the player is standing, so the question stops being a
        /// search.
        /// </para>
        ///
        /// <para>
        /// It goes through the same <see cref="Place"/> call as everything else - same build,
        /// same pickup, same wall-mounting - so testing it tests the real item and not a
        /// special case that only exists here.
        /// </para>
        /// </summary>
        private void PlaceTestItemAtSpawn(EquipmentDefinition testItem)
        {
            if (testItem == null)
            {
                if (!string.IsNullOrEmpty(spawnTestItemId))
                    CIYCLog.Warn(LogTag + "'" + spawnTestItemId + "' is named as the test item " +
                                 "at the spawn, but no definition carries that id, so nothing " +
                                 "is laid there. Check the spelling against EquipmentIds.");
                return;
            }

            Transform spawn = ResolveSpawn();
            if (spawn == null)
            {
                CIYCLog.Warn(LogTag + "no spawn to lay '" + testItem.Id + "' at, so it goes " +
                             "nowhere. Wire 'playerSpawn' on the LobbyAtmosphere beside this.");
                return;
            }

            Vector3 probe = spawn.position + spawn.forward * spawnTestDistance;

            Physics.SyncTransforms();

            // Its own floor height, measured here rather than taken from the kit's spot - the
            // kit may have ended up on the other side of the room, at another height.
            float y = probe.y;
            if (Physics.Raycast(probe + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit,
                                4f, ~0, QueryTriggerInteraction.Ignore))
            {
                y = hit.point.y;
            }
            else
            {
                CIYCLog.Warn(LogTag + "no floor under the spawn at " + probe.ToString("F2") +
                             ", so '" + testItem.Id + "' is laid at the spawn's own height.");
            }

            probe.y = y;

            EquipmentBase item = Place(testItem, probe);
            if (item == null)
            {
                CIYCLog.Error(LogTag + "'" + testItem.Id + "' produced no object, so there is " +
                              "nothing at the spawn to pick up. This is a build failure, not a " +
                              "placement one.");
                return;
            }

            _placed.Add(item);

            // The exact position, because "it did not spawn" and "it is behind me" are the same
            // report from inside the game and different bugs outside it.
            CIYCLog.Info(LogTag + "TEST ITEM: '" + testItem.Id + "' (" + testItem.DisplayName +
                         ") lies at the player's feet, world " + item.transform.position.ToString("F2") +
                         ", " + spawnTestDistance.ToString("F1") + " m in front of the spawn" +
                         (EquipmentRuntimeFactory.HasRuntimePath(testItem.Id)
                              ? "."
                              : ", as a DEBUG PLACEHOLDER - it has no runtime object of its own."));
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
