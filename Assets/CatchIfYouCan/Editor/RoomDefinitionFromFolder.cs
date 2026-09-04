using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CatchIfYouCan.Procedural;
using CatchIfYouCan.Procedural.Deterministic;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Fuellt die RoomDefinitions mit echten Raum-Prefabs aus einem gekauften Asset-Pack.
    ///
    /// <para>
    /// <b>Das ist die Zeile, an der bisher alles haengt.</b> RoomDefinitionFactory setzt fuer
    /// jede der vierzehn Kategorien Category, Size und Weight - und laesst PrefabVariants leer.
    /// Ein leeres Array heisst fuer den Generator "es gibt kein Modell", und dann baut
    /// PrimitiveRoomFactory eine Kiste aus Quadern. Deshalb sind die Raeume nackte Boxen,
    /// obwohl an der Generierung selbst nichts fehlt.
    /// </para>
    ///
    /// <para>
    /// Dieses Fenster ordnet Prefabs aus einem Ordner den Kategorien zu - ueber den Dateinamen,
    /// mit einer Liste von Synonymen pro Kategorie, weil kein Pack die Namen des Projekts
    /// benutzt. Es zeigt die Zuordnung ZUERST an und schreibt erst auf Knopfdruck; was es nicht
    /// zuordnen kann, sagt es beim Namen, statt es still fallen zu lassen.
    /// </para>
    ///
    /// <para>
    /// Bewusst packunabhaengig: kein Pfad und kein Herstellername steht hier fest. Ein zweites
    /// Pack spaeter braucht kein zweites Werkzeug, nur einen anderen Ordner.
    /// </para>
    /// </summary>
    public sealed class RoomDefinitionFromFolder : EditorWindow
    {
        private const string DefinitionsRoot = "Assets/CatchIfYouCan/Definitions/Rooms";

        private string _folder = "Assets";
        private Vector2 _scroll;
        private readonly Dictionary<RoomCategory, List<GameObject>> _matched =
            new Dictionary<RoomCategory, List<GameObject>>();
        private readonly List<string> _unmatched = new List<string>();
        private int _scanned;

        [MenuItem("Catch If You Can/Rooms/Build Room Definitions From Folder")]
        public static void Open()
        {
            var w = GetWindow<RoomDefinitionFromFolder>(true, "Raum-Prefabs zuordnen");
            w.minSize = new Vector2(520f, 420f);
        }

        /// <summary>
        /// Wonach pro Kategorie im Dateinamen gesucht wird. Kleingeschrieben verglichen, und die
        /// laengste Uebereinstimmung gewinnt - sonst faengt "room" jeden "LivingRoom" ab.
        /// </summary>
        private static readonly Dictionary<RoomCategory, string[]> Synonyms =
            new Dictionary<RoomCategory, string[]>
            {
                { RoomCategory.Entrance,   new[] { "entrance", "entry", "foyer", "hall_entry", "porch" } },
                { RoomCategory.Hallway,    new[] { "hallway", "corridor", "hall", "passage" } },
                { RoomCategory.LivingRoom, new[] { "livingroom", "living_room", "living", "lounge", "sittingroom" } },
                { RoomCategory.Kitchen,    new[] { "kitchen", "kitchenette" } },
                { RoomCategory.DiningRoom, new[] { "diningroom", "dining_room", "dining" } },
                { RoomCategory.Bedroom,    new[] { "bedroom", "bed_room", "master", "guestroom" } },
                { RoomCategory.Bathroom,   new[] { "bathroom", "bath", "toilet", "wc", "shower" } },
                { RoomCategory.Storage,    new[] { "storage", "store", "pantry", "closet", "utility" } },
                { RoomCategory.Laundry,    new[] { "laundry", "washroom", "washing" } },
                { RoomCategory.Office,     new[] { "office", "study", "workroom", "library" } },
                { RoomCategory.KidsRoom,   new[] { "kidsroom", "kids", "child", "nursery", "playroom" } },
                { RoomCategory.Garage,     new[] { "garage", "carport", "workshop" } },
                { RoomCategory.Basement,   new[] { "basement", "cellar", "crawlspace" } },
                { RoomCategory.Attic,      new[] { "attic", "loft", "roofspace" } },
            };

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Ordner mit den Raum-Prefabs des Packs waehlen, dann Scannen. Die Zuordnung " +
                "wird angezeigt, bevor irgendetwas geschrieben wird.", MessageType.Info);

            _folder = EditorGUILayout.TextField("Prefab-Ordner", _folder);
            EditorGUILayout.LabelField(" ", "z. B. Assets/HQ Modular House Interior/Prefabs");

            if (GUILayout.Button("Scannen"))
                Scan();

            if (_scanned == 0)
                return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"{_scanned} Prefabs gefunden, " +
                                       $"{_matched.Values.Sum(v => v.Count)} zugeordnet, " +
                                       $"{_unmatched.Count} nicht zugeordnet",
                                       EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (RoomCategory cat in System.Enum.GetValues(typeof(RoomCategory)))
            {
                int n = _matched.TryGetValue(cat, out var list) ? list.Count : 0;
                EditorGUILayout.LabelField(cat.ToString(), n == 0 ? "-" : n + " Variante(n)");
                if (n == 0)
                    continue;
                foreach (var go in list)
                    EditorGUILayout.LabelField(" ", go.name);
            }

            if (_unmatched.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Nicht zugeordnet", EditorStyles.boldLabel);
                foreach (var u in _unmatched)
                    EditorGUILayout.LabelField(" ", u);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            if (_matched.Count > 0 && GUILayout.Button("RoomDefinitions schreiben"))
                Write();
        }

        private void Scan()
        {
            _matched.Clear();
            _unmatched.Clear();
            _scanned = 0;

            if (!AssetDatabase.IsValidFolder(_folder))
            {
                Debug.LogError("[CIYC][Rooms] '" + _folder + "' ist kein Ordner im Projekt.");
                return;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { _folder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go == null)
                    continue;

                _scanned++;
                if (TryClassify(Path.GetFileNameWithoutExtension(p), out RoomCategory cat))
                {
                    if (!_matched.TryGetValue(cat, out var list))
                        _matched[cat] = list = new List<GameObject>();
                    list.Add(go);
                }
                else
                {
                    _unmatched.Add(go.name);
                }
            }

            // Stabile Reihenfolge. Die Auswahl einer Variante ist deterministisch aus dem Seed,
            // also darf sie nicht davon abhaengen, in welcher Reihenfolge das Dateisystem
            // geantwortet hat - sonst baut dieselbe Saat auf zwei Rechnern zwei Haeuser.
            foreach (var list in _matched.Values)
                list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        /// <summary>Laengste Uebereinstimmung gewinnt, damit "room" nicht "LivingRoom" schlaegt.</summary>
        private static bool TryClassify(string fileName, out RoomCategory category)
        {
            string lower = fileName.ToLowerInvariant();
            category = default;
            int best = 0;

            foreach (var pair in Synonyms)
            {
                foreach (string s in pair.Value)
                {
                    if (lower.Contains(s) && s.Length > best)
                    {
                        best = s.Length;
                        category = pair.Key;
                    }
                }
            }

            return best > 0;
        }

        private void Write()
        {
            if (!AssetDatabase.IsValidFolder(DefinitionsRoot))
            {
                Directory.CreateDirectory(DefinitionsRoot);
                AssetDatabase.Refresh();
            }

            var report = new StringBuilder();
            int written = 0;

            foreach (var pair in _matched)
            {
                string assetPath = $"{DefinitionsRoot}/RoomDefinition_{pair.Key}.asset";
                var def = AssetDatabase.LoadAssetAtPath<RoomDefinition>(assetPath);
                bool isNew = def == null;
                if (isNew)
                    def = ScriptableObject.CreateInstance<RoomDefinition>();

                def.Category = pair.Key;
                def.PrefabVariants = pair.Value.ToArray();

                // StableId NICHT ueberschreiben, wenn schon eine da ist: sie geht in den
                // Layout-Hash ein, und eine geaenderte Id ist ein anderes Haus aus derselben
                // Saat. Eine neue bekommt die abgeleitete Voreinstellung.
                if (isNew)
                {
                    AssetDatabase.CreateAsset(def, assetPath);
                    written++;
                }
                else
                {
                    EditorUtility.SetDirty(def);
                }

                report.AppendLine($"{pair.Key}: {pair.Value.Count} Variante(n)");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (string u in _unmatched)
                report.AppendLine($"NICHT ZUGEORDNET: {u}");

            Debug.Log("[CIYC][Rooms] " + written + " neue RoomDefinitions unter " +
                      DefinitionsRoot + "\n" + report);
            EditorUtility.DisplayDialog("Catch If You Can",
                $"{_matched.Count} Kategorien geschrieben, {written} neu angelegt.\n\n" +
                $"Nicht zugeordnet: {_unmatched.Count}\n\nEinzelheiten in der Console.", "OK");
        }
    }
}
