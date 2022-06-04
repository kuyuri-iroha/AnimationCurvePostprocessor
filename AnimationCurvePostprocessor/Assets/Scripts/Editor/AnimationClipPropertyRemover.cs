using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using static UnityEditor.AnimationUtility;
using UnityEngine.UIElements;



public class AnimationClipPropertyRemover : EditorWindow
{
    [MenuItem("Tools/AnimationClipPropertyRemover")]
    public static void ShowWindow()
    {
        GetWindow(typeof(AnimationClipPropertyRemover));
    }

    
    [SerializeField] private int m_SelectedIndex = -1;
    public List<AnimationClip> animationClips = new List<AnimationClip>(){};
    
    private Dictionary<AnimationClip, string> resutlAnimationClips = new Dictionary<AnimationClip,string>();
    VisualElement root;
    ListView _listView;
    private bool _removeLessThan2KeyFrames = false;
    private string _propertyName = "";
    private StringBuilder _stringBuilder = new StringBuilder();
    public void CreateGUI()
    {
        
        root = rootVisualElement;
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>
            ("Assets/AnimationClipUtils/Editor/AnimationClipPropertyRemoverUI.uxml");
        VisualElement visualElement = visualTree.CloneTree();
        
        
        _listView = new ListView();
        root.Add(_listView);

        _listView.makeItem = () => new ObjectField()
        {
            objectType = typeof(AnimationClip)
        };
        _listView.bindItem = (item, index) =>
        {
            var objectField = (item as ObjectField);
            objectField.value = animationClips[index];
            objectField.userData = index;
            objectField.RegisterValueChangedCallback((e) =>
            {
                if(e.newValue != e.previousValue)
                {
                    InitPlaceHolder();
                }
                animationClips[(int)(e.target as ObjectField).userData] = e.newValue as AnimationClip;
            });
            Debug.Log(objectField.value);
        };
        _listView.itemsSource = animationClips;
        _listView.showAddRemoveFooter = true;
        _listView.selectionType = SelectionType.Multiple;
        _listView.selectedIndex = m_SelectedIndex;
        _listView.reorderable = true;
        _listView.showBorder = true;
        _listView.showFoldoutHeader = true;
        _listView.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
        _listView.showBoundCollectionSize = true;
        _listView.showAddRemoveFooter = true;
        _listView.onSelectionChange += (items) =>
        {
            m_SelectedIndex = _listView.selectedIndex;
        };
        
        
        root.Add(visualElement);

        var toggleField = root.Q<Toggle>("RemoveFlag");
        toggleField.value = _removeLessThan2KeyFrames;
        
        toggleField.RegisterValueChangedCallback((e)=>
        {
            _removeLessThan2KeyFrames = e.newValue;
            
        });
     
        
        var nameField = root.Q<TextField>("PropertyName");
        nameField.value = _propertyName;
        nameField.RegisterValueChangedCallback((e) =>
        {
            _propertyName = e.newValue;
        });
        
        var removeButton = root.Q<Button>("ExportButton");
        removeButton.SetEnabled(false);
        root.Q<Button>("SearchButton").clicked+=() =>
        {
            Debug.Log("Search remove property");
            _stringBuilder.Clear();
            resutlAnimationClips.Clear();
            foreach (var animationClip in animationClips)
            {
                if (animationClip != null)
                {
                    Remove(animationClip,_propertyName);        
                }
            }
            
            root.Q<TextField>("ResultTextField").value = _stringBuilder.ToString();
            if(resutlAnimationClips.Count > 0)removeButton.SetEnabled(resutlAnimationClips.Count>0);
        };

        
        removeButton.clicked += () =>
        {
            if (resutlAnimationClips.Count <= 0)
            {
                removeButton.SetEnabled(false); 
                return;
            }
            Export();
        };

    }


    public void Export()
    {
        foreach (var resutl in resutlAnimationClips.Keys)
        {
            var path = resutlAnimationClips[resutl];
            AssetDatabase.CreateAsset(resutl,path);
        }
    }


    public void InitPlaceHolder()
    {
        _stringBuilder.Clear();
        root.Q<TextField>("ResultTextField").value = "";
        resutlAnimationClips.Clear();
        root.Q<Button>("ExportButton").SetEnabled(false);
    }

    public void Remove(AnimationClip animationClip, string propName)
    {
        var refCurveBindings = GetCurveBindings(animationClip);
        var newCurveBindings = new List<EditorCurveBinding>();
        var newAnimationCurves = new List<AnimationCurve>();
       
         // EditorCur   veBinding editorCurveBinding;
        resutlAnimationClips.Clear();
        _stringBuilder.AppendLine(animationClip.name);
        var newAnimationClip = new AnimationClip();
        var hitCount = 0;
        foreach (var c in refCurveBindings)
        {
            // Debug.Log(c.path);
            // Debug.Log(c);   
            var copyEditorCurveBinding = c;
            var animationCurve =  GetEditorCurve(animationClip,c);
            
            // 検索文字列が含まれている場合は消す
            // KeyFrameが2つ以下の場合はそもそもキーが入ってないので、ユーザーが任意で消せるようにする
            var isRemove = (propName != "" && c.propertyName.Contains(propName) )||
                           (_removeLessThan2KeyFrames && animationCurve.keys.Length <= 2);

            // Propertyを個別に消すことができないので、消す対象出なかった場合は新規で追加し、値をコピーしないといけない
            if(!isRemove)
            {
                copyEditorCurveBinding = new EditorCurveBinding
                {
                    type = c.type,
                    path =c.path,
                    propertyName = c.propertyName
                };
                newCurveBindings.Add(copyEditorCurveBinding);
                newAnimationCurves.Add(animationCurve);
            }
            else
            {
                hitCount++;
            }
           
           
        }
        
        _stringBuilder.AppendLine($"Found {hitCount} properties");
        // _animationClip.ClearCurves();
        if (hitCount > 0)
        {
            SetEditorCurves(newAnimationClip,newCurveBindings.ToArray(),newAnimationCurves.ToArray());
            EditorUtility.SetDirty(newAnimationClip);
            newAnimationClip.name = animationClip.name.Replace(".anim","_removed.anim");
            resutlAnimationClips.Add(newAnimationClip,AssetDatabase.GetAssetPath(animationClip).Replace(".anim","_removed.anim"));       
        }
     
    }

    
    // }
}
