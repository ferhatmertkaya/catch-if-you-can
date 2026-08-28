using CatchIfYouCan.Utilities;
using UnityEngine;

namespace CatchIfYouCan.Save
{
    public class StatisticsTracker : SingletonBehaviour<StatisticsTracker>
    {

        private float _sessionStart;

        public StatisticsData Stats
        {
            get
            {
                if (SaveManager.Instance != null)
                    return SaveManager.Instance.Data.Statistics;
                return new StatisticsData();
            }
        }

        protected override void Awake()
        {
            persist = true;
            base.Awake();
        }

        private void Start()
        {
            _sessionStart = Time.realtimeSinceStartup;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                FlushTimePlayed();
        }

        private void OnApplicationQuit()
        {
            FlushTimePlayed();
        }

        private void Update()
        {
            if (Time.frameCount % 300 == 0)
                FlushTimePlayed(false);
        }

        public void RecordInvestigation()
        {
            Stats.Investigations++;
            Persist();
        }

        public void RecordSuccessfulCase()
        {
            Stats.SuccessfulCases++;
            Persist();
        }

        public void RecordDeath()
        {
            Stats.Deaths++;
            Persist();
        }

        public void RecordCorrectIdentification()
        {
            Stats.CorrectIdentifications++;
            Persist();
        }

        public void RecordGhostPhoto()
        {
            Stats.GhostPhotos++;
            Persist();
        }

        public void RecordHuntSurvived()
        {
            Stats.HuntsSurvived++;
            Persist();
        }

        public void RecordEvidenceFound()
        {
            Stats.EvidenceFound++;
            Persist();
        }

        public void FlushTimePlayed(bool persistNow = true)
        {
            float elapsed = Time.realtimeSinceStartup - _sessionStart;
            if (elapsed > 0.5f)
            {
                Stats.TimePlayedSeconds += elapsed;
                _sessionStart = Time.realtimeSinceStartup;
                if (persistNow)
                    Persist();
            }
        }

        private void Persist()
        {
            if (SaveManager.Instance != null)
                SaveManager.Instance.Save();
        }
    }
}
