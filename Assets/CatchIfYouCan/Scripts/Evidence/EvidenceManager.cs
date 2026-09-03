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

        // Unity cannot serialize Nullable<T>, so this field is runtime-only. Journal
        // entries are never written to a save file or an inspector, so the nullable
        // reads better here than a sentinel enum value would.
        [NonSerialized] public EvidenceType? RelatedEvidence;
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

            // Through the validator, not straight into the found set. A journal entry carrying
            // an evidence type is a device's finding written down, and it was the one door into
            // RegisterEvidence that skipped every check - which is how the EVP recorder proved
            // EVP Response against ghosts that do not make one.
            if (entry.RelatedEvidence.HasValue)
            {
                EvidenceValidator.Submit(new EvidenceObservation(
                    entry.RelatedEvidence.Value, entry.Title ?? "journal", 1f, Vector3.zero));
            }

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
