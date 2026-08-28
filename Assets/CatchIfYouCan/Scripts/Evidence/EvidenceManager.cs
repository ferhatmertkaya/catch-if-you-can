using System;
using System.Collections.Generic;
using UnityEngine;
using CatchIfYouCan.Core;
using ServiceLocator = CatchIfYouCan.Core.ServiceLocator;
using CatchIfYouCan.Evidence;
using CatchIfYouCan.Utilities;

namespace CatchIfYouCan.Evidence
{
    [Serializable]
    public class JournalEntry
    {
        public string Id;
        public string Title;
        public string Body;
        public EvidenceType? RelatedEvidence;
        public long TimestampUtcTicks;

        public JournalEntry()
        {
            Id = Guid.NewGuid().ToString("N");
            TimestampUtcTicks = DateTime.UtcNow.Ticks;
        }
    }

    public class EvidenceManager : SingletonBehaviour<EvidenceManager>
    {
        private readonly HashSet<EvidenceType> _foundEvidence = new HashSet<EvidenceType>();
        private readonly List<PhotoResult> _photos = new List<PhotoResult>();
        private readonly List<JournalEntry> _journalEntries = new List<JournalEntry>();

        public IReadOnlyCollection<EvidenceType> FoundEvidence => _foundEvidence;
        public IReadOnlyList<PhotoResult> Photos => _photos;
        public IReadOnlyList<JournalEntry> JournalEntries => _journalEntries;

        protected override void Awake()
        {
            base.Awake();
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
                ServiceLocator.Unregister<EvidenceManager>();
            base.OnDestroy();
        }

        public void ResetMission()
        {
            _foundEvidence.Clear();
            _photos.Clear();
            _journalEntries.Clear();
        }

        public bool HasEvidence(EvidenceType type) => _foundEvidence.Contains(type);

        public bool RegisterEvidence(EvidenceType type)
        {
            if (!_foundEvidence.Add(type))
                return false;

            GameEvents.EvidenceDetected(type);
            CIYCLog.Info($"Evidence registered: {type}");
            return true;
        }

        public void AddPhoto(PhotoResult photo)
        {
            if (photo == null)
                return;

            _photos.Add(photo);
            GameEvents.PhotoTaken(photo.Stars);
            CIYCLog.Info($"Photo saved ({photo.Stars} stars): {photo.Caption}");
        }

        public void AddJournalEntry(JournalEntry entry)
        {
            if (entry == null)
                return;

            _journalEntries.Add(entry);
            if (entry.RelatedEvidence.HasValue)
                RegisterEvidence(entry.RelatedEvidence.Value);

            CIYCLog.Info($"Journal entry added: {entry.Title}");
        }

        public void AddJournalEntry(string title, string body, EvidenceType? relatedEvidence = null)
        {
            AddJournalEntry(new JournalEntry
            {
                Title = title,
                Body = body,
                RelatedEvidence = relatedEvidence
            });
        }
    }
}
