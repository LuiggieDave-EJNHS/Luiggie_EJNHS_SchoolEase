using System.Net;
using System.Text;
using SchoolEase.Web.Models;

namespace SchoolEase.Web.Services;

/// <summary>
/// Builds the HTML body for emails SchoolEase sends.
/// The markup is table based with inline styles on purpose: mail clients strip
/// external stylesheets and ignore modern CSS layout, so this is the portable way.
/// </summary>
public static class EmailTemplates
{
    private const string Brand = "SchoolEase";
    private const string School = "Emilio Jacinto National High School";
    private const string Font = "-apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

    // Brand palette, kept in sync with wwwroot/css/schoolease.css
    private const string Primary = "#800000";
    private const string Navy = "#26060a";
    private const string NavyText = "#e0cfc9";
    private const string Ink = "#16202c";
    private const string InkSoft = "#3b4855";
    private const string Muted = "#6b7a90";
    private const string Line = "#e4e9f2";
    private const string Bg = "#f4f6fb";
    private const string Success = "#1aa179";

    // ------------------------------------------------------------------
    // Public templates
    // ------------------------------------------------------------------

    /// <summary>Activation email sent immediately after a student registers.</summary>
    public static string AccountActivation(string? fullName, string confirmationLink, string logoUrl)
    {
        var greeting = string.IsNullOrWhiteSpace(fullName)
            ? "Hello,"
            : $"Hi {WebUtility.HtmlEncode(fullName.Trim())},";

        // Encode so query-string "&" stays valid inside the href attribute.
        var link = WebUtility.HtmlEncode(confirmationLink);

        var content = $"""
            {Heading("Activate your account")}
            {Paragraph(greeting)}
            {Paragraph($"Welcome to <strong>{Brand}</strong>, the online portal of {School}. Your registration was received &mdash; confirm your email address to finish setting up your account.")}
            {Button("Activate my account", confirmationLink)}
            <p style="margin:0 0 6px;font-family:{Font};font-size:13px;line-height:20px;color:{Muted};">
              Button not working? Copy this link into your browser:
            </p>
            <p style="margin:0 0 24px;font-family:{Font};font-size:13px;line-height:20px;word-break:break-all;">
              <a href="{link}" style="color:{Primary};text-decoration:underline;">{link}</a>
            </p>
            {CalloutBox("Didn't sign up?", "You can safely ignore this email. No account is activated unless you click the button above.")}
            """;

        return Shell("Confirm your email address to finish setting up your SchoolEase account.", content, logoUrl);
    }

    /// <summary>Confirmation sent to a learner once the registrar releases a requested document.</summary>
    public static string DocumentReleased(
        string? fullName,
        string requestNumber,
        string documentName,
        int copies,
        DateTime releasedAt,
        string? remarks,
        string? portalUrl,
        string logoUrl)
    {
        var greeting = string.IsNullOrWhiteSpace(fullName)
            ? "Hello,"
            : $"Hi {WebUtility.HtmlEncode(fullName.Trim())},";

        var rows = new List<(string Label, string Value)>
        {
            ("Request no.", requestNumber),
            ("Document", documentName),
            ("Copies", copies.ToString()),
            ("Released on", releasedAt.ToLocalTime().ToString("MMM d, yyyy h:mm tt"))
        };

        var content = $"""
            {Heading("Your document has been released")}
            {Paragraph(greeting)}
            {Paragraph($"Your request for <strong>{WebUtility.HtmlEncode(documentName)}</strong> has been processed and released. Keep this email as your official record of the transaction.")}
            {Banner("Released", Success)}
            {DetailsCard(rows)}
            {(string.IsNullOrWhiteSpace(remarks) ? string.Empty : CalloutBox("Registrar's remarks", WebUtility.HtmlEncode(remarks)))}
            {(string.IsNullOrWhiteSpace(portalUrl) ? string.Empty : Button("View request", portalUrl))}
            {CalloutBox("Questions?", $"If you did not receive your document or need help, please contact the registrar's office of {School}.")}
            """;

        return Shell($"Request {requestNumber} for {documentName} has been released.", content, logoUrl);
    }

    /// <summary>Wraps a plain administrative message (the Emailer composer) in the branded shell.</summary>
    public static string Message(string subject, string bodyHtml, string logoUrl)
    {
        var heading = string.IsNullOrWhiteSpace(subject) ? $"Message from {Brand}" : subject;

        var content = $"""
            {Heading(heading)}
            {Paragraph(bodyHtml)}
            """;

        return Shell(subject, content, logoUrl);
    }

    // ------------------------------------------------------------------
    // Reusable content blocks
    // ------------------------------------------------------------------

    private static string Heading(string text) => $"""
        <h1 style="margin:0 0 10px;font-family:{Font};font-size:22px;line-height:30px;font-weight:700;color:{Ink};">{WebUtility.HtmlEncode(text)}</h1>
        """;

    private static string Paragraph(string html) => $"""
        <p style="margin:0 0 18px;font-family:{Font};font-size:15px;line-height:24px;color:{InkSoft};">{html}</p>
        """;

    private static string Button(string label, string href)
    {
        var link = WebUtility.HtmlEncode(href);
        return $"""
            <table role="presentation" cellpadding="0" cellspacing="0" style="margin:4px 0 24px;">
              <tr>
                <td align="center" bgcolor="{Primary}" style="border-radius:10px;">
                  <a href="{link}"
                     style="display:inline-block;padding:14px 34px;font-family:{Font};font-size:15px;font-weight:600;line-height:20px;color:#ffffff;text-decoration:none;border-radius:10px;">
                    {WebUtility.HtmlEncode(label)}
                  </a>
                </td>
              </tr>
            </table>
            """;
    }

    /// <summary>Coloured status strip (table based so it renders in Outlook).</summary>
    private static string Banner(string label, string background) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{background};border-radius:10px;margin:0 0 22px;">
          <tr>
            <td align="center" style="padding:12px 16px;font-family:{Font};font-size:12px;font-weight:700;letter-spacing:.6px;color:#ffffff;text-transform:uppercase;">
              {WebUtility.HtmlEncode(label)}
            </td>
          </tr>
        </table>
        """;

    /// <summary>Bordered key/value card used to summarise a transaction.</summary>
    private static string DetailsCard(IEnumerable<(string Label, string Value)> rows)
    {
        var sb = new StringBuilder();
        var index = 0;
        foreach (var (label, value) in rows)
        {
            var border = index++ == 0 ? "none" : $"1px solid {Line}";
            sb.Append($"""
                <tr>
                  <td style="padding:11px 0;border-top:{border};font-family:{Font};font-size:13px;line-height:20px;color:{Muted};width:45%;vertical-align:top;">{WebUtility.HtmlEncode(label)}</td>
                  <td style="padding:11px 0;border-top:{border};font-family:{Font};font-size:14px;line-height:20px;color:{Ink};font-weight:600;text-align:right;">{WebUtility.HtmlEncode(value)}</td>
                </tr>
                """);
        }

        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border:1px solid {Line};border-radius:12px;margin:0 0 22px;">
              <tr>
                <td style="padding:2px 18px 8px;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                    {sb}
                  </table>
                </td>
              </tr>
            </table>
            """;
    }

    private static string CalloutBox(string title, string html) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{Bg};border-radius:12px;margin:0 0 18px;">
          <tr>
            <td style="padding:16px 18px;font-family:{Font};font-size:13px;line-height:20px;color:{Muted};">
              <strong style="color:{InkSoft};">{WebUtility.HtmlEncode(title)}</strong><br>{html}
            </td>
          </tr>
        </table>
        """;

    /// <summary>Shared outer shell so every message looks like the same brand.</summary>
    private static string Shell(string preheader, string content, string logoUrl)
    {
        var logo = string.IsNullOrWhiteSpace(logoUrl)
            ? string.Empty
            : $"""<img src="{logoUrl}" alt="{Brand}" height="44" style="display:block;height:44px;width:auto;border-radius:8px;">""";

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <meta name="color-scheme" content="light">
              <title>{Brand}</title>
            </head>
            <body style="margin:0;padding:0;background:{Bg};">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:{Bg};font-size:1px;line-height:1px;">
                {WebUtility.HtmlEncode(preheader)}&#847;&zwnj;&nbsp;&#847;&zwnj;&nbsp;&#847;&zwnj;&nbsp;&#847;&zwnj;&nbsp;
              </div>

              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{Bg};">
                <tr>
                  <td align="center" style="padding:36px 16px;">

                    <table role="presentation" width="600" cellpadding="0" cellspacing="0"
                           style="width:100%;max-width:600px;background:#ffffff;border:1px solid {Line};border-radius:16px;overflow:hidden;">

                      <tr>
                        <td style="background:{Navy};padding:26px 32px;">
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                            <tr>
                              <td style="padding-right:14px;" valign="middle">{logo}</td>
                              <td valign="middle">
                                <div style="font-family:{Font};font-size:19px;font-weight:700;line-height:24px;color:#ffffff;">{Brand}</div>
                                <div style="font-family:{Font};font-size:12px;line-height:18px;color:{NavyText};margin-top:2px;">{School}</div>
                              </td>
                              <td valign="middle" align="right" style="font-family:{Font};font-size:11px;line-height:16px;color:{NavyText};white-space:nowrap;">
                                S.Y. {SchoolInfo.SchoolYear}
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>

                      <tr>
                        <td style="height:5px;background:{Primary};font-size:0;line-height:5px;">&nbsp;</td>
                      </tr>

                      <tr>
                        <td style="padding:34px 32px 10px;">{content}</td>
                      </tr>

                      <tr>
                        <td style="padding:0 32px;">
                          <div style="height:1px;background:{Line};font-size:0;line-height:1px;">&nbsp;</div>
                        </td>
                      </tr>

                      <tr>
                        <td style="padding:18px 32px 4px;font-family:{Font};font-size:12px;line-height:19px;color:{Muted};">
                          <strong style="color:{InkSoft};">{School}</strong><br>
                          {SchoolInfo.SchoolAddress} &middot; {SchoolInfo.Division}
                        </td>
                      </tr>

                      <tr>
                        <td style="padding:6px 32px 26px;font-family:{Font};font-size:11px;line-height:18px;color:{Muted};">
                          This is an automated message from {Brand} &mdash; please do not reply to this email.
                        </td>
                      </tr>

                    </table>

                    <div style="max-width:600px;margin:16px auto 0;font-family:{Font};font-size:11px;line-height:18px;color:{Muted};text-align:center;">
                      &copy; {DateTime.Now.Year} {School} &middot; All rights reserved.
                    </div>

                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }
}
