using Prowl.Runtime;
using Prowl.Editor.PropertyDrawers;
using Hexa.NET.ImGui;
using System.Numerics;
using System.Reflection;
using Prowl.Icons;
using Prowl.Editor.Editor.Preferences;
using Prowl.Editor.Editor.ProjectSettings;

namespace Prowl.Editor.EditorWindows;

public class ProjectSettingsWindow : SingletonEditorWindow
{
    public ProjectSettingsWindow() : base("Project Settings") { }

    public ProjectSettingsWindow(Type settingToOpen) : base(settingToOpen) { }

    public override void RenderSideView()
    {
        RenderSideViewElement(PhysicsSetting.Instance);
        RenderSideViewElement(BuildProjectSetting.Instance);
    }
}

public class PreferencesWindow : SingletonEditorWindow
{
    public PreferencesWindow() : base("Preferences") { }

    public PreferencesWindow(Type settingToOpen) : base(settingToOpen) { }
    
    public override void RenderSideView()
    {
        RenderSideViewElement(GeneralPreferences.Instance);
        RenderSideViewElement(AssetPipelinePreferences.Instance);
        RenderSideViewElement(SceneViewPreferences.Instance);
    }
}

public abstract class SingletonEditorWindow : EditorWindow
{

    protected override ImGuiWindowFlags Flags => ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse;

    protected override int Width { get; } = 512;
    protected override int Height { get; } = 512;

    private Type? currentType;
    private object? currentSingleton;

    public SingletonEditorWindow(string title) : base() { Title = FontAwesome6.Gear + " " + title; }

    public SingletonEditorWindow(Type settingToOpen) : base() { currentType = settingToOpen; }

    protected override void Draw()
    {
        const ImGuiTableFlags tableFlags = ImGuiTableFlags.Resizable | ImGuiTableFlags.ContextMenuInBody | ImGuiTableFlags.Resizable;
        System.Numerics.Vector2 availableRegion = ImGui.GetContentRegionAvail();
        if (ImGui.BeginTable("MainViewTable", 2, tableFlags, availableRegion))
        {
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 200);
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.None);

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);

            ImGui.BeginChild("SettingTypes");
            if (Project.HasProject) RenderSideView();
            ImGui.EndChild();
            ImGui.TableSetColumnIndex(1);
            ImGui.BeginChild("Settings");
            if (Project.HasProject) RenderBody();
            ImGui.EndChild();

            ImGui.EndTable();
        }
    }

    public abstract void RenderSideView();

    protected void RenderSideViewElement<T>(T elementInstance)
    {
        Type settingType = elementInstance.GetType();
        if (ImGui.Selectable(settingType.Name, currentType == settingType))
        {
            currentType = settingType;
            currentSingleton = elementInstance;
        }
    }

    private void RenderBody()
    {
        if (currentType == null) return;

        // Draw Settings
        var setting = currentSingleton;

        foreach (var field in RuntimeUtils.GetSerializableFields(setting))
        {
            // Draw the field using PropertyDrawer.Draw
            if (PropertyDrawer.Draw(setting, field))
            {
                // Use reflection to find a method "protected void Save()" and Validate
                MethodInfo? validateMethod = setting.GetType().GetMethod("Validate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                validateMethod?.Invoke(setting, null);
                MethodInfo? saveMethod = setting.GetType().GetMethod("Save", BindingFlags.Instance | BindingFlags.Public);
                saveMethod?.Invoke(setting, null);
            }

        }

        // Draw any Buttons
        EditorGui.HandleAttributeButtons(setting);
    }
}
