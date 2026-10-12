using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using OniMcp.Support;
using OniMcp.Config;
using UnityEngine;

namespace OniMcp.Server
{
    /// <summary>
    /// MCP Streamable HTTP サーバーの実装
    /// System.Net.HttpListener（.NET Framework に組み込み）を使用する
    /// </summary>
    public partial class McpHttpServer : MonoBehaviour
    {
        private bool TryHandleSettingsRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            string path = request.Url == null ? string.Empty : request.Url.AbsolutePath;
            if (path != "/settings" && path != "/settings/")
                return false;

            var remoteAddress = request.RemoteEndPoint == null ? null : request.RemoteEndPoint.Address;
            if (remoteAddress == null || !IPAddress.IsLoopback(remoteAddress))
            {
                SendHtml(response, SettingsHtml("<h1>{{settings.error.title}}</h1><p>{{settings.error.localOnly}}</p>"), 403);
                return true;
            }

            if (request.HttpMethod == "GET")
            {
                SendHtml(response, RenderSettingsHtml(), 200);
                return true;
            }
            if (request.HttpMethod != "POST")
            {
                response.StatusCode = 405;
                response.Close();
                return true;
            }

            try
            {
                var body = HttpRequestBody.Read(request.InputStream, request.ContentEncoding, request.ContentLength64, HttpRequestBody.MaxSettingsBytes);
                var form = ParseQueryString(body);

                var current = OniMcpOptions.Current;
                var options = ParseSettingsForm(form, current);

                bool portChanged = options.Port != current.Port;
                OniMcpOptions.Save(options);
                string message = portChanged
                    ? McpLocalization.Format("settings.savedPort", null, options.Port)
                    : McpLocalization.Get("settings.saved");
                SendHtml(response, RenderSettingsHtml(message, true), 200);
                ScheduleSettingsRestartAfterResponse();
            }
            catch (RequestBodyTooLargeException ex)
            {
                SendHtml(response, RenderSettingsHtml(ex.Message, false), 413);
            }
            catch (Exception ex)
            {
                SendHtml(response, RenderSettingsHtml(McpLocalization.Format("settings.error.save", null, ex.Message), false), 400);
            }
            return true;
        }

        internal static OniMcpOptions ParseSettingsForm(Dictionary<string, string> form, OniMcpOptions current)
        {
            string host = RequiredFormValue(form, "host");
            if (!IsValidSettingsHost(host))
                throw new InvalidOperationException(McpLocalization.Get("settings.error.host"));
            int port = RequiredFormInt(form, "port", 1024, 65535);
            int retention = RequiredFormInt(form, "screenshotRetentionMinutes", 1, 10080);
            int maxFiles = RequiredFormInt(form, "screenshotMaxFiles", 1, 1000);
            string replacement = form.ContainsKey("authTokenReplacement")
                ? (form["authTokenReplacement"] ?? string.Empty).Trim()
                : string.Empty;

            return new OniMcpOptions
            {
                SecurityMigrationVersion = current.SecurityMigrationVersion,
                Language = OniMcpOptions.SubmittedLanguage(form, current.Language),
                Host = host,
                Port = port,
                AuthEnabled = form.ContainsKey("authEnabled") && form["authEnabled"] == "true",
                AuthToken = string.IsNullOrEmpty(replacement) ? current.AuthToken : replacement,
                RequirePrintingPodSelection = form.ContainsKey("printingPodPolicyPresent") || form.ContainsKey("requirePrintingPodSelection")
                    ? form.ContainsKey("requirePrintingPodSelection") && form["requirePrintingPodSelection"] == "true"
                    : current.RequirePrintingPodSelection,
                GlobalAutoDisinfectDisabled = form.ContainsKey("globalAutoDisinfectDisabled") && form["globalAutoDisinfectDisabled"] == "true",
                ScreenshotCleanupEnabled = form.ContainsKey("screenshotCleanupEnabled") && form["screenshotCleanupEnabled"] == "true",
                ScreenshotRetentionMinutes = retention,
                ScreenshotMaxFiles = maxFiles
            };
        }

        private static void ScheduleSettingsRestartAfterResponse()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(250);
                try
                {
                    MainThreadBridge.EnqueueDeferred(() =>
                    {
                        if (Instance != null)
                            Instance.RestartServer();
                    });
                }
                catch (InvalidOperationException)
                {
                    // The game may destroy the bridge before this delayed callback runs.
                }
            });
        }

        private static string RequiredFormValue(Dictionary<string, string> form, string key)
        {
            string value = form.ContainsKey(key) ? (form[key] ?? string.Empty).Trim() : string.Empty;
            if (string.IsNullOrEmpty(value))
                throw new InvalidOperationException(McpLocalization.Format("settings.error.required", null, key));
            return value;
        }

        private static int RequiredFormInt(Dictionary<string, string> form, string key, int minimum, int maximum)
        {
            string value = RequiredFormValue(form, key);
            if (!int.TryParse(value, out int parsed) || parsed < minimum || parsed > maximum)
                throw new InvalidOperationException(McpLocalization.Format("settings.error.range", null, key, minimum, maximum));
            return parsed;
        }

        private static bool IsValidSettingsHost(string host)
        {
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
                || IPAddress.TryParse(host, out _);
        }

        private static string SettingsHtml(string template, bool forFormatting = false)
        {
            return Regex.Replace(template, @"\{\{(settings\.[A-Za-z.]+|language)\}\}", match =>
            {
                string text = match.Groups[1].Value == "language"
                    ? McpLocalization.CurrentLanguage : McpLocalization.Get(match.Groups[1].Value);
                // Translation prose is not part of the caller's composite-format syntax.
                if (forFormatting)
                    text = text.Replace("{", "{{").Replace("}", "}}");
                return WebUtility.HtmlEncode(text);
            });
        }

        private string RenderSettingsHtml(string message = null, bool success = true)
        {
            var options = OniMcpOptions.Current;
            var sb = new StringBuilder();
            sb.Append(SettingsHtml(@"<!doctype html>
<html lang=""{{language}}"">
<head>
<meta charset=""utf-8"">
<title>{{settings.title}}</title>
<style>
  body { font-family: system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, 'Open Sans', 'Helvetica Neue', sans-serif; background: #1a202c; color: #e2e8f0; margin: 0; padding: 40px; }
  .container { max-width: 600px; margin: 0 auto; background: #2d3748; padding: 30px; border-radius: 12px; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06); }
  h1 { margin-top: 0; color: #fff; font-size: 24px; border-bottom: 2px solid #4a5568; padding-bottom: 12px; }
  .form-group { margin-bottom: 20px; }
  label { display: block; margin-bottom: 6px; font-weight: 600; font-size: 14px; color: #cbd5e0; }
  select, input[type=""text""], input[type=""number""], input[type=""password""] { width: 100%; padding: 10px; border: 1px solid #4a5568; background: #1a202c; color: #fff; border-radius: 6px; box-sizing: border-box; }
  input[type=""text""]:focus, input[type=""number""]:focus, input[type=""password""]:focus { outline: none; border-color: #3182ce; }
  .checkbox-group { display: flex; align-items: center; gap: 8px; margin-top: 10px; }
  .checkbox-group label { display: inline; margin-bottom: 0; cursor: pointer; }
  input[type=""checkbox""] { width: 16px; height: 16px; cursor: pointer; }
  .btn { display: inline-block; background: #3182ce; color: #fff; border: none; padding: 12px 20px; border-radius: 6px; cursor: pointer; font-weight: bold; width: 100%; text-align: center; }
  .btn:hover { background: #2b6cb0; }
  .alert { padding: 12px; border-radius: 6px; margin-bottom: 20px; font-size: 14px; font-weight: bold; }
  .alert-success { background: #c6f6d5; color: #22543d; border: 1px solid #9ae6b4; }
  .alert-error { background: #fed7d7; color: #742a2a; border: 1px solid #feb2b2; }
  .help-text { font-size: 12px; color: #a0aec0; margin-top: 4px; }
  .back-link { display: inline-block; margin-bottom: 20px; color: #63b3ed; text-decoration: none; font-weight: bold; }
</style>
</head>
<body>
  <div class=""container"">
    <a class=""back-link"" href=""/"">{{settings.back}}</a>
    <h1>{{settings.title}}</h1>
"));
            if (!string.IsNullOrEmpty(message))
            {
                string alertClass = success ? "alert-success" : "alert-error";
                sb.AppendFormat("    <div class=\"alert {0}\">{1}</div>\n", alertClass, WebUtility.HtmlEncode(message));
            }

            sb.Append(SettingsHtml(@"    <form method=""POST"" action=""/settings"">
      <div class=""form-group""><label for=""language"">{{settings.language}}</label>
        <select id=""language"" name=""language"">"));
            foreach (string language in new[] { "auto", "en", "ja", "zh-Hans" })
            {
                string label = language == "auto" ? McpLocalization.Get("settings.language.auto")
                    : language == "ja" ? "日本語" : language == "zh-Hans" ? "简体中文" : "English";
                sb.AppendFormat("<option value=\"{0}\"{1}>{2}</option>", language,
                    options.Language == language ? " selected" : string.Empty, WebUtility.HtmlEncode(label));
            }
            sb.Append(SettingsHtml(@"</select><div class=""help-text"">{{settings.language.help}}</div></div>"));
            sb.AppendFormat(SettingsHtml(@"      <div class=""form-group"">
        <label for=""host"">{{settings.host}}</label>
        <input type=""text"" id=""host"" name=""host"" value=""{0}"" required />
        <div class=""help-text"">{{settings.host.help}}</div>
      </div>

      <div class=""form-group"">
        <label for=""port"">{{settings.port}}</label>
        <input type=""number"" id=""port"" name=""port"" value=""{1}"" min=""1024"" max=""65535"" required />
        <div class=""help-text"">{{settings.port.help}}</div>
      </div>

      <div class=""form-group"">
        <div class=""checkbox-group"">
          <input type=""checkbox"" id=""authEnabled"" name=""authEnabled"" value=""true"" {2} />
          <label for=""authEnabled"">{{settings.auth}}</label>
        </div>
        <div class=""help-text"">{{settings.auth.help}}</div>
      </div>

      <div class=""form-group"">
        <label for=""authTokenReplacement"">{{settings.token}}</label>
        <input type=""password"" id=""authTokenReplacement"" name=""authTokenReplacement"" value="""" autocomplete=""new-password"" />
        <div class=""help-text"">{{settings.token.help}}</div>
      </div>

      <div class=""form-group"">
        <div class=""checkbox-group"">
          <input type=""checkbox"" id=""globalAutoDisinfectDisabled"" name=""globalAutoDisinfectDisabled"" value=""true"" {3} />
          <label for=""globalAutoDisinfectDisabled"">{{settings.disinfect}}</label>
        </div>
        <div class=""help-text"">{{settings.disinfect.help}}</div>
      </div>

      <div class=""form-group"">
        <div class=""checkbox-group"">
          <input type=""hidden"" name=""printingPodPolicyPresent"" value=""true"" />
          <input type=""checkbox"" id=""requirePrintingPodSelection"" name=""requirePrintingPodSelection"" value=""true"" {7} />
          <label for=""requirePrintingPodSelection"">{{settings.printingPod}}</label>
        </div>
        <div class=""help-text"">{{settings.printingPod.help}}</div>
      </div>

      <div class=""form-group"">
        <div class=""checkbox-group"">
          <input type=""checkbox"" id=""screenshotCleanupEnabled"" name=""screenshotCleanupEnabled"" value=""true"" {4} />
          <label for=""screenshotCleanupEnabled"">{{settings.screenshots}}</label>
        </div>
      </div>

      <div class=""form-group"">
        <label for=""screenshotRetentionMinutes"">{{settings.retention}}</label>
        <input type=""number"" id=""screenshotRetentionMinutes"" name=""screenshotRetentionMinutes"" value=""{5}"" min=""1"" required />
      </div>

      <div class=""form-group"">
        <label for=""screenshotMaxFiles"">{{settings.maxFiles}}</label>
        <input type=""number"" id=""screenshotMaxFiles"" name=""screenshotMaxFiles"" value=""{6}"" min=""1"" required />
      </div>

      <button type=""submit"" class=""btn"">{{settings.save}}</button>
    </form>
  </div>
</body>
</html>", forFormatting: true),
                WebUtility.HtmlEncode(options.Host),
                options.Port,
                options.AuthEnabled ? "checked" : "",
                options.GlobalAutoDisinfectDisabled ? "checked" : "",
                options.ScreenshotCleanupEnabled ? "checked" : "",
                options.ScreenshotRetentionMinutes,
                options.ScreenshotMaxFiles,
                options.RequirePrintingPodSelection ? "checked" : ""
            );

            return sb.ToString();
        }
    }
}
