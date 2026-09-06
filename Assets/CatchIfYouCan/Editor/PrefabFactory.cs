using System.Collections.Generic;
using CatchIfYouCan.Equipment;
using CatchIfYouCan.Ghost;
using CatchIfYouCan.Player;
using CatchIfYouCan.Interaction;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    public static class PrefabFactory
    {
        private const string PrefabRoot = "Assets/CatchIfYouCan/Prefabs";

        [MenuItem("Catch If You Can/Debug and Legacy/Platzhalter-Prefabs erzeugen [SCHREIBT ASSET]", false, 1203)]
        public static void GeneratePlaceholderPrefabs()
        {
            EnsureFolder("Assets/CatchIfYouCan");
            EnsureFolder(PrefabRoot);

            var created = new List<string>();
            CreateDoor(created);
            CreateFurniture(created);
            CreateEquipment(created);
            CreateCharacterPrefabs(created);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PrefabFactory] Created/updated {created.Count} prefabs under {PrefabRoot}.\n" +
                      string.Join("\n", created));
        }

        private static void CreateDoor(List<string> created)
        {
            var root = new GameObject("Door");
            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Frame";
            frame.transform.SetParent(root.transform, false);
            frame.transform.localScale = new Vector3(1.2f, 2.2f, 0.15f);

            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(root.transform, false);
            hinge.localPosition = new Vector3(-0.55f, 0f, 0f);

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            panel.transform.SetParent(hinge, false);
            panel.transform.localScale = new Vector3(1f, 2f, 0.08f);
            panel.transform.localPosition = new Vector3(0.5f, 0f, 0f);

            var door = root.AddComponent<InteractiveDoor>();
            SetPrivateField(door, "hinge", hinge);

            SavePrefab(root, "Door", created);
        }

        private static void CreateFurniture(List<string> created)
        {
            SavePrimitive("Bed", PrimitiveType.Cube, new Vector3(2f, 0.5f, 3f), created);
            SavePrimitive("Wardrobe", PrimitiveType.Cube, new Vector3(1.5f, 2.2f, 0.6f), created);
            SavePrimitive("Table", PrimitiveType.Cube, new Vector3(1.2f, 0.75f, 0.8f), created);
            SavePrimitive("Chair", PrimitiveType.Cube, new Vector3(0.5f, 0.9f, 0.5f), created);
        }

        private static void CreateEquipment(List<string> created)
        {
            SaveEquipment("EMF_Reader", typeof(EMFDetector), new Vector3(0.15f, 0.08f, 0.25f), created);
            SaveEquipment("Camera", typeof(PhotoCameraEquipment), new Vector3(0.12f, 0.1f, 0.18f), created);
            SaveEquipment("Flashlight", typeof(HeldFlashlight), new Vector3(0.08f, 0.08f, 0.22f), created);
            SaveEquipment("UV_Light", typeof(UVLight), new Vector3(0.1f, 0.08f, 0.2f), created);
            SaveEquipment("Thermometer", typeof(ThermometerEquipment), new Vector3(0.05f, 0.15f, 0.05f), created);
            SaveEquipment("Salt", typeof(SaltEquipment), new Vector3(0.2f, 0.15f, 0.2f), created);
            SaveEquipment("Warding_Relic", typeof(WardingRelic), new Vector3(0.18f, 0.25f, 0.18f), created);
        }

        private static void CreateCharacterPrefabs(List<string> created)
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            player.AddComponent<CharacterController>();
            player.AddComponent<PlayerController>();
            player.AddComponent<PlayerInventory>();
            player.tag = "Player";
            SavePrefab(player, "Player", created);

            var ghost = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ghost.name = "Ghost_Presence";
            ghost.transform.localScale = new Vector3(0.7f, 1.2f, 0.7f);
            ghost.AddComponent<GhostController>();
            ghost.tag = "Ghost";
            SavePrefab(ghost, "Ghost_Presence", created);
        }

        private static void SaveEquipment(string name, System.Type componentType, Vector3 scale, List<string> created)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.localScale = scale;
            go.AddComponent(componentType);
            go.tag = "Equipment";
            SavePrefab(go, name, created);
        }

        private static void SavePrimitive(string name, PrimitiveType type, Vector3 scale, List<string> created)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.localScale = scale;
            SavePrefab(go, name, created);
        }

        private static void SavePrefab(GameObject root, string fileName, List<string> created)
        {
            string path = $"{PrefabRoot}/{fileName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            created.Add(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
