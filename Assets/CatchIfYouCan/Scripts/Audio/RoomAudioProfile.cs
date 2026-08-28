using System;
using System.Collections.Generic;
using CatchIfYouCan.Procedural;
using UnityEngine;

namespace CatchIfYouCan.Audio
{
    [CreateAssetMenu(fileName = "RoomAudioProfile", menuName = "Catch If You Can/Room Audio Profile")]
    public class RoomAudioProfile : ScriptableObject
    {
        [Serializable]
        public class CategoryMapping
        {
            public RoomCategory Category;
            public string RoomToneEventId;
            public string ReverbProfileId;
            public string[] RandomEventIds;
            public bool ExteriorLeakage;
        }

        [SerializeField] private List<CategoryMapping> mappings = new List<CategoryMapping>();
        [SerializeField] private string defaultRoomTone = "Env.RoomTone.Generic";
        [SerializeField] private string defaultReverb = "Hallway";
        [SerializeField] private string[] houseRandomEvents =
        {
            "Env.House.PipeTick",
            "Env.House.FloorCreak",
            "Env.House.WindGust",
            "Env.House.DistantTraffic",
            "Env.House.Settling"
        };

        public IReadOnlyList<string> HouseRandomEvents => houseRandomEvents;

        public string GetRoomTone(RoomCategory category)
        {
            var map = Find(category);
            return map != null && !string.IsNullOrEmpty(map.RoomToneEventId)
                ? map.RoomToneEventId
                : defaultRoomTone;
        }

        public string GetReverbProfile(RoomCategory category)
        {
            var map = Find(category);
            return map != null && !string.IsNullOrEmpty(map.ReverbProfileId)
                ? map.ReverbProfileId
                : defaultReverb;
        }

        public bool HasExteriorLeakage(RoomCategory category)
        {
            var map = Find(category);
            return map != null && map.ExteriorLeakage;
        }

        public string GetRandomRoomEvent(RoomCategory category)
        {
            var map = Find(category);
            if (map?.RandomEventIds != null && map.RandomEventIds.Length > 0)
                return map.RandomEventIds[UnityEngine.Random.Range(0, map.RandomEventIds.Length)];
            if (houseRandomEvents != null && houseRandomEvents.Length > 0)
                return houseRandomEvents[UnityEngine.Random.Range(0, houseRandomEvents.Length)];
            return "Env.House.Settling";
        }

        private CategoryMapping Find(RoomCategory category)
        {
            for (int i = 0; i < mappings.Count; i++)
            {
                if (mappings[i].Category == category)
                    return mappings[i];
            }
            return null;
        }

        public static RoomAudioProfile CreateDefaultRuntime()
        {
            var profile = CreateInstance<RoomAudioProfile>();
            profile.mappings = new List<CategoryMapping>
            {
                Map(RoomCategory.Kitchen, "Env.RoomTone.Kitchen.FridgeHum", "Hallway", false),
                Map(RoomCategory.Bathroom, "Env.RoomTone.Bathroom.Pipes", "Bathroom", false),
                Map(RoomCategory.Basement, "Env.RoomTone.Basement.Vent", "Basement", false),
                Map(RoomCategory.Attic, "Env.RoomTone.Attic.Wind", "SmallBedroom", true),
                Map(RoomCategory.Garage, "Env.RoomTone.Garage.Metal", "Exterior", true),
                Map(RoomCategory.LivingRoom, "Env.RoomTone.LivingRoom.Clock", "Hallway", true),
                Map(RoomCategory.Bedroom, "Env.RoomTone.Bedroom.Quiet", "SmallBedroom", true),
                Map(RoomCategory.Entrance, "Env.RoomTone.Entrance.Draft", "Hallway", true),
                Map(RoomCategory.Hallway, "Env.RoomTone.Hallway.Air", "Hallway", false)
            };
            return profile;
        }

        private static CategoryMapping Map(RoomCategory cat, string tone, string reverb, bool leak)
        {
            return new CategoryMapping
            {
                Category = cat,
                RoomToneEventId = tone,
                ReverbProfileId = reverb,
                ExteriorLeakage = leak,
                RandomEventIds = new[] { "Env.House.FloorCreak", "Env.House.PipeTick" }
            };
        }
    }
}
