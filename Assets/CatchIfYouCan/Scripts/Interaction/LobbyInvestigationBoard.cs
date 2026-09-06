using CatchIfYouCan.Art;
using CatchIfYouCan.UI;
using UnityEngine;

namespace CatchIfYouCan.Interaction
{
    /// <summary>
    /// The mission board on the lobby wall: a real object the player walks up to, not a
    /// floating button.
    ///
    /// <para>
    /// It is an ordinary <see cref="IInteractable"/>, so the existing
    /// <see cref="InteractionController"/> finds it, raycasts it, ranges it and raises the
    /// existing prompt. There is no second interaction system and no second prompt.
    /// </para>
    ///
    /// <para>
    /// The board builds itself from this one transform, the way <c>MirrorCorner</c> does, so
    /// the whole object moves by moving one thing and no scene YAML had to be hand-written for
    /// a graph of cross-referencing documents. The geometry is <b>placeholder</b> and says so:
    /// assign <see cref="boardPrefab"/> and the Art team replaces the whole look without a
    /// line of this file changing.
    /// </para>
    ///
    /// <para>
    /// Local +Z is the face. The object should be turned to face into the room.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Lobby Investigation Board")]
    public sealed class LobbyInvestigationBoard : MonoBehaviour, IInteractable
#if UNITY_EDITOR
        , Art.IEditorPreviewBuildable
#endif
    {
        [Header("Interaction")]
        [Tooltip("Reach, in metres. Matches the other lobby interactables rather than " +
                 "declaring a new number - the interaction controller's own maximum is 2.75.")]
        [SerializeField, Min(0.5f)] private float distance = 2.2f;

        [SerializeField] private string prompt = "Read the investigation board";

        [Header("Board")]
        [Tooltip("An authored board prop. When set, none of the placeholder geometry below is " +
                 "built and the prefab is used as-is. This is the swap the art pipeline makes; " +
                 "gameplay does not change with it.")]
        [SerializeField] private GameObject boardPrefab;

        [Tooltip("Board size in metres. Large enough to read across the lobby.")]
        [SerializeField] private Vector2 boardSize = new Vector2(2.2f, 1.5f);

        [Tooltip("Centre height above the floor, in metres. Eye height is 1.68.")]
        [SerializeField] private float boardHeight = 1.55f;

        [Tooltip("Aged dark timber. Deliberately not black: a board nobody can pick out of a " +
                 "dark room is a board nobody walks to.")]
        [SerializeField] private Color frameColor = new Color(0.13f, 0.11f, 0.09f);

        [SerializeField] private Color surfaceColor = new Color(0.09f, 0.11f, 0.10f);

        [Tooltip("The project's green. Restrained on purpose - an accent, not a sign.")]
        [SerializeField] private Color accentColor = new Color(0.34f, 1f, 0.41f);

        [Header("Attention")]
        [Tooltip("A very subtle green fill on the board face while the player is in range. " +
                 "Off makes the board inert-looking; bright makes it arcade.")]
        [SerializeField] private bool glowWhenInRange = true;

        [SerializeField, Range(0f, 1.5f)] private float glowIntensity = 0.45f;

        private Renderer _surfaceRenderer;
        private Material _surfaceMaterial;
        private MaterialPropertyBlock _block;
        private Transform _player;
        private bool _built;
        private bool _glowing;

        // ---- IInteractable ----------------------------------------------------------------

        public string Prompt => prompt;
        public float HoldDuration => 0f;
        public InteractionType InteractionType => InteractionType.Use;
        public float Distance => distance;

        /// <summary>
        /// Refuses while the panel is already up, which is what stops a second press opening a
        /// duplicate. The controller stops offering the prompt at the same moment.
        /// </summary>
        public bool CanInteract(GameObject interactor) => !LobbyBoardUI.IsOpen;

        public void Interact(GameObject interactor)
        {
            if (LobbyBoardUI.IsOpen)
                return;

            LobbyBoardUI.Open();
        }

        // ---- construction -------------------------------------------------------------

        private void Start()
        {
            Build();
        }

#if UNITY_EDITOR
        /// <summary>
        /// The same Build the game runs. It makes a board and a trigger and nothing else - no
        /// screen is opened until somebody interacts with it, which nobody does in Edit Mode.
        /// </summary>
        void Art.IEditorPreviewBuildable.BuildEditorPreview() => Build();

        void Art.IEditorPreviewBuildable.ForgetEditorPreview()
        {
            _built = false;
            _surfaceRenderer = null;
            _surfaceMaterial = null;
        }
#endif

        private void OnDestroy()
        {
            if (_surfaceMaterial != null)
                Destroy(_surfaceMaterial);
        }

        private void Build()
        {
            if (_built)
                return;
            _built = true;

            if (boardPrefab != null)
            {
                var authored = Instantiate(boardPrefab, transform);
                authored.transform.localPosition = new Vector3(0f, boardHeight, 0f);
                authored.transform.localRotation = Quaternion.identity;
                _surfaceRenderer = authored.GetComponentInChildren<Renderer>();
                EnsureTrigger();
                return;
            }

            // One shader, and it is the one the rest of the room is built from. Never a
            // built-in fallback: Standard resolves everywhere and draws magenta under URP.
            Shader lit = CiycShaders.FindLit();

            BuildFrame(lit);
            BuildSurface(lit);
            BuildHeaderRail(lit);
            EnsureTrigger();
        }

        private void BuildFrame(Shader lit)
        {
            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Board_Frame";
            frame.transform.SetParent(transform, false);
            frame.transform.localPosition = new Vector3(0f, boardHeight, 0.04f);
            frame.transform.localScale =
                new Vector3(boardSize.x + 0.14f, boardSize.y + 0.14f, 0.08f);
            Destroy(frame.GetComponent<Collider>());

            if (lit == null)
                return;

            var material = new Material(lit) { name = "LobbyBoard_Frame_Runtime" };
            material.color = frameColor;
            frame.GetComponent<Renderer>().sharedMaterial = material;
        }

        /// <summary>
        /// The face. Set proud of the frame rather than flush with it, because two opaque
        /// surfaces on one plane is a z-fight that reads as the board flickering.
        /// </summary>
        private void BuildSurface(Shader lit)
        {
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "Board_Surface";
            surface.transform.SetParent(transform, false);
            surface.transform.localPosition = new Vector3(0f, boardHeight, -0.005f);
            surface.transform.localScale = new Vector3(boardSize.x, boardSize.y, 0.02f);
            Destroy(surface.GetComponent<Collider>());

            _surfaceRenderer = surface.GetComponent<Renderer>();

            if (lit == null)
                return;

            _surfaceMaterial = new Material(lit) { name = "LobbyBoard_Surface_Runtime" };
            _surfaceMaterial.color = surfaceColor;
            if (_surfaceMaterial.HasProperty("_Smoothness"))
                _surfaceMaterial.SetFloat("_Smoothness", 0.18f);
            _surfaceRenderer.sharedMaterial = _surfaceMaterial;
        }

        /// <summary>A thin lit rail along the top. The whole of the green accent.</summary>
        private void BuildHeaderRail(Shader lit)
        {
            var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = "Board_AccentRail";
            rail.transform.SetParent(transform, false);
            rail.transform.localPosition =
                new Vector3(0f, boardHeight + boardSize.y * 0.5f + 0.05f, -0.01f);
            rail.transform.localScale = new Vector3(boardSize.x * 0.92f, 0.018f, 0.02f);
            Destroy(rail.GetComponent<Collider>());

            if (lit == null)
                return;

            var material = new Material(lit) { name = "LobbyBoard_Accent_Runtime" };
            material.color = accentColor;
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", accentColor * 1.6f);
            }
            rail.GetComponent<Renderer>().sharedMaterial = material;
        }

        /// <summary>
        /// One collider, on this object, sized to the board. The interaction controller
        /// raycasts for it, so without a collider the board is scenery.
        /// </summary>
        private void EnsureTrigger()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null)
                box = gameObject.AddComponent<BoxCollider>();

            box.isTrigger = false;
            box.center = new Vector3(0f, boardHeight, 0f);
            box.size = new Vector3(boardSize.x + 0.14f, boardSize.y + 0.14f, 0.12f);
        }

        // ---- attention ------------------------------------------------------------------

        /// <summary>
        /// A very subtle lift on the face while the player is close enough to read it.
        ///
        /// <para>
        /// Through a <see cref="MaterialPropertyBlock"/> rather than by touching the material,
        /// so nothing is allocated per frame and the shared material is never mutated. The
        /// distance is measured against the local player's registered position rather than by
        /// sweeping the scene.
        /// </para>
        /// </summary>
        private void Update()
        {
            if (!glowWhenInRange || _surfaceRenderer == null)
                return;

            // Cached. Resolving falls through to Camera.main when no player is registered,
            // which is a lookup every frame for the whole time before one spawns.
            if (_player == null)
                _player = Core.LocalPlayerService.ResolveListenerTransform();

            bool near = _player != null &&
                        Vector3.Distance(_player.position, transform.position) <= distance * 1.6f &&
                        !LobbyBoardUI.IsOpen;

            if (near == _glowing)
                return;

            _glowing = near;

            _block ??= new MaterialPropertyBlock();
            _surfaceRenderer.GetPropertyBlock(_block);
            Color tint = near
                ? Color.Lerp(surfaceColor, accentColor, 0.12f * glowIntensity)
                : surfaceColor;
            _block.SetColor("_BaseColor", tint);
            _surfaceRenderer.SetPropertyBlock(_block);
        }
    }
}
