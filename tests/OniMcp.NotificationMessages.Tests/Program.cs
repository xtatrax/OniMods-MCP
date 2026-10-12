using System;
using System.Collections.Generic;
using OniMcp.Tools;

public class Notification { }
public class MessageNotification : Notification { public Message message; }
public abstract class Message
{
    public abstract string GetMessageBody();
    public abstract string GetTooltip();
    public virtual void OnClick() { throw new Exception("Read must not click"); }
    public virtual void OnCleanUp() { throw new Exception("Read must not clear"); }
    public virtual bool ShowDialog() { throw new Exception("Read must not open dialog"); }
}
internal class SampleMessage : Message
{
    internal string Body = "<b>酸素</b>\n<link=OXYGEN>Oxygen</link>";
    internal string Tooltip = "説明";
    internal bool FailBody;
    internal bool FailTooltip;
    internal int BodyReads;
    public override string GetMessageBody() { BodyReads++; if (FailBody) throw new InvalidOperationException(); return Body; }
    public override string GetTooltip() { if (FailTooltip) throw new InvalidOperationException(); return Tooltip; }
}
internal static class Program
{
    private static Dictionary<string, object> Field(Dictionary<string, object> result, string field)
        { return (Dictionary<string, object>)result[field]; }
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); }
    private static void Main()
    {
        var source = new SampleMessage();
        var notification = new MessageNotification { message = source };
        var result = NotificationMessageReader.Read(notification);
        Check((string)Field(result, "body")["text"] == source.Body, "Native rich text/newlines preserved");
        Check((bool)Field(result, "body")["available"], "Body available");
        Check((string)Field(result, "tooltip")["text"] == "説明", "Tooltip separate");
        Check(source.BodyReads == 1 && notification.message == source, "Read leaves message attached");
        source.Body = "";
        Check((bool)Field(NotificationMessageReader.Read(notification), "body")["available"], "Empty is available");
        source.Body = null;
        Check((string)Field(NotificationMessageReader.Read(notification), "body")["reason"] == "returned_null", "Null is unavailable");
        source.FailBody = true;
        result = NotificationMessageReader.Read(notification);
        Check((string)Field(result, "body")["reason"] == "read_failed" && (bool)Field(result, "tooltip")["available"], "Body failure isolated");
        source.FailBody = false; source.Body = "本文"; source.FailTooltip = true;
        result = NotificationMessageReader.Read(notification);
        Check((bool)Field(result, "body")["available"] && !(bool)Field(result, "tooltip")["available"], "Tooltip failure isolated");
        Check((string)Field(NotificationMessageReader.Read(new Notification()), "body")["reason"] == "not_message_notification", "Ordinary notification distinguished");
        Check((string)Field(NotificationMessageReader.Read(new MessageNotification()), "body")["reason"] == "message_missing", "Missing message distinguished");
        Console.WriteLine("PASS: notification message text, availability, failures and non-click read contracts");
    }
}
