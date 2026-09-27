using System.IO;

namespace PictureExifclone.Services;

/// <summary>
/// Replaces only the EXIF APP1 segment of a JPEG. Entropy-coded image data, ICC, XMP and all
/// other segments are copied byte for byte, so metadata changes never re-encode pixels.
/// </summary>
public static class JpegExifWriter
{
    private static ReadOnlySpan<byte> ExifHeader => "Exif\0\0"u8;

    public static byte[] ReplaceExif(ReadOnlySpan<byte> jpeg, ReadOnlySpan<byte> exif)
    {
        if (jpeg.Length < 4 || jpeg[0] != 0xFF || jpeg[1] != 0xD8) throw new InvalidDataException("Keine gültige JPEG-Datei.");
        if (exif.IsEmpty) throw new ArgumentException("EXIF-Daten sind leer.", nameof(exif));
        int payloadLength = (exif.StartsWith(ExifHeader) ? 0 : ExifHeader.Length) + exif.Length;
        if (payloadLength + 2 > ushort.MaxValue) throw new NotSupportedException("EXIF-Block überschreitet 64 KB; Datei bleibt unverändert.");

        int position = 2, insertAt = 2, exifStart = -1, exifEnd = -1;
        bool foundScan = false;
        while (position + 4 <= jpeg.Length)
        {
            if (jpeg[position] != 0xFF) throw new InvalidDataException("Beschädigte JPEG-Segmentstruktur.");
            byte marker = jpeg[position + 1];
            if (marker == 0xFF) { position++; continue; }
            if (marker is 0xDA or 0xD9) { foundScan = marker == 0xDA; break; }
            int length = (jpeg[position + 2] << 8) | jpeg[position + 3];
            if (length < 2 || position + 2 + length > jpeg.Length) throw new InvalidDataException("Beschädigtes JPEG-Segment.");
            if (marker == 0xE1 && length >= 8 && jpeg.Slice(position + 4, 6).SequenceEqual(ExifHeader))
            {
                if (exifStart >= 0) throw new NotSupportedException("Mehrere EXIF-Blöcke werden nicht geschrieben; Datei bleibt unverändert.");
                exifStart = position; exifEnd = position + 2 + length;
            }
            else if (marker == 0xE0 && insertAt == position) insertAt = position + 2 + length; // keep JFIF APP0 first
            position += 2 + length;
        }
        if (!foundScan) throw new InvalidDataException("JPEG ohne Bilddaten (SOS) gefunden.");

        using var output = new MemoryStream(jpeg.Length + payloadLength + 4);
        int before = exifStart >= 0 ? exifStart : insertAt, after = exifStart >= 0 ? exifEnd : insertAt;
        output.Write(jpeg[..before]);
        output.Write([0xFF, 0xE1, (byte)((payloadLength + 2) >> 8), (byte)((payloadLength + 2) & 0xFF)]);
        if (!exif.StartsWith(ExifHeader)) output.Write(ExifHeader);
        output.Write(exif);
        output.Write(jpeg[after..]);
        return output.ToArray();
    }
}
