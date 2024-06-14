﻿using Hexa.NET.ImGui;
using Hexa.NET.ImPlot;
using Prowl.Runtime;

namespace Prowl.Editor.PropertyDrawers;

public class PropertyDrawerAnimationCurve : PropertyDrawer<AnimationCurve> {

    private ImPlotPoint[] points;
    private ImPlotPoint[] evaluatedPoints = new ImPlotPoint[100];

#warning TODO: Tangents, AddKey, RemoveKey, etc.

    protected override bool Draw(string label, ref AnimationCurve c, float width)
    {
        bool changed = false;
        if (c == null)
        {
            c = new AnimationCurve();
            c.Keys.Add(new KeyFrame(0, 0));
            c.Keys.Add(new KeyFrame(1, 1));
            changed = true;
        }

        //DrawLabel(label, ref width);

        // Initialize the points array based on the keyframes in the AnimationCurve
        points = new ImPlotPoint[c.Keys.Count];
        for (int i = 0; i < c.Keys.Count; i++)
        {
            KeyFrame keyframe = c.Keys[i];
            points[i] = new();
            points[i].X = keyframe.Position;
            points[i].Y = keyframe.Value;
        }

        ImPlotAxisFlags axisFlags = ImPlotAxisFlags.None;

        if (ImPlot.BeginPlot("##AnimationCurve", new Vector2(-1, 150), ImPlotFlags.CanvasOnly))
        {
            ImPlot.SetupAxes("", "", axisFlags, axisFlags);
            ImPlot.SetupAxesLimits(0, 1, 0, 1);

            bool[] clicked = new bool[points.Length];
            bool[] hovered = new bool[points.Length];
            bool[] held = new bool[points.Length];

            for (int i = 0; i < points.Length; i++)
            {
                ImPlot.DragPoint(i, ref points[i].X, ref points[i].Y, new Vector4(0, 0.9f, 0, 1), 4, ImPlotDragToolFlags.None,
                    ref clicked[i], ref hovered[i], ref held[i]);
            }

            // Update the keyframes in the AnimationCurve based on the modified points
            List<Vector4> previousKeyframes = new List<Vector4>();
            for (int i = 0; i < c.Keys.Count; i++)
            {
                previousKeyframes.Add(new Vector4(c.Keys[i].Position, c.Keys[i].Value, c.Keys[i].TangentIn, c.Keys[i].TangentOut));
            }
            c.Keys.Clear();
            for (int i = 0; i < points.Length; i++)
            {
                KeyFrame keyframe = new KeyFrame(points[i].X, points[i].Y);
                if (i < previousKeyframes.Count)
                {
                    keyframe.TangentIn = previousKeyframes[i].z;
                    keyframe.TangentOut = previousKeyframes[i].w;
                }
                c.Keys.Add(keyframe);
            }

            // Evaluate the AnimationCurve at regular intervals to visualize the curve
            for (int i = 0; i < evaluatedPoints.Length; i++)
            {
                float t = i / (float)(evaluatedPoints.Length - 1);
                evaluatedPoints[i].X = t;
                evaluatedPoints[i].Y = c.Evaluate(t);
            }

            ImPlot.SetNextLineStyle(new Vector4(0, 0.9f, 0, 1), 2.0f);
            ImPlot.PlotLine("##curve", ref evaluatedPoints[0].X, ref evaluatedPoints[0].Y, evaluatedPoints.Length, ImPlotLineFlags.None, 0, sizeof(double) * 2);

            for (int i = 0; i < points.Length; i++)
                DrawTangents(i, c.Keys[i]);

            ImPlot.EndPlot();
        }

        return changed;
    }

    private void DrawTangents(int i, KeyFrame keyframe)
    {
        Vector2 pointPos = new Vector2(points[i].X, points[i].Y);
        double tangentInAngle = Mathf.Atan(keyframe.TangentIn);
        double tangentOutAngle = Mathf.Atan(keyframe.TangentOut);

        double plotZoom = ImPlot.GetPlotLimits().Y.Max - ImPlot.GetPlotLimits().Y.Min;
        double tangentDistance = plotZoom * 0.1;
        Vector2 tangentInPos = pointPos + (new Vector2(Mathf.Cos(tangentInAngle), Mathf.Sin(tangentInAngle)) * tangentDistance);
        Vector2 tangentOutPos = pointPos + (new Vector2(Mathf.Cos(tangentOutAngle), Mathf.Sin(tangentOutAngle)) * tangentDistance);

        var p = ImPlot.PlotToPixels(points[i]);
        var p1 = ImPlot.PlotToPixels(new ImPlotPoint() { X = tangentInPos.x, Y = tangentInPos.y });
        var p2 = ImPlot.PlotToPixels(new ImPlotPoint() { X = tangentOutPos.x, Y = tangentOutPos.y });
        ImPlot.PushPlotClipRect();
        if (i > 0)
        {
            ImPlot.GetPlotDrawList().AddLine(p, p1, ImGui.GetColorU32(new Vector4(0.9f, 0, 0, 1)), 2);
            ImPlot.GetPlotDrawList().AddCircleFilled(p1, 4, ImGui.GetColorU32(new Vector4(0.9f, 0, 0, 1)));
        }
        if (i < points.Length - 1)
        {
            ImPlot.GetPlotDrawList().AddLine(p, p2, ImGui.GetColorU32(new Vector4(0.9f, 0, 0, 1)), 2);
            ImPlot.GetPlotDrawList().AddCircleFilled(p2, 4, ImGui.GetColorU32(new Vector4(0.9f, 0, 0, 1)));
        }
        ImPlot.PopPlotClipRect();
    }
}
