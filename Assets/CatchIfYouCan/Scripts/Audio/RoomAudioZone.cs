using System.Collections.Generic;
using CatchIfYouCan.Procedural;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    [System.Serializable]
    public class RoomAmbientSpawnPoint
    {
        public Transform Point;
        public string EventId;
        [Range(0f, 1f)] public float Weight = 1f;
    }

    public class RoomAudioZone : MonoBehaviour
    {
        [SerializeField] private RoomCategory category = RoomCategory.Hallway;
        [SerializeField] private string roomToneEventId = "Env.RoomTone.Generic";
        [SerializeField] private string reverbProfileId = "Hallway";
        [SerializeField] private bool exteriorLeakage;
        [SerializeField] private List<RoomAmbientSpawnPoint> ambientSpawns = new List<RoomAmbientSpawnPoint>();
        [SerializeField] private List<AudioPortal> portals = new List<AudioPortal>();

        private RoomModule _module;
        private Collider _zoneCollider;

        public RoomCategory Category => category;
        public string RoomToneEventId => roomToneEventId;
        public string ReverbProfileId => reverbProfileId;
        public bool ExteriorLeakage => exteriorLeakage;
        public IReadOnlyList<AudioPortal> Portals => portals;
        public Bounds ZoneBounds => _zoneCollider != null ? _zoneCollider.bounds : new Bounds(transform.position, Vector3.one * 4f);

        public void Configure(RoomModule module, RoomAudioProfile profile)
        {
            _module = module;
            if (module != null)
                category = module.Category;

            if (profile != null)
            {
                roomToneEventId = profile.GetRoomTone(category);
                reverbProfileId = profile.GetReverbProfile(category);
                exteriorLeakage = profile.HasExteriorLeakage(category);
            }

            EnsureCollider();
            CollectPortals();
        }

        public void AddAmbientSpawn(Transform point, string eventId, float weight = 1f)
        {
            ambientSpawns.Add(new RoomAmbientSpawnPoint { Point = point, EventId = eventId, Weight = weight });
        }

        public Vector3 GetRandomAmbientPoint(out string eventId)
        {
            eventId = null;
            if (ambientSpawns.Count == 0)
            {
                eventId = roomToneEventId;
                return transform.position;
            }

            float total = 0f;
            for (int i = 0; i < ambientSpawns.Count; i++)
                total += ambientSpawns[i].Weight;

            float roll = Random.Range(0f, total);
            for (int i = 0; i < ambientSpawns.Count; i++)
            {
                roll -= ambientSpawns[i].Weight;
                if (roll <= 0f)
                {
                    eventId = ambientSpawns[i].EventId;
                    return ambientSpawns[i].Point != null ? ambientSpawns[i].Point.position : transform.position;
                }
            }

            var last = ambientSpawns[ambientSpawns.Count - 1];
            eventId = last.EventId;
            return last.Point != null ? last.Point.position : transform.position;
        }

        public bool ContainsPoint(Vector3 worldPoint)
        {
            return ZoneBounds.Contains(worldPoint);
        }

        private void EnsureCollider()
        {
            _zoneCollider = GetComponent<Collider>();
            if (_zoneCollider != null) return;

            var box = gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            if (_module != null)
            {
                var bounds = _module.LocalBounds;
                box.center = bounds.center;
                box.size = bounds.size;
            }
            else
            {
                box.size = new Vector3(6f, 3f, 6f);
            }
            _zoneCollider = box;
        }

        private void CollectPortals()
        {
            portals.Clear();
            GetComponentsInChildren(true, portals);
        }
    }
}
