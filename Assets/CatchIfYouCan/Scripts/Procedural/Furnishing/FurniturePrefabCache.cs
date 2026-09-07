using System.Collections.Generic;
using CatchIfYouCan.Content;
using UnityEngine;

namespace CatchIfYouCan.Procedural.Furnishing
{
    /// <summary>Was ein Möbelstück tatsächlich ist, nachdem man es angesehen hat.</summary>
    public struct FurnitureMetrics
    {
        /// <summary>Ausdehnung in der EIGENEN Achsen des Modells, in Metern.</summary>
        public Vector3 Size;

        /// <summary>
        /// Wo die Mitte der Geometrie relativ zum Pivot liegt.
        ///
        /// Nicht Deko: dieses Paket setzt Pivots bis zu 40 m neben die eigene Geometrie. Ein
        /// Möbelstück über seinen Pivot zu setzen heisst dann, es irgendwohin zu setzen.
        /// </summary>
        public Vector3 CentreOffset;

        public bool Valid;

        public float Width => Size.x;
        public float Depth => Size.z;
        public float Height => Size.y;
    }

    /// <summary>
    /// Lädt Möbel-Prefabs und merkt sich, wie gross sie sind.
    ///
    /// <para>
    /// <b>Gemessen, nicht aus dem Katalog gelesen.</b> Eine im Asset eingetippte Grösse ist eine
    /// zweite Wahrheit neben dem Modell; beim ersten Reimport gehen sie auseinander, und das
    /// Ergebnis ist ein Schrank in einer Lücke, in die er nicht passt. Also wird das erste
    /// Exemplar einer Sorte gemessen - und BENUTZT, nicht weggeworfen -, und die Zahl bleibt für
    /// alle weiteren im Cache.
    /// </para>
    ///
    /// <para>
    /// <b>Kein AssetDatabase, kein Ordner-Scan.</b> Zur Laufzeit wird ausschliesslich über die
    /// Referenz im Katalog oder über einen Resources-Pfad geladen, und der Ladevorgang selbst ist
    /// ebenfalls gecacht: ein Haus mit acht Schlafzimmern lädt das Bett einmal.
    /// </para>
    /// </summary>
    public sealed class FurniturePrefabCache
    {
        private readonly Dictionary<string, GameObject> _loaded = new Dictionary<string, GameObject>(64);
        // Das Prefab-Asset selbst als Schluessel, nicht seine Instanz-ID: die ist in Unity 6
        // zugunsten von GetEntityId veraltet, und ein Name waere bei zwei gleich benannten
        // Prefabs aus verschiedenen Ordnern die falsche Antwort. Prefab-Assets werden nicht
        // zerstoert, also kann hier kein toter Schluessel liegenbleiben.
        private readonly Dictionary<GameObject, FurnitureMetrics> _measured =
            new Dictionary<GameObject, FurnitureMetrics>(64);
        private readonly List<string> _missing = new List<string>();

        /// <summary>Die Einträge, deren Modell nicht aufzutreiben war - einmal je Id.</summary>
        public IReadOnlyList<string> Missing => _missing;

        /// <summary>
        /// Das Modell für diesen Eintrag, oder null mit einer Meldung.
        /// </summary>
        public GameObject Resolve(FurnitureEntry entry)
        {
            if (entry.Prefab != null)
                return entry.Prefab;

            if (string.IsNullOrEmpty(entry.ResourcePath))
                return null;

            if (_loaded.TryGetValue(entry.ResourcePath, out GameObject cached))
                return cached;

            GameObject loaded = Resources.Load<GameObject>(entry.ResourcePath);
            _loaded[entry.ResourcePath] = loaded;

            if (loaded == null)
            {
                // Erwartet wird eine Datei unter Assets/**/Resources/<Pfad>.<endung>. Der Pfad
                // ist relativ zu einem Resources-Ordner, ohne Ordnerpräfix und ohne Endung -
                // genau der Fehler, der in diesem Projekt schon einmal jahrelang unentdeckt
                // blieb (CLAUDE.md Fehler 3).
                _missing.Add(entry.Id + " (Resources/" + entry.ResourcePath + ")");
            }

            return loaded;
        }

        /// <summary>
        /// Misst ein instanziiertes Exemplar in seinem EIGENEN Raum.
        ///
        /// <para>
        /// Nicht <c>Renderer.bounds</c>: das ist eine weltachsenparallele Box und bei einem
        /// gedrehten Objekt grösser als das Objekt. Eine lokale Grösse daraus abzuleiten, indem
        /// man durch irgendetwas teilt, war in diesem Projekt schon zweimal derselbe Fehler
        /// (CLAUDE.md Fehler 12) - einmal als 2 mm lange Taschenlampe, einmal als hundertfach zu
        /// grosse Wand.
        /// </para>
        /// </summary>
        public FurnitureMetrics Measure(GameObject prefab, GameObject instance)
        {
            if (prefab != null && _measured.TryGetValue(prefab, out FurnitureMetrics known))
                return known;

            FurnitureMetrics metrics = MeasureInstance(instance);

            if (prefab != null)
                _measured[prefab] = metrics;

            return metrics;
        }

        /// <summary>Ob diese Sorte schon vermessen ist, ohne dafür etwas zu bauen.</summary>
        public bool TryGetMeasured(GameObject prefab, out FurnitureMetrics metrics)
        {
            metrics = default;
            return prefab != null && _measured.TryGetValue(prefab, out metrics);
        }

        private static readonly List<Renderer> _buffer = new List<Renderer>(32);

        private static FurnitureMetrics MeasureInstance(GameObject instance)
        {
            if (instance == null)
                return new FurnitureMetrics { Valid = false };

            _buffer.Clear();
            instance.GetComponentsInChildren(true, _buffer);

            bool any = false;
            Bounds local = default;

            Matrix4x4 toRoot = instance.transform.worldToLocalMatrix;

            for (int i = 0; i < _buffer.Count; i++)
            {
                Renderer renderer = _buffer[i];
                if (renderer == null)
                    continue;

                Mesh mesh = MeshOf(renderer);
                if (mesh == null)
                    continue;

                // Die Ecken des Mesh-Bounds, durch die Kette bis zur Wurzel des Möbelstücks
                // transformiert. So zählt die Skalierung und Drehung im Prefab mit, ohne dass
                // eine Welt-AABB entsteht.
                Bounds mb = mesh.bounds;
                Matrix4x4 m = toRoot * renderer.transform.localToWorldMatrix;

                for (int c = 0; c < 8; c++)
                {
                    var corner = new Vector3(
                        (c & 1) == 0 ? mb.min.x : mb.max.x,
                        (c & 2) == 0 ? mb.min.y : mb.max.y,
                        (c & 4) == 0 ? mb.min.z : mb.max.z);

                    Vector3 p = m.MultiplyPoint3x4(corner);

                    if (!any)
                    {
                        local = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        local.Encapsulate(p);
                    }
                }
            }

            if (!any)
                return new FurnitureMetrics { Valid = false };

            return new FurnitureMetrics
            {
                Size = local.size,
                CentreOffset = local.center,
                Valid = local.size.x > 0.001f && local.size.y > 0.001f && local.size.z > 0.001f,
            };
        }

        private static Mesh MeshOf(Renderer renderer)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                return filter.sharedMesh;

            return renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : null;
        }
    }
}
