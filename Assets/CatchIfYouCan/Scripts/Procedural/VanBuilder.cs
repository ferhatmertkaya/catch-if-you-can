using CatchIfYouCan.Interaction;
using UnityEngine;

namespace CatchIfYouCan.Procedural
{
    public class VanBuildResult
    {
        public GameObject Root;
        public Transform PlayerSpawnPoint;
        public Transform ExitDoor;
        public Transform MissionBoard;
    }

    public static class VanBuilder
    {
        private static readonly Color NeonGreen = new Color(0.2f, 1f, 0.35f);
        private static readonly Vector3 VanSize = new Vector3(4.5f, 2.6f, 8f);

        public static VanBuildResult Build(Transform parent, Vector3 position, Quaternion rotation)
        {
            EnsureMaterials();

            var root = new GameObject("InvestigationVan");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);

            BuildShell(root.transform);
            BuildFloor(root.transform);
            BuildEquipmentRack(root.transform, new Vector3(-1.5f, 0.5f, -2.5f));
            BuildEquipmentRack(root.transform, new Vector3(1.5f, 0.5f, -2.5f));
            BuildMonitors(root.transform, new Vector3(0f, 1.45f, -3.2f));
            var missionBoard = BuildMissionBoard(root.transform, new Vector3(0f, 1.35f, 3.1f));
            BuildShelves(root.transform, new Vector3(-1.8f, 1f, 1.2f));
            BuildTable(root.transform, new Vector3(0f, 0.45f, 0.5f));
            BuildRadio(root.transform, new Vector3(1.6f, 0.95f, 2.2f));
            var spawn = BuildPlayerSpawn(root.transform, new Vector3(0f, 0f, 2.5f));
            var exitDoor = BuildExitDoor(root.transform, new Vector3(0f, 1.1f, -VanSize.z * 0.5f + 0.1f));

            return new VanBuildResult
            {
                Root = root,
                PlayerSpawnPoint = spawn,
                ExitDoor = exitDoor,
                MissionBoard = missionBoard
            };
        }

        private static Material _bodyMaterial;
        private static Material _interiorMaterial;
        private static Material _neonMaterial;

        private static void EnsureMaterials()
        {
            if (_bodyMaterial != null)
                return;

            _bodyMaterial = CreateMaterial(new Color(0.18f, 0.2f, 0.24f));
            _interiorMaterial = CreateMaterial(new Color(0.12f, 0.13f, 0.16f));
            _neonMaterial = CreateEmissiveMaterial(NeonGreen, 2.5f);
        }

        private static Material CreateMaterial(Color color)
        {
            // Standard first meant the URP shader was never reached; a built-in shader under
            // URP is a magenta van.
            var shader = Art.CiycShaders.FindLit();
            if (shader == null)
                return null;

            var mat = new Material(shader);
            mat.color = color;
            return mat;
        }

        private static Material CreateEmissiveMaterial(Color color, float intensity)
        {
            var mat = CreateMaterial(color);
            if (mat == null)
                return null;

            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * intensity);
            return mat;
        }

        private static void BuildShell(Transform parent)
        {
            var body = CreateCube(parent, "VanBody", VanSize, _bodyMaterial);
            body.transform.localPosition = new Vector3(0f, VanSize.y * 0.5f, 0f);

            var interior = CreateCube(parent, "VanInteriorCut", VanSize - new Vector3(0.4f, 0.2f, 0.4f), _interiorMaterial);
            interior.transform.localPosition = new Vector3(0f, VanSize.y * 0.5f, 0f);
        }

        private static void BuildFloor(Transform parent)
        {
            var floor = CreateCube(parent, "VanFloor", new Vector3(VanSize.x - 0.3f, 0.08f, VanSize.z - 0.3f), _interiorMaterial);
            floor.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            floor.tag = "Environment";
        }

        private static void BuildEquipmentRack(Transform parent, Vector3 localPos)
        {
            var rack = CreateCube(parent, "EquipmentRack", new Vector3(0.9f, 1.8f, 0.45f), _interiorMaterial);
            rack.transform.localPosition = localPos;

            for (int i = 0; i < 3; i++)
            {
                var shelf = CreateCube(parent, "RackShelf", new Vector3(0.85f, 0.05f, 0.4f), _neonMaterial);
                shelf.transform.localPosition = localPos + new Vector3(0f, -0.6f + i * 0.55f, 0f);
            }
        }

        private static void BuildMonitors(Transform parent, Vector3 localPos)
        {
            for (int i = -1; i <= 1; i += 2)
            {
                var monitor = CreateCube(parent, "Monitor", new Vector3(0.7f, 0.45f, 0.06f), _interiorMaterial);
                monitor.transform.localPosition = localPos + new Vector3(i * 0.85f, 0f, 0f);

                var screen = CreateCube(parent, "MonitorScreen", new Vector3(0.62f, 0.38f, 0.02f), _neonMaterial);
                screen.transform.localPosition = monitor.transform.localPosition + new Vector3(0f, 0f, -0.04f);
            }
        }

        private static Transform BuildMissionBoard(Transform parent, Vector3 localPos)
        {
            var board = CreateCube(parent, "MissionBoard", new Vector3(2.2f, 1.2f, 0.08f), _interiorMaterial);
            board.transform.localPosition = localPos;

            var glow = CreateCube(parent, "MissionBoardGlow", new Vector3(2f, 1f, 0.02f), _neonMaterial);
            glow.transform.localPosition = localPos + new Vector3(0f, 0f, -0.05f);
            return board.transform;
        }

        private static void BuildShelves(Transform parent, Vector3 localPos)
        {
            var shelfUnit = CreateCube(parent, "ShelfUnit", new Vector3(0.5f, 1.6f, 1.2f), _interiorMaterial);
            shelfUnit.transform.localPosition = localPos;

            for (int i = 0; i < 4; i++)
            {
                var shelf = CreateCube(parent, "Shelf", new Vector3(0.48f, 0.04f, 1.1f), _neonMaterial);
                shelf.transform.localPosition = localPos + new Vector3(0f, -0.65f + i * 0.4f, 0f);
            }
        }

        private static void BuildTable(Transform parent, Vector3 localPos)
        {
            var table = CreateCube(parent, "InvestigationTable", new Vector3(1.6f, 0.08f, 0.9f), _interiorMaterial);
            table.transform.localPosition = localPos;

            var accent = CreateCube(parent, "TableAccent", new Vector3(1.4f, 0.02f, 0.75f), _neonMaterial);
            accent.transform.localPosition = localPos + new Vector3(0f, 0.06f, 0f);
        }

        private static void BuildRadio(Transform parent, Vector3 localPos)
        {
            var radio = CreateCube(parent, "Radio", new Vector3(0.35f, 0.18f, 0.22f), _interiorMaterial);
            radio.transform.localPosition = localPos;

            var dial = CreateCube(parent, "RadioDial", new Vector3(0.12f, 0.12f, 0.04f), _neonMaterial);
            dial.transform.localPosition = localPos + new Vector3(0f, 0.05f, -0.12f);
        }

        private static Transform BuildPlayerSpawn(Transform parent, Vector3 localPos)
        {
            var spawnGo = new GameObject("PlayerSpawn");
            spawnGo.transform.SetParent(parent, false);
            spawnGo.transform.localPosition = localPos;
            spawnGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            return spawnGo.transform;
        }

        private static Transform BuildExitDoor(Transform parent, Vector3 localPos)
        {
            var frame = CreateCube(parent, "VanExitFrame", new Vector3(1.2f, 2.1f, 0.12f), _interiorMaterial);
            frame.transform.localPosition = localPos;

            var door = CreateCube(parent, "VanExitDoor", new Vector3(1.05f, 2f, 0.08f), _neonMaterial);
            door.transform.localPosition = localPos + new Vector3(0.55f, 0f, 0.05f);
            door.tag = "Door";

            door.AddComponent<InteractiveDoor>();
            return door.transform;
        }

        private static GameObject CreateCube(Transform parent, string name, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = size;
            // Not "assign it if there is one". Without the assignment the primitive keeps
            // Unity's built-in default material, which is a Built-in-pipeline shader and draws
            // magenta under URP - so the null case was the visible one.
            Art.PrimitiveSurface.Apply(go, material, "van part " + name);
            return go;
        }
    }
}
