﻿using Prowl.Runtime;
using Prowl.Runtime.Audio;
using Prowl.Runtime.GUI;
using Prowl.Runtime.Utils;
using System.Buffers.Binary;

namespace Prowl.Editor.Assets.Importers
{
    [Importer("FileIcon.png", typeof(AudioClip), ".wav")]
    public class AudioClipImporter : ScriptedImporter
    {
        public override void Import(SerializedAsset ctx, FileInfo assetPath)
        {
            ctx.SetMainObject(assetPath.Extension.ToLower() switch {
                ".wav" => LoadWav(assetPath),
                ".wave" => LoadWav(assetPath),
                _ => throw new InvalidOperationException("Unsupported audio format: " + assetPath.Extension.ToLower()),
            });
        }

        #region Wave Format

        private static AudioClip LoadWav(FileInfo file)
        {
            using (FileStream stream = file.OpenRead())
            {
                var buffer = new byte[stream.Length];
                stream.Read(buffer, 0, buffer.Length);
                ReadOnlySpan<byte> fileSpan = new ReadOnlySpan<byte>(buffer);

                int index = 0;
                if (fileSpan[index++] != 'R' || fileSpan[index++] != 'I' || fileSpan[index++] != 'F' || fileSpan[index++] != 'F')
                {
                    throw new InvalidDataException("Given file is not in RIFF format");
                }

                var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(fileSpan.Slice(index, 4));
                index += 4;

                if (fileSpan[index++] != 'W' || fileSpan[index++] != 'A' || fileSpan[index++] != 'V' || fileSpan[index++] != 'E')
                {
                    throw new InvalidDataException("Given file is not in WAVE format");
                }

                short numChannels = -1;
                int sampleRate = -1;
                int byteRate = -1;
                short blockAlign = -1;
                short bitsPerSample = -1;
                byte[] audioData = null;

                while (index + 4 < fileSpan.Length)
                {
                    var identifier = "" + (char)fileSpan[index++] + (char)fileSpan[index++] + (char)fileSpan[index++] + (char)fileSpan[index++];
                    var size = BinaryPrimitives.ReadInt32LittleEndian(fileSpan.Slice(index, 4));
                    index += 4;

                    if (identifier == "fmt ")
                    {
                        if (size != 16)
                        {
                            throw new InvalidDataException($"Unknown Audio Format with subchunk1 size {size}");
                        }
                        else
                        {
                            var audioFormat = BinaryPrimitives.ReadInt16LittleEndian(fileSpan.Slice(index, 2));
                            index += 2;
                            if (audioFormat != 1)
                            {
                                throw new InvalidDataException($"Unknown Audio Format with ID {audioFormat}");
                            }
                            else
                            {
                                numChannels = BinaryPrimitives.ReadInt16LittleEndian(fileSpan.Slice(index, 2));
                                index += 2;
                                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(fileSpan.Slice(index, 4));
                                index += 4;
                                byteRate = BinaryPrimitives.ReadInt32LittleEndian(fileSpan.Slice(index, 4));
                                index += 4;
                                blockAlign = BinaryPrimitives.ReadInt16LittleEndian(fileSpan.Slice(index, 2));
                                index += 2;
                                bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(fileSpan.Slice(index, 2));
                                index += 2;
                            }
                        }
                    }
                    else if (identifier == "data")
                    {
                        audioData = fileSpan.Slice(index, size).ToArray();
                        index += size;
                    }
                    else
                    {
                        index += size;
                    }
                }

                if (audioData == null)
                {
                    throw new InvalidDataException("WAV file does not contain a data chunk");
                }

                AudioClip audioClip = AudioClip.Create(file.Name, audioData, numChannels, bitsPerSample, sampleRate);
                return audioClip;
            }
        }

        #endregion

    }

    [CustomEditor(typeof(AudioClipImporter))]
    public class AudioClipEditor : ScriptedEditor
    {
        ActiveAudio? preview;

        SerializedAsset serialized;

        public override void OnEnable()
        {
            serialized = AssetDatabase.LoadAsset((target as MetaFile).AssetPath);
        }

        public override void OnDisable()
        {
            if (preview != null)
            {
                preview.Stop();
                preview = null;
            }
        }

        public override void OnInspectorGUI()
        {
            var importer = (AudioClipImporter)(target as MetaFile).importer;

            try
            {
                gui.CurrentNode.Layout(LayoutType.Column);

                var audioClip = (AudioClip)serialized.Main;
                gui.TextNode("name", "Name: " + audioClip.Name).ExpandWidth().Height(GuiStyle.ItemHeight);
                gui.TextNode("ch", "Channels: " + audioClip.Channels).ExpandWidth().Height(GuiStyle.ItemHeight);
                gui.TextNode("bps", "Bits Per Sample: " + audioClip.BitsPerSample).ExpandWidth().Height(GuiStyle.ItemHeight);
                gui.TextNode("sr", "Sample Rate: " + audioClip.SampleRate).ExpandWidth().Height(GuiStyle.ItemHeight);
                gui.TextNode("dur", "Duration: " + audioClip.Duration + "s").ExpandWidth().Height(GuiStyle.ItemHeight);
                gui.TextNode("size", "Size in Bytes: " + audioClip.SizeInBytes).ExpandWidth().Height(GuiStyle.ItemHeight);
                gui.TextNode("form", "Format: " + audioClip.Format.ToString()).ExpandWidth().Height(GuiStyle.ItemHeight);

                //g.SeperatorHNode();

                if (audioClip.Data == null)
                {
                    gui.TextNode("err", "Audio Data is Null!").ExpandWidth().Height(GuiStyle.ItemHeight);
                    return;
                }

                // Play
                if(preview != null && preview.IsPlaying)
                {
                    using (gui.Node("StopBtn").ExpandWidth().Height(GuiStyle.ItemHeight).Enter())
                    {
                        gui.Draw2D.DrawRectFilled(gui.CurrentNode.LayoutData.Rect, GuiStyle.Base4 * 0.8f, 4);

                        gui.Draw2D.DrawText("Stop", gui.CurrentNode.LayoutData.Rect, GuiStyle.Base8);

                        if (gui.IsNodePressed())
                        {
                            preview.Stop();
                            preview = null;
                        }

                        if (gui.IsNodeHovered())
                            gui.Draw2D.DrawRectFilled(gui.CurrentNode.LayoutData.Rect, GuiStyle.Base5, 4);
                    }
                }
                else
                {
                    using (gui.Node("PlayBtn").ExpandWidth().Height(GuiStyle.ItemHeight).Enter())
                    {
                        gui.Draw2D.DrawRectFilled(gui.CurrentNode.LayoutData.Rect, GuiStyle.Base4 * 0.8f, 4);

                        gui.Draw2D.DrawText("Play", gui.CurrentNode.LayoutData.Rect, GuiStyle.Base8);

                        if (gui.IsNodePressed())
                            preview = AudioSystem.PlaySound(audioClip);

                        if (gui.IsNodeHovered())
                            gui.Draw2D.DrawRectFilled(gui.CurrentNode.LayoutData.Rect, GuiStyle.Base5, 4);
                    }
                }

            }
            catch
            {
                gui.TextNode("error", "Failed to display AudioClip Data").ExpandWidth().Height(GuiStyle.ItemHeight);
            }
        }
    }
}
