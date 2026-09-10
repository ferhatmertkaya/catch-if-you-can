#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using CatchIfYouCan.Core;
using CatchIfYouCan.Interaction;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// Checks that a spawned item is actually PLAYABLE, not merely visible.
    ///
    /// <para>
    /// <b>A visually correct item that nothing can interact with passed four review rounds.</b>
    /// The model was there, the size was right, the material was right - and looking at it did
    /// nothing, because the interaction ray had no collider to find. Every one of those rounds
    /// went looking at a different isolated symptom, because "it is there and it does nothing"
    /// has no single obvious owner: it could be the collider, the layer, the mask, the pickup
    /// component, the definition, the inventory or the hierarchy, and from inside the game all
    /// seven look the same.
    /// </para>
    ///
    /// <para>
    /// So the chain is walked once, at spawn, and every link is named. A missing link is an
    /// ERROR with the link's name in it, which is the difference between a session spent
    /// searching and a line that says where to look.
    /// </para>
    ///
    /// <para>
    /// Development only, and it decides nothing: it reports on an item that has already been
    /// built and changes none of it.
    /// </para>
    /// </summary>
    public static class EquipmentSpawnDiagnostic
    {
        private const string LogTag = "[CIYC][EquipCheck] ";

        public static void Report(EquipmentBase item, string context)
        {
            if (item == null)
            {
                CIYCLog.Error(LogTag + context + ": no item at all.");
                return;
            }

            string id = item.Definition != null ? item.Definition.Id : "<no definition>";
            string shown = item.Definition != null ? item.Definition.DisplayName : "<no name>";
            var problems = new List<string>();
            var lines = new List<string>();

            // 1. something drawn
            var renderers = item.GetComponentsInChildren<Renderer>(true);
            int drawn = 0;
            int effect = 0;
            foreach (Renderer r in renderers)
            {
                if (r == null)
                    continue;
                // Counted apart, because an effect volume is off for most of its life and a
                // report of "1 enabled of 3" reads as two broken renderers rather than as one
                // device and two switched-off effects.
                if (EffectVolume.Encloses(r.transform)) { effect++; continue; }
                if (r.enabled) drawn++;
            }
            lines.Add("visual: " + drawn + " enabled renderer(s) of " +
                      (renderers.Length - effect) +
                      (effect > 0 ? " (+" + effect + " effect volume(s))" : string.Empty));
            if (drawn == 0) problems.Add("nothing is drawn");

            // 2. exactly one visual root - two means the clone rebuilt on top of itself
            var roots = item.GetComponentsInChildren<EquipmentVisualRoot>(true);
            lines.Add("visual roots: " + roots.Length);
            if (roots.Length > 1)
                problems.Add(roots.Length + " visual roots: the clone rebuilt on top of itself");

            // 3. the definition
            lines.Add("definition: " + id + " / \"" + shown + "\"");
            if (item.Definition == null) problems.Add("no EquipmentDefinition bound");

            // 4. the pickup component
            var pickup = item.GetComponent<InteractivePickup>();
            lines.Add("InteractivePickup: " + (pickup != null ? "present" : "MISSING"));
            if (pickup == null) problems.Add("no InteractivePickup, so the ray resolves nothing");

            // 5. something the ray can hit
            var colliders = item.GetComponentsInChildren<Collider>(true);
            int hittable = 0;
            string shapes = string.Empty;
            foreach (Collider c in colliders)
            {
                if (c == null) continue;
                shapes += (shapes.Length > 0 ? ", " : "") + c.GetType().Name +
                          (c.enabled ? "(on" : "(OFF") + (c.isTrigger ? ",trigger)" : ")");
                if (c.enabled) hittable++;
            }
            lines.Add("colliders: " + (colliders.Length == 0 ? "none" : shapes));
            if (hittable == 0)
                problems.Add("no ENABLED collider, so the interaction ray passes straight through");

            // 6. the layer the ray has to see
            lines.Add("layer: " + item.gameObject.layer + " (" +
                      LayerMask.LayerToName(item.gameObject.layer) + ")");

            // 7. physics state - a placed item must not fall or be shoved
            var body = item.GetComponent<Rigidbody>();
            lines.Add("rigidbody: " + (body == null
                          ? "none"
                          : (body.isKinematic ? "kinematic" : "DYNAMIC") +
                            (body.useGravity ? ", gravity ON" : ", no gravity")));

            string head = LogTag + context + " '" + id + "'";
            if (problems.Count == 0)
            {
                CIYCLog.Info(head + " is playable. " + string.Join("; ", lines.ToArray()) + ".");
                return;
            }

            CIYCLog.Error(head + " is VISIBLE BUT NOT PLAYABLE - " +
                          string.Join("; ", problems.ToArray()) + ". Full chain: " +
                          string.Join("; ", lines.ToArray()) + ".");
        }
    }
}
#endif
