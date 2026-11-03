using System.Net;

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
    private const string Primary = "#2f6fed";
    private const string Navy = "#102a4c";
    private const string NavyText = "#c3d0e4";
    private const string Ink = "#16202c";
    private const string InkSoft = "#3b4855";
    private const string Muted = "#6b7a90";
    private const string Line = "#e4e9f2";
    private const string Bg = "#f4f6fb";

    /// <summary>Activation email sent immediately after a student registers.</summary>
    public static string AccountActivation(string? fullName, string confirmationLink, string logoUrl)
    {
        var greeting = string.IsNullOrWhiteSpace(fullName)
            ? "Hello,"
            : $"Hi {WebUtility.HtmlEncode(fullName.Trim())},";

        // Encode so query-string "&" stays valid inside the href attribute.
        var link = WebUtility.HtmlEncode(confirmationLink);

        var content = $"""
            <h1 style="margin:0 0 8px;font-family:{Font};font-size:22px;line-height:30px;font-weight:700;color:{Ink};">
              Activate your account
            </h1>
            <p style="margin:0 0 20px;font-family:{Font};font-size:15px;line-height:24px;color:{InkSoft};">
              {greeting}
            </p>
            <p style="margin:0 0 20px;font-family:{Font};font-size:15px;line-height:24px;color:{InkSoft};">
              Welcome to <strong>{Brand}</strong>, the online portal of {School}.
              Your registration was received &mdash; confirm your email address to finish setting up your account.
            </p>

            <table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 0 24px;">
              <tr>
                <td align="center" bgcolor="{Primary}" style="border-radius:10px;">
                  <a href="{link}"
                     style="display:inline-block;padding:14px 34px;font-family:{Font};font-size:15px;font-weight:600;line-height:20px;color:#ffffff;text-decoration:none;border-radius:10px;">
                    Activate my account
                  </a>
                </td>
              </tr>
            </table>

            <p style="margin:0 0 6px;font-family:{Font};font-size:13px;line-height:20px;color:{Muted};">
              Button not working? Copy this link into your browser:
            </p>
            <p style="margin:0 0 24px;font-family:{Font};font-size:13px;line-height:20px;word-break:break-all;">
              <a href="{link}" style="color:{Primary};text-decoration:underline;">{link}</a>
            </p>

            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:{Bg};border-radius:12px;">
              <tr>
                <td style="padding:16px 18px;font-family:{Font};font-size:13px;line-height:20px;color:{Muted};">
                  <strong style="color:{InkSoft};">Didn't sign up?</strong><br>
                  You can safely ignore this email. No account is activated unless you click the button above.
                </td>
              </tr>
            </table>
            """;

        return Shell("Confirm your email address to finish setting up your SchoolEase account.", content, logoUrl);
    }

    /// <summary>Shared outer shell so every message looks like the same brand.</summary>
    private static string Shell(string preheader, string content, string logoUrl)
    {
        var logo = string.IsNullOrWhiteSpace(logoUrl)
            ? string.Empty
            : $"""<img src="{logoUrl}" alt="{Brand}" height="40" style="display:block;height:40px;width:auto;border-radius:8px;">""";

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
                  <td align="center" style="padding:32px 16px;">

                    <table role="presentation" width="600" cellpadding="0" cellspacing="0"
                           style="width:100%;max-width:600px;background:#ffffff;border:1px solid {Line};border-radius:16px;overflow:hidden;">

                      <tr>
                        <td style="background:{Navy};padding:24px 32px;">
                          <table role="presentation" cellpadding="0" cellspacing="0">
                            <tr>
                              <td style="padding-right:14px;" valign="middle">{logo}</td>
                              <td valign="middle">
                                <div style="font-family:{Font};font-size:18px;font-weight:700;line-height:24px;color:#ffffff;">{Brand}</div>
                                <div style="font-family:{Font};font-size:12px;line-height:18px;color:{NavyText};">{School}</div>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>

                      <tr>
                        <td style="height:4px;background:{Primary};font-size:0;line-height:4px;">&nbsp;</td>
                      </tr>

                      <tr>
                        <td style="padding:32px;">{content}</td>
                      </tr>

                      <tr>
                        <td style="padding:20px 32px 26px;background:#fbfcfe;border-top:1px solid {Line};font-family:{Font};font-size:12px;line-height:19px;color:{Muted};">
                          <strong style="color:{InkSoft};">{School}</strong><br>
                          This is an automated message from {Brand} &mdash; please do not reply to this email.
                        </td>
                      </tr>

                    </table>

                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }
}
