using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GamebuinoAKA.Core.Models;

namespace GamebuinoAKA.Core.Services
{
    /// <summary>
    /// Implémentation série du protocole AKAT v1.1 (voir ITransferService). NON TESTÉE sur un vrai port —
    /// écrite et relue contre la spécification et contre l'implémentation de référence côté appareil
    /// (transfer.cpp, elle vérifiée par 18 contrôles automatiques sur le simulateur AKA-Love, écran
    /// interactif "Recevoir un code" compris), mais ce dépôt n'a pas d'accès à un port série physique
    /// pour aller plus loin. Le premier essai réel avec une console AKA branchée est le bon prochain test.
    /// </summary>
    public sealed class SerialTransferService : ITransferService
    {
        private static readonly byte[] Magic = { (byte)'A', (byte)'K', (byte)'A', (byte)'T' };
        private const byte Version = 1;
        private const byte CmdPing = 0x01;
        private const byte CmdPut = 0x02;
        private const int BaudRate = 115200;
        // 35 s : si l'appareil est sur son écran interactif "Recevoir un code" et que le fichier existe
        // déjà, il attend jusqu'à 30 s que le joueur choisisse (Écraser/Renommer/Annuler) avant de
        // répondre — voir docs/PROTOCOLE_TRANSFERT.md, section "Délai d'attente côté PC".
        private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(35);

        public async Task<TransferResult> PingAsync(string portName, CancellationToken cancellationToken = default)
        {
            using var port = OpenPort(portName);
            var frame = new byte[Magic.Length + 2];
            Magic.CopyTo(frame, 0);
            frame[4] = Version;
            frame[5] = CmdPing;
            return await SendAndReadReplyAsync(port, frame, cancellationToken).ConfigureAwait(false);
        }

        public async Task<TransferResult> PutFileAsync(string portName, CodeLanguage language, string devicePath,
            string localFilePath, CancellationToken cancellationToken = default)
        {
            var data = await File.ReadAllBytesAsync(localFilePath, cancellationToken).ConfigureAwait(false);
            var pathBytes = Encoding.UTF8.GetBytes(devicePath.Replace('\\', '/'));
            if (pathBytes.Length == 0 || pathBytes.Length >= 220)
                return new TransferResult(TransferStatus.BadPath, null);

            using var port = OpenPort(portName);
            var frame = BuildPutFrame((byte)language, pathBytes, data);
            return await SendAndReadReplyAsync(port, frame, cancellationToken).ConfigureAwait(false);
        }

        // ── Construction de la trame (miroir exact de akatransfer.py / transfer.cpp) ─────────────────

        private static byte[] BuildPutFrame(byte langId, byte[] pathBytes, byte[] data)
        {
            using var ms = new MemoryStream();
            ms.Write(Magic);
            ms.WriteByte(Version);
            ms.WriteByte(CmdPut);
            ms.WriteByte(langId);
            WriteU16(ms, (ushort)pathBytes.Length);
            ms.Write(pathBytes);
            WriteU32(ms, (uint)data.Length);
            ms.Write(data);
            WriteU32(ms, Crc32.Compute(data));
            return ms.ToArray();
        }

        private static void WriteU16(Stream s, ushort v)
        {
            Span<byte> b = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(b, v);
            s.Write(b);
        }

        private static void WriteU32(Stream s, uint v)
        {
            Span<byte> b = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(b, v);
            s.Write(b);
        }

        // ── Port et échange ────────────────────────────────────────────────────────────────────────

        private static SerialPort OpenPort(string portName)
        {
            var port = new SerialPort(portName, BaudRate) { ReadTimeout = (int)ReplyTimeout.TotalMilliseconds };
            port.Open();
            return port;
        }

        private static async Task<TransferResult> SendAndReadReplyAsync(SerialPort port, byte[] frame,
            CancellationToken cancellationToken)
        {
            port.Write(frame, 0, frame.Length);
            var deadline = DateTime.UtcNow + ReplyTimeout;

            var header = new byte[7];
            if (!await ReadExactAsync(port, header, deadline, cancellationToken).ConfigureAwait(false))
                return new TransferResult(TransferStatus.NoResponse, null);
            if (header[0] != Magic[0] || header[1] != Magic[1] || header[2] != Magic[2] || header[3] != Magic[3])
                return new TransferResult(TransferStatus.NoResponse, null);

            var status = (TransferStatus)header[5];
            var deviceId = header[6];

            // OK_RENAMED (v1.1, écran interactif) : la réponse continue par la longueur puis le chemin
            // final réellement utilisé — voir docs/PROTOCOLE_TRANSFERT.md.
            if (status != TransferStatus.OkRenamed)
                return new TransferResult(status, deviceId);

            var lenBuf = new byte[2];
            if (!await ReadExactAsync(port, lenBuf, deadline, cancellationToken).ConfigureAwait(false))
                return new TransferResult(TransferStatus.NoResponse, deviceId);
            var pathLen = BinaryPrimitives.ReadUInt16LittleEndian(lenBuf);
            var pathBuf = new byte[pathLen];
            if (!await ReadExactAsync(port, pathBuf, deadline, cancellationToken).ConfigureAwait(false))
                return new TransferResult(TransferStatus.NoResponse, deviceId);

            return new TransferResult(status, deviceId, Encoding.UTF8.GetString(pathBuf));
        }

        /// <summary>Lit exactement <c>buffer.Length</c> octets avant <paramref name="deadline"/> ; false sinon.</summary>
        private static async Task<bool> ReadExactAsync(SerialPort port, byte[] buffer, DateTime deadline,
            CancellationToken cancellationToken)
        {
            var got = 0;
            try
            {
                while (got < buffer.Length && DateTime.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (port.BytesToRead > 0)
                        got += port.Read(buffer, got, buffer.Length - got);
                    else
                        await Task.Delay(5, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (TimeoutException)
            {
                // laisse `got` tel quel : traité comme incomplet juste en dessous
            }
            return got == buffer.Length;
        }
    }

    /// <summary>CRC32 « zip » standard (polynôme 0xEDB88320) — même calcul que transfer.cpp et akatransfer.py.</summary>
    public static class Crc32
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; ++i)
            {
                var c = i;
                for (var k = 0; k < 8; ++k)
                    c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[i] = c;
            }
            return table;
        }

        public static uint Compute(byte[] data)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var b in data)
                crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return ~crc;
        }
    }
}
