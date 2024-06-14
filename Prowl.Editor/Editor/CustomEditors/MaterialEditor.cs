﻿using Hexa.NET.ImGui;
using Prowl.Editor.Assets;
using Prowl.Editor.ImGUI.Widgets;
using Prowl.Editor.PropertyDrawers;
using Prowl.Icons;
using Prowl.Runtime;
using Prowl.Runtime.Rendering.OpenGL;

namespace Prowl.Editor.EditorWindows.CustomEditors
{
    [CustomEditor(typeof(Material))]
    public class MaterialEditor : ScriptedEditor
    {
        private Action onChange;
        public static Guid assignedGUID;
        public static ushort assignedFileID;
        public static int guidAssignedToID = -1;

        public MaterialEditor() { }

        public MaterialEditor(Material mat, Action onChange)
        {
            base.target = mat;
            this.onChange = onChange;
        }

        public override void OnInspectorGUI()
        {
            var mat = (Material)target;
            mat ??= new Material();

            PropertyDrawer.Draw(mat, typeof(Material).GetField("Shader")!);
            bool changed = false;
            if (mat.Shader.IsAvailable)
            {
                int id = 0;
                foreach (var property in mat.Shader.Res.Properties)
                {
                    ImGui.PushID(id++);
                    switch (property.Type)
                    {
                        case Shader.Property.PropertyType.FLOAT:
                            float f = mat.PropertyBlock.GetFloat(property.Name);
                            changed |= ImGui.DragFloat(property.DisplayName, ref f, 0.01f);
                            if (changed) mat.PropertyBlock.SetFloat(property.Name, f);
                            break;
                        case Shader.Property.PropertyType.INTEGER:
                            int i = mat.PropertyBlock.GetInt(property.Name);
                            changed |= ImGui.DragInt(property.DisplayName, ref i, 1);
                            if (changed) mat.PropertyBlock.SetInt(property.Name, i);
                            break;
                        case Shader.Property.PropertyType.VEC2:
                            var v2 = mat.PropertyBlock.GetVector2(property.Name).ToFloat();
                            changed |= ImGui.DragFloat2(property.DisplayName, ref v2, 0.01f);
                            if (changed) mat.PropertyBlock.SetVector(property.Name, v2);
                            break;
                        case Shader.Property.PropertyType.VEC3:
                            var v3 = mat.PropertyBlock.GetVector3(property.Name).ToFloat();
                            changed |= ImGui.DragFloat3(property.DisplayName, ref v3, 0.01f);
                            if (changed) mat.PropertyBlock.SetVector(property.Name, v3);
                            break;
                        case Shader.Property.PropertyType.VEC4:
                            var v4 = mat.PropertyBlock.GetVector4(property.Name).ToFloat();
                            changed |= ImGui.DragFloat4(property.DisplayName, ref v4, 0.01f);
                            if (changed) mat.PropertyBlock.SetVector(property.Name, v4);
                            break;
                        case Shader.Property.PropertyType.COLOR:
                            var c = mat.PropertyBlock.GetVector4(property.Name).ToFloat();
                            changed |= ImGui.ColorEdit4(property.DisplayName, ref c);
                            if (changed) mat.PropertyBlock.SetVector(property.Name, c);
                            break;

                        case Shader.Property.PropertyType.TEXTURE2D:
                            var texNullable = mat.PropertyBlock.GetTexture(property.Name);
                            if (texNullable == null) break;
                            var tex = texNullable.Value;

                            ImDrawListPtr drawList = ImGui.GetWindowDrawList();

                            ImGui.Columns(2);
                            ImGui.Text(property.DisplayName);
                            ImGui.SetColumnWidth(0, 70);
                            ImGui.NextColumn();

                            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
                            ImGui.PushID(property.Name);

                            var imguiID = ImGui.GetItemID();

                            if (guidAssignedToID != -1 && guidAssignedToID == imguiID)
                            {
                                tex.AssetID = assignedGUID;
                                tex.FileID = assignedFileID;
                                assignedGUID = Guid.Empty;
                                assignedFileID = 0;
                                guidAssignedToID = -1;
                                changed = true;
                            }


                            var pos = ImGui.GetCursorPos();
                            if (tex.IsExplicitNull)
                            {
                                if (ImGui.Selectable("(Null)", false, new System.Numerics.Vector2(50, 50)))
                                {
                                    AssetDatabase.Ping(tex.AssetID);
                                }
                                drawList.AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetColorU32(new System.Numerics.Vector4(0.9f, 0.1f, 0.1f, 0.3f)));
                            }
                            else if (tex.IsRuntimeResource)
                            {
                                if (ImGui.Selectable("(Runtime)" + tex.Name, false, new System.Numerics.Vector2(50, 50)))
                                    AssetDatabase.Ping(tex.AssetID);
                                drawList.AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetColorU32(new System.Numerics.Vector4(0.1f, 0.1f, 0.9f, 0.3f)));
                            }
                            else
                            {
                                if (AssetDatabase.TryGetFile(tex.AssetID, out var fileInfo))
                                {
                                    var thumbnail = Application.AssetProvider.LoadAsset<Texture2D>(tex);
                                    if (thumbnail.IsAvailable)
                                    {
                                        var cPos = ImGui.GetCursorScreenPos();
                                        ImGui.SetCursorScreenPos(new System.Numerics.Vector2(cPos.X, cPos.Y + 50));
                                        ImGui.Image(new ImTextureID((nint)(thumbnail.Res!.Handle as GLTexture)!.Handle), new System.Numerics.Vector2(50, -50));
                                        ImGui.SetCursorScreenPos(cPos);
                                    }
                                }

                                if (ImGui.Selectable(AssetDatabase.ToRelativePath(fileInfo), false, new System.Numerics.Vector2(50, 50)))
                                    AssetDatabase.Ping(tex.AssetID);
                            }

                            if (ImGui.IsItemClicked(ImGuiMouseButton.Left) && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                                new AssetSelectorWindow(typeof(Texture2D), (guid, fileid) => { assignedGUID = guid; guidAssignedToID = imguiID; assignedFileID = fileid; });

                            // Drag and drop support
                            if (DragnDrop.Drop(out var instance, typeof(Texture2D)))
                            {
                                // SetInstance() will also set the AssetID if the instance is an asset
                                tex.SetInstance(instance);
                                changed = true;
                            }

                            ImGui.PopID();
                            ImGui.PopItemWidth();
                            ImGui.Columns(1);

                            mat.PropertyBlock.SetTexture(property.Name, tex);

                            break;

                    }
                    ImGui.PopID();
                }
            }

            if (changed)
            {
                onChange?.Invoke();
            }

        }

    }
}
