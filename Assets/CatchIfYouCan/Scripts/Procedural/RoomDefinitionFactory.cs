using System.Collections.Generic;
using CatchIfYouCan.Procedural;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    public static class RoomDefinitionFactory
    {
        public static RoomDefinition[] CreateAllDefaults()
        {
            var categories = (RoomCategory[])System.Enum.GetValues(typeof(RoomCategory));
            var list = new List<RoomDefinition>(categories.Length);

            for (int i = 0; i < categories.Length; i++)
            {
                var def = ScriptableObject.CreateInstance<RoomDefinition>();
                def.Category = categories[i];
                def.Size = PrimitiveRoomFactory.DefaultRoomSize;
                def.Weight = GetWeight(categories[i]);
                list.Add(def);
            }

            return list.ToArray();
        }

        private static float GetWeight(RoomCategory category)
        {
            switch (category)
            {
                case RoomCategory.Hallway:
                case RoomCategory.Entrance:
                    return 1.4f;
                case RoomCategory.LivingRoom:
                case RoomCategory.Kitchen:
                case RoomCategory.Bedroom:
                    return 1.2f;
                case RoomCategory.Bathroom:
                case RoomCategory.Storage:
                    return 0.9f;
                case RoomCategory.Attic:
                case RoomCategory.Basement:
                    return 0.75f;
                default:
                    return 1f;
            }
        }
    }
}
