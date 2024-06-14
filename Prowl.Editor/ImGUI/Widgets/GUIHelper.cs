﻿using Hexa.NET.ImGui;
using Prowl.Icons;
using Prowl.Runtime;
using System.Runtime.InteropServices;

namespace Prowl.Editor
{
    public class GUIHelper
    {
        public static void Space(int amount = 1) => ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (5 * amount));

        /// <summary>
        /// Creates a tooltip for the hovered item drawn before this is called.
        /// </summary>
        public static void Tooltip(string tooltip, string shortcut = "")
        {
            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.BeginTooltip();
                ImGui.Text(tooltip);
                ImGui.EndTooltip();
            }
        }

        public static void EnumComboBox<T>(string label, ref T value, bool showLabel = true) where T : Enum
        {
            if (showLabel) ImGui.Text(label);
            ImGui.SameLine();
            if (ImGui.BeginCombo($"##{label}", value.ToString(), ImGuiComboFlags.NoArrowButton))
            {
                foreach (var v in Enum.GetValues(typeof(T)))
                {
                    if (ImGui.Selectable(v.ToString(), v.Equals(value)))
                        value = (T)v;
                }
                ImGui.EndCombo();
            }
        }

        public static void EnumComboBox<T>(string label, string preview, ref T value, bool showLabel = true) where T : Enum
        {
            if (showLabel) ImGui.Text(label);
            if (ImGui.BeginCombo($"##{label}", preview, ImGuiComboFlags.NoArrowButton))
            {
                foreach (var v in Enum.GetValues(typeof(T)))
                {
                    if (ImGui.Selectable(v.ToString(), v.Equals(value)))
                        value = (T)v;
                }
                ImGui.EndCombo();
            }
        }

        public static void TextCenter(string text, float size = 1f, bool vertically = false)
        {
            float oldX = ImGui.GetCursorPosX();
            float oldY = ImGui.GetCursorPosY();
            ImGui.SetWindowFontScale(size);
            var ts = ImGui.CalcTextSize(text);
            ImGui.SetCursorPosX(Math.Max(0f, (ImGui.GetWindowWidth() - ts.X) * 0.5f));
            if (vertically)
                ImGui.SetCursorPosY((ImGui.GetWindowHeight() - ts.Y) * 0.5f);
            ImGui.TextWrapped(text);
            ImGui.SetWindowFontScale(1.0f);
            ImGui.SetCursorPosX(oldX);
            if (vertically)
                ImGui.SetCursorPosY(oldY);
        }

        public static bool MenuItemTooltip(string name, string tooltip, string shortcut = "")
        {
            bool item = ImGui.MenuItem(name, shortcut);

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text(tooltip);
                if (!string.IsNullOrEmpty(shortcut))
                {
                    ImGui.Text($"Shortcut: {shortcut}");
                }
                ImGui.EndTooltip();
            }
            return item;
        }

        public static bool ComboScrollable<T>(string key, string text, ref T selectedItem, Action propertyChanged = null, ImGuiComboFlags flags = ImGuiComboFlags.None)
        {
            return ComboScrollable(key, text, ref selectedItem, Enum.GetValues(typeof(T)).Cast<T>(), propertyChanged, flags);
        }

        public static bool ComboScrollable<T>(string key, string text, ref T selectedItem, IEnumerable<T> items, Action propertyChanged = null, ImGuiComboFlags flags = ImGuiComboFlags.None)
        {
            if (ImGui.BeginCombo(key, text, flags)) //Check for combo box popup and add items
            {
                foreach (T item in items)
                {
                    bool isSelected = item.Equals(selectedItem);
                    if (ImGui.Selectable(item.ToString(), isSelected))
                    {
                        selectedItem = item;
                        propertyChanged?.Invoke();
                    }
                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();

                return true;
            }
            if (ImGui.IsItemHovered()) //Check for combo box hover
            {
                var delta = ImGui.GetIO().MouseWheel;
                if (delta < 0) //Check for mouse scroll change going up
                {
                    var list = items.ToList();
                    int index = list.IndexOf(selectedItem);
                    if (index < list.Count - 1)
                    { //Shift upwards if possible
                        selectedItem = list[index + 1];
                        propertyChanged?.Invoke();
                    }
                }
                if (delta > 0) //Check for mouse scroll change going down
                {
                    var list = items.ToList();
                    int index = list.IndexOf(selectedItem);
                    if (index > 0)
                    { //Shift downwards if possible
                        selectedItem = list[index - 1];
                        propertyChanged?.Invoke();

                        return true;
                    }
                }
            }
            return false;
        }

        public unsafe bool CustomTreeNode(string label)
        {
            var style = ImGui.GetStyle();
            var storage = ImGui.GetStateStorage();

            int id = ImGui.GetID(label);
            int opened = storage.GetInt(id, 0);
            float x = ImGui.GetCursorPosX();
            ImGui.BeginGroup();
            if (ImGui.InvisibleButton(label, new Vector2(-1, ImGui.GetFontSize() + style.FramePadding.Y * 2)))
            {
                opened = storage.GetInt(id, 0);
                //  opened = p_opened == p_opened;
            }
            bool hovered = ImGui.IsItemHovered();
            bool active = ImGui.IsItemActive();
            if (hovered || active)
            {
                var col = ImGui.GetStyle().Colors[(int)(active ? ImGuiCol.HeaderActive : ImGuiCol.HeaderHovered)];
                ImGui.GetWindowDrawList().AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.ColorConvertFloat4ToU32(col));
            }
            ImGui.SameLine();
            ImGui.ColorButton("color_btn", opened == 1 ? new Vector4(1, 1, 1, 1) : new Vector4(1, 0, 0, 1));
            ImGui.SameLine();
            ImGui.Text(label);
            ImGui.EndGroup();
            if (opened == 1)
                ImGui.TreePush(label);
            return opened != 0;
        }

        public static void ItemRectFilled(Vector4 color, float expand = 0.0f, float roundness = 0.0f) => ItemRectFilled((float)color.x, (float)color.y, (float)color.z, (float)color.w, expand, roundness);

        public static void ItemRectFilled(float r, float g, float b, float a, float expand = 0.0f, float roundness = 0.0f) => ItemRectFilled(ImGui.GetColorU32(new Vector4(r, g, b, a)), expand, roundness);

        public static void ItemRectFilled(uint color, float expand = 0.0f, float roundness = 0.0f)
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            if(expand > 0)
            {
                min.X -= expand;
                min.Y -= expand;
                max.X += expand;
                max.Y += expand;
            }
            ImGui.GetWindowDrawList().AddRectFilled(min, max, color, roundness);
        }

        public static void ItemRect(float r, float g, float b, float a, float expand = 0.0f, float roundness = 0.0f, float thickness = 1.0f)
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            if(expand > 0)
            {
                min.X -= expand;
                min.Y -= expand;
                max.X += expand;
                max.Y += expand;
            }
            ImGui.GetWindowDrawList().AddRect(min, max, ImGui.GetColorU32(new Vector4(r, g, b, a)), roundness, thickness);
        }

        public static bool Search(string v, ref string searchText, float x)
        {
            searchText ??= "";
            float cPX = ImGui.GetCursorPosX();
            ImGui.SetNextItemWidth(x);
            bool changed = ImGui.InputText(v, ref searchText, 0x100);
            bool isSearching = !string.IsNullOrEmpty(searchText);
            if (!isSearching)
            {
                ImGui.SameLine();
                ImGui.SetCursorPosX(cPX + ImGui.GetFontSize() * 0.5f);
                ImGui.TextDisabled(FontAwesome6.MagnifyingGlass + " Search...");
            }
            return changed;
        }

        public static bool DragByte(string v1, ref byte value, float v2)
        {
            unsafe
            {
                fixed (byte* v = &value)
                    return ImGui.DragScalar(v1, ImGuiDataType.U8, v, v2, "%u");
            }
        }

        public static bool DragSByte(string v1, ref sbyte value, float v2)
        {
            unsafe
            {
                fixed (sbyte* v = &value)
                    return ImGui.DragScalar(v1, ImGuiDataType.S8, v, v2, "%d");
            }
        }

        public static bool DragShort(string v1, ref short value, float v2)
        {
            unsafe
            {
                fixed (short* v = &value)
                    return ImGui.DragScalar(v1, ImGuiDataType.S16, v, v2, "%d");
            }
        }

        public static bool DragUShort(string v1, ref ushort value, float v2)
        {
            unsafe
            {
                fixed (ushort* v = &value)
                    return ImGui.DragScalar(v1, ImGuiDataType.U16, v, v2, "%u");
            }
        }

        public static bool DragInt(string v1, ref int value, float v2)
        {
            unsafe
            {
                fixed (int* v = &value)
                    return ImGui.DragScalar(v1, ImGuiDataType.S32, v, v2, "%d");
            }
        }

        public static bool DragUInt(string v1, ref uint value, float v2)
        {
            unsafe
            {
                fixed (uint* v = &value)
                    return ImGui.DragScalar(v1, ImGuiDataType.U32, v, v2, "%u");
            }
        }

        public static bool DragLong(string v1, ref long value, float v2)
        {
            unsafe
            {
                fixed (long* v = &value)
                    return ImGui.DragScalar(v1, ImGuiDataType.S64, v, v2, "%lld");
            }
        }

        public static bool DragULong(string v1, ref ulong value, float v2)
        {
            unsafe
            {
                fixed (ulong* v = &value)
                    return ImGui.DragScalar(v1, ImGuiDataType.U64, v, v2, "%lld");
            }
        }

        public static bool DragDouble(string v1, ref double value, float v2)
        {
            unsafe
            {
                fixed (double* v = &value)
                    return ImGui.DragScalar(v1, ImGuiDataType.Double, v, v2, "%g");
            }
        }

        public static bool DragFloat(string v1, ref float value, float v2)
        {
            unsafe
            {
                fixed (float* v = &value)
                    return ImGui.DragScalar(v1, ImGuiDataType.Float, v, v2, "%g");
            }
        }

        public static void ShadeVertsLinearColorGradient(ImDrawListPtr drawList, int vertStartIdx, int vertEndIdx, System.Numerics.Vector2 gradientP0, System.Numerics.Vector2 gradientP1, uint col0, uint col1)
        {
            var gradientExtent = gradientP1 - gradientP0;
            float gradientInvLength2 = 1.0f / ImLengthSqr(gradientExtent);

            unsafe
            {
                ImDrawVert* vertStart = (ImDrawVert*)drawList.VtxBuffer.Data + vertStartIdx;
                ImDrawVert* vertEnd = (ImDrawVert*)drawList.VtxBuffer.Data + vertEndIdx;

                int col0R = (int)(col0 >> IM_COL32_R_SHIFT) & 0xFF;
                int col0G = (int)(col0 >> IM_COL32_G_SHIFT) & 0xFF;
                int col0B = (int)(col0 >> IM_COL32_B_SHIFT) & 0xFF;
                int col0A = (int)(col0 >> IM_COL32_A_SHIFT) & 0xFF;

                int col1R = (int)(col1 >> IM_COL32_R_SHIFT) & 0xFF;
                int col1G = (int)(col1 >> IM_COL32_G_SHIFT) & 0xFF;
                int col1B = (int)(col1 >> IM_COL32_B_SHIFT) & 0xFF;
                int col1A = (int)(col1 >> IM_COL32_A_SHIFT) & 0xFF;

                int colDeltaR = col1R - col0R;
                int colDeltaG = col1G - col0G;
                int colDeltaB = col1B - col0B;
                int colDeltaA = col1A - col0A;

                for (ImDrawVert* vert = vertStart; vert < vertEnd; vert++)
                {
                    float d = ImDot(vert->pos - gradientP0, gradientExtent);
                    float t = ImClamp(d * gradientInvLength2, 0.0f, 1.0f);

                    int r = (int)(col0R + colDeltaR * t);
                    int g = (int)(col0G + colDeltaG * t);
                    int b = (int)(col0B + colDeltaB * t);
                    int a = (int)(col0A + colDeltaA * t);

                    vert->col = (uint)((r << IM_COL32_R_SHIFT) | (g << IM_COL32_G_SHIFT) | (b << IM_COL32_B_SHIFT) | (a << IM_COL32_A_SHIFT));
                }
            }
        }

        private static float ImLengthSqr(System.Numerics.Vector2 v)
        {
            return v.X * v.X + v.Y * v.Y;
        }

        private static float ImDot(System.Numerics.Vector2 a, System.Numerics.Vector2 b)
        {
            return a.X * b.X + a.Y * b.Y;
        }

        private static float ImClamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }

        private const int IM_COL32_R_SHIFT = 0;
        private const int IM_COL32_G_SHIFT = 8;
        private const int IM_COL32_B_SHIFT = 16;
        private const int IM_COL32_A_SHIFT = 24;

        [StructLayout(LayoutKind.Sequential)]
        public struct ImDrawVert
        {
            public System.Numerics.Vector2 pos;
            public System.Numerics.Vector2 uv;
            public uint col;
        }
    }
}
