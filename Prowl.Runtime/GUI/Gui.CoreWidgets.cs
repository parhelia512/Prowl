﻿namespace Prowl.Runtime.GUI
{
    public partial class Gui
    {
        public void ScrollV()
        {
            const int width = 6;
            const int padding = 2;

            var n = CurrentNode;
            CurrentNode.VScroll = GetStorage<double>("VScroll");

            using (Node().Width(width).Height(Size.Percentage(1f, -(padding * 2))).Left(Offset.Percentage(1f, -(width + padding))).Top(padding).IgnoreLayout().Enter())
            {
                Rect scrollRect = CurrentNode.LayoutData.Rect;
                if (n.HasLayoutData)
                {
                    if (n.LayoutData.ContentRect.height > n.LayoutData.Rect.height)
                    {
                        double overflowHeight = n.LayoutData.ContentRect.height - n.LayoutData.Rect.height;

                        double scrollRatio = n.LayoutData.Rect.height / n.LayoutData.ContentRect.height;
                        double scrollBarHeight = scrollRatio * scrollRect.height;

                        double scrollBarY = (n.VScroll / overflowHeight) * (scrollRect.height - scrollBarHeight);

                        Rect barRect = new(scrollRect.x, scrollRect.y + scrollBarY, scrollRect.width, scrollBarHeight);
                        if (IsPressed(barRect))
                        {
                            DrawRectFilled(barRect, Color.green, 20f);
                            {
                                n.VScroll += Input.MouseDelta.y * 2f;
                                layoutDirty = true;
                            }
                        }
                        else if (IsHovering(barRect)) DrawRectFilled(barRect, Color.blue, 20f);
                        else DrawRectFilled(barRect, Color.red, 20f);

                        if (IsHovering(n.LayoutData.Rect) && Input.MouseWheelDelta != 0)
                        {
                            n.VScroll -= Input.MouseWheelDelta * 10;
                            layoutDirty = true;
                        }

                        n.VScroll = Mathf.Clamp(n.VScroll, 0, overflowHeight);
                    }
                    else if(n.VScroll != 0)
                    {
                        n.VScroll = 0;
                        layoutDirty = true;
                    }
                }
            }

            SetStorage("VScroll", CurrentNode.VScroll);
        }

    }
}