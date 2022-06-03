using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class UIElementsListViewExample : EditorWindow
{
    [MenuItem("Tools/UIElements ListView Example")]
    public static void ShowWindow()
    {
        // This method is called when the user selects the menu item in the Editor
        var window = GetWindow(typeof(UIElementsListViewExample));
        window.titleContent = new GUIContent("UIElements ListView Example");
        window.minSize = new Vector2(450, 200);
        window.maxSize = new Vector2(1920, 720);
    }

    private int m_SelectedIndex = -1;
    private List<AnimationClip> _animationClips = new List<AnimationClip>() {null};

    public void CreateGUI()
    {
        var listView = new ListView();
        rootVisualElement.Add(listView);

        listView.makeItem = () => new ObjectField()
        {
            objectType = typeof(AnimationClip)
        };
        listView.bindItem = (item, index) =>
        {
            var objectField = (item as ObjectField);
            objectField.value = _animationClips[index];
            objectField.RegisterValueChangedCallback((e) => { _animationClips[index] = e.newValue as AnimationClip; });
            Debug.Log(objectField.value);
        };

        listView.itemsSource = _animationClips;
        listView.showAddRemoveFooter = true;
        listView.selectionType = SelectionType.Multiple;
        listView.selectedIndex = m_SelectedIndex;
        listView.reorderable = true;
        listView.showBorder = true;
        listView.showFoldoutHeader = true;
        listView.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
        listView.showBoundCollectionSize = true;
        listView.showAddRemoveFooter = true;
        listView.onSelectionChange += (items) => { m_SelectedIndex = listView.selectedIndex; };


        rootVisualElement.Add(listView);
    }
}