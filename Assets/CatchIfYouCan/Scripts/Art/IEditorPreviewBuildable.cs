#if UNITY_EDITOR
namespace CatchIfYouCan.Art
{
    /// <summary>
    /// A component whose visible parts are built in <c>Start</c>, and can also be built once, on
    /// demand, for authoring.
    ///
    /// <para>
    /// Four objects in the lobby carry a script and no renderer - the mirror corner, the
    /// armchair, the antique table and the investigation board. Their geometry does not exist
    /// until the game runs, so in Edit Mode they are empty transforms and there is nothing to
    /// decorate around. This is how the editor asks for it early.
    /// </para>
    /// <para>
    /// It asks the SAME builder. A separate editor-side reconstruction would be a second
    /// implementation of the same room - the mistake this project has already made with two
    /// flashlights and two inventories - and it would drift the first time somebody changed a
    /// measurement in one of them. What the flag changes is only what a preview must not have:
    /// the mirror's reflection camera and its RenderTexture are runtime state, and building
    /// those in the editor would leave a camera rendering every repaint.
    /// </para>
    /// <para>
    /// Editor-only, deliberately. Nothing in a build can reach this, so no shipping code path
    /// can accidentally take the preview route.
    /// </para>
    /// </summary>
    public interface IEditorPreviewBuildable
    {
        /// <summary>
        /// Builds the visible parts and nothing else. Does nothing if they already exist, so
        /// calling it twice cannot produce two of anything.
        /// </summary>
        void BuildEditorPreview();

        /// <summary>
        /// Forgets that it built anything, after the editor has destroyed the preview objects.
        /// Without this a second look would find the "already built" flag still set and show an
        /// empty holder again.
        /// </summary>
        void ForgetEditorPreview();
    }
}
#endif
