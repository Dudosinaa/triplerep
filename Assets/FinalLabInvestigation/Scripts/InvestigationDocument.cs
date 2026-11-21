using System;
using UnityEngine;

namespace FinalLab.Investigation
{
    public enum EvidenceKind { Document, Photograph, Detail, Symbol }
    public enum EvidencePresentation { Mounted, Lifted }

    [Serializable]
    public sealed class EvidenceRecord
    {
        public string id, title, date, body, style, stamp;
        public int point;
    }

    [Serializable]
    public sealed class EvidenceCatalog
    {
        public EvidenceRecord[] documents;
    }

    public sealed class InvestigationDocument : MonoBehaviour
    {
        [SerializeField] private EvidenceRecord record;
        [SerializeField] private EvidenceKind kind;
        [SerializeField] private EvidencePresentation presentation;
        [SerializeField] private Vector2 size;
        [SerializeField] private Collider hitArea;
        [SerializeField] private GameObject outline;

        public EvidenceRecord Record => record;
        public string Id => record.id;
        public EvidenceKind Kind => kind;
        public bool Lifts => presentation == EvidencePresentation.Lifted;
        public Vector2 Size => size;
        public Collider HitArea => hitArea;
        public bool IsHighlighted => outline != null && outline.activeSelf;

        public void Configure(EvidenceRecord data, EvidenceKind type, EvidencePresentation motion, Vector2 pageSize,
            Collider area, GameObject hoverOutline)
        {
            record = data;
            kind = type;
            presentation = motion;
            size = pageSize;
            hitArea = area;
            outline = hoverOutline;
        }

        public void SetHighlighted(bool visible)
        {
            if (outline != null && outline.activeSelf != visible) outline.SetActive(visible);
        }
    }
}
