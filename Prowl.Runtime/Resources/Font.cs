﻿using Prowl.Runtime.GUI.Graphics;
using Prowl.Runtime.Rendering.Primitives;
using Silk.NET.OpenAL;
using StbTrueTypeSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Prowl.Runtime.GUI.Graphics.UIDrawList;
using static System.Net.Mime.MediaTypeNames;

namespace Prowl.Runtime
{
    public struct GlyphInfo
    {
        public float X, Y, Width, Height;
        public float XOffset, YOffset;
        public float XAdvance;
    }

    public sealed class Font : EngineObject, ISerializable
    {
        public double FontSize = 20.0;
        public double DisplayFontSize = 20.0;
        public Dictionary<uint, GlyphInfo> Glyphs;
        public Color32[] Bitmap;
        public int Width;
        public int Height;

        public Vector2 TexUvWhitePixel => new(0.5f * (1.0f / Width), 0.5f * (1.0f / Height));

        public Texture2D? Texture { get; private set; }

        public void CreateResource()
        {
            Texture = new Texture2D((uint)Width, (uint)Height, false, Rendering.Primitives.TextureImageFormat.Color4b);
            Memory<Color32> data = new Memory<Color32>(Bitmap);
            Texture.SetData(data);
            Texture.SetTextureFilters(TextureMin.Linear, TextureMag.Linear);
        }

        public static FontBuilder BuildNewFont(int width, int height)
        {
            FontBuilder builder = new();
            builder.Begin(width, height);
            return builder;
        }

        public static Font CreateFromTTFMemory(byte[] ttf, float fontSize, int width, int height, CharacterRange[] characterRanges)
        {
            FontBuilder builder = new();
            builder.Begin(width, height);
            builder.Add(ttf, fontSize, characterRanges);
            return builder.End(fontSize, fontSize);
        }

        public float GetCharAdvance(char c)
        {
            if (Glyphs.TryGetValue(c, out GlyphInfo glyph))
            {
                return glyph.XAdvance;
            }
            else
            {
                // Return a default value or handle the case when the character is not found
                return 10.0f;
            }
        }

        public Vector2 InputTextCalcTextSizeW(string text, int text_begin, int text_end, ref int? remaining, ref Vector2? out_offset, bool stop_on_new_line = false)
        {
            double line_height = DisplayFontSize;
            double scale = line_height / FontSize;

            Vector2 text_size = new Vector2(0, 0);
            double line_width = 0.0f;

            int s = text_begin;
            while (s < text_end)
            {
                char c = text[s++];
                if (c == '\n')
                {
                    text_size.x = MathD.Max(text_size.x, line_width);
                    text_size.y += line_height;
                    line_width = 0.0f;
                    if (stop_on_new_line)
                        break;
                    continue;
                }
                if (c == '\r')
                    continue;

                double char_width = GetCharAdvance(c) * scale;
                line_width += char_width;
            }

            if (text_size.x < line_width)
                text_size.x = line_width;

            if (out_offset.HasValue)
                out_offset = new Vector2(line_width, text_size.y + line_height);  // offset allow for the possibility of sitting after a trailing \n

            if (line_width > 0 || text_size.y == 0.0f)                        // whereas size.y will ignore the trailing \n
                text_size.y += line_height;

            if (remaining.HasValue)
                remaining = s;

            return text_size;
        }

        public Vector2 CalcTextSize(string str, int beginIndex, double wrap_width = -1f)
            => CalcTextSize(str, DisplayFontSize, beginIndex, wrap_width);
        public Vector2 CalcTextSize(string str, double font_size, int beginIndex, double wrap_width = -1f)
        {
            int text_display_end = str.Length;

            if (beginIndex == text_display_end)
                return new Vector2(0.0f, font_size);
            Vector2 text_size;
            CalcTextSizeA(out text_size, font_size, double.MaxValue, wrap_width, str, beginIndex, text_display_end);

            // Cancel out character spacing for the last character of a line (it is baked into glyph->XAdvance field)
            double font_scale = font_size / FontSize;
            double character_spacing_x = 1.0f * font_scale;
            if (text_size.x > 0.0f)
                text_size.x -= character_spacing_x;
            text_size.x = (int)(text_size.x + 0.95f);

            return text_size;
        }

        public int CalcTextSizeA(out Vector2 textSize, double size, double maxWidth, double wrapWidth, string text, int textBegin, int textEnd = -1)
        {
            if (textEnd == -1)
                textEnd = text.Length; // FIXME-OPT: Need to avoid this.

            double lineHeight = size;
            double scale = size / FontSize;
            Vector2 textSizeResult = new Vector2(0, 0);
            double lineWidth = 0.0f;
            bool wordWrapEnabled = (wrapWidth > 0.0f);
            int wordWrapEol = -1;
            int s = textBegin;

            while (s < textEnd)
            {
                if (wordWrapEnabled)
                {
                    // Calculate how far we can render. Requires two passes on the string data but keeps the code simple and not intrusive for what's essentially an uncommon feature.
                    if (wordWrapEol == -1)
                    {
                        wordWrapEol = CalcWordWrapPositionA(scale, text, s, textEnd, wrapWidth - lineWidth);
                        if (wordWrapEol == s) // Wrap_width is too small to fit anything. Force displaying 1 character to minimize the height discontinuity.
                            wordWrapEol++; // +1 may not be a character start point in UTF-8 but it's ok because we use s >= word_wrap_eol below
                    }

                    if (s >= wordWrapEol)
                    {
                        if (textSizeResult.x < lineWidth)
                            textSizeResult.x = lineWidth;

                        textSizeResult.y += lineHeight;
                        lineWidth = 0.0;
                        wordWrapEol = -1;

                        // Wrapping skips upcoming blanks
                        while (s < textEnd)
                        {
                            char wc = text[s];
                            if (char.IsSeparator(wc)) { s++; }
                            else if (wc == '\n') { s++; break; }
                            else { break; }
                        }
                        continue;
                    }
                }

                // Decode and advance source
                int prevS = s;
                char c = text[s++];

                if (c < 32)
                {
                    if (c == '\n')
                    {
                        textSizeResult.x = Math.Max(textSizeResult.x, lineWidth);
                        textSizeResult.y += lineHeight;
                        lineWidth = 0.0;
                        continue;
                    }
                    if (c == '\r')
                        continue;
                }

                double charWidth = 0.0;
                if (Glyphs.TryGetValue(c, out GlyphInfo glyph))
                {
                    charWidth = glyph.XAdvance * scale;
                }

                if (lineWidth + charWidth >= maxWidth)
                {
                    s = prevS;
                    break;
                }

                lineWidth += charWidth;
            }

            if (textSizeResult.x < lineWidth)
                textSizeResult.x = lineWidth;

            if (lineWidth > 0 || textSizeResult.y == 0.0)
                textSizeResult.y += lineHeight;

            textSize = textSizeResult;
            return s; // Return the position we stopped at
        }

        int CalcWordWrapPositionA(double scale, string text, int textBegin, int textEnd, double wrapWidth)
        {
            // Simple word-wrapping for English, not full-featured. Please submit failing cases!
            // FIXME: Much possible improvements (don't cut things like "word !", "word!!!" but cut within "word,,,,", more sensible support for punctuations, support for Unicode punctuations, etc.)
            // For references, possible wrap point marked with ^
            // "aaa bbb, ccc,ddd. eee fff. ggg!"
            // ^ ^ ^ ^ ^__ ^ ^
            // List of hardcoded separators: .,;!?'"
            // Skip extra blanks after a line returns (that includes not counting them in width computation)
            // e.g. "Hello world" --> "Hello" "World"
            // Cut words that cannot possibly fit within one line.
            // e.g.: "The tropical fish" with ~5 characters worth of width --> "The tr" "opical" "fish"

            double lineWidth = 0.0;
            double wordWidth = 0.0;
            double blankWidth = 0.0;
            int wordEnd = textBegin;
            int prevWordEnd = -1;
            bool insideWord = true;

            int s = textBegin;
            while (s < textEnd)
            {
                char c = text[s];
                int nextS = s + 1;

                if (c == 0)
                    break;

                if (c < 32)
                {
                    if (c == '\n')
                    {
                        lineWidth = wordWidth = blankWidth = 0.0f;
                        insideWord = true;
                        s = nextS;
                        continue;
                    }
                    if (c == '\r')
                    {
                        s = nextS;
                        continue;
                    }
                }

                double charWidth = 0.0;
                if (Glyphs.TryGetValue(c, out GlyphInfo glyph))
                {
                    charWidth = glyph.XAdvance * scale;
                }

                if (char.IsSeparator(c))
                {
                    if (insideWord)
                    {
                        lineWidth += blankWidth;
                        blankWidth = 0.0;
                    }
                    blankWidth += charWidth;
                    insideWord = false;
                }
                else
                {
                    wordWidth += charWidth;
                    if (insideWord)
                    {
                        wordEnd = nextS;
                    }
                    else
                    {
                        prevWordEnd = wordEnd;
                        lineWidth += wordWidth + blankWidth;
                        wordWidth = blankWidth = 0.0;
                    }
                    // Allow wrapping after punctuation.
                    insideWord = !(c == '.' || c == ',' || c == ';' || c == '!' || c == '?' || c == '\"');
                }

                // We ignore blank width at the end of the line (they can be skipped)
                if (lineWidth + wordWidth >= wrapWidth)
                {
                    // Words that cannot possibly fit within an entire line will be cut anywhere.
                    if (wordWidth < wrapWidth)
                        s = prevWordEnd > -1 ? prevWordEnd : wordEnd;
                    break;
                }

                s = nextS;
            }

            return s;
        }

        public Rect RenderText(double size, Vector2 pos, uint color, Vector4 clipRect, string text, int textBegin, int textEnd, UIDrawList drawList, double wrapWidth = 0.0, bool cpuFineClip = false)
        {
            if (textEnd == -1)
                textEnd = text.Length;

            // Align to be pixel perfect
            pos.x = (int)pos.x;
            pos.y = (int)pos.y;
            double x = pos.x;
            double y = pos.y;
            if (y > clipRect.w)
                return Rect.Empty;

            double scale = size / FontSize;
            double lineHeight = FontSize * scale;
            bool wordWrapEnabled = wrapWidth > 0.0;
            int wordWrapEol = -1;

            int vtxWrite = drawList._VtxWritePtr;
            int idxWrite = drawList._IdxWritePtr;
            uint vtxCurrentIdx = drawList._VtxCurrentIdx;

            int s = textBegin;
            if (!wordWrapEnabled && y + lineHeight < clipRect.y)
                while (s < textEnd && text[s] != '\n')  // Fast-forward to next line
                    s++;

            while (s < textEnd)
            {
                if (wordWrapEnabled)
                {
                    // Calculate how far we can render. Requires two passes on the string data but keeps the code simple and not intrusive for what's essentially an uncommon feature.
                    if (wordWrapEol == -1)
                    {
                        wordWrapEol = CalcWordWrapPositionA(scale, text, s, textEnd, wrapWidth - (x - pos.x));
                        if (wordWrapEol == s) // Wrap_width is too small to fit anything. Force displaying 1 character to minimize the height discontinuity.
                            wordWrapEol++;    // +1 may not be a character start point in UTF-8 but it's ok because we use s >= word_wrap_eol below
                    }

                    if (s >= wordWrapEol)
                    {
                        x = pos.x;
                        y += lineHeight;
                        wordWrapEol = -1;

                        // Wrapping skips upcoming blanks
                        while (s < textEnd)
                        {
                            char wc = text[s];
                            if (char.IsSeparator(wc)) { s++; }
                            else if (wc == '\n') { s++; break; }
                            else { break; }
                        }
                        continue;
                    }
                }

                // Decode and advance source
                char c = text[s++];

                if (c < 32)
                {
                    if (c == '\n')
                    {
                        x = pos.x;
                        y += lineHeight;

                        if (y > clipRect.w)
                            break;
                        if (!wordWrapEnabled && y + lineHeight < clipRect.y)
                            while (s < textEnd && text[s] != '\n')  // Fast-forward to next line
                                s++;

                        continue;
                    }
                    if (c == '\r')
                        continue;
                }

                double charWidth = 0.0f;
                if (Glyphs.TryGetValue(c, out GlyphInfo glyph))
                {
                    charWidth = glyph.XAdvance * scale;

                    // Arbitrarily assume that both space and tabs are empty glyphs as an optimization
                    if (c != ' ' && c != '\t')
                    {
                        double x1 = x + glyph.XOffset * scale;
                        double x2 = x1 + glyph.Width * scale;
                        double y1 = y + glyph.YOffset * scale;
                        double y2 = y1 + glyph.Height * scale;

                        if (x1 <= clipRect.z && x2 >= clipRect.x)
                        {
                            // Render a character
                            double u1 = (double)glyph.X / Width;
                            double v1 = (double)glyph.Y / Height;
                            double u2 = (double)(glyph.X + glyph.Width) / Width;
                            double v2 = (double)(glyph.Y + glyph.Height) / Height;

                            // CPU side clipping used to fit text in their frame when the frame is too small. Only does clipping for axis aligned quads.
                            if (cpuFineClip)
                            {
                                if (x1 < clipRect.x)
                                {
                                    u1 = u1 + (1.0f - (x2 - clipRect.x) / (x2 - x1)) * (u2 - u1);
                                    x1 = clipRect.x;
                                }
                                if (y1 < clipRect.y)
                                {
                                    v1 = v1 + (1.0f - (y2 - clipRect.y) / (y2 - y1)) * (v2 - v1);
                                    y1 = clipRect.y;
                                }
                                if (x2 > clipRect.z)
                                {
                                    u2 = u1 + (clipRect.z - x1) / (x2 - x1) * (u2 - u1);
                                    x2 = clipRect.z;
                                }
                                if (y2 > clipRect.w)
                                {
                                    v2 = v1 + (clipRect.w - y1) / (y2 - y1) * (v2 - v1);
                                    y2 = clipRect.w;
                                }
                                if (y1 >= y2)
                                {
                                    x += charWidth;
                                    continue;
                                }
                            }

                            // We are NOT calling PrimRectUV() here because non-inlined causes too much overhead in a debug build.
                            // Inlined here:
                            {
                                drawList.IdxBuffer[idxWrite++] = (ushort)vtxCurrentIdx; drawList.IdxBuffer[idxWrite++] = (ushort)(vtxCurrentIdx + 1); drawList.IdxBuffer[idxWrite++] = (ushort)(vtxCurrentIdx + 2);
                                drawList.IdxBuffer[idxWrite++] = (ushort)vtxCurrentIdx; drawList.IdxBuffer[idxWrite++] = (ushort)(vtxCurrentIdx + 2); drawList.IdxBuffer[idxWrite++] = (ushort)(vtxCurrentIdx + 3);
                                drawList.VtxBuffer[vtxWrite++] = new UIVertex { pos = new Vector3(x1, y1, drawList._primitiveCount), uv = new Vector2(u1, v1), col = color };
                                drawList.VtxBuffer[vtxWrite++] = new UIVertex { pos = new Vector3(x2, y1, drawList._primitiveCount), uv = new Vector2(u2, v1), col = color };
                                drawList.VtxBuffer[vtxWrite++] = new UIVertex { pos = new Vector3(x2, y2, drawList._primitiveCount), uv = new Vector2(u2, v2), col = color };
                                drawList.VtxBuffer[vtxWrite++] = new UIVertex { pos = new Vector3(x1, y2, drawList._primitiveCount), uv = new Vector2(u1, v2), col = color };
                                vtxCurrentIdx += 4;
                            }
                        }
                    }
                }

                x += charWidth;
            }

            drawList._VtxWritePtr = vtxWrite;
            drawList._VtxCurrentIdx = vtxCurrentIdx;
            drawList._IdxWritePtr = idxWrite;

            return new Rect(pos.x, pos.y, x, y + lineHeight);
        }


        public SerializedProperty Serialize(Serializer.SerializationContext ctx)
        {
            var compoundTag = SerializedProperty.NewCompound();
            compoundTag.Add("Width", new(Width));
            compoundTag.Add("Height", new(Height));

            using (MemoryStream memoryStream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(memoryStream))
            {
                for (int i = 0; i < Bitmap.Length; i++)
                {
                    writer.Write(Bitmap[i].red);
                    writer.Write(Bitmap[i].green);
                    writer.Write(Bitmap[i].blue);
                    writer.Write(Bitmap[i].alpha);
                }
                compoundTag.Add("Bitmap", new(memoryStream.ToArray()));
            }

            SerializedProperty glyphsTag = SerializedProperty.NewList();
            foreach (var glyph in Glyphs)
            {
                var glyphTag = SerializedProperty.NewCompound();
                glyphTag.Add("Unicode", new(glyph.Key));
                glyphTag.Add("X", new(glyph.Value.X));
                glyphTag.Add("Y", new(glyph.Value.Y));
                glyphTag.Add("Width", new(glyph.Value.Width));
                glyphTag.Add("Height", new(glyph.Value.Height));
                glyphTag.Add("XOffset", new(glyph.Value.XOffset));
                glyphTag.Add("YOffset", new(glyph.Value.YOffset));
                glyphTag.Add("XAdvance", new(glyph.Value.XAdvance));
                glyphsTag.ListAdd(glyphTag);
            }
            compoundTag.Add("Glyphs", glyphsTag);

            return compoundTag;
        }

        public void Deserialize(SerializedProperty value, Serializer.SerializationContext ctx)
        {
            Width = value["Width"].IntValue;
            Height = value["Height"].IntValue;
            //Bitmap = value["Bitmap"].ByteArrayValue;

            using (MemoryStream memoryStream = new MemoryStream(value["Bitmap"].ByteArrayValue))
            using (BinaryReader reader = new BinaryReader(memoryStream))
            {
                Bitmap = new Color32[Width * Height];
                for (int i = 0; i < Bitmap.Length; i++)
                {
                    Bitmap[i].red = reader.ReadByte();
                    Bitmap[i].green = reader.ReadByte();
                    Bitmap[i].blue = reader.ReadByte();
                    Bitmap[i].alpha = reader.ReadByte();
                }
            }

            Glyphs = new();
            var glyphsTag = value.Get("Glyphs");
            foreach (var glyphTag in glyphsTag.List)
            {
                var glyph = new GlyphInfo {
                    X = glyphTag["X"].IntValue,
                    Y = glyphTag["Y"].IntValue,
                    Width = glyphTag["Width"].IntValue,
                    Height = glyphTag["Height"].IntValue,
                    XOffset = glyphTag["XOffset"].IntValue,
                    YOffset = glyphTag["YOffset"].IntValue,
                    XAdvance = glyphTag["XAdvance"].IntValue
                };
                Glyphs.Add(glyphTag["Unicode"].UIntValue, glyph);
            }

            CreateResource();
        }

        public struct CharacterRange
        {
            public static readonly CharacterRange BasicLatin = new CharacterRange(0x0020, 0x007F);
            public static readonly CharacterRange Latin1Supplement = new CharacterRange(0x00A0, 0x00FF);
            public static readonly CharacterRange LatinExtendedA = new CharacterRange(0x0100, 0x017F);
            public static readonly CharacterRange LatinExtendedB = new CharacterRange(0x0180, 0x024F);
            public static readonly CharacterRange Cyrillic = new CharacterRange(0x0400, 0x04FF);
            public static readonly CharacterRange CyrillicSupplement = new CharacterRange(0x0500, 0x052F);
            public static readonly CharacterRange Hiragana = new CharacterRange(0x3040, 0x309F);
            public static readonly CharacterRange Katakana = new CharacterRange(0x30A0, 0x30FF);
            public static readonly CharacterRange Greek = new CharacterRange(0x0370, 0x03FF);
            public static readonly CharacterRange CjkSymbolsAndPunctuation = new CharacterRange(0x3000, 0x303F);
            public static readonly CharacterRange CjkUnifiedIdeographs = new CharacterRange(0x4e00, 0x9fff);
            public static readonly CharacterRange HangulCompatibilityJamo = new CharacterRange(0x3130, 0x318f);
            public static readonly CharacterRange HangulSyllables = new CharacterRange(0xac00, 0xd7af);

            public int Start { get; }

            public int End { get; }

            public int Size => End - Start + 1;

            public CharacterRange(int start, int end)
            {
                Start = start;
                End = end;
            }

            public CharacterRange(int single) : this(single, single)
            {
            }
        }

        public unsafe class FontBuilder
        {
            private byte[] _bitmap;
            private StbTrueType.stbtt_pack_context _context;
            private Dictionary<uint, GlyphInfo> _glyphs;
            private int bitmapWidth, bitmapHeight;

            public void Begin(int width, int height)
            {
                bitmapWidth = width;
                bitmapHeight = height;
                _bitmap = new byte[width * height];
                _context = new StbTrueType.stbtt_pack_context();

                fixed (byte* pixelsPtr = _bitmap)
                {
                    StbTrueType.stbtt_PackBegin(_context, pixelsPtr, width, height, width, 1, null);
                }

                _glyphs = [];
            }

            public void Add(byte[] ttf, float fontsize, IEnumerable<CharacterRange> characterRanges)
            {
                if (ttf == null || ttf.Length == 0)
                    throw new ArgumentNullException(nameof(ttf));

                if (characterRanges == null)
                    throw new ArgumentNullException(nameof(characterRanges));

                if (!characterRanges.Any())
                    throw new ArgumentException("characterRanges must have a least one value.");

                var fontInfo = StbTrueType.CreateFont(ttf, 0);
                if (fontInfo == null)
                    throw new Exception("Failed to init font.");

                var scaleFactor = StbTrueType.stbtt_ScaleForPixelHeight(fontInfo, (float)fontsize);

                int ascent, descent, lineGap;
                StbTrueType.stbtt_GetFontVMetrics(fontInfo, &ascent, &descent, &lineGap);

                foreach (var range in characterRanges)
                {
                    if (range.Start > range.End)
                        continue;

                    var cd = new StbTrueType.stbtt_packedchar[range.End - range.Start + 1];
                    fixed (StbTrueType.stbtt_packedchar* chardataPtr = cd)
                    {
                        StbTrueType.stbtt_PackFontRange(_context, fontInfo.data, 0, (float)fontsize,
                            range.Start,
                            range.End - range.Start + 1,
                            chardataPtr);
                    }

                    for (uint i = 0; i < cd.Length; ++i)
                    {
                        var glyphInfo = new GlyphInfo {
                            X = cd[i].x0,
                            Y = cd[i].y0,
                            Width = cd[i].x1 - cd[i].x0,
                            Height = cd[i].y1 - cd[i].y0,
                            XOffset = cd[i].xoff,
                            //YOffset = (float)(cd[i].yoff + Math.Round(MathF.Ceiling(ascent * scaleFactor))),
                            YOffset = cd[i].yoff, // TODO: Why is this better?
                            XAdvance = (float)Math.Round(cd[i].xadvance)
                        };

                        _glyphs[i + (uint)range.Start] = glyphInfo;
                    }
                }
            }

            public Font End(float fontsize, float targetfontsize)
            {
                Font font = new();
                font.FontSize = fontsize;
                font.DisplayFontSize = targetfontsize;
                font.Width = bitmapWidth;
                font.Height = bitmapHeight;
                font.Glyphs = _glyphs;

                // Offset by minimal offset
                var minimumOffsetY = 10000f;
                foreach (var pair in font.Glyphs)
                    if (pair.Value.YOffset < minimumOffsetY)
                        minimumOffsetY = pair.Value.YOffset;

                var keys = font.Glyphs.Keys.ToArray();
                foreach (var key in keys)
                {
                    var pc = font.Glyphs[key];
                    pc.YOffset -= minimumOffsetY;
                    font.Glyphs[key] = pc;
                }

                font.Bitmap = new Color32[bitmapWidth * bitmapHeight];
                for (var i = 0; i < _bitmap.Length; ++i)
                {
                    var b = _bitmap[i];
                    font.Bitmap[i].red = 255;
                    font.Bitmap[i].green = 255;
                    font.Bitmap[i].blue = 255;

                    font.Bitmap[i].alpha = b;
                }
                // Set the first pixel to white (TexUvWhitePixel)
                font.Bitmap[0] = new Color32 { red = 255, green = 255, blue = 255, alpha = 255 };

                font.CreateResource();

                return font;
            }
        }
    }
}
