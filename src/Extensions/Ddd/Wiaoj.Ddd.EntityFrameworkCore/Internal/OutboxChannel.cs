using System.Threading.Channels;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;

namespace Wiaoj.Ddd.EntityFrameworkCore.Internal; 
public sealed class OutboxChannel {
    private readonly Channel<OutboxMessage> _channel = Channel.CreateUnbounded<OutboxMessage>();

    public ChannelReader<OutboxMessage> Reader => this._channel.Reader;
    public ChannelWriter<OutboxMessage> Writer => this._channel.Writer;
}