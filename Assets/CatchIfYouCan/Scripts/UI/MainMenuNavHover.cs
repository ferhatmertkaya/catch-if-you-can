using UnityEngine;
using UnityEngine.EventSystems;

namespace CatchIfYouCan.UI
{
    /// <summary>
    /// Moves the menu highlight to the row the pointer is over.
    ///
    /// <para>
    /// Its own file rather than a second class beside
    /// <see cref="MainMenuNavigation"/>: Unity only lets a MonoBehaviour whose class name matches
    /// its file be added in the Inspector, and a rule that half-applies is one nobody can rely on
    /// while there is no editor here to try it in.
    /// </para>
    ///
    /// <para>
    /// Hover only SELECTS; it never activates. On touch there is no hover at all, so the row is
    /// selected by the same press that runs it, and nothing about the menu depends on a pointer
    /// existing.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Catch If You Can/Main Menu Nav Hover")]
    public sealed class MainMenuNavHover : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private MainMenuNavigation navigation;
        [SerializeField] private int index;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (navigation != null)
                navigation.Select(index, instant: false);
        }

        /// <summary>
        /// Wiring, so <see cref="MainMenuScreenBuilder"/> never reaches into a private field.
        /// Called from runtime code as well as from the editor tool, so it is not called
        /// EditorBind any more - a name that says editor while runtime code calls it is the
        /// kind of comment CLAUDE.md mistake 33 is about.
        /// </summary>
        public void Bind(MainMenuNavigation nav, int rowIndex)
        {
            navigation = nav;
            index = rowIndex;
        }
    }
}
