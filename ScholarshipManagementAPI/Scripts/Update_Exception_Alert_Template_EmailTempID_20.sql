-- ==============================================================================
-- Migration Script: Update Exception Alert HTML Template in zz_admin_email_template
-- Table: zz_admin_email_template
-- Target Record: EmailTempID = 20
-- ==============================================================================

UPDATE [dbo].[zz_admin_email_template]
SET 
    [Template] = N'<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>IFNOSS System Error</title>
</head>
<body style="margin: 0; padding: 0; background-color: #f4f6f8; font-family: -apple-system, BlinkMacSystemFont, ''Segoe UI'', Roboto, ''Helvetica Neue'', Arial, sans-serif; font-size: 13px; line-height: 1.5; color: #1e293b;">
  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color: #f4f6f8; padding: 24px 12px;">
    <tr>
      <td align="center">
        <!-- Card Container -->
        <table role="presentation" width="760" cellpadding="0" cellspacing="0" border="0" style="max-width: 760px; width: 100%; background-color: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05);">
          
          <!-- Header Bar with IFNOSS branding -->
          <tr>
            <td style="padding: 20px 24px; background-color: #ffffff; border-bottom: 2px solid #ef4444;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                <tr>
                  <td valign="middle">
                    <img src="###logoUrl###" alt="IFNOSS" height="40" style="display: block; border: 0; max-height: 40px; width: auto;" />
                  </td>
                  <td align="right" valign="middle">
                    <div style="font-size: 20px; font-weight: 700; color: #dc2626; letter-spacing: -0.3px; margin-bottom: 2px;">IFNOSS System Error</div>
                    <div style="font-size: 12px; color: #64748b;">Diagnostic Notification &bull; ###occurredOn###</div>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- Application Name -->
          <tr>
            <td style="padding: 12px 20px; background-color: #ffffff; border-bottom: 1px solid #e2e8f0; font-size: 14px; color: #0f172a;">
              <strong>Application: ###applicationName###</strong>
            </td>
          </tr>

          <!-- Section 1: Error Details -->
          <tr>
            <td style="padding: 0;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                <tr style="background-color: #f1f5f9;">
                  <th colspan="2" style="padding: 10px 20px; font-size: 12px; font-weight: 700; color: #334155; text-align: left; text-transform: uppercase; letter-spacing: 0.5px; border-bottom: 1px solid #e2e8f0;">
                    1. Error Details
                  </th>
                </tr>
                <tr>
                  <td style="width: 170px; padding: 10px 20px; font-size: 13px; font-weight: 700; color: #1e40af; border-bottom: 1px solid #f1f5f9; vertical-align: top; white-space: nowrap;"><strong>Error</strong></td>
                  <td style="padding: 10px 20px; font-size: 13px; color: #0f172a; border-bottom: 1px solid #f1f5f9; vertical-align: top; font-family: Consolas, monospace; font-weight: 700;"><strong>###exceptionType###</strong></td>
                </tr>
                <tr>
                  <td style="width: 170px; padding: 10px 20px; font-size: 13px; font-weight: 700; color: #1e40af; border-bottom: 1px solid #f1f5f9; vertical-align: top; white-space: nowrap;"><strong>Details</strong></td>
                  <td style="padding: 10px 20px; font-size: 13px; color: #b91c1c; border-bottom: 1px solid #f1f5f9; vertical-align: top; font-weight: 700;"><strong>###message###</strong></td>
                </tr>
                ###innerException###
              </table>
            </td>
          </tr>

          <!-- Section 2: Execution Information -->
          <tr>
            <td style="padding: 0;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                <tr style="background-color: #f1f5f9;">
                  <th colspan="2" style="padding: 10px 20px; font-size: 12px; font-weight: 700; color: #334155; text-align: left; text-transform: uppercase; letter-spacing: 0.5px; border-top: 1px solid #e2e8f0; border-bottom: 1px solid #e2e8f0;">
                    2. Execution Information
                  </th>
                </tr>
                <tr>
                  <td style="width: 170px; padding: 10px 20px; font-size: 13px; font-weight: 600; color: #1e40af; border-bottom: 1px solid #f1f5f9; vertical-align: top; white-space: nowrap;">Method</td>
                  <td style="padding: 10px 20px; font-size: 13px; color: #0f172a; border-bottom: 1px solid #f1f5f9; vertical-align: top;">
                    <span style="display: inline-block; padding: 2px 8px; background-color: #e0f2fe; color: #0369a1; border-radius: 4px; font-weight: 700; font-size: 12px;">###method###</span>
                  </td>
                </tr>
                <tr>
                  <td style="width: 170px; padding: 10px 20px; font-size: 13px; font-weight: 600; color: #1e40af; border-bottom: 1px solid #f1f5f9; vertical-align: top; white-space: nowrap;">Source</td>
                  <td style="padding: 10px 20px; font-size: 13px; color: #0f172a; border-bottom: 1px solid #f1f5f9; vertical-align: top;">###source###</td>
                </tr>
                <tr>
                  <td style="width: 170px; padding: 10px 20px; font-size: 13px; font-weight: 600; color: #1e40af; border-bottom: 1px solid #f1f5f9; vertical-align: top; white-space: nowrap;">Exception Source</td>
                  <td style="padding: 10px 20px; font-size: 13px; color: #0f172a; border-bottom: 1px solid #f1f5f9; vertical-align: top;">###exceptionSource###</td>
                </tr>
                <tr>
                  <td style="width: 170px; padding: 10px 20px; font-size: 13px; font-weight: 600; color: #1e40af; border-bottom: 1px solid #f1f5f9; vertical-align: top; white-space: nowrap;">Date & Time (UTC)</td>
                  <td style="padding: 10px 20px; font-size: 13px; color: #0f172a; border-bottom: 1px solid #f1f5f9; vertical-align: top;">###occurredOn###</td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- Section 3: Request Information -->
          <tr>
            <td style="padding: 0;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                <tr style="background-color: #f1f5f9;">
                  <th colspan="2" style="padding: 10px 20px; font-size: 12px; font-weight: 700; color: #334155; text-align: left; text-transform: uppercase; letter-spacing: 0.5px; border-top: 1px solid #e2e8f0; border-bottom: 1px solid #e2e8f0;">
                    3. Request Information
                  </th>
                </tr>
                <tr>
                  <td style="width: 170px; padding: 10px 20px; font-size: 13px; font-weight: 600; color: #1e40af; border-bottom: 1px solid #f1f5f9; vertical-align: top; white-space: nowrap;">URL</td>
                  <td style="padding: 10px 20px; font-size: 13px; color: #0f172a; border-bottom: 1px solid #f1f5f9; vertical-align: top; word-break: break-all;">
                    ###url###
                  </td>
                </tr>
                <tr>
                  <td style="width: 170px; padding: 10px 20px; font-size: 13px; font-weight: 600; color: #1e40af; border-bottom: 1px solid #f1f5f9; vertical-align: top; white-space: nowrap;">Request Host</td>
                  <td style="padding: 10px 20px; font-size: 13px; color: #0f172a; border-bottom: 1px solid #f1f5f9; vertical-align: top;">###requestHost###</td>
                </tr>
                <tr>
                  <td style="width: 170px; padding: 10px 20px; font-size: 13px; font-weight: 600; color: #1e40af; border-bottom: 1px solid #f1f5f9; vertical-align: top; white-space: nowrap;">Host Name</td>
                  <td style="padding: 10px 20px; font-size: 13px; color: #0f172a; border-bottom: 1px solid #f1f5f9; vertical-align: top;">###hostName###</td>
                </tr>
                <tr>
                  <td style="width: 170px; padding: 10px 20px; font-size: 13px; font-weight: 600; color: #1e40af; border-bottom: 1px solid #f1f5f9; vertical-align: top; white-space: nowrap;">Request IP Address</td>
                  <td style="padding: 10px 20px; font-size: 13px; color: #0f172a; border-bottom: 1px solid #f1f5f9; vertical-align: top;">###requestIp###</td>
                </tr>
                <tr>
                  <td style="width: 170px; padding: 10px 20px; font-size: 13px; font-weight: 600; color: #1e40af; border-bottom: 1px solid #f1f5f9; vertical-align: top; white-space: nowrap;">User Agent</td>
                  <td style="padding: 10px 20px; font-size: 13px; color: #475569; border-bottom: 1px solid #f1f5f9; vertical-align: top; word-break: break-all;">###userAgent###</td>
                </tr>
                <tr>
                  <td style="width: 170px; padding: 10px 20px; font-size: 13px; font-weight: 600; color: #1e40af; border-bottom: 1px solid #f1f5f9; vertical-align: top; word-break: break-all;">###urlReferer###</td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- Section 4: User Information -->
          <tr>
            <td style="padding: 0;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                <tr style="background-color: #f1f5f9;">
                  <th colspan="2" style="padding: 10px 20px; font-size: 12px; font-weight: 700; color: #334155; text-align: left; text-transform: uppercase; letter-spacing: 0.5px; border-top: 1px solid #e2e8f0; border-bottom: 1px solid #e2e8f0;">
                    4. User Information
                  </th>
                </tr>
                <tr>
                  <td style="width: 170px; padding: 10px 20px; font-size: 13px; font-weight: 600; color: #1e40af; border-bottom: 1px solid #f1f5f9; vertical-align: top; white-space: nowrap;">UserName</td>
                  <td style="padding: 10px 20px; font-size: 13px; color: #0f172a; border-bottom: 1px solid #f1f5f9; vertical-align: top;">###userName###</td>
                </tr>
                <tr>
                  <td style="width: 170px; padding: 10px 20px; font-size: 13px; font-weight: 600; color: #1e40af; border-bottom: 1px solid #f1f5f9; vertical-align: top; white-space: nowrap;">User ID</td>
                  <td style="padding: 10px 20px; font-size: 13px; color: #0f172a; border-bottom: 1px solid #f1f5f9; vertical-align: top;">###userId###</td>
                </tr>
                <tr>
                  <td style="width: 170px; padding: 10px 20px; font-size: 13px; font-weight: 600; color: #1e40af; border-bottom: 1px solid #f1f5f9; vertical-align: top; white-space: nowrap;">Role ID</td>
                  <td style="padding: 10px 20px; font-size: 13px; color: #0f172a; border-bottom: 1px solid #f1f5f9; vertical-align: top;">###roleId###</td>
                </tr>
                <tr>
                  <td style="width: 170px; padding: 10px 20px; font-size: 13px; font-weight: 600; color: #1e40af; border-bottom: 1px solid #f1f5f9; vertical-align: top; white-space: nowrap;">Current Sessions</td>
                  <td style="padding: 10px 20px; font-size: 12px; color: #334155; border-bottom: 1px solid #f1f5f9; vertical-align: top; line-height: 1.6;">###currentSessions###</td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- Section 5: Form Values -->
          <tr>
            <td style="padding: 0;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                <tr style="background-color: #f1f5f9;">
                  <th style="padding: 10px 20px; font-size: 12px; font-weight: 700; color: #334155; text-align: left; text-transform: uppercase; letter-spacing: 0.5px; border-top: 1px solid #e2e8f0; border-bottom: 1px solid #e2e8f0;">
                    5. Form Values
                  </th>
                </tr>
                <tr>
                  <td style="padding: 14px 20px; font-size: 13px; color: #334155; border-bottom: 1px solid #f1f5f9;">
                    ###formValues###
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- Section 6: Stack Trace -->
          <tr>
            <td style="padding: 0;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                <tr style="background-color: #f1f5f9;">
                  <th style="padding: 10px 20px; font-size: 12px; font-weight: 700; color: #334155; text-align: left; text-transform: uppercase; letter-spacing: 0.5px; border-top: 1px solid #e2e8f0; border-bottom: 1px solid #e2e8f0;">
                    6. Stack Trace
                  </th>
                </tr>
                <tr>
                  <td style="padding: 14px 20px;">
                    <pre style="margin: 0; padding: 14px; background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 4px; font-family: Consolas, ''Courier New'', monospace; font-size: 11.5px; line-height: 1.5; color: #334155; white-space: pre-wrap; word-break: break-all; max-height: 450px; overflow-y: auto;">###stackTrace###</pre>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td style="padding: 16px 20px; background-color: #f8fafc; border-top: 1px solid #e2e8f0; text-align: center; font-size: 12px; color: #64748b;">
              IFNOSS Scholarship Management System &bull; Automated Diagnostic Notification<br/>
              This diagnostic email was automatically generated and delivered to designated system administrators.
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>',
    [UpdatedDate] = SYSUTCDATETIME()
WHERE [EmailTempID] = 20;

-- Verification query
SELECT [EmailTempID], [TemplateName], [Subject], [IsActive], LEN([Template]) AS [TemplateLength], [UpdatedDate]
FROM [dbo].[zz_admin_email_template]
WHERE [EmailTempID] = 20;
