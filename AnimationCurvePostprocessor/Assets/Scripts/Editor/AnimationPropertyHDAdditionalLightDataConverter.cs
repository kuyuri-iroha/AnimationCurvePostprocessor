using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static UnityEditor.AnimationUtility;



#if UNITY_EDITOR

// エディター拡張クラス
[CustomEditor(typeof(AnimationPropertyHDAdditionalLightDataConverter))]
public class ExtendedEditor : Editor
{// Editor クラスを継承
    // Extend クラスの変数を扱うために宣言
    AnimationPropertyHDAdditionalLightDataConverter extend;

    void OnEnable()
    {// 最初に実行
        // Extend クラスに target を代入
        extend = (AnimationPropertyHDAdditionalLightDataConverter)target;
    }

    public override void OnInspectorGUI()
    {// Inspector に表示
        // これ以降の要素に関してエディタによる変更を記録
        
        
        EditorGUI.BeginChangeCheck();
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("animationClip"), new GUIContent("Animation Clip"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("animationClips"), new GUIContent("Animation Clip List"));
        // ラベルの作成
        var label = "Target Animation Property";
        // 初期値として表示する項目のインデックス番号
        var selectedIndex = extend.index;
        // プルダウンメニューに登録する文字列配列
        
        var bindings = extend.GetProperties();

        var propNames = new List<string>();
        foreach (var binding in bindings)
        {
            propNames.Add(binding.propertyName);
        }

       
        var displayOptions = propNames.ToArray();
        // プルダウンメニューの作成
        var index = displayOptions.Length > 0 ? EditorGUILayout.Popup(label, selectedIndex, displayOptions)
            : -1;

        if (index != extend.index)
        {// インデックス番号が変わったら番号をログ出力
            // Debug.Log(index);
        }
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("newPropName"), new GUIContent("New Prop Name"));

        
        if(GUILayout.Button("Replace"))
        {
            extend.Replace();
        }

        if (EditorGUI.EndChangeCheck())
        {// 操作を Undo に登録
            // Extend クラスの変更を記録
            var objectToUndo = extend;
            // Undo メニューに表示する項目名
            var name = "Properties";
            // 記録準備
            Undo.RecordObject(objectToUndo, name);
            // インデックス番号を登録
            extend.index = index;
        }
        
        
        
        serializedObject.ApplyModifiedProperties();
    }
}
// ここまでエディター上でのみ有効
#endif


[ExecuteAlways]


public class AnimationPropertyHDAdditionalLightDataConverter : MonoBehaviour
{

    public AnimationClip animationClip = null;
    public List<AnimationClip> animationClips = new List<AnimationClip>();
    // private string refPropName = "Sphere";
    public string newPropName = "m_LightDimmer";
    // public string refProp
    [HideInInspector]public int index = 0;
    // public int propertyNames;

// Start is called before the first frame update
    void Start()
    {

        
    }

    public EditorCurveBinding[] GetProperties()
    {
        return animationClip != null ? GetCurveBindings(animationClip) : new EditorCurveBinding[1];
    }

    [ContextMenu("Add Property")]
    public void AddProperty()
    {
        SetEditorCurves(animationClip, new EditorCurveBinding[] { new EditorCurveBinding {
            type = typeof(Light),
            path = "",
            propertyName = newPropName
        } }, new AnimationCurve[] { AnimationCurve.Linear(0, 0, 1, 1) });
        
    }

    [ContextMenu("Find Property")]
    public void FindProperty()
    {
        var refCurveBindings = GetCurveBindings(animationClip);
        
        
        foreach (var c in refCurveBindings)
        {
            Debug.Log($"path:{c.path}, property:{c.propertyName}");

            foreach (var VARIABLE in GetEditorCurve(animationClip,c).keys)
            {
                
            }
        }
        
        // Debug.Log(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(animationClip)));
        //
        //
        // Debug.Log(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(this)));
        // Debug.Log(AssetDatabase.GUIDToAssetPath("24bb5e1fdd5489848a000a2492c32392"));
    }
    [ContextMenu("Replace")]
    public void Replace()
    {
        var propName = GetProperties()[index].propertyName;

        Replace(this.animationClip,propName);
        foreach (var ac in animationClips)
        {
            Debug.Log(ac.name);
            Replace(ac,propName);
        }

    }

    public void Replace(AnimationClip _animationClip, string propName)
    {
        var refCurveBindings = GetCurveBindings(_animationClip);
        var newCurveBindings = new List<EditorCurveBinding>();
        var newAnimationCurves = new List<AnimationCurve>();
        // EditorCurveBinding editorCurveBinding;
        
       
        foreach (var c in refCurveBindings)
        {
            // Debug.Log(c.path);
            Debug.Log(c);
            var copyEditorCurveBinding = c;
            var animationCurve =  GetEditorCurve(_animationClip,c);
            if (c.propertyName == propName)
            {
                

                copyEditorCurveBinding = new EditorCurveBinding
                {
                    type = typeof(Light),
                    path = "",
                    propertyName = newPropName
                };
            }
           
            newCurveBindings.Add(copyEditorCurveBinding);
            newAnimationCurves.Add(animationCurve);     
            
           
        }
        _animationClip.ClearCurves();
        SetEditorCurves(_animationClip,newCurveBindings.ToArray(),newAnimationCurves.ToArray());
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}