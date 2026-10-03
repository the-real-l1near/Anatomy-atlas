using UnityEngine;
using UnityEngine.EventSystems;

namespace TranslucentUIFX.Demo
{
    [AddComponentMenu("")]
    public sealed class LiquidGlassDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        private Vector2 m_Offset;
        public void OnBeginDrag(PointerEventData eventData)
        {
            var rect = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent, eventData.position, eventData.pressEventCamera, out Vector2 p))
                m_Offset = (Vector2)rect.localPosition - p;
        }
        public void OnDrag(PointerEventData eventData)
        {
            var rect = (RectTransform)transform;
            var parent = (RectTransform)rect.parent;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out Vector2 p)) return;
            Vector2 size = rect.rect.size * 0.5f;
            p += m_Offset;
            p.x = Mathf.Clamp(p.x, parent.rect.xMin + size.x, parent.rect.xMax - size.x);
            p.y = Mathf.Clamp(p.y, parent.rect.yMin + size.y, parent.rect.yMax - size.y);
            rect.localPosition = new Vector3(p.x, p.y, rect.localPosition.z);
        }
    }
}
