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

        protected void MarkComplete()
        {
            IsComplete = true;
            Progress = 1f;
        }

        protected void SetProgress(float value)
        {
            Progress = UnityEngine.Mathf.Clamp01(value);
            if (Progress >= 1f)
                MarkComplete();
        }
    }
}
