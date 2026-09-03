namespace CatchIfYouCan.Objectives
{
    public abstract class ObjectiveBase
    {
        public string Id { get; protected set; }
        public string Description { get; protected set; }
        public bool IsComplete { get; protected set; }
        public float Progress { get; protected set; }
        public bool IsOptional { get; protected set; }

        protected ObjectiveBase(string id, string description, bool optional)
        {
            Id = id;
            Description = description;
            IsOptional = optional;
        }

        public abstract void Activate();
        public abstract void Deactivate();

        /// <summary>
        /// Completes the objective.
        ///
        /// <para>
        /// Host-only. Objectives complete on evidence and on world events, and both of those
        /// are already the host's to decide - so a client completing one locally would be a
        /// client awarding itself a mission. In single player the local player is the host and
        /// this changes nothing.
        /// </para>
        ///
        /// <para>
        /// A refused completion is silent rather than logged. Every listening objective sees
        /// every event, so on a client this would fire on each one of them, several times a
        /// mission, saying nothing a reader needs.
        /// </para>
        /// </summary>
        protected void MarkComplete()
        {
            if (!Core.SessionAuthority.IsHost)
                return;

            IsComplete = true;
            Progress = 1f;
        }

        /// <summary>
        /// Sets completion from replicated truth, without asking who decided.
        ///
        /// <para>
        /// The counterpart to the gate above: the host confirms, and every client is told. A
        /// client's journal and objective list have to show what the session agreed, and they
        /// cannot reach that through <see cref="MarkComplete"/> because it would refuse them.
        /// </para>
        /// </summary>
        public void ApplyReplicatedProgress(float progress, bool complete)
        {
            Progress = UnityEngine.Mathf.Clamp01(progress);
            IsComplete = complete;
        }

        protected void SetProgress(float value)
        {
            Progress = UnityEngine.Mathf.Clamp01(value);
            if (Progress >= 1f)
                MarkComplete();
        }
    }
}
