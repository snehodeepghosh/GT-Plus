using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace GtPlus.Services.Psd;

/// <summary>what a layer record is in Photoshop's flat layer list: a normal layer, or one end of a group</summary>
public enum PsdSection
{
    Layer        = 0,
    OpenFolder   = 1,
    ClosedFolder = 2,

    /// <summary>the hidden "&lt;/Layer group&gt;" record that sits below a group's children and marks where it starts</summary>
    GroupEnd     = 3,
}

/// <summary>one layer record from a PSD/PSB, with its pixels decoded to straight (non-premultiplied) RGBA</summary>
public sealed class PsdLayer
{
    public string Name { get; set; } = "";
    public int Top { get; set; }
    public int Left { get; set; }
    public int Bottom { get; set; }
    public int Right { get; set; }
    public int Width  => Math.Max(0, Right - Left);
    public int Height => Math.Max(0, Bottom - Top);

    /// <summary>layer opacity, 0-255</summary>
    public byte Opacity { get; set; } = 255;

    /// <summary>fill opacity ('iOpa'), 0-255; scales the pixels but not the layer style, and with no layer styles carried over it simply multiplies in</summary>
    public byte FillOpacity { get; set; } = 255;

    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
    public bool Clipped { get; set; }
    public string BlendMode { get; set; } = "norm";
    public PsdSection Section { get; set; }

    /// <summary>RGBA, row-major, <see cref="Width"/> × <see cref="Height"/>; null when the layer carries no pixel channels</summary>
    public byte[]? Pixels { get; set; }

    /// <summary>set for a type layer</summary>
    public PsdText? Text { get; set; }

    public bool IsGroupStart => Section is PsdSection.OpenFolder or PsdSection.ClosedFolder;

    public override string ToString() => $"{Name} [{Left},{Top} {Width}x{Height}] {Section}{(Text is null ? "" : " text")}";
}

/// <summary>the parts of a type layer an importer needs; sizes are as Photoshop stores them, before the layer transform</summary>
public sealed class PsdText
{
    public string Content { get; set; } = "";

    /// <summary>the layer's text transform: xx, xy, yx, yy, tx, ty (tx/ty is the text origin, the first baseline for point text)</summary>
    public double[] Transform { get; set; } = { 1, 0, 0, 1, 0, 0 };

    public string FontPostScriptName { get; set; } = "";
    public double FontSize { get; set; } = 12;
    public bool AutoLeading { get; set; } = true;
    public double Leading { get; set; }
    public bool FauxBold { get; set; }
    public bool FauxItalic { get; set; }
    public bool Underline { get; set; }
    public bool Strikethrough { get; set; }

    /// <summary>Photoshop's FontCaps: 0 normal, 1 small caps, 2 all caps</summary>
    public int FontCaps { get; set; }

    /// <summary>ARGB, 0-1</summary>
    public double[] Color { get; set; } = { 1, 0, 0, 0 };

    /// <summary>Photoshop's paragraph Justification: 0 left, 1 right, 2 centre, 3-5 justify with the last line left/right/centre, 6 justify all</summary>
    public int Justification { get; set; }

    /// <summary>paragraph (box) text rather than point text</summary>
    public bool IsBox { get; set; }

    /// <summary>left, top, right, bottom of the paragraph box in text space (before <see cref="Transform"/>)</summary>
    public double[]? BoxBounds { get; set; }
}

/// <summary>a decoded PSD/PSB: header, resolution and the flat layer list, bottom layer first as the file stores it</summary>
public sealed class PsdFile
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int Depth { get; set; }
    public int ColorMode { get; set; }
    public bool IsPsb { get; set; }

    /// <summary>horizontal resolution in pixels per inch, 72 when the file carries no ResolutionInfo</summary>
    public double DpiX { get; set; } = 72;

    public List<PsdLayer> Layers { get; } = new();

    /// <summary>layers whose pixels could not be decoded (an unsupported compression, a truncated file); reported rather than fatal</summary>
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// reader for Adobe Photoshop documents (PSD, and PSB "large document" files), written from Adobe's published file format
/// specification. It reads what a title importer needs: the canvas, the layer records with their flags and group structure,
/// each layer's pixels (raw, RLE or ZIP compressed; 8, 16 or 32 bits; RGB, greyscale or CMYK), layer masks, and the text of type
/// layers including the style held in their EngineData. Layer effects, adjustment layers and blend modes are not evaluated
/// </summary>
public static class PsdReader
{
    public static PsdFile Read(string path)
    {
        using var fs = File.OpenRead(path);
        return Read(fs);
    }

    public static PsdFile Read(Stream stream)
    {
        var r = new BigEndianReader(stream);
        var psd = new PsdFile();

        if (r.ReadAscii(4) != "8BPS") throw new InvalidDataException("Not a Photoshop file (missing 8BPS signature).");
        int version = r.ReadUInt16();
        if (version is not (1 or 2)) throw new InvalidDataException($"Unsupported Photoshop file version {version}.");
        psd.IsPsb = version == 2;
        r.Skip(6);
        int channels = r.ReadUInt16();
        psd.Height    = (int)r.ReadUInt32();
        psd.Width     = (int)r.ReadUInt32();
        psd.Depth     = r.ReadUInt16();
        psd.ColorMode = r.ReadUInt16();
        _ = channels;

        if (psd.Depth is not (8 or 16 or 32))
            throw new InvalidDataException($"{psd.Depth}-bit documents are not supported; save the PSD as 8, 16 or 32 bits per channel.");
        if (psd.ColorMode is not (1 or 3 or 4))
            throw new InvalidDataException($"Colour mode {ColorModeName(psd.ColorMode)} is not supported; convert the document to RGB.");

        // colour mode data (indexed palettes, duotone curves) is not needed
        r.Skip(r.ReadUInt32());

        ReadImageResources(r, psd);
        ReadLayerAndMaskInfo(r, psd);
        return psd;
    }

    private static string ColorModeName(int mode) => mode switch
    {
        0 => "Bitmap", 1 => "Grayscale", 2 => "Indexed", 3 => "RGB", 4 => "CMYK", 7 => "Multichannel", 8 => "Duotone", 9 => "Lab",
        _ => mode.ToString(),
    };

    private static void ReadImageResources(BigEndianReader r, PsdFile psd)
    {
        long length = r.ReadUInt32();
        long end = r.Position + length;
        while (r.Position + 12 <= end)
        {
            if (r.ReadAscii(4) != "8BIM") break;
            int id = r.ReadUInt16();
            int nameLen = r.ReadByte();
            r.Skip(nameLen + ((nameLen + 1) % 2));          // Pascal name padded to an even total
            long size = r.ReadUInt32();
            long dataStart = r.Position;

            // ResolutionInfo: hRes as 16.16 fixed pixels per inch
            if (id == 0x03ED && size >= 4)
                psd.DpiX = r.ReadUInt32() / 65536.0;

            r.Position = dataStart + size + (size % 2);
        }
        r.Position = end;
    }

    private static void ReadLayerAndMaskInfo(BigEndianReader r, PsdFile psd)
    {
        long sectionLength = psd.IsPsb ? (long)r.ReadUInt64() : r.ReadUInt32();
        if (sectionLength == 0) return;
        long sectionEnd = r.Position + sectionLength;

        long layerInfoLength = psd.IsPsb ? (long)r.ReadUInt64() : r.ReadUInt32();
        long layerInfoEnd = r.Position + layerInfoLength;
        if (layerInfoLength > 0)
            ReadLayerInfo(r, psd, layerInfoEnd);
        r.Position = layerInfoEnd;

        // global layer mask info
        if (r.Position + 4 <= sectionEnd)
        {
            long globalMask = r.ReadUInt32();
            r.Skip(globalMask);
        }

        // 16- and 32-bit documents keep their layers in an 'Lr16'/'Lr32' block here instead, with an empty layer info above
        while (psd.Layers.Count == 0 && r.Position + 12 <= sectionEnd)
        {
            var sig = r.ReadAscii(4);
            if (sig is not ("8BIM" or "8B64")) break;
            var key = r.ReadAscii(4);
            long len = psd.IsPsb && IsLongKey(key) ? (long)r.ReadUInt64() : r.ReadUInt32();
            long blockEnd = r.Position + len;
            if (key is "Lr16" or "Lr32" or "Layr")
                ReadLayerInfo(r, psd, blockEnd);
            r.Position = blockEnd;
            AlignTo(r, 4, sectionEnd);
        }

        r.Position = sectionEnd;
    }

    /// <summary>additional-info keys whose length is 8 bytes in a PSB</summary>
    private static bool IsLongKey(string key) => key is "LMsk" or "Lr16" or "Lr32" or "Layr" or "Mt16" or "Mt32" or "Mtrn"
                                                    or "Alph" or "FMsk" or "lnk2" or "FEid" or "FXid" or "PxSD";

    private static void AlignTo(BigEndianReader r, int alignment, long limit)
    {
        // writers pad these blocks to 2 or 4 bytes; rather than trust one rule, step over zero padding up to the next signature
        int steps = 0;
        while (r.Position < limit && steps < alignment - 1)
        {
            long here = r.Position;
            if (here + 4 <= limit)
            {
                var peek = r.ReadAscii(4);
                r.Position = here;
                if (peek is "8BIM" or "8B64") return;
            }
            r.Position = here + 1;
            steps++;
        }
    }

    private sealed class ChannelInfo
    {
        public short Id;
        public long Length;
    }

    private sealed class MaskInfo
    {
        public int Top, Left, Bottom, Right;
        public byte DefaultColor;
        public bool Disabled;
        public bool Inverted;
        public int Width  => Math.Max(0, Right - Left);
        public int Height => Math.Max(0, Bottom - Top);
    }

    private static void ReadLayerInfo(BigEndianReader r, PsdFile psd, long end)
    {
        int count = Math.Abs(r.ReadInt16());
        var layers   = new List<PsdLayer>(count);
        var channels = new List<List<ChannelInfo>>(count);
        var masks    = new List<MaskInfo?>(count);

        for (int i = 0; i < count; i++)
        {
            var layer = new PsdLayer
            {
                Top    = r.ReadInt32(),
                Left   = r.ReadInt32(),
                Bottom = r.ReadInt32(),
                Right  = r.ReadInt32(),
            };

            int nch = r.ReadUInt16();
            var chs = new List<ChannelInfo>(nch);
            for (int c = 0; c < nch; c++)
                chs.Add(new ChannelInfo { Id = r.ReadInt16(), Length = psd.IsPsb ? (long)r.ReadUInt64() : r.ReadUInt32() });

            if (r.ReadAscii(4) != "8BIM") throw new InvalidDataException("Corrupt layer record (blend mode signature).");
            layer.BlendMode = r.ReadAscii(4);
            layer.Opacity   = r.ReadByte();
            layer.Clipped   = r.ReadByte() != 0;
            int flags       = r.ReadByte();
            layer.Visible   = (flags & 0x02) == 0;
            r.Skip(1);

            long extraLength = r.ReadUInt32();
            long extraEnd = r.Position + extraLength;

            masks.Add(ReadMask(r));

            long blendRanges = r.ReadUInt32();
            r.Skip(blendRanges);

            int nameLen = r.ReadByte();
            layer.Name = Encoding.Latin1.GetString(r.ReadBytes(nameLen));
            int padded = ((nameLen + 1 + 3) / 4) * 4;
            r.Skip(padded - nameLen - 1);

            ReadAdditionalInfo(r, psd, layer, extraEnd);
            r.Position = extraEnd;

            layers.Add(layer);
            channels.Add(chs);
        }

        // channel image data follows every record, layer by layer in the same order; the channel lengths are absolute, so a
        // layer that fails to decode is stepped over by its own byte count and the layers after it still read
        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            long start = r.Position;
            long total = 0;
            foreach (var ch in channels[i]) total += ch.Length;
            try
            {
                ReadLayerPixels(r, psd, layer, channels[i], masks[i]);
            }
            catch (Exception ex)
            {
                psd.Warnings.Add($"Could not read the pixels of \"{layer.Name}\": {ex.Message}");
                layer.Pixels = null;
            }
            r.Position = start + total;
        }

        psd.Layers.AddRange(layers);
    }

    private static MaskInfo? ReadMask(BigEndianReader r)
    {
        long length = r.ReadUInt32();
        if (length == 0) return null;
        long end = r.Position + length;

        var mask = new MaskInfo
        {
            Top    = r.ReadInt32(),
            Left   = r.ReadInt32(),
            Bottom = r.ReadInt32(),
            Right  = r.ReadInt32(),
            DefaultColor = r.ReadByte(),
        };
        int flags = r.ReadByte();
        mask.Disabled = (flags & 0x02) != 0;
        mask.Inverted = (flags & 0x04) != 0;

        r.Position = end;
        return mask;
    }

    private static void ReadAdditionalInfo(BigEndianReader r, PsdFile psd, PsdLayer layer, long end)
    {
        while (r.Position + 12 <= end)
        {
            var sig = r.ReadAscii(4);
            if (sig is not ("8BIM" or "8B64")) break;
            var key = r.ReadAscii(4);
            long len = psd.IsPsb && IsLongKey(key) ? (long)r.ReadUInt64() : r.ReadUInt32();
            long blockEnd = r.Position + len;

            try
            {
                switch (key)
                {
                    case "luni":
                    {
                        int chars = (int)r.ReadUInt32();
                        var name = Encoding.BigEndianUnicode.GetString(r.ReadBytes(chars * 2)).TrimEnd('\0');
                        if (name.Length > 0) layer.Name = name;
                        break;
                    }
                    case "lsct":
                    case "lsdk":
                        layer.Section = (PsdSection)Math.Clamp((int)r.ReadUInt32(), 0, 3);
                        break;
                    case "lspf":
                        // bit 0 transparency, bit 1 composite (pixels), bit 2 position; 0x80000000 is "lock all"
                        layer.Locked = (r.ReadUInt32() & 0x80000007) != 0;
                        break;
                    case "iOpa":
                        layer.FillOpacity = r.ReadByte();
                        break;
                    case "TySh":
                        layer.Text = PsdTextReader.ReadTypeTool(r, blockEnd);
                        break;
                }
            }
            catch (Exception ex)
            {
                psd.Warnings.Add($"Skipped the '{key}' data of \"{layer.Name}\": {ex.Message}");
            }

            r.Position = blockEnd;
            AlignTo(r, 4, end);
        }
    }

    private static void ReadLayerPixels(BigEndianReader r, PsdFile psd, PsdLayer layer, List<ChannelInfo> chs, MaskInfo? mask)
    {
        int w = layer.Width, h = layer.Height;
        int bytesPerSample = psd.Depth / 8;

        byte[]? red = null, green = null, blue = null, black = null, alpha = null, userMask = null;
        foreach (var ch in chs)
        {
            long start = r.Position;
            if (ch.Length < 2) { r.Position = start + ch.Length; continue; }

            // a layer mask channel has the mask's own rectangle, every other channel the layer's
            int cw = w, chh = h;
            if (ch.Id == -2 && mask is not null) { cw = mask.Width; chh = mask.Height; }
            else if (ch.Id is -3) { r.Position = start + ch.Length; continue; }   // vector "real" mask, not needed

            byte[]? plane = cw > 0 && chh > 0
                ? ReadChannel(r, psd, cw, chh, bytesPerSample, start + ch.Length)
                : null;
            r.Position = start + ch.Length;

            switch (ch.Id)
            {
                case 0:  red   = plane; break;
                case 1:  green = plane; break;
                case 2:  blue  = plane; break;
                case 3:  black = plane; break;
                case -1: alpha = plane; break;
                case -2: userMask = plane; break;
            }
        }

        if (w <= 0 || h <= 0 || red is null) { layer.Pixels = null; return; }

        var rgba = new byte[w * h * 4];
        for (int i = 0, p = 0; i < w * h; i++, p += 4)
        {
            byte rr, gg, bb;
            if (psd.ColorMode == 1)
            {
                rr = gg = bb = red[i];
            }
            else if (psd.ColorMode == 4)
            {
                // CMYK samples are stored inverted (255 = no ink); a plain conversion, not a colour-managed one
                double c = 1 - red[i] / 255.0, m = 1 - (green?[i] ?? 255) / 255.0, y = 1 - (blue?[i] ?? 255) / 255.0, k = 1 - (black?[i] ?? 255) / 255.0;
                rr = (byte)Math.Round(255 * (1 - c) * (1 - k));
                gg = (byte)Math.Round(255 * (1 - m) * (1 - k));
                bb = (byte)Math.Round(255 * (1 - y) * (1 - k));
            }
            else
            {
                rr = red[i]; gg = green?[i] ?? red[i]; bb = blue?[i] ?? red[i];
            }
            rgba[p] = rr; rgba[p + 1] = gg; rgba[p + 2] = bb;
            rgba[p + 3] = alpha?[i] ?? 255;
        }

        // an enabled layer mask cuts the layer the way Photoshop shows it; outside its rectangle the mask's default colour applies
        if (mask is not null && !mask.Disabled && (userMask is not null || mask.DefaultColor != 255))
        {
            for (int y = 0; y < h; y++)
            {
                int docY = layer.Top + y;
                for (int x = 0; x < w; x++)
                {
                    int docX = layer.Left + x;
                    int mx = docX - mask.Left, my = docY - mask.Top;
                    byte m = userMask is not null && mx >= 0 && my >= 0 && mx < mask.Width && my < mask.Height
                        ? userMask[my * mask.Width + mx]
                        : mask.DefaultColor;
                    if (mask.Inverted) m = (byte)(255 - m);
                    int a = (y * w + x) * 4 + 3;
                    rgba[a] = (byte)(rgba[a] * m / 255);
                }
            }
        }

        layer.Pixels = rgba;
    }

    /// <summary>one channel plane reduced to 8 bits per sample</summary>
    private static byte[] ReadChannel(BigEndianReader r, PsdFile psd, int w, int h, int bytesPerSample, long end)
    {
        int compression = r.ReadUInt16();
        int rowBytes = w * bytesPerSample;
        byte[] raw;

        switch (compression)
        {
            case 0:
                raw = r.ReadBytes(rowBytes * h);
                break;

            case 1:
            {
                // PackBits, row by row; the row byte counts come first (4 bytes each in a PSB)
                var counts = new int[h];
                for (int y = 0; y < h; y++) counts[y] = psd.IsPsb ? (int)r.ReadUInt32() : r.ReadUInt16();
                raw = new byte[rowBytes * h];
                for (int y = 0; y < h; y++)
                {
                    var packed = r.ReadBytes(counts[y]);
                    UnpackBits(packed, raw, y * rowBytes, rowBytes);
                }
                break;
            }

            case 2:
            case 3:
            {
                var compressed = r.ReadBytes((int)(end - r.Position));
                using var zs = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress);
                raw = new byte[rowBytes * h];
                int read = 0, n;
                while (read < raw.Length && (n = zs.Read(raw, read, raw.Length - read)) > 0) read += n;
                if (compression == 3) Unpredict(raw, w, h, bytesPerSample);
                break;
            }

            default:
                throw new InvalidDataException($"unknown compression {compression}");
        }

        if (bytesPerSample == 1) return raw;

        var plane = new byte[w * h];
        if (bytesPerSample == 2)
        {
            for (int i = 0; i < plane.Length; i++) plane[i] = raw[i * 2];   // high byte of the big-endian sample
        }
        else
        {
            for (int i = 0; i < plane.Length; i++)
            {
                float f = BitConverter.ToSingle(new[] { raw[i * 4 + 3], raw[i * 4 + 2], raw[i * 4 + 1], raw[i * 4] }, 0);
                // 32-bit documents are linear light; encode to sRGB so the colours match what Photoshop displays
                double v = Math.Clamp(f, 0f, 1f);
                v = v <= 0.0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1 / 2.4) - 0.055;
                plane[i] = (byte)Math.Round(v * 255);
            }
        }
        return plane;
    }

    private static void UnpackBits(byte[] src, byte[] dst, int offset, int length)
    {
        int i = 0, o = offset, end = offset + length;
        while (i < src.Length && o < end)
        {
            int n = (sbyte)src[i++];
            if (n >= 0)
            {
                int count = Math.Min(n + 1, Math.Min(src.Length - i, end - o));
                Buffer.BlockCopy(src, i, dst, o, count);
                i += n + 1; o += count;
            }
            else if (n != -128)
            {
                if (i >= src.Length) break;
                byte b = src[i++];
                int count = Math.Min(1 - n, end - o);
                for (int k = 0; k < count; k++) dst[o++] = b;
            }
        }
    }

    /// <summary>undoes ZIP-with-prediction: each sample was stored as the difference from the one to its left</summary>
    private static void Unpredict(byte[] data, int w, int h, int bytesPerSample)
    {
        if (bytesPerSample == 1)
        {
            for (int y = 0; y < h; y++)
                for (int x = 1; x < w; x++)
                    data[y * w + x] = (byte)(data[y * w + x] + data[y * w + x - 1]);
        }
        else if (bytesPerSample == 2)
        {
            for (int y = 0; y < h; y++)
                for (int x = 1; x < w; x++)
                {
                    int i = (y * w + x) * 2, p = i - 2;
                    int v = ((data[i] << 8) | data[i + 1]) + ((data[p] << 8) | data[p + 1]);
                    data[i] = (byte)(v >> 8); data[i + 1] = (byte)v;
                }
        }
        else
        {
            // 32-bit prediction works on the bytes of each row after they were split into planes; reverse both steps
            int rowBytes = w * 4;
            var row = new byte[rowBytes];
            for (int y = 0; y < h; y++)
            {
                int o = y * rowBytes;
                for (int x = 1; x < rowBytes; x++) data[o + x] = (byte)(data[o + x] + data[o + x - 1]);
                for (int x = 0; x < w; x++)
                {
                    row[x * 4]     = data[o + x];
                    row[x * 4 + 1] = data[o + w + x];
                    row[x * 4 + 2] = data[o + 2 * w + x];
                    row[x * 4 + 3] = data[o + 3 * w + x];
                }
                Buffer.BlockCopy(row, 0, data, o, rowBytes);
            }
        }
    }
}

/// <summary>big-endian primitives over a seekable stream</summary>
internal sealed class BigEndianReader
{
    private readonly Stream _s;
    private readonly byte[] _buf = new byte[8];

    public BigEndianReader(Stream s)
    {
        if (!s.CanSeek)
        {
            var ms = new MemoryStream();
            s.CopyTo(ms);
            ms.Position = 0;
            s = ms;
        }
        _s = s;
    }

    public long Position { get => _s.Position; set => _s.Position = value; }
    public long Length => _s.Length;

    private void Fill(int n)
    {
        int read = 0;
        while (read < n)
        {
            int got = _s.Read(_buf, read, n - read);
            if (got <= 0) throw new EndOfStreamException("The Photoshop file ends early.");
            read += got;
        }
    }

    public byte ReadByte() { Fill(1); return _buf[0]; }
    public ushort ReadUInt16() { Fill(2); return (ushort)((_buf[0] << 8) | _buf[1]); }
    public short ReadInt16() => (short)ReadUInt16();
    public uint ReadUInt32() { Fill(4); return (uint)((_buf[0] << 24) | (_buf[1] << 16) | (_buf[2] << 8) | _buf[3]); }
    public int ReadInt32() => (int)ReadUInt32();
    public ulong ReadUInt64() => ((ulong)ReadUInt32() << 32) | ReadUInt32();
    public long ReadInt64() => (long)ReadUInt64();

    public double ReadDouble()
    {
        Fill(8);
        var b = new byte[8];
        for (int i = 0; i < 8; i++) b[i] = _buf[7 - i];
        return BitConverter.ToDouble(b, 0);
    }

    public byte[] ReadBytes(long n)
    {
        if (n < 0 || _s.Position + n > _s.Length) throw new EndOfStreamException("The Photoshop file ends early.");
        var data = new byte[n];
        int read = 0;
        while (read < n)
        {
            int got = _s.Read(data, read, (int)(n - read));
            if (got <= 0) throw new EndOfStreamException("The Photoshop file ends early.");
            read += got;
        }
        return data;
    }

    public string ReadAscii(int n) => Encoding.ASCII.GetString(ReadBytes(n));

    public void Skip(long n) => _s.Position += n;
}
