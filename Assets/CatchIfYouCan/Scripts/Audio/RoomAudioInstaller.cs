using System.Collections;
using CatchIfYouCan.Procedural;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class RoomAudioInstaller : MonoBehaviour
    {
        [SerializeField] private RoomAudioProfile profile;
        [SerializeField] private float houseRandomMin = 7f;
        [SerializeField] private float houseRandomMax = 35f;

        private Coroutine _randomRoutine;

        public void Install(GeneratedHouse house, RoomAudioProfile overrideProfile = null)
        {
            if (house == null) return;
            profile = overrideProfile != null ? overrideProfile : profile ?? RoomAudioProfile.CreateDefaultRuntime();

            for (int i = 0; i < house.Rooms.Count; i++)
            {
                var room = house.Rooms[i];
                if (room?.Module == null || room.Root == null) continue;
                InstallRoomZone(room);
            }

            WireDoorPortals(house);
            if (_randomRoutine != null)
                StopCoroutine(_randomRoutine);
            _randomRoutine = StartCoroutine(HouseRandomEvents());
        }

        private void InstallRoomZone(GeneratedRoomInstance room)
        {
            var zoneGo = room.Root.GetComponent<RoomAudioZone>() != null
                ? room.Root
                : room.Root;
            var zone = zoneGo.GetComponent<RoomAudioZone>();
            if (zone == null)
                zone = zoneGo.AddComponent<RoomAudioZone>();

            zone.Configure(room.Module, profile);

            if (profile.HasExteriorLeakage(room.Category))
                zone.AddAmbientSpawn(room.Root.transform, "Env.Exterior.WindLeak", 0.6f);

            AddCategorySpawn(room, zone);
        }

        private void AddCategorySpawn(GeneratedRoomInstance room, RoomAudioZone zone)
        {
            string spawnId = profile.GetRandomRoomEvent(room.Category);
            var spawnPoint = new GameObject("AmbientSpawn").transform;
            spawnPoint.SetParent(room.Root.transform, false);
            spawnPoint.localPosition = Vector3.up * 1.2f;
            zone.AddAmbientSpawn(spawnPoint, spawnId, 1f);
        }

        private void WireDoorPortals(GeneratedHouse house)
        {
            for (int i = 0; i < house.Doors.Count; i++)
            {
                var conn = house.Doors[i];
                if (conn?.Door == null) continue;
                var portal = conn.Door.gameObject.GetComponent<AudioPortal>();
                if (portal == null)
                    portal = conn.Door.gameObject.AddComponent<AudioPortal>();

                RoomAudioZone zoneA = conn.RoomA?.Root?.GetComponent<RoomAudioZone>();
                RoomAudioZone zoneB = conn.RoomB?.Root?.GetComponent<RoomAudioZone>();
                portal.Configure(conn.Door, zoneA, zoneB);
            }
        }

        private IEnumerator HouseRandomEvents()
        {
            while (true)
            {
                float wait = Random.Range(houseRandomMin, houseRandomMax);
                yield return new WaitForSeconds(wait);

                var zones = FindObjectsByType<RoomAudioZone>();
                if (zones.Length == 0) continue;

                var zone = zones[Random.Range(0, zones.Length)];
                string eventId = profile.GetRandomRoomEvent(zone.Category);
                if (eventId.StartsWith("Ghost.")) continue;

                Vector3 pos = zone.GetRandomAmbientPoint(out _);
                AudioManager.Instance?.PlayEvent(eventId, pos, Random.Range(0.35f, 0.65f));
            }
        }
    }
}
