using UnityEngine;
using UnityEngine.UI;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(CanvasRenderer))]
[RequireComponent(typeof(RectTransform))]
public class HomographyImage : Image
{
    public Material Material
	{
		get
		{
            if (_material == null)
            {
                if (_shader == null)
                {
                    _shader = Shader.Find("UI/Homography");
                }
                _material = new Material(_shader);
                _material.enableInstancing = true;
                material = _material;
            }
            return _material;
        }
	}
    private Material _material;
    private Material _prevCanvasMaterial;

    private Shader _shader;

    public float[] Homography { get; private set; }
    public float[] InvHomography { get; private set; }

    public Vector3[] Points { get { return _points; } set { _points = value; } }
    [HideInInspector, SerializeField]
    private Vector3[] _points = 
    {
        new Vector3(0f, 0f),
        new Vector3(1f, 0f),
        new Vector3(1f, 1f),
        new Vector3(0f, 1f)
    };

    protected override void Awake()
    {
        base.Awake();
        SetMaterialProperties();
    }

	protected void Update()
	{
        // Detect material change by Mask
        var currentMaterial = canvasRenderer.GetMaterial();
        if (_material != currentMaterial && _prevCanvasMaterial != currentMaterial)
		{
            SetMaterialProperties();
		}
	}

	public void SetPoint(int index, Vector2 val)
	{
        Points[index] = ClampVector(val);
        SetMaterialProperties();
	}

    public Vector3 GetPoint(int index)
	{
        return Points[index];
	}

    public void SetPointsWorld(Vector3[] p)
    {
        if (p.Length != 4) return;

        var xmin = Mathf.Min(p[0].x, p[1].x, p[2].x, p[3].x);
        var xmax = Mathf.Max(p[0].x, p[1].x, p[2].x, p[3].x);
        var ymin = Mathf.Min(p[0].y, p[1].y, p[2].y, p[3].y);
        var ymax = Mathf.Max(p[0].y, p[1].y, p[2].y, p[3].y);

        var width = xmax - xmin;
        var height = ymax - ymin;
        rectTransform.position = new Vector3(xmin + width * rectTransform.pivot.x, ymin + height * rectTransform.pivot.y);

        var localWidth = (rectTransform.lossyScale.x != 0) ? width / rectTransform.lossyScale.x : 0;
        var localHeight = (rectTransform.lossyScale.y != 0) ? height / rectTransform.lossyScale.y : 0;

        var sizeDelta = new Vector2(localWidth, localHeight);
        if (rectTransform.anchorMin.x != rectTransform.anchorMax.x)
        {
            var anchorRate = rectTransform.anchorMax.x - rectTransform.anchorMin.x;
            var parentLocalWidth = (rectTransform.parent as RectTransform).rect.width;
            sizeDelta.x = -(parentLocalWidth * anchorRate - localWidth);
        }
        if (rectTransform.anchorMin.y != rectTransform.anchorMax.y)
        {
            var anchorRate = rectTransform.anchorMax.y - rectTransform.anchorMin.y;
            var parentLocalHeight = (rectTransform.parent as RectTransform).rect.height;
            sizeDelta.y = -(parentLocalHeight * anchorRate - localHeight);
        }
        rectTransform.sizeDelta = sizeDelta;

        var invW = (localWidth != 0) ? 1.0f / localWidth : 0;
        var invH = (localHeight != 0) ? 1.0f / localHeight : 0;
        var inverseScale = new Vector2(invW, invH);
        var pivotOffset = new Vector3(localWidth * rectTransform.pivot.x, localHeight * rectTransform.pivot.y, 0f);
        for (var i = 0; i < 4; i++)
        {
            var nextPosition = rectTransform.InverseTransformPoint(p[i]);
            nextPosition += pivotOffset;
            nextPosition.Scale(inverseScale);
            Points[i] = ClampVector(nextPosition);
        }

        SetMaterialProperties();
    }

    public Vector2 GetPointWorld(int index)
    {
        var width = rectTransform.rect.width;
        var height = rectTransform.rect.height;
        var scale = new Vector2(width, height);
        var worldPosition = Points[index];
        worldPosition.Scale(scale);
        worldPosition -= new Vector3(width * rectTransform.pivot.x, height * rectTransform.pivot.y, 0f);
        worldPosition = rectTransform.TransformPoint(worldPosition);
        return worldPosition;
    }

    private void SetMaterialProperties()
    {
        Homography = CalcHomographyMatrix();
        InvHomography = CalcInverseMatrix(Homography);
        Material.SetFloatArray("_InvHomography", InvHomography);

        var currentMaterial = canvasRenderer.GetMaterial();
        if (currentMaterial != null && currentMaterial != _material)
        {
            currentMaterial.SetFloatArray("_InvHomography", InvHomography);
            _prevCanvasMaterial = currentMaterial;
        }
    }

    private float[] CalcHomographyMatrix()
    {
        var p00 = Points[0];
        var p10 = Points[1];
        var p11 = Points[2];
        var p01 = Points[3];

        var x00 = p00.x;
        var y00 = p00.y;
        var x01 = p01.x;
        var y01 = p01.y;
        var x10 = p10.x;
        var y10 = p10.y;
        var x11 = p11.x;
        var y11 = p11.y;

        var a = x10 - x11;
        var b = x01 - x11;
        var c = x00 - x01 - x10 + x11;
        var d = y10 - y11;
        var e = y01 - y11;
        var f = y00 - y01 - y10 + y11;

        var h13 = x00;
        var h23 = y00;
        var h32 = (c * d - a * f) / (b * d - a * e);
        var h31 = (c * e - b * f) / (a * e - b * d);
        var h11 = x10 - x00 + h31 * x10;
        var h12 = x01 - x00 + h32 * x01;
        var h21 = y10 - y00 + h31 * y10;
        var h22 = y01 - y00 + h32 * y01;

        return new float[] { h11, h12, h13, h21, h22, h23, h31, h32, 1f };
    }

    private float[] CalcInverseMatrix(float[] mat)
    {
        var i11 = mat[0];
        var i12 = mat[1];
        var i13 = mat[2];
        var i21 = mat[3];
        var i22 = mat[4];
        var i23 = mat[5];
        var i31 = mat[6];
        var i32 = mat[7];
        var i33 = 1f;
        var a = 1f / (
            +(i11 * i22 * i33)
            + (i12 * i23 * i31)
            + (i13 * i21 * i32)
            - (i13 * i22 * i31)
            - (i12 * i21 * i33)
            - (i11 * i23 * i32)
        );

        var o11 = (i22 * i33 - i23 * i32) / a;
        var o12 = (-i12 * i33 + i13 * i32) / a;
        var o13 = (i12 * i23 - i13 * i22) / a;
        var o21 = (-i21 * i33 + i23 * i31) / a;
        var o22 = (i11 * i33 - i13 * i31) / a;
        var o23 = (-i11 * i23 + i13 * i21) / a;
        var o31 = (i21 * i32 - i22 * i31) / a;
        var o32 = (-i11 * i32 + i12 * i31) / a;
        var o33 = (i11 * i22 - i12 * i21) / a;

        return new float[] { o11, o12, o13, o21, o22, o23, o31, o32, o33 };
    }

    private Vector3 ClampVector(Vector3 v, float min = 0f, float max = 1f)
    {
        return new Vector3(Mathf.Clamp(v.x, min, max), Mathf.Clamp(v.y, min,max));
    }

#if UNITY_EDITOR

	protected override void Reset()
    {
        base.Reset();
        SetMaterialProperties();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        SetMaterialProperties();
    }

#endif

}

#if UNITY_EDITOR

[CustomEditor(typeof(HomographyImage), true)]
public class HomographyImageEditor : Editor
{
    private Vector3[] _nextPositions = new Vector3[4];
    private bool _isOpenRaycastPadding = false;
    private bool _isOpenPoints = false;

    public override void OnInspectorGUI()
    {
        var img = target as HomographyImage;

        EditorGUI.BeginChangeCheck();

        img.sprite = EditorGUILayout.ObjectField("Sprite Image", img.sprite, typeof(Sprite), false) as Sprite;
        img.color = EditorGUILayout.ColorField("Color", img.color);
        img.raycastTarget = EditorGUILayout.Toggle("Raycast Target", img.raycastTarget);
		_isOpenRaycastPadding = EditorGUILayout.Foldout(_isOpenRaycastPadding, "Raycast Padding");
		if (_isOpenRaycastPadding)
		{
			EditorGUI.indentLevel++;
			float x, y, z, w;
			x = EditorGUILayout.FloatField("X", img.raycastPadding.x);
			y = EditorGUILayout.FloatField("Y", img.raycastPadding.y);
			z = EditorGUILayout.FloatField("Z", img.raycastPadding.z);
			w = EditorGUILayout.FloatField("W", img.raycastPadding.w);
			img.raycastPadding = new Vector4(x, y, z, w);
			EditorGUI.indentLevel--;
		}
        img.maskable = EditorGUILayout.Toggle("Maskable", img.maskable);

        _isOpenPoints = EditorGUILayout.Foldout(_isOpenPoints, "Points");
        if (_isOpenPoints)
        {
            EditorGUI.indentLevel++;
            Vector3 p0, p1, p2, p3;
            p0 = EditorGUILayout.Vector2Field("P0", img.GetPoint(0));
            p1 = EditorGUILayout.Vector2Field("P1", img.GetPoint(1));
            p2 = EditorGUILayout.Vector2Field("P2", img.GetPoint(2));
            p3 = EditorGUILayout.Vector2Field("P3", img.GetPoint(3));
            if (p0 != img.GetPoint(0)) img.SetPoint(0, p0);
            if (p1 != img.GetPoint(1)) img.SetPoint(1, p1);
            if (p2 != img.GetPoint(2)) img.SetPoint(2, p2);
            if (p3 != img.GetPoint(3)) img.SetPoint(3, p3);
            EditorGUI.indentLevel--;
        }

        if (EditorGUI.EndChangeCheck())
		{
            Undo.RecordObject(img, "Settings Undo");
            EditorUtility.SetDirty(img);
        }
    }

    protected void OnSceneGUI()
    {
        var homographyImage = target as HomographyImage;
        var isUpdated = false;

        for (var i = 0; i < 4; i++)
        {
            Handles.color = Color.green;
            var handlePosition = homographyImage.GetPointWorld(i);

            var handleSize = HandleUtility.GetHandleSize(handlePosition) * 0.1f;

            EditorGUI.BeginChangeCheck();
            var nextPosition = Handles.FreeMoveHandle(handlePosition, Quaternion.identity, handleSize, new Vector3(1f, 1f, 0), Handles.CircleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                isUpdated = true;
            }

            _nextPositions[i] = nextPosition;
        }

        if (isUpdated)
        {
            Undo.RecordObjects(new Object[] { homographyImage, homographyImage.rectTransform }, "Undo Change HomographyImage");
            homographyImage.SetPointsWorld(_nextPositions);
            EditorUtility.SetDirty(homographyImage);
        }
    }
}

#endif
