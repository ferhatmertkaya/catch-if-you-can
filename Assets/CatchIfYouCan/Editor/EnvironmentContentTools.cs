using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CatchIfYouCan.Content;
using CatchIfYouCan.Procedural;
using CatchIfYouCan.Procedural.Deterministic;
using UnityEditor;
using UnityEngine;

namespace CatchIfYouCan.EditorTools
{
    /// <summary>
    /// Prueft den Umgebungs-Content und bereitet ein gekauftes Modular-Paket vor.
    ///
    /// <para>
    /// Zwei Werkzeuge, ein Fenster, weil sie dieselbe Frage aus zwei Richtungen stellen: was
    /// der Generator an Raeumen und Props tatsaechlich erreichen kann. Die Pruefung sieht auf
    /// den vorhandenen Raum- und Prop-Bestand, das Audit auf ein neu importiertes Paket.
    /// </para>
    ///
    /// <para>
    /// <b>Das Audit erfindet nichts.</b> Es meldet, was auf der Platte liegt, und klassifiziert
    /// das Paket erst danach - fertige Raeume, Baukasten oder gemischt. Ein Paket, das nicht da
    /// ist, ergibt keinen Bericht mit Platzhaltern, sondern die Aussage, dass es nicht da ist.
    /// Definitionen, die auf fehlende Prefabs zeigen, sind genau die Sorte Fehler, die diese
    /// Pruefung finden soll; sie zu erzeugen waere absurd.
    /// </para>
    /// </summary>
    public sealed class EnvironmentContentTools : EditorWindow
    {
        private const string RoomDefinitionsRoot = "Assets/CatchIfYouCan/ScriptableObjects/Rooms";
        private const string PropDefinitionsRoot = "Assets/CatchIfYouCan/ScriptableObjects/Props";

        private string _packFolder = "Assets";
        private Vector2 _scroll;
        private string _report = string.Empty;

        [MenuItem("Catch If You Can/9. ENTWICKLER - DEBUG/External Assets/Content-Bestand pruefen [NUR LESEN]", false, 960)]
        public static void OpenValidate()
        {
            var w = GetWindow<EnvironmentContentTools>(true, "Umgebungs-Content");
            w.minSize = new Vector2(560f, 460f);
            w._report = Validate();
        }

        [MenuItem("Catch If You Can/2. HQ MODULAR HOUSE/Paket pruefen [NUR LESEN]", false, 205)]
        public static void OpenAudit()
        {
            var w = GetWindow<EnvironmentContentTools>(true, "Umgebungs-Content");
            w.minSize = new Vector2(560f, 460f);
            w._report = "Ordner des importierten Pakets eintragen und 'Paket pruefen' druecken.";
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Pruefen liest den vorhandenen Raum- und Prop-Bestand (nach der Entfernung des " +
                "Kenney-Hausbestands zunaechst leer). Paket pruefen sieht sich einen " +
                "neu importierten Ordner an und sagt, um was fuer ein Paket es sich handelt.",
                MessageType.Info);

            if (GUILayout.Button("Vorhandenen Content pruefen"))
                _report = Validate();

            EditorGUILayout.Space();
            _packFolder = EditorGUILayout.TextField("Paket-Ordner", _packFolder);
            if (GUILayout.Button("Paket pruefen"))
                _report = AuditPack(_packFolder);

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report);
            EditorGUILayout.EndScrollView();
        }

        // ---------------------------------------------------------------- Pruefung

        /// <summary>
        /// Findet genau die Fehler, die zur Laufzeit als "der Raum ist eine nackte Kiste"
        /// erscheinen: eine Definition ohne Prefab, ein Prefab, das es nicht mehr gibt, zwei
        /// Definitionen mit derselben Id, und ein Katalog, der weniger kennt als auf der Platte
        /// liegt. Alle vier sind still - keiner davon wirft etwas.
        /// </summary>
        private static string Validate()
        {
            var sb = new StringBuilder();
            int problems = 0;

            var rooms = LoadAll<RoomDefinition>(RoomDefinitionsRoot);
            var props = LoadAll<PropDefinition>(PropDefinitionsRoot);

            sb.AppendLine("=== RAUM-DEFINITIONEN (" + rooms.Count + ") ===");
            var roomIds = new Dictionary<string, string>();
            foreach (var def in rooms)
            {
                string id = def.ResolveStableId();
                string name = def.name;

                if (roomIds.TryGetValue(id, out string other))
                {
                    sb.AppendLine("  FEHLER  doppelte StableId '" + id + "': " + name + " und " + other);
                    problems++;
                }
                else
                {
                    roomIds[id] = name;
                }

                int variants = def.PrefabVariants?.Count(v => v != null) ?? 0;
                int missing = (def.PrefabVariants?.Length ?? 0) - variants;

                if (variants == 0)
                {
                    sb.AppendLine("  FEHLER  " + name + " (" + def.Category +
                                  ") hat KEIN Prefab - der Generator baut hier eine " +
                                  "Primitiv-Kiste.");
                    problems++;
                }
                else if (missing > 0)
                {
                    sb.AppendLine("  FEHLER  " + name + ": " + missing +
                                  " Prefab-Verweis(e) zeigen ins Leere.");
                    problems++;
                }
                else
                {
                    sb.AppendLine("  ok      " + name + " (" + def.Category + ") " +
                                  variants + " Variante(n)");
                }
            }

            sb.AppendLine();
            sb.AppendLine("=== PROP-DEFINITIONEN (" + props.Count + ") ===");
            int propProblems = 0;
            foreach (var def in props)
            {
                if (def.Prefab == null)
                {
                    sb.AppendLine("  FEHLER  " + def.name + " hat kein Prefab.");
                    propProblems++;
                }
            }
            sb.AppendLine(propProblems == 0
                ? "  ok      alle " + props.Count + " zeigen auf ein Prefab"
                : "  " + propProblems + " ohne Prefab");
            problems += propProblems;

            sb.AppendLine();
            sb.AppendLine("=== KATALOG ===");
            var catalog = Resources.Load<InvestigationContentCatalog>(
                "CatchIfYouCan/InvestigationContentCatalog");
            if (catalog == null)
            {
                sb.AppendLine("  FEHLER  Kein Katalog unter Resources/CatchIfYouCan. Der " +
                              "Generator findet dann gar keine Definitionen.");
                problems++;
            }
            else
            {
                int cr = catalog.RoomDefinitions?.Count(r => r != null) ?? 0;
                int cp = catalog.PropDefinitions?.Count(p => p != null) ?? 0;
                sb.AppendLine("  Raeume im Katalog: " + cr + " / auf der Platte: " + rooms.Count);
                sb.AppendLine("  Props im Katalog:  " + cp + " / auf der Platte: " + props.Count);

                if (cr < rooms.Count)
                {
                    sb.AppendLine("  WARNUNG Der Katalog kennt weniger Raeume als es gibt. Die " +
                                  "fehlenden werden nie gebaut.");
                    problems++;
                }
            }

            sb.AppendLine();
            sb.AppendLine(problems == 0
                ? "ERGEBNIS: keine Probleme."
                : "ERGEBNIS: " + problems + " Problem(e).");

            Debug.Log("[CIYC][Content]\n" + sb);
            return sb.ToString();
        }

        // ---------------------------------------------------------------- Paket-Audit

        /// <summary>
        /// Sagt, was in einem Ordner liegt und um was fuer ein Paket es sich handelt. Die
        /// Klassifikation kommt aus den Dateinamen der Prefabs, nicht aus einer Annahme: ein
        /// Baukasten heisst Wall/Floor/Corner, fertige Raeume heissen Kitchen/Bedroom.
        /// </summary>
        private static string AuditPack(string folder)
        {
            var sb = new StringBuilder();

            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                return "PAKET NICHT GEFUNDEN.\n\n'" + folder + "' ist kein Ordner im Projekt.\n" +
                       "Das Paket ist noch nicht importiert, oder der Pfad stimmt nicht.\n\n" +
                       "Es werden bewusst KEINE Platzhalter angelegt: Definitionen, die auf " +
                       "nicht vorhandene Prefabs zeigen, sind genau der Fehler, den die " +
                       "Content-Pruefung finden soll.";
            }

            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath).ToList();
            var models = AssetDatabase.FindAssets("t:Model", new[] { folder }).Length;
            var materials = AssetDatabase.FindAssets("t:Material", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath).ToList();
            var textures = AssetDatabase.FindAssets("t:Texture", new[] { folder }).Length;
            var scenes = AssetDatabase.FindAssets("t:Scene", new[] { folder }).Length;

            sb.AppendLine("=== " + folder + " ===");
            sb.AppendLine("  Prefabs:    " + prefabs.Count);
            sb.AppendLine("  Modelle:    " + models);
            sb.AppendLine("  Materialien:" + materials.Count);
            sb.AppendLine("  Texturen:   " + textures);
            sb.AppendLine("  Szenen:     " + scenes + "  (Demo-Szenen NICHT in die Produktion)");

            // Bauteile gegen Raeume zaehlen.
            string[] moduleWords = { "wall", "floor", "ceiling", "corner", "door", "window",
                                     "stair", "trim", "pillar", "column", "beam", "roof" };
            string[] roomWords = { "kitchen", "bedroom", "bathroom", "living", "dining", "hall",
                                   "office", "attic", "basement", "garage", "laundry", "storage" };

            int moduleHits = 0, roomHits = 0;
            var byWord = new Dictionary<string, int>();
            foreach (string p in prefabs)
            {
                string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                foreach (string w in moduleWords)
                    if (n.Contains(w)) { moduleHits++; byWord[w] = byWord.GetValueOrDefault(w) + 1; break; }
                foreach (string w in roomWords)
                    if (n.Contains(w)) { roomHits++; break; }
            }

            sb.AppendLine();
            sb.AppendLine("=== KLASSIFIKATION ===");
            sb.AppendLine("  Bauteil-Namen: " + moduleHits + "   Raum-Namen: " + roomHits);
            string kind =
                moduleHits > 0 && roomHits > 0 ? "MIXED - Bauteile UND fertige Raeume" :
                moduleHits > 0 ? "MODULAR KIT - Raeume muessen aus Bauteilen gebaut werden" :
                roomHits > 0 ? "COMPLETE ROOMS - Raeume koennen direkt zugeordnet werden" :
                "UNKNOWN - die Namen passen zu keinem Muster, bitte Liste unten ansehen";
            sb.AppendLine("  " + kind);

            if (byWord.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== BAUTEIL-FAMILIEN ===");
                foreach (var pair in byWord.OrderByDescending(p => p.Value))
                    sb.AppendLine("  " + pair.Key + ": " + pair.Value);
            }

            // Shader, die unter URP nicht laufen. Das ist der haeufigste Grund fuer Magenta.
            sb.AppendLine();
            sb.AppendLine("=== MATERIALIEN ===");
            var shaders = new Dictionary<string, int>();
            foreach (string p in materials)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                string s = m != null && m.shader != null ? m.shader.name : "<keiner>";
                shaders[s] = shaders.GetValueOrDefault(s) + 1;
            }
            int nonUrp = 0;
            foreach (var pair in shaders.OrderByDescending(p => p.Value))
            {
                bool urp = pair.Key.StartsWith("Universal Render Pipeline");
                if (!urp) nonUrp += pair.Value;
                sb.AppendLine("  " + (urp ? "ok      " : "NICHT-URP ") + pair.Key + ": " + pair.Value);
            }
            if (nonUrp > 0)
            {
                sb.AppendLine();
                sb.AppendLine("  " + nonUrp + " Materialien laufen nicht unter URP und werden " +
                              "magenta. Umwandeln mit:");
                sb.AppendLine("  Window > Rendering > Render Pipeline Converter > " +
                              "Built-in to URP, und dabei NUR diesen Ordner auswaehlen.");
            }

            sb.AppendLine();
            sb.AppendLine("=== ERSTE 40 PREFABS ===");
            foreach (string p in prefabs.Take(40))
                sb.AppendLine("  " + Path.GetFileNameWithoutExtension(p));

            Debug.Log("[CIYC][Content] Paket-Audit\n" + sb);
            return sb.ToString();
        }

        private static List<T> LoadAll<T>(string folder) where T : Object
        {
            if (!AssetDatabase.IsValidFolder(folder))
                return new List<T>();

            return AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(a => a != null)
                .ToList();
        }
    }
}
