using System;
using UnityEngine;

namespace CatchIfYouCan.Evidence
{
    [Serializable]
    public class PhotoResult
    {
        public string Id;
        public int Stars;
        public float DistanceToSubject;
        public float VisibilityScore;
        public float CenteringScore;
        public bool CapturedEvent;

        // Unity cannot serialize Nullable<T>; photo results only ever live in
        // EvidenceManager's runtime list, so the nullable stays.
        [NonSerialized] public EvidenceType? RelatedEvidence;
        public Vector3 CapturePosition;
        public Vector3 SubjectPosition;
        public string Caption;
        public long TimestampUtcTicks;
        public Texture2D Thumbnail;

        public PhotoResult()
        {
            Id = Guid.NewGuid().ToString("N");
            TimestampUtcTicks = DateTime.UtcNow.Ticks;
        }

        public string StarLabel => Stars switch
        {
            3 => "Excellent",
            2 => "Good",
            1 => "Poor",
            _ => "None"
        };
    }
}
