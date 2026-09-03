using UnityEngine;
using CatchIfYouCan.Core;
using CatchIfYouCan.Evidence;

namespace CatchIfYouCan.Ghost
{
    [RequireComponent(typeof(GhostController))]
    public class GhostInteractionBrain : MonoBehaviour
    {
        [SerializeField] private float interactionRange = 8f;
        [SerializeField] private LayerMask interactableMask = ~0;
        [SerializeField] private string doorTag = "Door";
        [SerializeField] private string lightTag = "LightSwitch";
        [SerializeField] private string throwableTag = "Throwable";

        private GhostController _ghost;

        private void Awake()
        {
            _ghost = GetComponent<GhostController>();
        }

        public bool TryRandomInteraction()
        {
            if (_ghost?.Definition == null) return false;

            // Every branch below rolls dice and then changes something every player can see -
            // a door, a light, a thrown object, a mark on a wall. Host-only, or four machines
            // roll four different outcomes and the house disagrees with itself.
            if (!Core.SessionAuthority.CanSimulateGhost) return false;

            float roll = Random.value;
            var def = _ghost.Definition;

            if (roll < def.DoorInteractionChance * def.ResponseFrequency)
                return TryDoorInteraction(false);

            roll -= def.DoorInteractionChance * def.ResponseFrequency;
            if (roll < def.LightInteractionChance * def.ResponseFrequency)
                return TryLightInteraction();

            roll -= def.LightInteractionChance * def.ResponseFrequency;
            if (roll < def.ObjectThrowChance * def.ResponseFrequency)
                return TryObjectThrow();

            return false;
        }

        public bool TryDoorInteraction(bool slam)
        {
            var door = FindNearestTagged(doorTag);
            if (door == null) return false;

            var rb = door.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 push = (door.transform.position - transform.position).normalized;
                rb.AddForce(push * (slam ? 6f : 2f), ForceMode.Impulse);
            }
            else
            {
                door.transform.Rotate(0f, slam ? 75f : 30f, 0f, Space.World);
            }

            GameEvents.DoorOpened();
            GameEvents.NoiseGenerated(slam ? 0.7f : 0.35f, door.transform.position);
            return true;
        }

        public bool TryLightInteraction()
        {
            var lightSwitch = FindNearestTagged(lightTag);
            if (lightSwitch == null)
            {
                var lights = FindObjectsByType<Light>();
                if (lights.Length == 0) return false;
                var light = lights[Random.Range(0, lights.Length)];
                light.enabled = !light.enabled;
                light.intensity = light.enabled ? Random.Range(0.3f, 1.2f) : 0f;
            }
            else
            {
                var light = lightSwitch.GetComponentInChildren<Light>();
                if (light != null)
                {
                    light.enabled = !light.enabled;
                    if (light.enabled)
                        light.intensity *= Random.Range(0.2f, 1f);
                }
            }

            GameEvents.BreakerChanged();
            return true;
        }

        public bool TryObjectThrow()
        {
            var obj = FindNearestTagged(throwableTag);
            if (obj == null) return false;

            var rb = obj.GetComponent<Rigidbody>();
            if (rb == null)
                rb = obj.AddComponent<Rigidbody>();

            Vector3 dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) + 0.3f;
            rb.AddForce(dir.normalized * Random.Range(3f, 7f), ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);

            // The object is now evidence, for a while. This is the only thing in the game that
            // makes PhysicalDisturbance true: the evidence is not a reading, it is the claim
            // that something moved on its own, and the claim is only honest if something did.
            //
            // Deliberately not on the door interaction. A door the ghost swung is a noise and a
            // scare; it is not an object left out of place for somebody to photograph, and
            // marking every slam would make the evidence mean nothing.
            GhostDisturbance.MarkObject(obj);

            GameEvents.NoiseGenerated(0.55f, obj.transform.position);
            if (GhostActivitySystem.Instance != null)
                GhostActivitySystem.Instance.RegisterGhostEvent(0.6f);

            return true;
        }

        public void ExecuteHorrorInteraction(HorrorEventType type)
        {
            switch (type)
            {
                case HorrorEventType.ChairMove:
                case HorrorEventType.CabinetOpening:
                    TryDoorInteraction(false);
                    break;
                case HorrorEventType.ToyActivation:
                case HorrorEventType.TVActivation:
                    GameEvents.BreakerChanged();
                    break;
                case HorrorEventType.MirrorWriting:
                    // Writing on a mirror leaves a mark on the mirror. It does not tell the
                    // player they have found UV Traces - that used to be exactly what it did,
                    // announcing the evidence with nothing written anywhere and no lamp ever
                    // switched on. Now something is actually there to be found.
                    LeaveWrittenMark();
                    break;
            }
        }

        /// <summary>
        /// Leaves the mark the ghost just made where the ghost made it, so a UV lamp has
        /// something to find.
        ///
        /// <para>
        /// Not anchored to a mirror on purpose. The only mirror this project builds is the one
        /// the DEV labs install, so a mark placed on "the mirror" would exist in a lab and
        /// nowhere in a mission - which is the same nothing this branch used to leave behind,
        /// dressed up.
        /// </para>
        /// </summary>
        private void LeaveWrittenMark()
        {
            var evidence = GetComponent<GhostEvidenceManager>();
            if (evidence == null)
                return;

            Vector3 spot = transform.position + transform.forward * 1.2f + Vector3.up * 1.4f;
            evidence.Manifest(EvidenceType.UVTraces, spot);
        }

        private GameObject FindNearestTagged(string tag)
        {
            GameObject[] objects;
            try
            {
                objects = GameObject.FindGameObjectsWithTag(tag);
            }
            catch
            {
                return null;
            }

            if (objects == null || objects.Length == 0) return null;

            GameObject best = null;
            float bestDist = interactionRange;

            for (int i = 0; i < objects.Length; i++)
            {
                float d = Vector3.Distance(transform.position, objects[i].transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = objects[i];
                }
            }

            return best;
        }
    }
}
