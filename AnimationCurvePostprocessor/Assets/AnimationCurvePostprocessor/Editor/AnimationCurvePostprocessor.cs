using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Kuyuri.Tools.AnimationPostprocess;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Kuyuri.Tools
{
    public class AnimationCurvePostprocessor : EditorWindow
    {
        private enum Method
        {
            RemoveProperties,
            RDPReduction,
            FritschCarlsonSmoothing,
            StinemanSmoothing,
            SetKeyMode,
        }

        private enum PropertySpecifyMode
        {
            OnlyPropertyName,
            OnlyPath,
            FullPath,
        }

        private enum PropertyFilteringMode
        {
            Whitelist,
            Blacklist,
        }

        private enum PropertyMatchMode
        {
            Broad,
            Exact,
        }

        private static string FilenameExpression = "$FILENAME";
        
        [SerializeField] private VisualTreeAsset m_VisualTreeAsset = default;

        private List<AnimationClip> _animationClips = new List<AnimationClip>();
        private List<string> _propertyNames = new List<string>();
        private PropertySpecifyMode _propertySpecifyMode = PropertySpecifyMode.OnlyPropertyName;
        private PropertyFilteringMode _propertyFilteringMode = PropertyFilteringMode.Whitelist;
        private PropertyMatchMode _propertyMatchMode = PropertyMatchMode.Broad;

        private List<string> _targetPropertyNames = new List<string>();

        [MenuItem("Tools/AnimationCurvePostprocessor")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<AnimationCurvePostprocessor>();
            wnd.titleContent = new GUIContent("Animation Curve Postprocessor");
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            VisualElement labelFromUxml = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUxml);

            var sourceAnimationClips = root.Q<ListView>("sourceAnimationClips");
            var clipInfo = root.Q<TextField>("clipInfo");
            var propertyNames = root.Q<ListView>("propertyNames");
            var propertySpecifyMode = root.Q<RadioButtonGroup>("propertySpecifyMode");
            var propertyFilteringMode = root.Q<RadioButtonGroup>("propertyFilteringMode");
            var propertyMatchMode = root.Q<RadioButtonGroup>("propertyMatchMode");
            var selectedPropertiesInfo = root.Q<TextField>("selectedPropertiesInfo");
            var methodSelector = root.Q<EnumField>("methodSelector");
            var methodParameterContainer = root.Q<VisualElement>("methodParameterContainer");
            var overwriteToggle = root.Q<Toggle>("overwriteToggle");
            var exportFileName = root.Q<TextField>("exportFileName");
            var executeButton = root.Q<Button>("executeButton");

            // AnimationClips
            sourceAnimationClips.itemsSource = _animationClips;
            sourceAnimationClips.makeItem = () => new ObjectField()
            {
                objectType = typeof(AnimationClip)
            };
            
            void ClipListValueChanged(ChangeEvent<Object> evt)
            {
                var data = (evt.target as ObjectField)?.userData;
                if (data == null) return;
                var clip = evt.newValue as AnimationClip;
                if (clip != null)
                {
                    _animationClips[(int) data] = clip;
                    clipInfo.SetValueWithoutNotify(GetClipInfo(clip));
                }
                else
                {
                    _animationClips[(int) data] = null;
                    clipInfo.SetValueWithoutNotify(string.Empty);
                }

                executeButton.SetEnabled(_animationClips.Any(c => c != null));
                
                SearchTargetProperties();
                selectedPropertiesInfo.SetValueWithoutNotify(GetSelectedPropertiesInfo());
            }
            sourceAnimationClips.bindItem = (item, index) =>
            {
                if (item is not ObjectField objectField) return;
                objectField.value = _animationClips[index];
                objectField.userData = index;
                objectField.RegisterValueChangedCallback(ClipListValueChanged);
            };
            sourceAnimationClips.unbindItem = (item, index) =>
            {
                var textField = item as ObjectField;
                textField.UnregisterValueChangedCallback(ClipListValueChanged);
            };
            sourceAnimationClips.onSelectionChange += objects =>
            {
                var animationClips = objects.Cast<AnimationClip>().ToArray();
                var count = animationClips.Length;
                switch (count)
                {
                    case 0:
                        clipInfo.SetValueWithoutNotify(string.Empty);
                        break;
                    
                    case 1:
                        clipInfo.SetValueWithoutNotify(GetClipInfo(animationClips.FirstOrDefault()));
                        break;
                    
                    default:
                        clipInfo.SetValueWithoutNotify(GetClipsInfo(animationClips));
                        break;
                }
            };
            
            // Property Names
            propertyNames.itemsSource = _propertyNames;
            propertyNames.makeItem = () => new TextField()
            {
                value = "",
                multiline = false,
            };
            
            void PropertyValueChanged(ChangeEvent<string> evt)
            {
                var data = (evt.target as TextField)?.userData;
                if (data != null)
                {
                    _propertyNames[(int) data] = evt.newValue;
                }
            }
            propertyNames.bindItem = (item, index) =>
            {
                if (item is not TextField textField) return;
                textField.value = _propertyNames[index];
                textField.userData = index;
                textField.RegisterValueChangedCallback(PropertyValueChanged);
            };
            propertyNames.unbindItem = (item, index) =>
            {
                var textField = item as TextField;
                textField.UnregisterValueChangedCallback(PropertyValueChanged);
            };
            
            propertyNames.RegisterCallback<BlurEvent>(evt =>
            {
                SearchTargetProperties();
                selectedPropertiesInfo.SetValueWithoutNotify(GetSelectedPropertiesInfo());
            });
            
            // Property Specify Mode
            propertySpecifyMode.RegisterValueChangedCallback(evt =>
            {
                _propertySpecifyMode = (PropertySpecifyMode) evt.newValue;
                SearchTargetProperties();
                selectedPropertiesInfo.SetValueWithoutNotify(GetSelectedPropertiesInfo());
            });

            // Property Filtering Mode
            propertyFilteringMode.RegisterValueChangedCallback(evt =>
            {
                _propertyFilteringMode = (PropertyFilteringMode) evt.newValue;
                SearchTargetProperties();
                selectedPropertiesInfo.SetValueWithoutNotify(GetSelectedPropertiesInfo());
            });
            
            // Property Match Mode
            propertyMatchMode.RegisterValueChangedCallback(evt =>
            {
                _propertyMatchMode = (PropertyMatchMode) evt.newValue;
                SearchTargetProperties();
                selectedPropertiesInfo.SetValueWithoutNotify(GetSelectedPropertiesInfo());
            });
            
            // Method Selector
            methodSelector.Init(Method.RDPReduction);
            Postprocess postprocess = new RDPReduction();
            methodParameterContainer.Add(postprocess);
            
            methodSelector.RegisterValueChangedCallback(evt =>
            {
                var method = (Method)evt.newValue;

                methodParameterContainer.Clear();

                postprocess = method switch
                {
                    Method.RemoveProperties => new RemoveProperties(_animationClips),
                    Method.RDPReduction => new RDPReduction(),
                    Method.FritschCarlsonSmoothing => new FritschCarlsonSmoothing(),
                    Method.StinemanSmoothing => new StinemanSmoothing(),
                    Method.SetKeyMode => new SetKeyMode(),
                    _ => throw new ArgumentOutOfRangeException()
                };

                methodParameterContainer.Add(postprocess);
            });

            // Overwrite Toggle
            overwriteToggle.RegisterValueChangedCallback(evt =>
            {
                exportFileName.SetEnabled(!evt.newValue);
            });

            // Execute Button
            executeButton.SetEnabled(false);
            executeButton.RegisterCallback<ClickEvent>(evt =>
            {
                var format = new string(Enumerable.Range(1, _animationClips.Count.ToString().Length).Select(x => '0').ToArray());
                for(var i = 0; i < _animationClips.Count; i++)
                {
                    var sourceClip = _animationClips[i];
                    if(sourceClip == null) continue;
                    
                    // 処理の実行
                    postprocess.ExecuteToAnimationClip(out var distClip, sourceClip, _targetPropertyNames);
                    
                    // ファイル名の決定
                    var path = AssetDatabase.GetAssetPath(sourceClip);
                    var newFilename =
                        exportFileName.value.Replace(FilenameExpression, Path.GetFileNameWithoutExtension(path));
                    var fileIndex = _animationClips.Count == 1 ? "" : $"_{i.ToString(format)}";
                    if (overwriteToggle.value || string.IsNullOrEmpty(exportFileName.value))
                    {
                        newFilename = $"{Path.GetFileNameWithoutExtension(path)}";
                    }
                    else if (!exportFileName.value.Contains(FilenameExpression))
                    {
                        newFilename = $"{newFilename}{fileIndex}";
                    }
                
                    // 書き出し
                    WriteAnimationCurve(distClip, Path.GetDirectoryName(path), newFilename);

                    _animationClips[i] = distClip;
                }
                
                // 入れ替え
                sourceAnimationClips.RefreshItems();
            });
        }
        
        private static string GetClipInfo(AnimationClip clip)
        {
            if(null == clip) return string.Empty;
            
            var propertyCount = 0;
            var keyMax = 0;
            var keyMin = int.MaxValue;
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                keyMax = Mathf.Max(curve.length, keyMax);
                keyMin = Mathf.Min(curve.length, keyMin);
                propertyCount++;
            }
            
            var info = new StringBuilder();
            info.AppendLine($"Name: {clip.name}");
            info.AppendLine($"Frame rate: {clip.frameRate}");
            info.AppendLine($"Length: {clip.length}");
            info.AppendLine($"Property count: {propertyCount}");
            info.AppendLine($"Max keyframe: {keyMax}");
            info.AppendLine($"Min keyframe: {keyMin}");
            return info.ToString();
        }
        
        private static string GetClipsInfo(IEnumerable<AnimationClip> clips)
        {
            var frameRateMin = float.MaxValue;
            var frameRateMax = 0f;
            var lengthMin = float.MaxValue;
            var lengthMax = 0f;
            var propertyCountMin = int.MaxValue;
            var propertyCountMax = 0;
            var keyMin = int.MaxValue;
            var keyMax = 0;
            
            foreach (var clip in clips)
            {
                if(clip == null) continue;
                
                var propertyCount = 0;
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    keyMin = Mathf.Min(curve.length, keyMin);
                    keyMax = Mathf.Max(curve.length, keyMax);
                    propertyCount++;
                }
                
                frameRateMin = Mathf.Min(clip.frameRate, frameRateMin);
                frameRateMax = Mathf.Max(clip.frameRate, frameRateMax);
                lengthMin = Mathf.Min(clip.length, lengthMin);
                lengthMax = Mathf.Max(clip.length, lengthMax);
                propertyCountMin = Mathf.Min(propertyCount, propertyCountMin);
                propertyCountMax = Mathf.Max(propertyCount, propertyCountMax);
            }
            
            var info = new StringBuilder();
            info.AppendLine($"Name: -Multiple selected-");
            info.AppendLine($"Frame rate min: {frameRateMin}");
            info.AppendLine($"Frame rate max: {frameRateMax}");
            info.AppendLine($"Length min: {lengthMin}");
            info.AppendLine($"Length max: {lengthMax}");
            info.AppendLine($"Property count min: {propertyCountMin}");
            info.AppendLine($"Property count max: {propertyCountMax}");
            info.AppendLine($"Min keyframe: {keyMin}");
            info.AppendLine($"Max keyframe: {keyMax}");
            return info.ToString();
        }

        // アニメーションクリップの保存
        private static void WriteAnimationCurve(AnimationClip animClip, string animClipDirectory, string animClipName)
        {
            animClip.name = animClipName;
            AssetDatabase.CreateAsset(animClip, $"{animClipDirectory}/{animClipName}.anim");
            AssetDatabase.Refresh();
        }
        
        // アニメーションプロパティの検索
        private void SearchTargetProperties()
        {
            _targetPropertyNames.Clear();
            
            var debugTmp = new StringBuilder();
            foreach (var clip in _animationClips)
            {
                if(clip == null) continue;
                
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    debugTmp.AppendLine($"{binding.path}/{binding.propertyName}");
                    
                    var evaluationName = _propertySpecifyMode switch {
                        PropertySpecifyMode.OnlyPropertyName => binding.propertyName,
                        PropertySpecifyMode.OnlyPath => binding.path,
                        PropertySpecifyMode.FullPath => $"{binding.path}/{binding.propertyName}",
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    
                    var matched = _propertyMatchMode switch
                    {
                        PropertyMatchMode.Broad =>
                            _propertyNames.Exists(item => evaluationName.Contains(item)),
                        PropertyMatchMode.Exact =>
                            _propertyNames.Exists(item => evaluationName.Equals(item)),
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    
                    var selectMode = _propertyFilteringMode switch
                    {
                        PropertyFilteringMode.Whitelist => true,
                        PropertyFilteringMode.Blacklist => false,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    
                    if (!(matched ^ selectMode) || _propertyNames.Count == 0 || _propertyNames.All(string.IsNullOrEmpty))
                    {
                        _targetPropertyNames.Add(binding.propertyName);
                    }
                }
            }

            Debug.Log(debugTmp);
            _targetPropertyNames = _targetPropertyNames.Distinct().ToList();
        }

        // 対象アニメーションプロパティ情報の取得
        private string GetSelectedPropertiesInfo()
        {
            var result = new StringBuilder();
            foreach (var clip in _animationClips)
            {
                if(clip == null) continue;
                var propCount = AnimationUtility.GetCurveBindings(clip).Count(binding => _targetPropertyNames.Exists(targetName => binding.propertyName.Equals(targetName)));
                result.AppendLine($"{clip.name} : Found {propCount} properties.");
            }

            return result.ToString();
        }
    }
}
