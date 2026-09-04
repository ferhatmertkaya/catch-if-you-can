using CatchIfYouCan.Art;
using UnityEngine;

namespace CatchIfYouCan.Equipment
{
    /// <summary>
    /// Builds the thing you see in the hand, from an <see cref="EquipmentVisualProfile"/>.
    ///
    /// <para>
    /// This is the flashlight's model loading, lifted out of the flashlight. It measured its
    /// mesh rather than assuming it, pinned a material that is known to exist, turned the
    /// model's own long axis into the carried transform's +Y and slid the grip end onto the
    /// pivot - all of which is correct and none of which is about being a torch. Ten other
    /// items needed the same hundred lines.
    /// </para>
    ///
    /// <para>
    /// It returns a carried root whose local +Y is the item's length and whose origin is the
    /// grip, which is the convention <see cref="EquipmentPresentation"/> and
    /// <see cref="HeldEquipmentBase"/> both assume.
    /// </para>
    /// </summary>
    public static class EquipmentVisualFactory
    {
        /// <summary>
        /// Builds the visual under <paramref name="parent"/> and reports how long it came out.
        /// The measured length is the real one, which is not necessarily the requested one.
        /// </summary>
        public static Transform Build(EquipmentVisualProfile profile, Transform parent,
                                      string itemName, out float measuredLength)
        {
            profile = profile != null ? profile : EquipmentVisualProfile.Fallback;

            var pivot = new GameObject(string.IsNullOrEmpty(itemName) ? "Visual" : itemName);
            var carried = pivot.transform;
            carried.SetParent(parent, false);

            var shader = CiycShaders.FindLit();

            if (profile.VisualPrefab != null)
            {
                measuredLength = BuildFromPrefab(profile, carried);
                return carried;
            }

            if (!string.IsNullOrEmpty(profile.ModelResourcePath))
            {
                var loaded = Resources.Load<GameObject>(profile.ModelResourcePath);
                if (loaded != null)
                {
                    measuredLength = BuildFromModel(profile, carried, loaded);
                    return carried;
                }

                Debug.LogError("[CIYC][Equipment] Resources.Load<GameObject>(\"" +
                               profile.ModelResourcePath + "\") ergab NULL fuer '" + itemName +
                               "'. Erwartet wird eine Datei unter " +
                               "Assets/**/Resources/" + profile.ModelResourcePath +
                               ".<endung>. Der Pfad ist relativ zu einem Resources-Ordner, " +
                               "ohne Ordnerpraefix und ohne Dateiendung.");

                // Produktionskunst bekommt KEINEN Ersatz. Ein Platzhalter an dieser Stelle
                // sieht aus wie ein halbfertiger Gegenstand und nicht wie ein Ladefehler, und
                // dann sucht wochenlang niemand nach dem Pfad. Leere Hand plus die Zeile
                // darueber ist die ehrliche Anzeige.
                if (!profile.IsDevPlaceholder)
                {
                    measuredLength = profile.Length;
                    return carried;
                }
            }

            measuredLength = BuildPlaceholder(profile, carried, shader);
            return carried;
        }

        /// <summary>Final art: instantiated as authored, scaled to the profile's length.</summary>
        private static float BuildFromPrefab(EquipmentVisualProfile profile, Transform carried)
        {
            var model = Object.Instantiate(profile.VisualPrefab);
            model.name = "Body";
            return Fit(profile, carried, model, null);
        }

        /// <summary>The Resources path, with the material pinned on.</summary>
        private static float BuildFromModel(EquipmentVisualProfile profile, Transform carried,
                                            GameObject prefab)
        {
            // Spawned loose, at the world origin with no rotation and no scale, so the renderer
            // bounds read below are the model's own numbers. Spawned into the hand instead they
            // are the model's bounds plus wherever in the level the player is standing, and the
            // slide at the end then pushes the item that whole distance away.
            var model = Object.Instantiate(prefab);
            model.name = "Body";

            Material pinned = string.IsNullOrEmpty(profile.ModelMaterialPath)
                ? null
                : Resources.Load<Material>(profile.ModelMaterialPath);

            if (pinned == null && !string.IsNullOrEmpty(profile.ModelMaterialPath))
            {
                Debug.LogWarning("[CIYC] No material at Resources/" + profile.ModelMaterialPath +
                                 "; keeping whatever the model imported with.");
            }

            return Fit(profile, carried, model, pinned);
        }

        /// <summary>
        /// Measures, scales, turns and slides the model so its long axis is the carried
        /// transform's +Y and its grip end sits on the pivot.
        /// </summary>
        private static float Fit(EquipmentVisualProfile profile, Transform carried,
                                 GameObject model, Material pinned)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Debug.LogError("[CIYC][Equipment] Resources/" + profile.ModelResourcePath +
                               " wurde geladen, hat aber KEINEN Renderer. Das Modell ist da " +
                               "und kann nichts zeichnen - im Importer pruefen, ob Meshes " +
                               "ueberhaupt importiert werden.");

                if (!profile.IsDevPlaceholder)
                {
                    Object.Destroy(model);
                    return profile.Length;
                }

                Object.Destroy(model);
                return BuildPlaceholder(profile, carried, CiycShaders.FindLit());
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            model.transform.SetParent(carried, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            // Made visible and given a material that is known to exist. An object the importer
            // decided was hidden, a renderer that arrived switched off, and a material whose
            // textures were never in the delivery all look the same from the player's side: a
            // hand holding nothing.
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].gameObject.activeSelf)
                    renderers[i].gameObject.SetActive(true);
                renderers[i].enabled = true;

                if (pinned == null)
                    continue;

                int slots = Mathf.Max(1, renderers[i].sharedMaterials.Length);
                var materials = new Material[slots];
                for (int m = 0; m < slots; m++)
                    materials[m] = pinned;
                renderers[i].sharedMaterials = materials;
            }

            float target = profile.Length;
            Vector3 axis = profile.ModelForwardAxis.sqrMagnitude < 0.0001f
                ? Vector3.up
                : profile.ModelForwardAxis.normalized;

            float along = Mathf.Abs(Vector3.Dot(bounds.size, Abs(axis)));
            if (along < 0.0001f)
                along = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));

            float scale = target / along;
            model.transform.localScale = Vector3.one * scale;

            // Turn the model's own long axis into the pivot's +Y, so everything downstream -
            // aiming, attachments, the capsule it lands on - shares one convention.
            model.transform.localRotation = Quaternion.FromToRotation(axis, Vector3.up);

            // And slide it so the grip end sits on the pivot rather than its middle.
            Vector3 centre = model.transform.localRotation * (bounds.center * scale);
            model.transform.localPosition = new Vector3(-centre.x, target * 0.5f - centre.y, -centre.z);

            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderers[i].receiveShadows = true;
            }

            if (profile.LogState)
            {
                var used = renderers[0].sharedMaterial != null
                    ? renderers[0].sharedMaterial.shader
                    : null;
                string source = profile.VisualPrefab != null
                    ? profile.VisualPrefab.name
                    : "Resources/" + profile.ModelResourcePath;

                Debug.Log("[CIYC][Equipment] " + carried.name + " visual = " + source +
                          ", renderers = " + renderers.Length +
                          " (active=" + model.activeInHierarchy +
                          " shader=" + (used != null ? used.name : "<none>") +
                          " measured=" + bounds.size.ToString("F3") +
                          " scale=" + scale.ToString("F4") +
                          " length=" + target.ToString("F3") + ")");
            }

            return target;
        }

        /// <summary>
        /// The stand-in: a capsule in an unmistakable colour, named so that a screenshot of it
        /// is self-explaining. It is meant to look wrong.
        /// </summary>
        private static float BuildPlaceholder(EquipmentVisualProfile profile, Transform carried,
                                              Shader shader)
        {
            Vector3 size = profile.FallbackSize;

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = profile.IsDevPlaceholder ? "DEV_PLACEHOLDER_Body" : "Body";
            body.transform.SetParent(carried, false);
            body.transform.localScale = new Vector3(size.x, size.y * 0.5f, size.z);
            body.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);

            var collider = body.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            if (shader != null)
            {
                var material = new Material(shader) { name = "Equipment_Placeholder_Runtime" };
                material.color = profile.PlaceholderColor;
                body.GetComponent<Renderer>().sharedMaterial = material;
            }

            return size.y;
        }

        private static Vector3 Abs(Vector3 v) =>
            new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
