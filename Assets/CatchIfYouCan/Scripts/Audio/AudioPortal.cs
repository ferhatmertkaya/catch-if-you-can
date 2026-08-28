using UnityEngine;

namespace CatchIfYouCan.Audio
{
    public class AudioPortal : MonoBehaviour
    {
        [SerializeField] private float openAmount = 1f;
        [SerializeField] private RoomAudioZone roomA;
        [SerializeField] private RoomAudioZone roomB;
        [SerializeField] private Transform portalCenter;

        private CatchIfYouCan.Interaction.InteractiveDoor _door;

        public float OpenAmount => openAmount;
        public RoomAudioZone RoomA => roomA;
        public RoomAudioZone RoomB => roomB;
        public Vector3 Center => portalCenter != null ? portalCenter.position : transform.position;

        public void Configure(CatchIfYouCan.Interaction.InteractiveDoor door, RoomAudioZone a, RoomAudioZone b)
        {
            _door = door;
            roomA = a;
            roomB = b;
            portalCenter = door != null ? door.transform : transform;
            RefreshOpenAmount();
        }

        private void Update()
        {
            RefreshOpenAmount();
        }

        public void SetOpenAmount(float amount)
        {
            openAmount = Mathf.Clamp01(amount);
        }

        private void RefreshOpenAmount()
        {
            if (_door == null) return;
            openAmount = _door.IsOpen ? 1f : 0f;
        }

        public float GetOcclusionAttenuationDb(bool listenerInA, bool sourceInA)
        {
            if (roomA == null || roomB == null) return -14f;
            bool sameSide = listenerInA == sourceInA;
            if (sameSide) return 0f;
            if (openAmount >= 0.95f) return -2f;
            if (openAmount <= 0.05f) return -9f;
            return Mathf.Lerp(-9f, -2f, openAmount);
        }

        public float GetLowPassCutoff()
        {
            if (openAmount >= 0.95f) return 18000f;
            if (openAmount <= 0.05f) return 1200f;
            return Mathf.Lerp(1200f, 18000f, openAmount);
        }
    }
}
