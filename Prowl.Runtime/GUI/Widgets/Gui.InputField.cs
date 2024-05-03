﻿using Prowl.Runtime.GUI.Graphics;
using Prowl.Runtime.GUI.TextEdit;
using SharpFont.Fnt;
using Silk.NET.Input;
using System;

namespace Prowl.Runtime.GUI
{
    public partial class Gui
    {
        // Input fields based on Dear ImGui

        [Flags]
        public enum InputFieldFlags : uint
        {
            None = 0,

            NumbersOnly = 1 << 0,
            Multiline = 1 << 1,
            AllowTab = 1 << 2,
            NoSelection = 1 << 3,
            AutoSelectAll = 1 << 4,
            EnterReturnsTrue = 1 << 5,
            OnlyDisplay = 1 << 6,
            Readonly = 1 << 7,
            NoHorizontalScroll = 1 << 8,
        }

        private static StbTextEditState stb;

        public static bool InputField(Font font, double fontsize, ref string value, uint maxLength, InputFieldFlags flags, Offset x, Offset y, Size width, float roundness = 2f)
        {
            var g = Gui.ActiveGUI;
            bool multiline = ((flags & InputFieldFlags.Multiline) == InputFieldFlags.Multiline);
            using (g.Node().Left(x).Top(y).Width(width).Height((multiline ? fontsize * 8 : fontsize) + 2.5).Padding(5).Enter())
            {
                Interactable interact = g.GetInteractable(true, true);

                interact.UpdateContext();

                g.DrawRectFilled(g.CurrentNode.LayoutData.Rect, Color.red, roundness);
                g.DrawRect(g.CurrentNode.LayoutData.Rect, Color.yellow, roundness);

                interact.TakeFocus();

                g.PushClip(g.CurrentNode.LayoutData.InnerRect);
                var ValueChanged = false;
                if (g.FocusID == interact.ID || g.ActiveID == interact.ID)
                {
                    ValueChanged = OnProcess(font, fontsize, interact, ref value, maxLength, flags);
                }
                else
                {
                    OnProcess(font, fontsize, interact, ref value, maxLength, flags | InputFieldFlags.OnlyDisplay);
                }
                g.PopClip();

                if (multiline)
                {
                    Vector2 textSize = font.CalcTextSize(value, 0, g.CurrentNode.LayoutData.InnerRect.width);
                    // Dummy node to update ContentRect
                    g.Node().Width(textSize.x).Height(textSize.y).IgnoreLayout();
                    g.ScrollV();
                }

                return ValueChanged;
            }

        }

        static int ImStrbolW(string data, int bufMidLine, int bufBegin)
        {
            while (bufMidLine > bufBegin && data[bufMidLine - 1] != '\n')
            {
                bufMidLine--;
            }
            return bufMidLine;
        }

        internal static bool OnProcess(Font font, double fontsize, Interactable interact, ref string Text, uint MaxLength, InputFieldFlags Flags)
        {
            var g = Gui.ActiveGUI;
            var ID = interact.ID;
            var render_pos = new Vector2(g.CurrentNode.LayoutData.InnerRect.x, g.CurrentNode.LayoutData.InnerRect.y);

            if (stb == null || stb.ID != ID)
            {
                stb = new();
                stb.ID = ID;
                stb.SingleLine = !((Flags & InputFieldFlags.Multiline) == InputFieldFlags.Multiline);
                stb.font = font;
                stb.fontSize = (float)fontsize;
                stb.Text = Text;
                if ((Flags & InputFieldFlags.AutoSelectAll) == InputFieldFlags.AutoSelectAll)
                {
                    stb.SelectStart = 0;
                    stb.SelectEnd = Text.Length;
                }
            }


            HandleKeyEvent(MaxLength, Flags);
            HandleMouseEvent();

            //g.DrawText(font, Text, fontsize, render_pos, Color.black);

            // Render
            Rect clip_rect = g.CurrentNode.LayoutData.InnerRect;
            Vector2 text_size = new Vector2(0f, 0f);
            stb.cursorAnim += Time.deltaTimeF;
            bool is_multiline = !stb.SingleLine;
            Vector2 size = new Vector2(g.CurrentNode.LayoutData.InnerRect.width, g.CurrentNode.LayoutData.InnerRect.height);

            // We need to:
            // - Display the text (this can be more easily clipped)
            // - Handle scrolling, highlight selection, display cursor (those all requires some form of 1d.2d cursor position calculation)
            // - Measure text height (for scrollbar)
            // We are attempting to do most of that in **one main pass** to minimize the computation cost (non-negligible for large amount of text) + 2nd pass for selection rendering (we could merge them by an extra refactoring effort)
            int text_begin = 0;
            Vector2 cursor_offset = Vector2.zero, select_start_offset = Vector2.zero;

            {
                // Count lines + find lines numbers straddling 'cursor' and 'select_start' position.
                int[] searches_input_ptr = new int[2];
                searches_input_ptr[0] = text_begin + stb.CursorIndex;
                searches_input_ptr[1] = -1;
                int searches_remaining = 1;
                int[] searches_result_line_number = { -1, -999 };
                if (stb.SelectStart != stb.SelectEnd)
                {
                    searches_input_ptr[1] = text_begin + Mathf.Min(stb.SelectStart, stb.SelectEnd);
                    searches_result_line_number[1] = -1;
                    searches_remaining++;
                }

                // Iterate all lines to find our line numbers
                // In multi-line mode, we never exit the loop until all lines are counted, so add one extra to the searches_remaining counter.
                searches_remaining += is_multiline ? 1 : 0;
                int line_count = 0;
                for (int s = text_begin; s < stb.Text.Length && stb.Text[s] != 0; s++)
                    if (stb.Text[s] == '\n')
                    {
                        line_count++;
                        if (searches_result_line_number[0] == -1 && s >= searches_input_ptr[0]) { searches_result_line_number[0] = line_count; if (--searches_remaining <= 0) break; }
                        if (searches_result_line_number[1] == -1 && s >= searches_input_ptr[1]) { searches_result_line_number[1] = line_count; if (--searches_remaining <= 0) break; }
                    }
                line_count++;
                if (searches_result_line_number[0] == -1) searches_result_line_number[0] = line_count;
                if (searches_result_line_number[1] == -1) searches_result_line_number[1] = line_count;

                int? remaining = null;
                Vector2? out_offset = null;
                // Calculate 2d position by finding the beginning of the line and measuring distance
                cursor_offset.x = font.InputTextCalcTextSizeW(stb.Text, ImStrbolW(stb.Text, searches_input_ptr[0], text_begin), searches_input_ptr[0], ref remaining, ref out_offset).x;
                cursor_offset.y = searches_result_line_number[0] * fontsize;
                if (searches_result_line_number[1] >= 0)
                {
                    select_start_offset.x = font.InputTextCalcTextSizeW(stb.Text, ImStrbolW(stb.Text, searches_input_ptr[1], text_begin), searches_input_ptr[1], ref remaining, ref out_offset).x;
                    select_start_offset.y = searches_result_line_number[1] * fontsize;
                }

                // Calculate text height
                if (is_multiline)
                    text_size = new Vector2(size.x, line_count * fontsize);
            }

            // Scroll
            if (stb.CursorFollow)
            {
                // Horizontal scroll in chunks of quarter width
                if ((Flags & InputFieldFlags.NoHorizontalScroll) == 0)
                {
                    double scroll_increment_x = size.x * 0.25f;
                    if (cursor_offset.x < stb.ScrollX)
                        stb.ScrollX = (int)Mathf.Max(0.0f, cursor_offset.x - scroll_increment_x);
                    else if (cursor_offset.x - size.x >= stb.ScrollX)
                        stb.ScrollX = (int)(cursor_offset.x - size.x + scroll_increment_x);
                }
                else
                {
                    stb.ScrollX = 0.0f;
                }

                // Vertical scroll
                if (is_multiline)
                {
                    double scroll_y = g.CurrentNode.VScroll;
                    if (cursor_offset.y - fontsize < scroll_y)
                        scroll_y = Mathf.Max(0.0f, cursor_offset.y - fontsize);
                    else if (cursor_offset.y - size.y >= scroll_y)
                        scroll_y = cursor_offset.y - size.y;
                    g.SetStorage("VScroll", scroll_y);
                }
            }
            stb.CursorFollow = false;
            if (is_multiline)
                render_pos.y -= g.CurrentNode.VScroll;
            Vector2 render_scroll = new Vector2(stb.ScrollX, 0.0f);

            if ((Flags & InputFieldFlags.OnlyDisplay) == InputFieldFlags.OnlyDisplay)
            {
                uint colb = UIDrawList.ColorConvertFloat4ToU32(Color.black);
                g.DrawList.AddText(font, (float)fontsize, render_pos - render_scroll, colb, stb.Text, 0, stb.Text.Length, 0.0f, (is_multiline ? null : (Vector4?)clip_rect));
                return false;
            }

            // Draw selection
            if (stb.SelectStart != stb.SelectEnd)
            {
                int text_selected_begin = text_begin + Mathf.Min(stb.SelectStart, stb.SelectEnd);
                int text_selected_end = text_begin + Mathf.Max(stb.SelectStart, stb.SelectEnd);

                float bg_offy_up = is_multiline ? 0.0f : -1.0f;    // FIXME: those offsets should be part of the style? they don't play so well with multi-line selection.
                float bg_offy_dn = is_multiline ? 0.0f : 2.0f;
                uint bg_color = UIDrawList.ColorConvertFloat4ToU32(Color.blue);
                Vector2 rect_pos = render_pos + select_start_offset - render_scroll;
                for (int p = text_selected_begin; p < text_selected_end;)
                {
                    if (rect_pos.y > clip_rect.y + clip_rect.height + fontsize)
                        break;
                    if (rect_pos.y < clip_rect.y)
                    {
                        while (p < text_selected_end)
                            if (Text[p++] == '\n') //TODO: what should we access here?
                                break;
                    }
                    else
                    {
                        var temp = (int?)p;
                        Vector2? out_offset = null;
                        Vector2 rect_size = font.InputTextCalcTextSizeW(Text, p, text_selected_end, ref temp, ref out_offset, true); p = temp.Value;
                        if (rect_size.x <= 0.0f) rect_size.x = (int)(font.GetCharAdvance(' ') * 0.50f); // So we can see selected empty lines
                        Rect rect = new Rect(rect_pos + new Vector2(0.0f, bg_offy_up - fontsize), new Vector2(rect_size.x, bg_offy_dn + fontsize));
                        rect.Clip(clip_rect);
                        if (rect.Overlaps(clip_rect))
                            g.DrawList.AddRectFilled(rect.Min, rect.Max, bg_color);
                    }
                    rect_pos.x = render_pos.x - render_scroll.x;
                    rect_pos.y += fontsize;
                }
            }


            uint col = UIDrawList.ColorConvertFloat4ToU32(Color.black);
            g.DrawList.AddText(font, (float)fontsize, render_pos - render_scroll, col, stb.Text, 0, stb.Text.Length, 0.0f, (is_multiline ? null : (Vector4?)clip_rect));
            //g.DrawText(font, fontsize, Text, render_pos - render_scroll, Color.black, 0, stb.CurLenA, 0.0f, (is_multiline ? null : (ImVec4?)clip_rect));

            // Draw blinking cursor
            Vector2 cursor_screen_pos = render_pos + cursor_offset - render_scroll;
            bool cursor_is_visible = (stb.cursorAnim <= 0.0f) || (stb.cursorAnim % 1.20f) <= 0.80f;
            if (cursor_is_visible)
                g.DrawList.AddLine(cursor_screen_pos + new Vector2(0.0f, -fontsize - 4f), cursor_screen_pos + new Vector2(0.0f, -5f), col);


            if ((Flags & InputFieldFlags.EnterReturnsTrue) == InputFieldFlags.EnterReturnsTrue)
            {
                Text = stb.Text;
                if (g.IsKeyPressed(Silk.NET.Input.Key.Enter))
                {
                    g.FocusID = 0;
                    return true;
                }
                return false;
            }
            else
            {
                if (g.IsKeyPressed(Silk.NET.Input.Key.Enter))
                    g.FocusID = 0;

                var oldText = Text;
                Text = stb.Text;
                return oldText != Text;
            }
        }

        private static void HandleKeyEvent(uint MaxLength, InputFieldFlags Flags)
        {
            var g = Gui.ActiveGUI;
            var KeyCode = g.KeyCode;
            if (KeyCode == Key.Unknown)
            {
                return;
            }

            if (!g.IsKeyPressed(KeyCode))
            {
                return;
            }

            StbTextEdit.ControlKeys? stb_key = null;
            var Ctrl = g.IsKeyDown(Key.ControlLeft);
            var Shift = g.IsKeyDown(Key.ShiftLeft);
            var Alt = g.IsKeyDown(Key.AltLeft);
            bool NoSelection = (Flags & InputFieldFlags.NoSelection) == InputFieldFlags.NoSelection;
            bool IsEditable = !((Flags & InputFieldFlags.Readonly) == InputFieldFlags.Readonly);
            bool Multiline = (Flags & InputFieldFlags.Multiline) == InputFieldFlags.Multiline;

            switch (KeyCode)
            {
                case Key.Tab:
                    if ((Flags & InputFieldFlags.AllowTab) == InputFieldFlags.AllowTab)
                    {
                        OnTextInput("\t", MaxLength, Flags);
                    }
                   //else Focus Next Focusable Interactable
                    break;
                case Key.A when Ctrl && !NoSelection:
                    stb.SelectStart = 0;
                    stb.SelectEnd = stb.Text.Length;
                    break;
                case Key.Escape:
                    stb.SelectStart = 0;
                    stb.SelectEnd = 0;
                    break;

                case Key.Insert when IsEditable:
                    stb_key = StbTextEdit.ControlKeys.InsertMode;
                    break;
                case Key.C when Ctrl && !NoSelection:
                    int selectStart = Math.Min(stb.SelectStart, stb.SelectEnd);
                    int selectEnd = Math.Max(stb.SelectStart, stb.SelectEnd);
                
                    if (selectStart < selectEnd)
                    {
                        Input.Clipboard = stb.Text.Substring(selectStart, selectEnd - selectStart);
                    }
                
                    break;
                case Key.X when Ctrl && !NoSelection:
                    selectStart = Math.Min(stb.SelectStart, stb.SelectEnd);
                    selectEnd = Math.Max(stb.SelectStart, stb.SelectEnd);
                
                    if (selectStart < selectEnd)
                    {
                        Input.Clipboard = stb.Text.Substring(selectStart, selectEnd - selectStart);
                        if (IsEditable)
                            StbTextEdit.Cut(stb);
                    }
                
                    break;
                case Key.V when Ctrl && IsEditable:
                    OnTextInput(Input.Clipboard, MaxLength, Flags);
                    break;
                case Key.Z when Ctrl && IsEditable:
                    stb_key = StbTextEdit.ControlKeys.Undo;
                    break;
                case Key.Y when Ctrl && IsEditable:
                    stb_key = StbTextEdit.ControlKeys.Redo;
                    break;
                case Key.Left:
                    if (Ctrl && Shift)
                    {
                        if (!NoSelection)
                            stb_key = StbTextEdit.ControlKeys.Shift | StbTextEdit.ControlKeys.WordLeft;
                    }
                    else if (Shift)
                    {
                        if (!NoSelection)
                            stb_key = StbTextEdit.ControlKeys.Shift | StbTextEdit.ControlKeys.Left;
                    }
                    else if (Ctrl)
                        stb_key = StbTextEdit.ControlKeys.WordLeft;
                    else
                        stb_key = StbTextEdit.ControlKeys.Left;
                    stb.CursorFollow = true;
                    break;
                case Key.Right:
                    if (Ctrl && Shift)
                    {
                        if (!NoSelection)
                            stb_key = StbTextEdit.ControlKeys.Shift | StbTextEdit.ControlKeys.WordRight;
                    }
                    else if (Shift)
                    {
                        if (!NoSelection)
                            stb_key = StbTextEdit.ControlKeys.Shift | StbTextEdit.ControlKeys.Right;
                    }
                    else if (Ctrl)
                        stb_key = StbTextEdit.ControlKeys.WordRight;
                    else
                        stb_key = StbTextEdit.ControlKeys.Right;
                    break;
                case Key.Up:
                    stb_key = StbTextEdit.ControlKeys.Up;
                    if (Shift && !NoSelection) stb_key |= StbTextEdit.ControlKeys.Shift;
                    break;
                case Key.Down:
                    stb_key = StbTextEdit.ControlKeys.Down;
                    if (Shift && !NoSelection) stb_key |= StbTextEdit.ControlKeys.Shift;
                    break;
                case Key.Backspace when IsEditable:
                    stb_key = StbTextEdit.ControlKeys.BackSpace;
                    if (Shift && !NoSelection) stb_key |= StbTextEdit.ControlKeys.Shift;
                    break;
                case Key.Delete when IsEditable:
                    stb_key = StbTextEdit.ControlKeys.Delete;
                    if (Shift && !NoSelection) stb_key |= StbTextEdit.ControlKeys.Shift;
                    break;
                case Key.Home:
                    if (Ctrl && Shift)
                    {
                        if (!NoSelection)
                            stb_key = StbTextEdit.ControlKeys.Shift | StbTextEdit.ControlKeys.TextStart;
                    }
                    else if (Shift)
                    {
                        if (!NoSelection)
                            stb_key = StbTextEdit.ControlKeys.Shift | StbTextEdit.ControlKeys.LineStart;
                    }
                    else if (Ctrl)
                        stb_key = StbTextEdit.ControlKeys.TextStart;
                    else
                        stb_key = StbTextEdit.ControlKeys.LineStart;
                    break;
                case Key.End:
                    if (Ctrl && Shift)
                    {
                        if (!NoSelection)
                            stb_key = StbTextEdit.ControlKeys.Shift | StbTextEdit.ControlKeys.TextEnd;
                    }
                    else if (Shift)
                    {
                        if (!NoSelection)
                            stb_key = StbTextEdit.ControlKeys.Shift | StbTextEdit.ControlKeys.LineEnd;
                    }
                    else if (Ctrl)
                        stb_key = StbTextEdit.ControlKeys.TextEnd;
                    else
                        stb_key = StbTextEdit.ControlKeys.LineEnd;
                    break;
                case Key.KeypadEnter when IsEditable && Multiline:
                case Key.Enter when IsEditable && Multiline:
                    OnTextInput("\n", MaxLength, Flags);
                    break;
            }

            if (stb_key != null)
            {
                stb.CursorFollow = true;
                StbTextEdit.Key(stb, stb_key.Value);
            }

            if(Input.LastPressedChar != null)
            {
                OnTextInput(Input.LastPressedChar.ToString(), MaxLength, Flags);
                // Consume the key
                // TODO: We should have a proper API to recieve Input Characters rather then consuming the only source of input so nothing else can see it
                Input.LastPressedChar = null;
            }
        }

        protected static bool OnTextInput(string c, uint MaxLength, InputFieldFlags Flags)
        {
            bool IsEditable = !((Flags & InputFieldFlags.Readonly) == InputFieldFlags.Readonly);
            if (c == null || !IsEditable)
                return false;

            if (stb.SelectStart != stb.SelectEnd)
            {
                StbTextEdit.DeleteSelection(stb);
            }

            int count;

            if (MaxLength >= 0)
            {
                var remains = MaxLength - stb.Length;
                if (remains <= 0)
                    return false;

                count = (int)Math.Min(remains, c.Length);
            }
            else
            {
                count = c.Length;
            }

            bool NumbersOnly = (Flags & InputFieldFlags.NumbersOnly) == InputFieldFlags.NumbersOnly;
            for (int i = 0; i < count; i++)
            {
                if ((NumbersOnly && !char.IsNumber(c[i])) || c[i] == '\r')
                    continue;

                StbTextEdit.InputChar(stb, c[i]);
                stb.CursorFollow = true;
            }

            return true;
        }

        private static void HandleMouseEvent()
        {
            var g = Gui.ActiveGUI;
            var Pos = g.PointerPos - g.CurrentNode.LayoutData.InnerRect.Position;
            Pos.x += stb.ScrollX;
            Pos.y += g.CurrentNode.VScroll;
            if (g.IsPointerClick(Silk.NET.Input.MouseButton.Left))
            {
                StbTextEdit.Click(stb, (float)Pos.x, (float)Pos.y);
                stb.cursorAnim = 0f;
            }
            if (g.IsPointerDown(Silk.NET.Input.MouseButton.Left) && g.IsPointerMoving)
            {
                StbTextEdit.Drag(stb, (float)Pos.x, (float)Pos.y);
                stb.cursorAnim = 0f;
                stb.CursorFollow = true;
            }
        }


    }
}