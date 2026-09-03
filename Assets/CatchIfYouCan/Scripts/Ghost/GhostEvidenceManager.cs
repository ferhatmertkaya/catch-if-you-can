using UnityEngine;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Equipment;

namespace CatchIfYouCan.Ghost
{
    /// <summary>
    /// Puts the ghost's evidence into the house, so there is something for the equipment to find.
    ///
    /// <para>
    /// <b>It no longer announces anything.</b> Every branch below used to end in
    /// <c>GameEvents.EvidenceDetected</c>, on a timer, for all three of the ghost's evidence
    /// types - and objectives complete on that event, the journal records it and the audio
    /// directors react to it. So forty-five seconds after the mission started, three objectives
    /// had completed themselves, evidence the player had not looked for was in the journal, and
    /// none of it required a device, a battery, a room or a player. It was the third and largest
    /// of the doors into the evidence system that skipped every check; AH closed the other two.
    /// </para>
    ///
    /// <para>
    /// What is left is what this class was always for: manifestation. It spawns the EMF spot,
    /// the UV mark and the orb, and it makes the air near the ghost cold. Those are real things
    /// in real places, and the detector, the lamp and the thermometer find them or do not.
    /// Whether that adds up to evidence is decided by <see cref="EvidenceValidator"/>, from a
    /// device's observation, against this ghost's own profile.
    /// </para>
    ///
    /// <para>
    /// The types with nothing to spawn - EVP, spectral grid, parabolic anomaly, electronic
    /// distortion - are not phenomena that leave an object lying in a room. Each is produced by
    /// the ghost's own state at the moment a device asks: the recorder needs the ghost in range,
    /// the projector needs it standing in the field, the microphone needs it to have made a
    /// noise, the torch needs it close enough to pull the current. Those checks live in the
    /// devices, where the asking happens, and they read this ghost - so there is nothing for
    /// this class to schedule on their behalf.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(GhostController))]
    public class GhostEvidenceManager : MonoBehaviour
    {
        [SerializeField] private GameObject emfSpotPrefab;
        [SerializeField] private GameObject uvMarkPrefab;
        [SerializeField] private GameObject orbPrefab;
        [SerializeField] private float spawnRadius = 6f;
        [SerializeField] private float temperatureDropAmount = 8f;
        [SerializeField] private float evidenceRefreshInterval = 45f;

        private GhostController _ghost;
        private float _nextSpawnTime;
        private float _localTemperatureOffset;

        public float TemperatureOffset => _localTemperatureOffset;

        public float GetTemperatureOffset() => _localTemperatureOffset;

        private void Awake()
        {
            _ghost = GetComponent<GhostController>();
        }

        private void Update()
        {
            if (_ghost?.Definition == null) return;
            if (Time.time < _nextSpawnTime) return;

            SpawnEvidenceForDefinition();
            _nextSpawnTime = Time.time + evidenceRefreshInterval * Random.Range(0.8f, 1.2f);
        }

        public void SpawnEvidenceForDefinition()
        {
            var def = _ghost.Definition;
            Manifest(def.Evidence1);
            Manifest(def.Evidence2);
            Manifest(def.Evidence3);
        }

        /// <summary>
        /// Puts one evidence type into the world near the ghost, if it is the kind that leaves
        /// something behind. Nothing here reports a detection: a mark on a wall is not a finding
        /// until somebody shines a lamp on it.
        /// </summary>
        public void Manifest(EvidenceType type)
        {
            Vector3 origin = _ghost != null ? _ghost.transform.position : transform.position;
            Vector3 offset = Random.insideUnitSphere * spawnRadius;
            offset.y = Random.Range(-0.5f, 0.5f);

            Manifest(type, origin + offset);
        }

        /// <summary>
        /// The same, at a place the ghost chose - a mirror it wrote on, a door it slammed. The
        /// interaction knows where it happened and this does not, so it passes the position in.
        /// </summary>
        public void Manifest(EvidenceType type, Vector3 position)
        {
            switch (type)
            {
                case EvidenceType.EMFSurge:
                    SpawnEmfSpot(position);
                    break;

                case EvidenceType.UVTraces:
                    SpawnPrefab(uvMarkPrefab, position, "UV trace");
                    break;

                case EvidenceType.GhostOrb:
                    SpawnOrb(position);
                    break;

                case EvidenceType.FreezingTemperature:
                    ApplyTemperatureInfluence();
                    break;

                case EvidenceType.PhysicalDisturbance:
                    SpawnPrefab(uvMarkPrefab, position, "physical disturbance");
                    break;

                // Nothing to leave lying about. See the class note: these live in the devices
                // that ask for them, because each one is a question about the ghost's state at
                // the moment of asking rather than an object in a room.
                case EvidenceType.EVPResponse:
                case EvidenceType.SpectralGrid:
                case EvidenceType.ParabolicAnomaly:
                case EvidenceType.ElectronicDistortion:
                    break;
            }
        }

        private void SpawnEmfSpot(Vector3 position)
        {
            if (emfSpotPrefab != null)
            {
                Instantiate(emfSpotPrefab, position, Quaternion.identity, transform);
                return;
            }

            var spotGo = new GameObject("EMFSpot");
            spotGo.transform.SetParent(transform);
            spotGo.transform.position = position;
            var spot = spotGo.AddComponent<EMFSpot>();
            spot.Initialize(0.85f, 10f, 0.12f, 4f);
        }

        private void SpawnPrefab(GameObject prefab, Vector3 position, string what)
        {
            if (prefab == null)
            {
                // Said, not swallowed. A null prefab here means the ghost exhibits an evidence
                // type that leaves nothing in the house, so the device looking for it will
                // search a room that is genuinely empty and the player will conclude the wrong
                // thing about the ghost.
                if (!_warnedMissingPrefab)
                {
                    _warnedMissingPrefab = true;
                    Core.CIYCLog.Warn("GhostEvidenceManager has no prefab for " + what +
                                      ", so this ghost leaves none. Nothing can find it.");
                }

                return;
            }

            Instantiate(prefab, position, Quaternion.identity, transform);
        }

        private bool _warnedMissingPrefab;

        private void SpawnOrb(Vector3 position)
        {
            GameObject prefab = orbPrefab;
            if (prefab == null)
            {
                var orbGo = new GameObject("GhostOrb");
                orbGo.transform.position = position;
                var orb = orbGo.AddComponent<GhostOrb>();
                orb.Configure(EvidenceType.GhostOrb, Random.Range(0.4f, 0.9f), true);
                return;
            }

            var instance = Instantiate(prefab, position, Quaternion.identity);
            var orbComp = instance.GetComponent<GhostOrb>();
            if (orbComp != null)
                orbComp.Configure(EvidenceType.GhostOrb, Random.Range(0.4f, 0.9f), true);
        }

        private void ApplyTemperatureInfluence()
        {
            _localTemperatureOffset = -temperatureDropAmount * Random.Range(0.6f, 1f);
            CancelInvoke(nameof(DecayTemperature));
            Invoke(nameof(DecayTemperature), 20f);
        }

        private void DecayTemperature()
        {
            _localTemperatureOffset = Mathf.MoveTowards(_localTemperatureOffset, 0f, Time.deltaTime * 2f);
            if (Mathf.Abs(_localTemperatureOffset) > 0.01f)
                Invoke(nameof(DecayTemperature), 1f);
            else
                _localTemperatureOffset = 0f;
        }
    }
}
