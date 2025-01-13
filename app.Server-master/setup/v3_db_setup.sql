-- Drop and recreate the database
IF EXISTS (SELECT name FROM sys.databases WHERE name = 'v3_Users')
BEGIN
    DROP DATABASE v3_Users;
END
GO

CREATE DATABASE v3_Users;
GO

USE v3_Users;
GO

-- Create Roles table
CREATE TABLE Roles (
    role_id INT IDENTITY(1,1) PRIMARY KEY,
    role_name VARCHAR(50) NOT NULL UNIQUE,
    is_active BIT DEFAULT 1
);
GO

-- Create Users table
CREATE TABLE Users (
    user_id INT IDENTITY(10001,1) PRIMARY KEY,
    first_name VARCHAR(50) NOT NULL,
    last_name VARCHAR(50) NOT NULL,
    email VARCHAR(100) NOT NULL UNIQUE,
    email_is_verified BIT DEFAULT 0,
    phone VARCHAR(15) NOT NULL UNIQUE,
    phone_is_verified BIT DEFAULT 0,
    password VARCHAR(255) NOT NULL,
    failed_attempts INT DEFAULT 0,
    is_active BIT DEFAULT 1, 
    is_blocked BIT DEFAULT 0, 
    blocked_desc VARCHAR(50),
    created_by INT,
    created_at DATETIME DEFAULT GETDATE(),
    updated_by INT,
    updated_at DATETIME,
    FOREIGN KEY (created_by) REFERENCES Users(user_id),
    FOREIGN KEY (updated_by) REFERENCES Users(user_id)
);
GO

-- Create UserRoles table
CREATE TABLE UserRoles (
    user_id INT NOT NULL,
    role_id INT NOT NULL,
    is_active BIT DEFAULT 1,
    PRIMARY KEY (user_id, role_id),
    FOREIGN KEY (user_id) REFERENCES Users(user_id) ON DELETE CASCADE,
    FOREIGN KEY (role_id) REFERENCES Roles(role_id) ON DELETE CASCADE
);
GO


CREATE PROCEDURE [dbo].[sp_signup_user]
    @FirstName NVARCHAR(50),
    @LastName NVARCHAR(50),
    @Email NVARCHAR(100),
    @Phone NVARCHAR(15),
    @Password NVARCHAR(255),
    @CreatedBy INT = NULL,
    @CreatedAt DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    -- Declare a table variable for output
    DECLARE @ResponseTable TABLE (
        response NVARCHAR(10),
        user_id INT NULL,
        first_name NVARCHAR(50) NULL,
        last_name NVARCHAR(50) NULL,
        email NVARCHAR(100) NULL,
        phone NVARCHAR(15) NULL,
        msg NVARCHAR(255) NULL
    );

    -- Check if email already exists
    IF EXISTS (SELECT 1 FROM Users WHERE email = @Email)
    BEGIN
        INSERT INTO @ResponseTable (response, msg)
        VALUES ('fail', 'Email is already in use.');
        SELECT * FROM @ResponseTable;
        RETURN;
    END

    -- Check if phone number already exists
    IF EXISTS (SELECT 1 FROM Users WHERE phone = @Phone)
    BEGIN
        INSERT INTO @ResponseTable (response, msg)
        VALUES ('fail', 'Phone number is already in use.');
        SELECT * FROM @ResponseTable;
        RETURN;
    END

    -- Insert new user into Users table
    DECLARE @NewUserId INT;
    BEGIN TRY
        INSERT INTO Users (
            first_name,
            last_name,
            email,
            phone,
            password,
            created_by,
            created_at
        )
        VALUES (
            @FirstName,
            @LastName,
            @Email,
            @Phone,
            @Password,
            @CreatedBy,
            @CreatedAt
        );

        -- Get the user ID of the newly created user
        SET @NewUserId = SCOPE_IDENTITY();

        -- If no CreatedBy was provided, set created_by to the new user's own ID
        IF @CreatedBy IS NULL
        BEGIN
            UPDATE Users
            SET created_by = @NewUserId
            WHERE user_id = @NewUserId;
        END

        -- Insert success response into the table variable
        INSERT INTO @ResponseTable (response, user_id, first_name, last_name, email, phone)
        VALUES ('success', @NewUserId, @FirstName, @LastName, @Email, @Phone);

    END TRY
    BEGIN CATCH
        -- Handle unexpected errors
        INSERT INTO @ResponseTable (response, msg)
        VALUES ('fail', ERROR_MESSAGE());
    END CATCH

    -- Return the response table
    SELECT * FROM @ResponseTable;
END;
GO

CREATE PROCEDURE [dbo].[sp_login_user]
    @EmailOrPhone NVARCHAR(100),
    @Password NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    -- Declare variables
    DECLARE @UserId INT;
    DECLARE @PasswordInDB NVARCHAR(255);
    DECLARE @FailedAttempts INT;
    DECLARE @IsBlocked BIT;
    DECLARE @FirstName NVARCHAR(50);
    DECLARE @LastName NVARCHAR(50);
    DECLARE @Email NVARCHAR(100);
    DECLARE @EmailIsVerified BIT;
    DECLARE @Phone NVARCHAR(15);
    DECLARE @PhoneIsVerified BIT;

    -- Check if the user exists by email or phone
    SELECT 
        @UserId = user_id,
        @PasswordInDB = password,
        @FailedAttempts = failed_attempts,
        @IsBlocked = is_blocked,
        @FirstName = first_name,
        @LastName = last_name,
        @Email = email,
        @EmailIsVerified = email_is_verified,
        @Phone = phone,
        @PhoneIsVerified = phone_is_verified
    FROM 
        Users
    WHERE 
        (email = @EmailOrPhone OR phone = @EmailOrPhone) AND is_active = 1;

    -- If the user does not exist
    IF @UserId IS NULL
    BEGIN
        SELECT 
            'fail' AS response,
            'Invalid credentials.' AS msg;
        RETURN;
    END

    -- Check if the account is blocked
    IF @IsBlocked = 1
    BEGIN
        SELECT 
            'fail' AS response,
            'Account is blocked. Please contact support.' AS msg;
        RETURN;
    END

    -- Verify the password
    IF @PasswordInDB = @Password
    BEGIN
        -- Successful login
        UPDATE Users
        SET 
            failed_attempts = 0, -- Reset failed attempts
            updated_at = GETDATE(), -- Update the last login timestamp
            updated_by = @UserId -- Set the user_id as the one updating
        WHERE 
            user_id = @UserId;

        -- Return user details
        SELECT 
            'success' AS response,
            @UserId AS [user_id],
            @FirstName AS [first_name],
            @LastName AS [last_name],
            @Email AS [email],
            @EmailIsVerified AS [email_is_verified],
            @Phone AS [phone],
            @PhoneIsVerified AS [phone_is_verified];
    END
    ELSE
    BEGIN
        -- Increment failed attempts
        SET @FailedAttempts = @FailedAttempts + 1;

        -- Block account if failed attempts reach 3
        IF @FailedAttempts >= 3
        BEGIN
            UPDATE Users
            SET 
                is_blocked = 1, 
                blocked_desc = 'incorrect password',
                updated_at = GETDATE(),
                updated_by = @UserId -- Set the user_id as the one updating
            WHERE 
                user_id = @UserId;

            SELECT 
                'fail' AS response,
                'Account has been blocked due to 3 consecutive failed login attempts.' AS msg;
        END
        ELSE
        BEGIN
            -- Update failed attempts count
            UPDATE Users
            SET 
                failed_attempts = @FailedAttempts,
                updated_at = GETDATE(),
                updated_by = @UserId -- Set the user_id as the one updating
            WHERE 
                user_id = @UserId;

            SELECT 
                'fail' AS response,
                'Invalid credentials.' AS msg;
        END
    END
END;
GO

CREATE PROCEDURE [dbo].[sp_validate_phone_number]
    @PhoneNumber VARCHAR(15)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Users
    SET phone_is_verified = 1
    WHERE phone = @PhoneNumber;

    select @@ROWCOUNT as 'count';
END;
GO

CREATE PROCEDURE [dbo].[sp_update_user_password](
	@userID INT,
	@oldPassword VARCHAR(255),
	@newPassword VARCHAR(255)
)
AS
BEGIN
	UPDATE Users
	SET
		password = @newPassword
	WHERE
		user_id = @userID AND
		password = @oldPassword

	SELECT @@ROWCOUNT AS 'RowCount'
END
GO

CREATE PROCEDURE [dbo].[sp_deactivate_user](
	@userID INT,
	@password VARCHAR(255)
)
AS
BEGIN
	UPDATE Users
	SET 
		is_active=0
	WHERE
		user_id=@userID and
		password=@password

	SELECT @@ROWCOUNT AS 'RowCount'
END
GO
