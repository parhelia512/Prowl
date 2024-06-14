﻿using Hexa.NET.ImGui;
using Prowl.Editor.Assets;
using Prowl.Editor.ImGUI.Widgets;
using Prowl.Editor.PropertyDrawers;
using Prowl.Editor.Utilities;
using Prowl.Runtime;
using Prowl.Runtime.Utils;
using System.Reflection;

namespace Prowl.Editor.EditorWindows.CustomEditors
{
    /// <summary>
    /// GameObject Custom Editor for the Inspector Window
    /// </summary>
    [CustomEditor(typeof(GameObject))]
    public class GameObjectEditor : ScriptedEditor
    {
        private string _searchText = string.Empty;
        private static MenuItemInfo rootMenuItem;
        private Dictionary<int, ScriptedEditor> compEditors = new();

        [OnAssemblyUnload]
        public static void ClearCache() => rootMenuItem = null;

        public override void OnDisable()
        {
            foreach (var editor in compEditors.Values)
                editor.OnDisable();
        }

        public override void OnInspectorGUI()
        {
            var go = target as GameObject;

            ImGui.PushID(go.GetHashCode());
            if (go.hideFlags.HasFlag(HideFlags.NotEditable)) ImGui.BeginDisabled();

            bool isEnabled = go.enabled;
            ImGui.Checkbox("##GOActive", ref isEnabled);
            if (isEnabled != go.enabled) go.enabled = isEnabled;
            GUIHelper.Tooltip("Is Enabled");
            ImGui.SameLine();

            ImGui.SetNextItemWidth(ImGui.GetWindowWidth() - 32);
            ImGui.InputText("##GOName", ref go.Name, 0x100);


            ImGui.SetCursorPosY(85);

            //float widthToWorkWith = ImGui.GetWindowWidth() - 24f;
            //ImGui.SetNextItemWidth((widthToWorkWith / 2) - (13));
            //ImGui.Combo("##Tag", ref go.tagIndex, TagLayerManager.tags.ToArray(), TagLayerManager.tags.Count);
            //GUIHelper.Tooltip("Tag");
            //ImGui.SameLine();
            //ImGui.SetNextItemWidth((widthToWorkWith / 2) - (14));
            //ImGui.Combo("##Layer", ref go.layerIndex, TagLayerManager.layers.ToArray(), TagLayerManager.layers.Count);
            //GUIHelper.Tooltip("Layer");
            //ImGui.SameLine();
            //bool isStatic = false;
            //ImGui.BeginDisabled();
            //ImGui.Button(isStatic ? FontAwesome6.Lock : FontAwesome6.Unlock);
            //GUIHelper.Tooltip("Is Static");
            //ImGui.EndDisabled();
            //
            //ImGui.Separator();

            if (go.IsPrefab)
            {
                // Show buttons to Ping Prefab Asset, Revert Prefab, and Apply Prefab
                ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.2f, 0.2f, 1f));

                if (ImGui.Button("Select", new System.Numerics.Vector2((ImGui.GetWindowWidth() / 3) - 3, 25f)))
                    AssetDatabase.Ping(go.AssetID);
                ImGui.SameLine();
                if (ImGui.Button("Revert", new System.Numerics.Vector2((ImGui.GetWindowWidth() / 3) - 3, 25f)))
                {
#warning TODO: Implement Revert Prefab
                }
                ImGui.SameLine();
                if (ImGui.Button("Apply", new System.Numerics.Vector2((ImGui.GetWindowWidth() / 3) - 3, 25f)))
                {
#warning TODO: Implement Apply Prefab
                }

                ImGui.PopStyleColor();
                ImGui.Separator();
            }

            if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.OpenOnArrow))
            {
                DragnDrop.Drag(go.Transform, typeof(Transform));
                ImGui.Indent();
                PropertyDrawer.Draw(go.Transform, typeof(Transform).GetProperty("localPosition")!, -1, "Position");
                PropertyDrawer.Draw(go.Transform, typeof(Transform).GetProperty("localEulerAngles")!, -1, "Rotation");
                PropertyDrawer.Draw(go.Transform, typeof(Transform).GetProperty("localScale")!, -1, "Scale");
                ImGui.Unindent();
            }

            // Draw Components
            HashSet<int> editorsNeeded = [];
            List<MonoBehaviour> toDelete = [];
            foreach (var comp in go.GetComponents<MonoBehaviour>()) {
                editorsNeeded.Add(comp.InstanceID);

                if (comp.hideFlags.HasFlag(HideFlags.Hide) || comp.hideFlags.HasFlag(HideFlags.HideAndDontSave))
                    continue;

                bool isCompEditable = !comp.hideFlags.HasFlag(HideFlags.NotEditable);
                if (!isCompEditable) ImGui.BeginDisabled();
                ImGui.PushID(comp.InstanceID);

                isEnabled = comp.Enabled;
                if (ImGui.Checkbox("##Enabled", ref isEnabled))
                    comp.Enabled = isEnabled;
                GUIHelper.Tooltip("Is Enabled");
                ImGui.SameLine();
                var cType = comp.GetType();
                if (ImGui.CollapsingHeader(GetComponentDisplayName(cType), ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.OpenOnArrow)) {

                    DragnDrop.Drag(comp, cType);
                    HandleComponentContextMenu(go, comp, ref toDelete);

                    ImGui.Indent();
                    if (compEditors.TryGetValue(comp.InstanceID, out var editor)) {
                        editor.OnInspectorGUI();
                        goto EndComponent;
                    } else {
                        var editorType = CustomEditorAttribute.GetEditor(cType);
                        if (editorType != null) {
                            editor = Activator.CreateInstance(editorType) as ScriptedEditor;
                            if (editor != null) {
                                compEditors[comp.InstanceID] = editor;
                                editor.target = comp;
                                editor.OnEnable();
                                editor.OnInspectorGUI();
                                goto EndComponent;
                            }
                        }
                    }

                    foreach (var field in RuntimeUtils.GetSerializableFields(comp))
                        if (PropertyDrawer.Draw(comp, field))
                            comp.OnValidate();
                    ImGui.Unindent();

                    // Draw any Buttons
                    EditorGui.HandleAttributeButtons(comp);
                }
                else
                {
                    DragnDrop.Drag(comp, cType);
                    HandleComponentContextMenu(go, comp, ref toDelete);
                }

                EndComponent:;

                ImGui.PopID();
                if (!isCompEditable) ImGui.EndDisabled();

                GUIHelper.Space();
            }

            // Handle Deletion
            foreach (var comp in toDelete)
                go.RemoveComponent(comp);

            // Remove any editors that are no longer needed
            HandleUnusedEditors(editorsNeeded);

            GUIHelper.Space(4);

            HandleAddComponentButton(go);

            if (go.hideFlags.HasFlag(HideFlags.NotEditable)) ImGui.EndDisabled();
            ImGui.PopID();
        }

        private static string GetComponentDisplayName(Type cType)
        {
            var addToMenuAttribute = cType.GetCustomAttribute<AddComponentMenuAttribute>();
            return addToMenuAttribute != null ? Path.GetFileName(addToMenuAttribute.Path) : cType.Name;
        }

        private void HandleUnusedEditors(HashSet<int> editorsNeeded)
        {
            foreach (var key in compEditors.Keys)
                if (!editorsNeeded.Contains(key)) {
                    compEditors[key].OnDisable();
                    compEditors.Remove(key);
                }
        }

        #region Add Component Popup

        private void HandleAddComponentButton(GameObject? go)
        {
            if (ImGui.Button("Add Component", new System.Numerics.Vector2(ImGui.GetWindowWidth() - 15f, 25f)))
                ImGui.OpenPopup("AddComponentContextMenu");

            ImGui.PushStyleColor(ImGuiCol.PopupBg, new System.Numerics.Vector4(0.1f, 0.1f, 0.1f, 0.6f));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new System.Numerics.Vector2(6, 6));
            if (ImGui.BeginPopup("AddComponentContextMenu")) {
                GUIHelper.Search("##searchBox", ref _searchText, ImGui.GetContentRegionAvail().X);

                ImGui.Separator();

                rootMenuItem ??= GetAddComponentMenuItems();

                DrawMenuItems(rootMenuItem, go);

                ImGui.EndPopup();
            }
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();
        }

        private static void HandleComponentContextMenu(GameObject? go, MonoBehaviour comp, ref List<MonoBehaviour> toDelete)
        {
            if (ImGui.BeginPopupContextItem()) {
                if (ImGui.MenuItem("Duplicate")) {
                    var serialized = Serializer.Serialize(comp);
                    var copy = Serializer.Deserialize<MonoBehaviour>(serialized);
                    go.AddComponent(copy);
                    copy.OnValidate();
                }
                if (ImGui.MenuItem("Delete")) toDelete.Add(comp);
                ImGui.EndPopup();
            }
        }

        private void DrawMenuItems(MenuItemInfo menuItem, GameObject go)
        {
            bool foundName = false;
            bool hasSearch = string.IsNullOrEmpty(_searchText) == false;
            foreach (var item in menuItem.Children) {
                if (hasSearch && (item.Name.Contains(_searchText, StringComparison.CurrentCultureIgnoreCase) == false || item.Type == null)) {
                    DrawMenuItems(item, go);
                    if (hasSearch && item.Name.Equals(_searchText, StringComparison.CurrentCultureIgnoreCase))
                        foundName = true;
                    continue;
                }

                if (item.Type != null) {
                    if (ImGui.MenuItem(item.Name))
                    {
                        go.AddComponent(item.Type).OnValidate();
                    }
                } else {
                    if (ImGui.BeginMenu(item.Name, true)) {
                        DrawMenuItems(item, go);
                        ImGui.EndMenu();
                    }
                }
            }

            if (PlayMode.Current != PlayMode.Mode.Editing) return; // Cannot create scripts during playmode
             
            // is first and found no component and were searching, lets create a new script
            if (hasSearch && !foundName && menuItem == rootMenuItem)
            {
                if (ImGui.MenuItem("Create Script " + _searchText))
                {
                    FileInfo file = new FileInfo(Project.ProjectAssetDirectory + $"/{_searchText}.cs");
                    if (File.Exists(file.FullName))
                        return;
                    using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Prowl.Editor.EmbeddedResources.NewScript.txt");
                    using StreamReader reader = new StreamReader(stream);
                    string script = reader.ReadToEnd();
                    script = script.Replace("%SCRIPTNAME%", EditorUtils.FilterAlpha(_searchText));
                    File.WriteAllText(file.FullName, script);
                    AssetDatabase.Ping(file);
                    // Trigger an update so the script get imported which will recompile all scripts
                    AssetDatabase.Update();

                    Type? type = Type.GetType($"{EditorUtils.FilterAlpha(_searchText)}, CSharp, Version=1.0.0.0, Culture=neutral");
                    if(type != null && type.IsAssignableTo(typeof(MonoBehaviour)))
                        go.AddComponent(type).OnValidate();
                    ImGui.EndMenu();
                }
            }
        }

        private MenuItemInfo GetAddComponentMenuItems()
        {
            var componentTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsSubclassOf(typeof(MonoBehaviour)) && !type.IsAbstract)
                .ToArray();

            var items = componentTypes.Select(type => {
                string Name = type.Name;
                var addToMenuAttribute = type.GetCustomAttribute<AddComponentMenuAttribute>();
                if (addToMenuAttribute != null)
                    Name = addToMenuAttribute.Path;
                return (Name, type);
            }).ToArray();


            // Create a root MenuItemInfo object to serve as the starting point of the tree
            MenuItemInfo root = new MenuItemInfo { Name = "Root" };

            foreach (var (path, type) in items) {
                string[] parts = path.Split('/');

                // If first part is 'Hidden' then skip this component
                if (parts[0] == "Hidden") continue;

                MenuItemInfo currentNode = root;

                for (int i = 0; i < parts.Length - 1; i++)  // Skip the last part
                {
                    string part = parts[i];
                    MenuItemInfo childNode = currentNode.Children.Find(c => c.Name == part);

                    if (childNode == null) {
                        childNode = new MenuItemInfo { Name = part };
                        currentNode.Children.Add(childNode);
                    }

                    currentNode = childNode;
                }

                MenuItemInfo leafNode = new MenuItemInfo {
                    Name = parts[^1],  // Get the last part
                    Type = type
                };

                currentNode.Children.Add(leafNode);
            }

            SortChildren(root);
            return root;
        }

        private void SortChildren(MenuItemInfo node)
        {
            node.Children.Sort((x, y) => x.Type == null ? -1 : 1);

            foreach (var child in node.Children)
                SortChildren(child);
        }

        private class MenuItemInfo
        {
            public string Name;
            public Type Type;
            public List<MenuItemInfo> Children = new();

            public MenuItemInfo() { }

            public MenuItemInfo(Type type)
            {
                Type = type;
                Name = type.Name;
                var addToMenuAttribute = type.GetCustomAttribute<AddComponentMenuAttribute>();
                if (addToMenuAttribute != null)
                    Name = addToMenuAttribute.Path;
            }
        }

        #endregion


    }
}
