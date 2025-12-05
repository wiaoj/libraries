using System;
using System.Collections.Generic;
using System.Text;
using Wiaoj.Results;

namespace Wiaoj.Consensus.Raft.Abstractions;

/// <summary>
/// Represents the payload of a command to be applied to the state machine.
/// This value object prevents primitive obsession and allows for central validation.
/// </summary>
public readonly record struct CommandPayload(ReadOnlyMemory<byte> Value) {
    public static readonly CommandPayload Empty = new(ReadOnlyMemory<byte>.Empty);

    public static Result<CommandPayload> Create(ReadOnlyMemory<byte> value, int maxSizeInBytes = 1_048_576) { // Default 1MB max
        if (value.Length > maxSizeInBytes) {
            return Error.Validation.InvalidFormat("Command.TooLarge", $"Command payload size ({value.Length} bytes) exceeds the maximum allowed size ({maxSizeInBytes} bytes).");
        }
        return new CommandPayload(value);
    }
}