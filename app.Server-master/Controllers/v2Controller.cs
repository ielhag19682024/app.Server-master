using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace Generic.server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class v2Controller : Controller
    {
        private readonly SqlExecutor _sqlExecutor;

        public v2Controller(SqlExecutor sqlExecutor)
        {
            _sqlExecutor = sqlExecutor;
        }

        // SignUp API Call
        [HttpPost("SignUp")]
        public async Task<IActionResult> SignUp([FromBody] UserSignUpRequest request)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(request.FirstName) ||
                string.IsNullOrWhiteSpace(request.LastName) ||
                string.IsNullOrWhiteSpace(request.Phone) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { response = "fail", msg = "All fields are mandatory." });
            }

            // Hash the password and get the current UTC time
            string hashedPassword = HashPassword(request.Password);
            var createdAt = DateTime.UtcNow;

            // Prepare parameters for the stored procedure
            var parameters = new Dictionary<string, object>
            {
                { "@FirstName", request.FirstName },
                { "@LastName", request.LastName },
                { "@Email", request.Email },
                { "@Phone", request.Phone },
                { "@Password", hashedPassword },
                { "@CreatedBy", request.CreatedBy }, // Can be null if not provided
                { "@CreatedAt", createdAt }
            };

            try
            {
                // Execute the stored procedure
                var result = await _sqlExecutor.ExecuteQueryAsync("sp_signup_user", parameters);

                // Check if the procedure returned rows (indicating success or failure)
                if (result.Rows.Count > 0)
                {
                    var response = result.Rows[0]["response"].ToString();

                    if (response == "success")
                    {
                        // Successful signup, return user details
                        return Ok(new
                        {
                            response = "success",
                            user_id = Convert.ToInt32(result.Rows[0]["user_id"]),
                            first_name = result.Rows[0]["first_name"].ToString(),
                            last_name = result.Rows[0]["last_name"].ToString(),
                            email = result.Rows[0]["email"].ToString(),
                            phone = result.Rows[0]["phone"].ToString()
                        });
                    }
                    else if (response == "fail")
                    {
                        // Failed signup, return error message
                        return BadRequest(new
                        {
                            response = "fail",
                            msg = result.Rows[0]["msg"].ToString()
                        });
                    }
                }

                // Unexpected condition, return a server error
                return StatusCode(500, new { response = "fail", msg = "An unexpected error occurred." });
            }
            catch (Exception ex)
            {
                // Handle exceptions and return internal server error
                return StatusCode(500, new
                {
                    response = "fail",
                    msg = "An internal error occurred while processing your request.",
                    details = ex.Message
                });
            }
        }


        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] UserLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.EmailOrPhone) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { Message = "Email/Phone and password are mandatory." });
            }

            // Hash the password before sending it to the stored procedure
            string hashedPassword = HashPassword(request.Password);

            var parameters = new Dictionary<string, object>
            {
                { "@EmailOrPhone", request.EmailOrPhone },
                { "@Password", hashedPassword }
            };

            try
            {
                var result = await _sqlExecutor.ExecuteQueryAsync("sp_login_user", parameters);

                // Check if the stored procedure returned any result
                if (result.Rows.Count > 0)
                {
                    var user = result.Rows[0];
                    string response = user["response"].ToString();

                    if (response == "success")
                    {
                        return Ok(new
                        {
                            Response = response,
                            UserId = Convert.ToInt32(user["user_id"]),
                            FirstName = user["first_name"].ToString(),
                            LastName = user["last_name"].ToString(),
                            Email = user["email"].ToString(),
                            EmailIsVerified = Convert.ToBoolean(user["email_is_verified"]),
                            Phone = user["phone"].ToString(),
                            PhoneIsVerified = Convert.ToBoolean(user["phone_is_verified"]),
                            Message = "Login successful."
                        });
                    }
                    else if (response == "fail" && user["msg"].ToString() == "account blocked")
                    {
                        return Unauthorized(new
                        {
                            Response = response,
                            Message = "Account is blocked. Please contact support."
                        });
                    }
                    else if (response == "fail" && user["msg"].ToString() == "invalid credentials")
                    {
                        return Unauthorized(new
                        {
                            Response = response,
                            Message = "Invalid email/phone or password."
                        });
                    }
                }

                return Unauthorized(new
                {
                    Response = "fail",
                    Message = "Invalid email/phone or password."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Response = "error",
                    Message = "An internal error occurred while processing your request.",
                    Details = ex.Message
                });
            }
        }


        [HttpPost("Deactivate")]
        public async Task<IActionResult> Deactivate([FromBody] UserDeactivateRequest request)
        {

            if (request.userID <= 0 ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { Message = "UserID and password are mandatory." });
            }

            // Hash the password before sending it to the stored procedure
            string hashedPassword = HashPassword(request.Password);

            var parameters = new Dictionary<string, object>
            {
                { "@userID", request.userID },
                { "@password", hashedPassword }
            };

            try
            {
                var result = await _sqlExecutor.ExecuteQueryAsync("sp_deactivate_user", parameters);

                if (result.Rows.Count > 0)
                {
                    var rowCount = Convert.ToInt32(result.Rows[0]["RowCount"]);

                    if (rowCount == 1)
                    {
                        return Ok(new { Response = "success", Message = "Account deactivated successfully." });
                    }
                    else
                    {
                        return BadRequest(new { Response = "fail", Message = "Incorrect password." });
                    }
                }

                return StatusCode(500, new { Response = "fail", Message = "An unexpected error occurred." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Response = "fail", Message = "An error occurred: " + ex.Message });
            }

        }


        [HttpPost("UpdatePassword")]
        public async Task<IActionResult> UpdatePassword([FromBody] UserUpdatePasswordRequest request)
        {
            if (request.userID <= 0 ||
                string.IsNullOrWhiteSpace(request.OldPassword) ||
                string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new { Response = "fail", Message = "all fields are mandatory." });
            }

            string hashedOldPassword = HashPassword(request.OldPassword);
            string hashedNewPassword = HashPassword(request.NewPassword);

            var parameters = new Dictionary<string, object>
            {
                { "@userID", request.userID },
                { "@oldPassword", hashedOldPassword },
                { "@newPassword", hashedNewPassword }
            };

            try
            {
                var result = await _sqlExecutor.ExecuteQueryAsync("sp_update_user_password", parameters);

                if (result.Rows.Count > 0)
                {
                    var rowCount = Convert.ToInt32(result.Rows[0]["RowCount"]);

                    if (rowCount == 1)
                    {
                        return Ok(new { Response = "success", Message = "Password updated successfully." });
                    }
                    else
                    {
                        return BadRequest(new { Response = "fail", Message = "Incorrect old password." });
                    }
                }

                return StatusCode(500, new { Response = "fail", Message = "An unexpected error occurred." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Response = "fail", Message = "An error occurred: " + ex.Message });
            }
        }



        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }


        public class UserLoginRequest
        {
            public string EmailOrPhone { get; set; }
            public string Password { get; set; }
        }

        public class UserSignUpRequest
        {
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Password { get; set; }
            public string? CreatedBy { get; set; } 
        }

        public class UserDeactivateRequest {
            public int userID { get; set; }
            public string Password { get; set; }
        }

        public class UserUpdatePasswordRequest
        {
            public int userID { get; set; }
            public string OldPassword { get; set; }
            public string NewPassword { get; set; }
        }



    }
}
