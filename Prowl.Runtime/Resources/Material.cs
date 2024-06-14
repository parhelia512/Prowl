﻿using Prowl.Runtime.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Prowl.Runtime
{
    public class MaterialPropertyBlock
    {
        [SerializeField] private Dictionary<string, Color> colors = new();
        [SerializeField] private Dictionary<string, Vector2> vectors2 = new();
        [SerializeField] private Dictionary<string, Vector3> vectors3 = new();
        [SerializeField] private Dictionary<string, Vector4> vectors4 = new();
        [SerializeField] private Dictionary<string, float> floats = new();
        [SerializeField] private Dictionary<string, int> ints = new();
        [SerializeField] private Dictionary<string, Matrix4x4> matrices = new();
        [SerializeField] private Dictionary<string, System.Numerics.Matrix4x4[]> matrixArr = new();
        [SerializeField] private Dictionary<string, AssetRef<Texture2D>> textures = new();

        //private Dictionary<string, int> textureSlots = new();

        public MaterialPropertyBlock() { }

        public MaterialPropertyBlock(MaterialPropertyBlock clone)
        {
            colors = new(clone.colors);
            vectors2 = new(clone.vectors2);
            vectors3 = new(clone.vectors3);
            vectors4 = new(clone.vectors4);
            floats = new(clone.floats);
            ints = new(clone.ints);
            matrices = new(clone.matrices);
            textures = new(clone.textures);
        }

        public bool isEmpty => colors.Count == 0 && vectors4.Count == 0 && vectors3.Count == 0 && vectors2.Count == 0 && floats.Count == 0 && ints.Count == 0 && matrices.Count == 0 && textures.Count == 0;

        public void SetColor(string name, Color value) => colors[name] = value;
        public Color GetColor(string name) => colors.ContainsKey(name) ? colors[name] : Color.white;

        public void SetVector(string name, Vector2 value) => vectors2[name] = value;
        public Vector2 GetVector2(string name) => vectors2.ContainsKey(name) ? vectors2[name] : Vector2.zero;
        public void SetVector(string name, Vector3 value) => vectors3[name] = value;
        public Vector3 GetVector3(string name) => vectors3.ContainsKey(name) ? vectors3[name] : Vector3.zero;
        public void SetVector(string name, Vector4 value) => vectors4[name] = value;
        public Vector4 GetVector4(string name) => vectors4.ContainsKey(name) ? vectors4[name] : Vector4.zero;
        public void SetFloat(string name, float value) => floats[name] = value;
        public float GetFloat(string name) => floats.ContainsKey(name) ? floats[name] : 0;
        public void SetInt(string name, int value) => ints[name] = value;
        public int GetInt(string name) => ints.ContainsKey(name) ? ints[name] : 0;
        public void SetMatrix(string name, Matrix4x4 value) => matrices[name] = value;
        public void SetMatrices(string name, System.Numerics.Matrix4x4[] value) => matrixArr[name] = value.Cast<System.Numerics.Matrix4x4>().ToArray();
        public Matrix4x4 GetMatrix(string name) => matrices.ContainsKey(name) ? matrices[name] : Matrix4x4.Identity;
        public void SetTexture(string name, Texture2D value) => textures[name] = value;
        public void SetTexture(string name, AssetRef<Texture2D> value) => textures[name] = value;
        public AssetRef<Texture2D>? GetTexture(string name) => textures.ContainsKey(name) ? textures[name] : null;

        public void Clear()
        {
            textures.Clear();
            matrices.Clear();
            ints.Clear();
            floats.Clear();
            vectors2.Clear();
            vectors3.Clear();
            vectors4.Clear();
            colors.Clear();
        }

        public static void Apply(MaterialPropertyBlock mpb, GraphicsProgram shader)
        {
            foreach (var item in mpb.floats)
                Graphics.Device.SetUniformF(shader, item.Key, item.Value);

            foreach (var item in mpb.ints)
                Graphics.Device.SetUniformI(shader, item.Key, (int)item.Value);

            foreach (var item in mpb.vectors2)
                Graphics.Device.SetUniformV2(shader, item.Key, item.Value);
            foreach (var item in mpb.vectors3)
                Graphics.Device.SetUniformV3(shader, item.Key, item.Value);
            foreach (var item in mpb.vectors4)
                Graphics.Device.SetUniformV4(shader, item.Key, item.Value);
            foreach (var item in mpb.colors)
                Graphics.Device.SetUniformV4(shader, item.Key, new System.Numerics.Vector4(item.Value.r, item.Value.g, item.Value.b, item.Value.a));

            foreach (var item in mpb.matrices)
            {
                var m = item.Value.ToFloat();
                Graphics.Device.SetUniformMatrix(shader, item.Key, 1, false, in m.M11);
            }

            foreach (var item in mpb.matrixArr)
            {
                var m = item.Value;
                Graphics.Device.SetUniformMatrix(shader, item.Key, (uint)item.Value.Length, false, in m[0].M11);
            }

            uint texSlot = 0;
            var keysToUpdate = new List<(string, AssetRef<Texture2D>)>();
            foreach (var item in mpb.textures)
            {
                var tex = item.Value;
                if (tex.IsAvailable)
                {
                    texSlot++;
                    Graphics.Device.SetUniformTexture(shader, item.Key, (int)texSlot, tex.Res!.Handle);

                    keysToUpdate.Add((item.Key, tex));
                }
            }
            foreach (var item in keysToUpdate)
                mpb.textures[item.Item1] = item.Item2;
        }
    }

    public sealed class Material : EngineObject
    {
        public AssetRef<Shader> Shader;
        public MaterialPropertyBlock PropertyBlock;

        // Key is Shader.GUID + "-" + keywords + "-" + Shader.globalKeywords
        static Dictionary<string, Shader.CompiledShader> passVariants = new();
        HashSet<string> keywords = new();

        public int PassCount => Shader.IsAvailable ? GetVariant(keywords.ToArray()).passes.Length : 0;

        public Material()
        {

        }

        public Material(AssetRef<Shader> shader)
        {
            if (shader.AssetID == Guid.Empty) throw new ArgumentNullException(nameof(shader));
            Shader = shader;
            PropertyBlock = new();
        }

        public void SetKeyword(string keyword, bool state)
        {
            if (state) EnableKeyword(keyword);
            else DisableKeyword(keyword);
        }

        public void EnableKeyword(string keyword)
        {
            string? key = keyword?.ToUpper().Replace(" ", "").Replace(";", "");
            if (string.IsNullOrWhiteSpace(key)) return;
            if (keywords.Contains(key)) return;
            keywords.Add(key);
        }

        public void DisableKeyword(string keyword)
        {
            string? key = keyword?.ToUpper().Replace(" ", "").Replace(";", "");
            if (string.IsNullOrWhiteSpace(key)) return;
            if (!keywords.Contains(key)) return;
            keywords.Remove(key);
        }

        public bool IsKeywordEnabled(string keyword) => keywords.Contains(keyword.ToUpper().Replace(" ", "").Replace(";", ""));

        public void SetPass(int pass, bool apply = false)
        {
            if (Shader.IsAvailable == false) return;
            var shader = GetVariant(keywords.ToArray());

            // Make sure we have a valid pass
            if (shader.passes == null || pass < 0 || pass >= shader.passes.Length) return;

            InternalSetPass(shader.passes[pass], apply);
        }

        public void SetShadowPass(bool apply = false)
        {
            if (Shader.IsAvailable == false) return;
            var shader = GetVariant(keywords.ToArray());
            InternalSetPass(shader.shadowPass, apply);
        }

        void InternalSetPass(Shader.CompiledShader.Pass pass, bool apply = false)
        {
            // Set the shader
            Graphics.Device.SetState(pass.State);
            Graphics.Device.BindProgram(pass.Program); // Ensure the program is in use

            if (apply)
                MaterialPropertyBlock.Apply(PropertyBlock, Graphics.Device.CurrentProgram);
        }

        Shader.CompiledShader GetVariant(string[] allKeywords)
        {
            if (Shader.IsAvailable == false) throw new Exception("Cannot compile without a valid shader assigned");
            passVariants ??= new();

            string keywords = string.Join("-", allKeywords);
            string key = Shader.Res.InstanceID + "-" + keywords + "-" + Runtime.Shader.globalKeywords;
            if (passVariants.TryGetValue(key, out var s)) return s;

            // Add each global togather making sure to not add duplicates
            string[] globals = Runtime.Shader.globalKeywords.ToArray();
            for (int i = 0; i < globals.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(globals[i])) continue;
                if (allKeywords.Contains(globals[i], StringComparer.OrdinalIgnoreCase)) continue;
                allKeywords = allKeywords.Append(globals[i]).ToArray();
            }
            // Remove empty keywords
            allKeywords = allKeywords.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

            // Compile Each Pass
            Shader.CompiledShader compiledPasses = Shader.Res!.Compile(allKeywords);

            passVariants[key] = compiledPasses;
            return compiledPasses;
        }

        public void SetColor(string name, Color value) => PropertyBlock.SetColor(name, value);
        public void SetVector(string name, Vector2 value) => PropertyBlock.SetVector(name, value);
        public void SetVector(string name, Vector3 value) => PropertyBlock.SetVector(name, value);
        public void SetVector(string name, Vector4 value) => PropertyBlock.SetVector(name, value);
        public void SetFloat(string name, float value) => PropertyBlock.SetFloat(name, value);
        public void SetInt(string name, int value) => PropertyBlock.SetInt(name, value);
        public void SetMatrix(string name, Matrix4x4 value) => PropertyBlock.SetMatrix(name, value);
        public void SetMatrices(string name, System.Numerics.Matrix4x4[] value) => PropertyBlock.SetMatrices(name, value);
        public void SetTexture(string name, Texture2D value) => PropertyBlock.SetTexture(name, value);
        public void SetTexture(string name, AssetRef<Texture2D> value) => PropertyBlock.SetTexture(name, value);

        //public CompoundTag Serialize(string tagName, TagSerializer.SerializationContext ctx)
        //{
        //    CompoundTag compoundTag = new CompoundTag(tagName);
        //    compoundTag.Add(TagSerializer.Serialize(Shader, "Shader", ctx));
        //    compoundTag.Add(TagSerializer.Serialize(PropertyBlock, "PropertyBlock", ctx));
        //    return compoundTag;
        //}
        //
        //public void Deserialize(CompoundTag value, TagSerializer.SerializationContext ctx)
        //{
        //    Shader = TagSerializer.Deserialize<AssetRef<Shader>>(value["Shader"], ctx);
        //    PropertyBlock = TagSerializer.Deserialize<MaterialPropertyBlock>(value["PropertyBlock"], ctx);
        //}
    }

}
