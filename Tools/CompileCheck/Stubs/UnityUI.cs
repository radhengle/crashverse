using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, float s) => new Vector2(a.x * s, a.y * s);
        public static Vector2 operator /(Vector2 a, float s) => new Vector2(a.x / s, a.y / s);
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0);
    }
    public class RectTransform : Transform
    {
        public Vector2 anchorMin, anchorMax, pivot, sizeDelta, anchoredPosition, offsetMin, offsetMax;
        public Rect rect;
    }
    public class Font : Object { }
    public class Texture : Object { public int width = 1, height = 1; public FilterMode filterMode; public TextureWrapMode wrapMode; }
    public class Texture2D : Texture { public Texture2D(int w, int h) { width = w; height = h; } }
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp, Mirror, MirrorOnce }
    public enum SpriteMeshType { FullRect, Tight }
    public class Sprite : Object
    {
        public Texture2D texture; public Rect rect;
        public static Sprite Create(Texture2D t, Rect r, Vector2 pivot, float ppu = 100f, uint extrude = 0, SpriteMeshType m = SpriteMeshType.Tight) => new Sprite { texture = t, rect = r };
    }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }
    public enum ScreenOrientation { Portrait, PortraitUpsideDown, LandscapeLeft, LandscapeRight, AutoRotation }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    public class Canvas : Behaviour { public static void ForceUpdateCanvases() { } public RenderMode renderMode; public int sortingOrder; public float scaleFactor = 1f; }
    public class Mesh : Object
    {
        public Vector3[] vertices; public int[] triangles; public Vector3[] normals; public Vector2[] uv;
        public void RecalculateNormals() { } public void RecalculateBounds() { } public void SetTriangles(int[] t, int s) { }
    }
    public class MeshFilter : Component { public Mesh sharedMesh, mesh; }
    public class MeshRenderer : Renderer { }
    public class LineRenderer : Renderer
    {
        public int positionCount; public float startWidth, endWidth, widthMultiplier; public bool useWorldSpace; public Color startColor, endColor;
        public Material sharedMaterial, material;
        public void SetPosition(int i, Vector3 p) { } public void SetPositions(Vector3[] p) { }
    }
    public class Material : Object { public Material(Shader s) { } public Shader shader; public Color color; public void SetColor(string n, Color c) { } }
    public class Shader : Object { public static Shader Find(string n) => new Shader(); public bool isSupported = true; }
    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }
    public struct Touch { public int fingerId; public Vector2 position; public Vector2 deltaPosition; public TouchPhase phase; }
    public enum KeyCode { Escape, Space, Return }
}

namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public class UnityEvent { public void AddListener(UnityAction a) { } public void RemoveAllListeners() { } public void Invoke() { } }
    public delegate void UnityAction<T0>(T0 a);
    public class UnityEvent<T0> { public void AddListener(UnityAction<T0> a) { } public void RemoveAllListeners() { } public void Invoke(T0 a) { } }
}

namespace UnityEngine.EventSystems
{
    public class UIBehaviour : MonoBehaviour { }
    public class EventSystem : UIBehaviour
    {
        public static EventSystem current; public int pixelDragThreshold;
        public bool IsPointerOverGameObject() => false; public bool IsPointerOverGameObject(int id) => false;
    }
    public class BaseInputModule : UIBehaviour { }
    public class StandaloneInputModule : BaseInputModule { }
    public class PointerEventData { public Vector2 delta, position; public int pointerId; }
    public interface IEventSystemHandler { }
    public interface IDragHandler : IEventSystemHandler { void OnDrag(PointerEventData e); }
    public interface IPointerDownHandler : IEventSystemHandler { void OnPointerDown(PointerEventData e); }
    public interface IPointerUpHandler : IEventSystemHandler { void OnPointerUp(PointerEventData e); }
}

namespace UnityEngine.UI
{
    using UnityEngine.EventSystems;
    public class Graphic : UIBehaviour { public Color color; public bool raycastTarget = true; public RectTransform rectTransform => new RectTransform(); }
    public class MaskableGraphic : Graphic { }
    public class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public Type type; public FillMethod fillMethod; public float fillAmount;
        public Sprite sprite; public bool preserveAspect; public int fillOrigin;
    }
    public class RawImage : MaskableGraphic { public Texture texture; }
    public class Shadow : UIBehaviour { public Color effectColor; public Vector2 effectDistance; public bool useGraphicAlpha = true; }
    public class Outline : Shadow { }
    public class AspectRatioFitter : UIBehaviour { public enum AspectMode { None, WidthControlsHeight, HeightControlsWidth, FitInParent, EnvelopeParent } public AspectMode aspectMode; public float aspectRatio = 1f; }
    public class InputField : Selectable
    {
        public enum ContentType { Standard, Autocorrected, IntegerNumber, DecimalNumber, Alphanumeric, Name, EmailAddress, Password, Pin, Custom }
        public enum LineType { SingleLine, MultiLineSubmit, MultiLineNewline }
        public class SubmitEvent : UnityEngine.Events.UnityEvent<string> { }
        public class OnChangeEvent : UnityEngine.Events.UnityEvent<string> { }
        public string text = ""; public int characterLimit; public ContentType contentType; public LineType lineType;
        public Text textComponent; public Graphic placeholder; public SubmitEvent onEndEdit = new SubmitEvent(); public OnChangeEvent onValueChanged = new OnChangeEvent();
        public bool isFocused; public void ActivateInputField() { } public void DeactivateInputField() { }
    }
    public class Text : MaskableGraphic
    {
        public Font font; public int fontSize; public string text; public TextAnchor alignment;
        public HorizontalWrapMode horizontalOverflow; public VerticalWrapMode verticalOverflow;
        public bool resizeTextForBestFit; public int resizeTextMinSize, resizeTextMaxSize;
    }
    public struct ColorBlock { public Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor; }
    public class Selectable : UIBehaviour { public bool interactable = true; public ColorBlock colors; public Image image; public Graphic targetGraphic; public bool IsInteractable() => interactable; }
    public class Button : Selectable { public class ButtonClickedEvent : UnityEngine.Events.UnityEvent { } public ButtonClickedEvent onClick = new ButtonClickedEvent(); }
    public class CanvasScaler : UIBehaviour { public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize } public ScaleMode uiScaleMode; public float scaleFactor = 1f; }
    public class GraphicRaycaster : UIBehaviour { }
    public static class LayoutRebuilder { public static void ForceRebuildLayoutImmediate(RectTransform rt) { } }
    public static class LayoutUtility { public static float GetPreferredHeight(RectTransform rt) => 0f; }
    public class LayoutElement : UIBehaviour { public float minHeight, minWidth, preferredHeight, preferredWidth, flexibleHeight, flexibleWidth; }
    public class LayoutGroup : UIBehaviour { public RectOffset padding = new RectOffset(); public TextAnchor childAlignment; }
    public class HorizontalOrVerticalLayoutGroup : LayoutGroup { public float spacing; public bool childControlHeight, childControlWidth, childForceExpandHeight, childForceExpandWidth; }
    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class ContentSizeFitter : UIBehaviour { public enum FitMode { Unconstrained, MinSize, PreferredSize } public FitMode verticalFit, horizontalFit; }
    public class ScrollRect : UIBehaviour { public enum MovementType { Unrestricted, Elastic, Clamped } public RectTransform content; public bool horizontal, vertical; public MovementType movementType; public RectTransform viewport; public float verticalNormalizedPosition; }
    public class RectMask2D : UIBehaviour { }
}

namespace UnityEngine
{
    public static class RectTransformUtility { public static Vector2 WorldToScreenPoint(Camera c, Vector3 p) => new Vector2(p.x, p.y); public static bool RectangleContainsScreenPoint(RectTransform r, Vector2 p, Camera c) => true; }

    public class RectOffset { public RectOffset() { } public RectOffset(int l, int r, int t, int b) { } }
}
namespace UnityEngine { public static class GameObjectExt2 { } }
