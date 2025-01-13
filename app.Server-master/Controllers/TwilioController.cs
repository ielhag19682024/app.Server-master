using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System;
using Twilio.Rest.Verify.V2.Service;
using System.Collections.Generic;
using Twilio.Exceptions;

namespace Generic.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class TwilioController : ControllerBase
    {
        private readonly SqlExecutor _sqlExecutor;

        private readonly string _twilioAccountSid = "AC3a8f2646973258e7619b036c3730970d";
        private readonly string _twilioAuthToken = "3fe49caad29abd25b4fef9f06fc751a7";
        private readonly string _twilioPhoneNumber = "VA7f474c4dd0d5b5540d047175ea9b0939";

        public TwilioController(SqlExecutor sqlExecutor)
        {
            _sqlExecutor = sqlExecutor;

            TwilioClient.Init(_twilioAccountSid, _twilioAuthToken);
        }

        [HttpPost("SendOtp")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.PhoneNumber))
                {
                    return BadRequest(new { Message = "Phone number is required." });
                }

                var verification = await VerificationResource.CreateAsync(
                    to: model.PhoneNumber,
                    channel: model.Channel,
                    pathServiceSid: _twilioPhoneNumber
                );

                Console.WriteLine($"Verification SID: {verification.Sid}");
                return Ok(new { Message = "OTP sent successfully.", VerificationSid = verification.Sid });
            }
            catch (TwilioException twilioEx)
            {
                return StatusCode(500, new
                {
                    Message = "An error occurred while sending OTP through Twilio.",
                    Details = twilioEx.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Message = "An unexpected error occurred while sending OTP.",
                    Details = ex.Message
                });
            }
        }


        [HttpPost("VerifyOtp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.PhoneNumber) || string.IsNullOrWhiteSpace(model.Otp))
                {
                    return BadRequest(new { Message = "Phone number and OTP are required." });
                }

                var verificationCheck = await VerificationCheckResource.CreateAsync(
                    to: model.PhoneNumber,
                    code: model.Otp,
                    pathServiceSid: _twilioPhoneNumber
                );

                if (verificationCheck.Status == "approved")
                {
                    var parameters = new Dictionary<string, object>
                    {
                        { "@PhoneNumber", model.PhoneNumber }
                    };

                    var result = await _sqlExecutor.ExecuteScalarAsync("sp_validate_phone_number", parameters);

                    Console.WriteLine($"Rows affected by the stored procedure: {result}");

                    if (Convert.ToInt32(result) > 0)
                    {
                        return Ok(new
                        {
                            Response = "success",
                            VerificationSid = verificationCheck.Sid,
                            Status = verificationCheck.Status
                        });
                    }
                    else
                    {
                        return StatusCode(500, new
                        {
                            Response = "fail",
                            VerificationSid = verificationCheck.Sid,
                            Status = verificationCheck.Status,
                            AffectedRows = result
                        });
                    }
                }
                else
                {
                    return BadRequest(new
                    {
                        Response = "fail",
                        VerificationSid = verificationCheck.Sid,
                        Status = verificationCheck.Status
                    });
                }
            }
            catch (TwilioException twilioEx)
            {
                return StatusCode(500, new
                {   
                    Response = "fail",
                    Message = "An error occurred while verifying OTP through Twilio.",
                    Details = twilioEx.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Response = "fail",
                    Message = "An unexpected error occurred during OTP verification.",
                    Details = ex.Message
                });
            }
        }

        public class SendOtpRequest
        {
            public string PhoneNumber { get; set; }

            public string Channel { get; set; }
        }

        public class VerifyOtpRequest
        {
            public string PhoneNumber { get; set; }
            public string Otp { get; set; }
        }
    }
}
