using System;
using System.Collections.Generic;

namespace OniMcp.Tools
{
    internal static class NotificationMessageReader
    {
        internal static Dictionary<string, object> Read(Notification notification)
        {
            var messageNotification = notification as MessageNotification;
            Message message = messageNotification?.message;
            string missing = messageNotification == null ? "not_message_notification" : "message_missing";
            return new Dictionary<string, object>
            {
                ["messageType"] = message?.GetType().FullName,
                ["format"] = "game_rich_text",
                ["body"] = ReadText(message == null ? (Func<string>)null : message.GetMessageBody,
                    "MessageNotification.message.GetMessageBody()", missing),
                ["tooltip"] = ReadText(message == null ? (Func<string>)null : message.GetTooltip,
                    "MessageNotification.message.GetTooltip()", missing)
            };
        }

        private static Dictionary<string, object> ReadText(Func<string> read, string source, string missing)
        {
            string text = null;
            string reason = missing;
            string errorType = null;
            if (read != null)
            {
                try
                {
                    text = read();
                    reason = text == null ? "returned_null" : null;
                }
                catch (Exception error)
                {
                    reason = "read_failed";
                    errorType = error.GetType().FullName;
                }
            }
            // A successfully read empty string is distinct from an unavailable value.
            return new Dictionary<string, object>
            {
                ["available"] = reason == null,
                ["text"] = text,
                ["source"] = source,
                ["reason"] = reason,
                ["errorType"] = errorType
            };
        }
    }
}
