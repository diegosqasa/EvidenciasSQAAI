/*
 * EvidenciasSQA - a free and open source screenshot tool
 * Copyright (C) 2004-2026 Thomas Braun, Jens Klingen, Robin Krom
 *
 * For more information see: https://evidenciassqa.com/
 * The EvidenciasSQA project is hosted on GitHub https://github.com/evidenciassqa/evidenciassqa
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 1 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.IO;
using log4net;

namespace EvidenciasSQA.Helpers
{
    /// <summary>
    /// Inyecta el chunk pHYs (96 DPI) en un PNG, replicando injectPhysDpi del
    /// image-worker del Electron.
    /// 96 DPI = 3779 pixels/meter (pixels per metre, unit=1).
    /// GDI+ escribe un pHYs propio (96 DPI = 3779 px/m) al guardar con
    /// ImageFormat.Png, por lo que un pHYs existente se REEMPLAZA in-place:
    /// el estándar PNG dicta que el último pHYs prevalece, así que insertar
    /// uno nuevo no bastaría. Solo si no existe se inserta tras IHDR.
    /// Nota (Fase 18): el valor pasó de 300 DPI (11811 px/m) a 96 DPI (3779)
    /// por corrección del pipeline; con pHYs 300 el visor WPF (Stretch=None)
    /// renderizaba el zoom 1:1 al 32 % (614×362 en vez de 1920×1132) sin
    /// scrollbars, rompiendo el zoom del contenedor. El reemplazo in-place
    /// además corrige PNGs antiguos horneados con 300 DPI.
    /// </summary>
    public static class PngPhysChunk
    {
        private const uint PixelsPerMeter = 3779;

        private static readonly ILog Log = LogManager.GetLogger(typeof(PngPhysChunk));

        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] PhysType = { 0x70, 0x48, 0x59, 0x73 }; // "pHYs"
        private static readonly uint[] CrcTable = BuildCrcTable();

        /// <summary>
        /// Inyecta el chunk pHYs 96 DPI en el PNG indicado (best-effort, in-place).
        /// No hace nada si el archivo no es PNG. Los fallos se registran en el log
        /// (nunca rompen el flujo de guardado).
        /// </summary>
        public static void Inject96Dpi(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return;
                }

                byte[] data = File.ReadAllBytes(filePath);
                byte[] result = Inject96Dpi(data);
                if (!ReferenceEquals(result, data))
                {
                    File.WriteAllBytes(filePath, result);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("[SQA-INTEGRATION] No se pudo inyectar pHYs 96 DPI (best-effort).", ex);
            }
        }

        /// <summary>
        /// Inyecta el chunk pHYs 96 DPI en un buffer PNG. Si el PNG ya trae pHYs
        /// (GDI+ siempre escribe 96 DPI), se reemplazan sus valores y se recalcula
        /// el CRC. Si no trae, se inserta justo después de IHDR.
        /// </summary>
        public static byte[] Inject96Dpi(byte[] data)
        {
            if (data == null || data.Length < 8 || !IsPng(data))
            {
                return data;
            }

            // Pasada 1: pHYs existente (el que escribe GDI+ con 96 DPI) → reemplazo
            // in-place de x/y/unit y recálculo del CRC para no corromper el PNG.
            int pos = 8;
            while (pos + 12 <= data.Length)
            {
                int chunkLen = ReadBe32(data, pos);
                string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
                if (type == "pHYs")
                {
                    var result = (byte[])data.Clone();
                    WriteBe32(result, pos + 8, PixelsPerMeter);
                    WriteBe32(result, pos + 12, PixelsPerMeter);
                    result[pos + 16] = 1; // unit = metro
                    WriteBe32(result, pos + 17, PngCrc(result, pos + 4, 13));
                    return result;
                }

                pos += 12 + chunkLen;
            }

            // Pasada 2: sin pHYs previo → insertar justo después del chunk IHDR completo.
            pos = 8;
            if (pos + 12 <= data.Length)
            {
                int ihdrLen = ReadBe32(data, pos);
                if (System.Text.Encoding.ASCII.GetString(data, pos + 4, 4) == "IHDR")
                {
                    return InsertPhysChunk(data, pos + 12 + ihdrLen);
                }
            }

            return data;
        }

        private static byte[] InsertPhysChunk(byte[] data, int insertAt)
        {
            // Chunk: len(4 BE) + "pHYs"(4) + data(9) + crc(4) = 21 bytes.
            var chunk = new byte[21];
            WriteBe32(chunk, 0, 9);
            Array.Copy(PhysType, 0, chunk, 4, 4);
            WriteBe32(chunk, 8, PixelsPerMeter);
            WriteBe32(chunk, 12, PixelsPerMeter);
            chunk[16] = 1; // unit = metro

            // CRC32 PNG sobre tipo(4) + data(9).
            var crcInput = new byte[13];
            Array.Copy(PhysType, 0, crcInput, 0, 4);
            Array.Copy(chunk, 8, crcInput, 4, 9);
            WriteBe32(chunk, 17, PngCrc(crcInput, 0, crcInput.Length));

            var result = new byte[data.Length + chunk.Length];
            Array.Copy(data, 0, result, 0, insertAt);
            Array.Copy(chunk, 0, result, insertAt, chunk.Length);
            Array.Copy(data, insertAt, result, insertAt + chunk.Length, data.Length - insertAt);
            return result;
        }

        private static bool IsPng(byte[] data)
        {
            for (int i = 0; i < PngSignature.Length; i++)
            {
                if (data[i] != PngSignature[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static int ReadBe32(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }

        private static void WriteBe32(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                }

                table[n] = c;
            }

            return table;
        }

        private static uint PngCrc(byte[] data, int offset, int length)
        {
            uint crc = 0xFFFFFFFF;
            for (int i = 0; i < length; i++)
            {
                crc = CrcTable[(crc ^ data[offset + i]) & 0xFF] ^ (crc >> 8);
            }

            return crc ^ 0xFFFFFFFF;
        }
    }
}