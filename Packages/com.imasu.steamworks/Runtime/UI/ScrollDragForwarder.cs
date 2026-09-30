using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Imasu.Steamworks
{
    /// <summary>
    /// Lets drag events starting on a child graphic (e.g. an invite row's own
    /// hover Image) still reach the owning ScrollRect. An EventTrigger on the
    /// row handles hover/click but does NOT implement the drag interfaces, so
    /// without this forwarder the ScrollRect never sees the drag and the list
    /// feels locked. Drop it on any row inside a ScrollRect's content.
    /// </summary>
    public class ScrollDragForwarder : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        ScrollRect target;

        void Awake()
        {
            target = GetComponentInParent<ScrollRect>();
        }

        void RefreshTarget()
        {
            if (target == null) target = GetComponentInParent<ScrollRect>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            RefreshTarget();
            if (target != null) target.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            RefreshTarget();
            if (target != null) target.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            RefreshTarget();
            if (target != null) target.OnEndDrag(eventData);
        }

        public void OnScroll(PointerEventData eventData)
        {
            RefreshTarget();
            if (target != null) target.OnScroll(eventData);
        }
    }
}