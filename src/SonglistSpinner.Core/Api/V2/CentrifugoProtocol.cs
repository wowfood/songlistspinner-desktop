using System.Text.Json;
using SonglistSpinner.Core.Contracts;

namespace SonglistSpinner.Core.Api.V2;

internal static class CentrifugoProtocol
{
    public static string CreateConnectCommand(int id)
    {
        return JsonSerializer.Serialize(new { id, connect = new { } });
    }

    public static string CreateSubscribeCommand(int id, string channel)
    {
        return JsonSerializer.Serialize(new { id, subscribe = new { channel } });
    }

    public static bool IsApplicationPing(string message)
    {
        return message.AsSpan().Trim().SequenceEqual("{}".AsSpan());
    }

    public static bool IsCommandReply(string message, int commandId, out string? error)
    {
        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;
        if (!root.TryGetProperty("id", out var id) || id.GetInt32() != commandId)
        {
            error = null;
            return false;
        }

        if (root.TryGetProperty("error", out var errorElement))
        {
            error = errorElement.TryGetProperty("message", out var errorMessage)
                ? errorMessage.GetString() ?? "Centrifugo rejected the command."
                : "Centrifugo rejected the command.";
        }
        else
        {
            error = null;
        }

        return true;
    }

    public static bool IsDisconnect(string message)
    {
        using var document = JsonDocument.Parse(message);
        return document.RootElement.TryGetProperty("push", out var push) &&
               push.TryGetProperty("disconnect", out _);
    }

    public static bool TryParseNotification(string message, out StreamerSongListEvent? notification)
    {
        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;
        if (!root.TryGetProperty("push", out var push) ||
            !push.TryGetProperty("pub", out var publication) ||
            !publication.TryGetProperty("data", out var envelope) ||
            envelope.ValueKind != JsonValueKind.Object ||
            !envelope.TryGetProperty("type", out var typeProperty))
        {
            notification = null;
            return false;
        }

        var eventType = typeProperty.GetString();
        var kind = eventType switch
        {
            "now_playing_update" or "queue_add" or "queue_clear" or "queue_remove" or
                "queue_reorder" or "queue_update" => StreamerSongListEventKind.QueueChanged,
            "play_history_add" or "play_history_remove" => StreamerSongListEventKind.PlayHistoryChanged,
            _ => (StreamerSongListEventKind?)null
        };

        notification = kind.HasValue
            ? new StreamerSongListEvent(kind.Value, eventType)
            : null;
        return notification is not null;
    }
}
