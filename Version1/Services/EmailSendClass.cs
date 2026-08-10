using DocumentFormat.OpenXml.Vml;
using Microsoft.Extensions.Hosting;
using Syncfusion.EJ2.Notifications;
using System.Net;
using System.Net.Mail;

namespace SQCScanner.Services
{
    public class EmailSendClass(IConfiguration _configuration, BravoServices _bravoServices)
    {
        private static readonly Random _otpGenerator = new Random();
        public async Task <String> emailRec(string ToEmail)
        {
            var otp = _otpGenerator.Next(100000, 1000000).ToString();
            var host = _configuration["SMTPSetting:Host"];
            var port = Convert.ToInt32(_configuration["SMTPSetting:Port"]);
            var enableSsl = Convert.ToBoolean(_configuration["SMTPSetting:EnableSsl"]);
            var email = _configuration["SMTPSetting:Email"];
            var pwd = _configuration["SMTPSetting:Password"];
            string htmlBody = GetOtpEmailTemplate(otp);
            using (var smtpClient = new SmtpClient(host, port))
            {
                smtpClient.Credentials = new NetworkCredential(email, pwd);
                smtpClient.EnableSsl = enableSsl;

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(email, "OMR IOS IT Team"),
                    Subject = "OTP Verification Code",
                    Body = htmlBody,
                    IsBodyHtml = true 
                };

                mailMessage.To.Add(ToEmail);
                await smtpClient.SendMailAsync(mailMessage);
            }
            return otp;
        }

        //Format of E-mail Template OTP 
        private string GetOtpEmailTemplate(string otpCode)
        {
             return $$"""
             <!DOCTYPE html>
             <html lang="en" xmlns="http://www.w3.org/1999/xhtml">
             <head>
                <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <meta http-equiv="X-UA-Compatible" content="IE=edge">
                <title>Security Verification Code</title>
                <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap" rel="stylesheet">
                <style>
                    body, table, td, a { -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; }
                    table, td { mso-table-lspace: 0pt; mso-table-rspace: 0pt; }
                    img { -ms-interpolation-mode: bicubic; border: 0; height: auto; line-height: 100%; outline: none; text-decoration: none; }
                    body { height: 100% !important; margin: 0 !important; padding: 0 !important; width: 100% !important; background-color: #F8FAFC; font-family: 'Plus Jakarta Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; }
                    @media screen and (max-width: 600px) {
                        .email-container { width: 100% !important; margin: auto !important; }
                        .content-padding { padding: 32px 20px !important; }
                        .otp-code { font-size: 32px !important; letter-spacing: 8px !important; }
                        .header-padding { padding: 32px 20px !important; }
                    }
                </style>
             </head>
             <body style="margin: 0; padding: 0; background-color: #F8FAFC; color: #0F172A;">
                <table border="0" cellpadding="0" cellspacing="0" width="100%" role="presentation" style="background-color: #F8FAFC; table-layout: fixed;">
                    <tr>
                        <td align="center" style="padding: 40px 16px;">
                            <table class="email-container" border="0" cellpadding="0" cellspacing="0" width="100%" role="presentation" style="max-width: 540px; background-color: #FFFFFF; border-radius: 16px; border: 1px solid #E2E8F0; overflow: hidden;">
                                <tr>
                                    <td style="height: 6px; background: linear-gradient(90deg, #4F46E5 0%, #818CF8 50%, #C084FC 100%);"></td>
                                </tr>
                                <tr>
                                    <td class="header-padding" align="center" style="padding: 40px 40px 20px 40px;">
                                        <table border="0" cellpadding="0" cellspacing="0" role="presentation">
                                            <tr>
                                                <td align="center">
                                                    <div style="background-color: #EEF2FF; border-radius: 12px; width: 48px; height: 48px; line-height: 48px; text-align: center; margin-bottom: 16px;">
                                                        <span style="font-size: 24px;">🛡️</span>
                                                    </div>
                                                </td>
                                            </tr>
                                            <tr>
                                                <td align="center">
                                                    <span style="font-size: 20px; font-weight: 800; letter-spacing: -0.5px; color: #0F172A; text-transform: uppercase;">
                                                        Image Base OMR<span style="color: #4F46E5;"> Application</span>
                                                    </span>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                                <tr>
                                    <td class="content-padding" style="padding: 0 40px 40px 40px;">
                                        <table border="0" cellpadding="0" cellspacing="0" width="100%" role="presentation">
                                            <tr>
                                                <td align="center" style="padding-bottom: 12px;">
                                                    <h1 style="margin: 0; font-size: 22px; font-weight: 700; color: #0F172A;">Verification Code</h1>
                                                </td>
                                            </tr>
                                            <tr>
                                                <td align="center" style="padding-bottom: 28px;">
                                                    <p style="margin: 0; font-size: 15px; line-height: 1.6; color: #64748B;">
                                                        Please enter the following one-time passcode to verify your account identity.
                                                    </p>
                                                </td>
                                            </tr>
                                            <tr>
                                                <td align="center" style="padding-bottom: 28px;">
                                                    <table border="0" cellpadding="0" cellspacing="0" role="presentation" style="width: 100%;">
                                                        <tr>
                                                            <td align="center" style="background-color: #F8FAFC; border: 1px dashed #C7D2FE; border-radius: 12px; padding: 24px 16px;">
                                                                <span class="otp-code" style="font-family: 'Courier New', Courier, monospace, sans-serif; font-size: 38px; font-weight: 800; color: #4F46E5; letter-spacing: 12px; padding-left: 12px;">{{otpCode}}</span>
                                                            </td>
                                                        </tr>
                                                    </table>
                                                </td>
                                            </tr>
                                            <tr>
                                                <td align="center" style="padding-bottom: 28px;">
                                                    <table border="0" cellpadding="0" cellspacing="0" role="presentation">
                                                        <tr>
                                                            <td style="background-color: #FFFBEB; border: 1px solid #FDE68A; border-radius: 20px; padding: 6px 14px;">
                                                                <span style="font-size: 13px; font-weight: 600; color: #B45309;">
                                                                    ⏳ Code expires in <strong>10 minutes</strong>
                                                                </span>
                                                            </td>
                                                        </tr>
                                                    </table>
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style="padding-top: 16px; border-top: 1px solid #F1F5F9;">
                                                    <p style="margin: 0; font-size: 13px; line-height: 1.5; color: #94A3B8; text-align: center;">
                                                        If you didn't request this code, you can safely ignore this email.
                                                    </p>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                                <tr>
                                    <td style="background-color: #F8FAFC; padding: 24px 40px; border-top: 1px solid #E2E8F0;">
                                        <table border="0" cellpadding="0" cellspacing="0" width="100%" role="presentation">
                                            <tr>
                                                <td align="center">
                                                    <p style="margin: 0; font-size: 12px; color: #94A3B8; line-height: 1.5;">
                                                        © SQCScanner. All rights reserved.
                                                    </p>
                                                </td>
                                            </tr>
                                        </table>
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
}