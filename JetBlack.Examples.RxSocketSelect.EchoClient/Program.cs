﻿using System;
using System.Net.Sockets;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBlack.Examples.Common;
using JetBlack.Network.Common;
using JetBlack.Network.RxSocketSelect;
using JetBlack.Network.RxSocketSelect.Sockets;

namespace JetBlack.Examples.RxSocketSelect.EchoClient
{
    class Program
    {
        static void Main(string[] args)
        {
            var endpoint = ProgramArgs.Parse(args, new[] { "127.0.0.1:9211" }).EndPoint;

            var cts = new CancellationTokenSource();
            var bufferManager = BufferManager.CreateBufferManager(2 << 16, 2 << 8);

            var selector = new Selector();
            Task.Factory.StartNew(() => selector.Dispatch(60000000, cts.Token), cts.Token);

            endpoint.ToConnectObservable(selector, cts.Token)
                .Subscribe(socket =>
                {
                    var frameClientSubject = socket.ToFrameClientSubject(SocketFlags.None, bufferManager, selector, cts.Token);

                    var observerDisposable =
                        frameClientSubject
                            .ObserveOn(TaskPoolScheduler.Default)
                            .Subscribe(
                                disposableBuffer =>
                                {
                                    Console.WriteLine("Read: " + Encoding.UTF8.GetString(disposableBuffer.Value.Array, 0, disposableBuffer.Value.Count));
                                    disposableBuffer.Dispose();
                                },
                                error => Console.WriteLine("Error: " + error.Message),
                                () => Console.WriteLine("OnCompleted: FrameReceiver"));

                    Console.In.ToLineObservable()
                        .Subscribe(
                            line =>
                            {
                                var writeBuffer = Encoding.UTF8.GetBytes(line);
                                frameClientSubject.OnNext(DisposableValue.Create(new ArraySegment<byte>(writeBuffer, 0, writeBuffer.Length), Disposable.Empty));
                            },
                            error => Console.WriteLine("Error: " + error.Message),
                            () => Console.WriteLine("OnCompleted: LineReader"));

                    observerDisposable.Dispose();

                    cts.Cancel();
                });

            cts.Token.WaitHandle.WaitOne();
        }
    }
}
