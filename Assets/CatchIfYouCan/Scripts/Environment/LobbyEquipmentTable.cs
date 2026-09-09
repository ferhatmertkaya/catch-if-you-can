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

        [Header("The table")]
        [Tooltip("The surface to lay the kit out on. Left empty, the nearest prop with a " +
                 "collider is measured instead.")]
        [SerializeField] private Transform table;

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
            Transform surface = ResolveTable();
            if (surface == null)
            {
                CIYCLog.Error(LogTag + "No table to lay the kit out on, so none of it exists. " +
                              "Assign 'table' on this component.");
                return;
            }

            if (!TryMeasureTop(surface, out Bounds top))
            {
                CIYCLog.Error(LogTag + "'" + surface.name + "' has neither a collider nor a " +
                              "renderer, so its top cannot be measured. Nothing is placed: " +
                              "eleven items dropped at a guessed height is eleven items in the " +
                              "floor.");
                return;
            }

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

            Transform surface = ResolveTable();
            Bounds top = fallbackTop;
            if (surface != null && TryMeasureTop(surface, out Bounds measured))
                top = measured;

            PlaceAll(top);

            if (takeTorchOutOfHands)
                StartCoroutine(TakeTorchWhenPlayerArrives());
        }

        // ---- the table ------------------------------------------------------------------------

        private Transform ResolveTable()
        {
            if (table != null)
                return table;

            // By SHAPE and role, never by name: a hard-coded object name that stops resolving
            // fails silently and forever (CLAUDE.md mistakes 3 and 10). A table is the prop in
            // this room with the largest flat top the player can reach.
            //
            // The height test asks where the TOP is, not where the middle is. That was the bug:
            // it tested the centre of the union box, so a low table measuring 0.30 m tall sat at
            // a centre of 0.15 and fell under a 0.25 floor - a table rejected for being a table.
            // What you put an object ON is the top surface, so that is the number to test.
            Transform best = null;
            float bestArea = 0f;
            var seen = new System.Text.StringBuilder();

            foreach (Art.RoomProp prop in FindObjectsByType<Art.RoomProp>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (prop == null)
                    continue;

                if (!TryMeasureTop(prop.transform, out Bounds b))
                {
                    Append(seen, prop.name, "no collider and no renderer");
                    continue;
                }

                float area = b.size.x * b.size.z;

                // Reachable. Above this a bookcase's top is over the player's head, and below it
                // the object is the floor or a rug.
                if (b.max.y > MaximumTopHeight)
                {
                    Append(seen, prop.name, "top at " + b.max.y.ToString("F2") + " m, too high");
                    continue;
                }

                if (b.max.y < MinimumTopHeight)
                {
                    Append(seen, prop.name, "top at " + b.max.y.ToString("F2") + " m, too low");
                    continue;
                }

                Append(seen, prop.name, "top " + b.max.y.ToString("F2") + " m, " +
                                        area.ToString("F2") + " m2");

                if (area > bestArea)
                {
                    bestArea = area;
                    best = prop.transform;
                }
            }

            // Said out loud either way. A rejection that is silent about WHY covers two different
            // problems with one symptom - "there is no table in this room" and "the table was
            // there and the test threw it out" need opposite fixes, and the second one cost a
            // session in this project already.
            if (best != null)
            {
                CIYCLog.Info(LogTag + "table = '" + best.name + "' (" + bestArea.ToString("F2") +
                             " m2). Considered:" + seen);
            }
            else
            {
                CIYCLog.Error(LogTag + "no prop in this room measures as a table between " +
                              MinimumTopHeight.ToString("F2") + " and " +
                              MaximumTopHeight.ToString("F2") + " m. Considered:" +
                              (seen.Length > 0 ? seen.ToString() : " <no RoomProp at all>") +
                              ". Assign 'table' on this component to settle it.");
            }

            return best;
        }

        /// <summary>Lowest surface still worth putting the kit on, in metres.</summary>
        private const float MinimumTopHeight = 0.20f;

        /// <summary>Highest surface the player can still reach over, in metres.</summary>
        private const float MaximumTopHeight = 1.40f;

        private static void Append(System.Text.StringBuilder sb, string name, string verdict)
        {
            if (sb.Length > 400)
                return;
            sb.Append(" ").Append(name).Append("(").Append(verdict).Append(")");
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
