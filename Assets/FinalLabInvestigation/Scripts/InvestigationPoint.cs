using System;
using UnityEngine;

namespace FinalLab.Investigation
{
    public sealed class InvestigationPoint : MonoBehaviour
    {
        [SerializeField] private string id, keyId, objective, prompt, hint, reaction;
        [SerializeField] private bool wall, container;
        [SerializeField] private float range;
        [SerializeField] private Transform overview, focus;
        [SerializeField] private GameObject outline;
        [SerializeField] private Collider[] approachSurfaces;
        [SerializeField] private InvestigationDocument[] evidence;

        public string Id => id;
        public string KeyId => keyId;
        public string Objective => objective;
        public string Prompt => prompt;
        public string Hint => hint;
        public string Reaction => reaction;
        public bool Wall => wall;
        public bool Container => container;
        public float Range => range;
        public Transform Overview => overview;
        public Transform Focus => focus;
        public bool IsHighlighted => outline != null && outline.activeSelf;
        public InvestigationDocument[] Evidence => evidence;

        public bool Owns(InvestigationDocument item) => Array.IndexOf(evidence, item) >= 0;

        public bool Owns(Collider area)
        {
            var item = area.GetComponentInParent<InvestigationDocument>();
            return (item != null && Owns(item)) || Array.IndexOf(approachSurfaces, area) >= 0;
        }

        public void Configure(string pointId, string key, string goal, string interaction, string routeHint, string quote,
            bool mountedWall, bool isContainer, float distance, Transform view, Transform approach,
            GameObject goalOutline, Collider[] surfaces, InvestigationDocument[] items)
        {
            id = pointId;
            keyId = key;
            objective = goal;
            prompt = interaction;
            hint = routeHint;
            reaction = quote;
            wall = mountedWall;
            container = isContainer;
            range = distance;
            overview = view;
            focus = approach;
            outline = goalOutline;
            approachSurfaces = surfaces;
            evidence = items;
        }

        public void SetHighlighted(bool visible)
        {
            if (outline != null && outline.activeSelf != visible) outline.SetActive(visible);
        }
    }
}
