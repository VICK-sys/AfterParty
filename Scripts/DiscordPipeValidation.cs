using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using DiscordRPC.IO;
using DiscordRPC.Logging;
using DiscordRPC.Unity;

public static class DiscordPipeValidation
{
    public static int Main(string[] args)
    {
        bool baseline = args.Length > 0 && args[0] == "baseline";
        using (var server = new NamedPipeServerStream("discord-ipc-9", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096))
        using (var pipe = new UnityNamedPipe { Logger = new NullLogger() })
        {
            var connection = server.BeginWaitForConnection(null, null);
            if (!pipe.Connect(9)) throw new Exception("Connection failed");
            server.EndWaitForConnection(connection);
            bool received = false;
            var reader = new Thread(() => received = pipe.ReadFrame(out var frame)) { IsBackground = true };
            var timer = Stopwatch.StartNew();
            reader.Start();
            bool returned = reader.Join(300);
            Console.WriteLine("Idle read returned=" + returned + ", elapsedMs=" + timer.ElapsedMilliseconds);
            byte[] message = { 3, 0, 0, 0, 2, 0, 0, 0, 123, 125 };
            if (baseline)
            {
                server.Write(message, 0, message.Length);
                server.Flush();
                if (!reader.Join(2000) || returned || !received) throw new Exception("Baseline did not demonstrate blocking read");
                Console.WriteLine("PASS blocking control");
                return 0;
            }
            if (!returned || received) throw new Exception("Idle pipe blocked or fabricated a frame");
            Console.WriteLine("Partial header");
            server.Write(message, 0, 4);
            server.Flush();
            if (pipe.ReadFrame(out var partialHeader)) throw new Exception("Partial header accepted");
            Console.WriteLine("Partial body");
            server.Write(message, 4, 5);
            server.Flush();
            if (pipe.ReadFrame(out var partialBody)) throw new Exception("Partial payload accepted");
            Console.WriteLine("Complete frames");
            server.Write(message, 9, 1);
            server.Write(message, 0, message.Length);
            server.Flush();
            if (!pipe.ReadFrame(out var first) || !pipe.ReadFrame(out var second)) throw new Exception("Queued frames lost");
            if (first.Opcode != Opcode.Ping || second.Opcode != Opcode.Ping) throw new Exception("Frame opcode changed");
            if (pipe.ReadFrame(out var empty)) throw new Exception("Queue did not drain");
            Console.WriteLine("Disconnect");
            server.Disconnect();
            if (pipe.ReadFrame(out var disconnected) || pipe.IsConnected) throw new Exception("Disconnected pipe remained connected");
            pipe.Dispose();
            pipe.Dispose();
            Console.WriteLine("PASS idle, partial header, partial payload, queued frames, disconnect, repeated disposal");
            return 0;
        }
    }
}


