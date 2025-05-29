# Backend Registration and Authentication Integration Suggestions

This document outlines suggestions for integrating a user registration and authentication system into the LinguaLink backend. This would allow for official user accounts, API key management, and pave the way for potential future premium/paid service tiers.

## 1. Purpose and Benefits

Integrating a registration system offers several advantages:

*   **User Accounts**: Allows users to create persistent accounts, potentially storing preferences or history.
*   **API Key Management**:
    *   **Official Service**: Enables the official service to issue and manage API keys for authenticated users. This is crucial for tracking usage, implementing quotas, or offering tiered access.
    *   **Security**: Prevents anonymous, unrestricted access to the translation API.
*   **Service Tiers**: Forms the foundation for introducing different service levels (e.g., free tier with limits, paid tiers with higher quotas or premium features).
*   **Usage Analytics**: Provides a way to gather (anonymized) usage data to improve the service.

## 2. Key Backend Components

### 2.1. User Database

A database is required to store user information. Key fields would include:

*   `UserID` (Primary Key)
*   `Username` (Unique, for login)
*   `Email` (Unique, for registration, password recovery)
*   `PasswordHash` (Hashed and salted password, **never store plain text passwords**)
*   `ApiKey` (The API key associated with this user for the official service)
*   `SubscriptionTier` (e.g., 'free', 'basic', 'premium') - For future use
*   `RegistrationDate`
*   `LastLoginDate`

### 2.2. Authentication Endpoints

New API endpoints will be needed:

*   **`POST /api/v1/auth/register`**:
    *   **Request**: `username`, `email`, `password`
    *   **Response**: Success message or error (e.g., username/email taken).
    *   **Action**: Validates input, checks for existing user, creates new user record, hashes password, generates an initial API key (or prompts user to generate one).

*   **`POST /api/v1/auth/login`**:
    *   **Request**: `username` (or `email`), `password`
    *   **Response**:
        *   Success: `access_token` (e.g., JWT), `api_key` (for the official service), `user_info` (username, email, etc.)
        *   Failure: Authentication error message.
    *   **Action**: Validates credentials against the database. Issues an access token for session management.

*   **`POST /api/v1/auth/logout`**: (Optional, if using session tokens that need invalidation)
    *   **Request**: `access_token` (in header)
    *   **Response**: Success message.
    *   **Action**: Invalidates the access token.

*   **`POST /api/v1/auth/refresh_token`**: (Optional, for more robust session management)
    *   **Request**: `refresh_token`
    *   **Response**: New `access_token`.

*   **`POST /api/v1/auth/request_password_reset`**:
    *   **Request**: `email`
    *   **Response**: Success/failure message.
    *   **Action**: Generates a secure, time-limited password reset token and sends a reset link to the user's email.

*   **`POST /api/v1/auth/reset_password`**:
    *   **Request**: `reset_token`, `new_password`
    *   **Response**: Success/failure message.
    *   **Action**: Validates token, updates user's password.

### 2.3. API Key Management

*   **Generation**: API keys should be cryptographically strong, random strings.
*   **Association**: Each API key for the official service should be tied to a user account.
*   **Revocation/Regeneration**: Users should be able to revoke or regenerate their API keys through their account management interface (a future web dashboard, or potentially a client-side feature).
*   **Scope (Optional)**: For advanced scenarios, API keys could have scopes ограничивающие доступ к определенным функциям.

### 2.4. Translation Endpoint Modification

The existing `/api/v1/translate_audio` endpoint will need to be modified to check for authentication:

*   **If Official Service Mode**:
    *   Expect an `Authorization` header (e.g., `Bearer <access_token>` or an `X-API-Key` header).
    *   Validate the token or API key against the user database.
    *   Enforce usage quotas or feature limitations based on the user's account/subscription.
*   **If Custom/Self-Hosted Service Mode (Bypass Authentication)**:
    *   The client already supports providing a custom URL and API key. The self-hosted backend can choose to validate this API key or operate without strict authentication if the user configures it that way.
    *   The backend could have a configuration flag (e.g., `REQUIRE_AUTH=false`) to disable mandatory authentication for self-hosted instances.

## 3. Client (Frontend) Interaction

### 3.1. New Account Page Logic

The `AccountPageViewModel.cs` will need to handle:

*   **Switching Modes**:
    *   **Official Service**: UI elements for login (username, password), registration, password reset. Disables custom URL/API key fields.
    *   **Custom Service**: UI elements for custom Server URL and API Key. Disables official login form.

*   **Calling Auth Endpoints**:
    *   Implement methods to call `/register`, `/login`.
    *   Store `access_token` and `api_key` securely upon successful login (e.g., using `Windows.Storage.ApplicationData.Current.LocalSettings` or a more secure credential manager if available and appropriate for WPF).
    *   Handle and display errors from auth endpoints.

*   **Managing Login State**:
    *   Update `IsLoggedIn`, `LoggedInUsername` properties.
    *   Clear stored credentials on logout.

### 3.2. API Requests (TranslationService.cs)

*   When in **Official Service mode** and logged in:
    *   The `TranslationService` should automatically include the `access_token` (as a Bearer token in the `Authorization` header) or the user's `api_key` (in an `X-API-Key` header) with requests to the translation endpoint. The choice depends on the backend's preferred authentication method for API calls post-login.
*   When in **Custom Service mode**:
    *   Continue to use the user-provided Server URL and API Key from `AppSettings`.

## 4. Security Considerations

*   **Password Hashing**: **NEVER** store plain-text passwords. Use a strong, one-way hashing algorithm with a unique salt per user (e.g., Argon2, scrypt, bcrypt, or PBKDF2).
*   **Secure Credential Storage (Client)**: Sensitive data like access tokens or API keys should be stored as securely as possible on the client. Investigate options like the Windows Credential Manager for more robust storage than plain files or local settings if dealing with highly sensitive official tokens.
*   **HTTPS**: All communication between the client and backend **must** be over HTTPS to protect credentials and data in transit.
*   **Rate Limiting**: Implement rate limiting on authentication endpoints (login, registration, password reset) to prevent brute-force attacks.
*   **Input Validation**: Rigorously validate all inputs on the backend to prevent injection attacks and other vulnerabilities.
*   **CSRF Protection**: If a web-based account management portal is created, ensure CSRF protection is in place.
*   **API Key Security**: Treat API keys as sensitive credentials. Advise users not to embed them directly in publicly shared code if they are using the custom server option with a key.

## 5. Maintaining Open-Source Friendliness (Bypass Option)

The core principle is to allow users to run their own backend without needing to interact with an official registration system.

*   **Backend Configuration**:
    *   The backend server should have a clear configuration option (e.g., an environment variable or a config file setting like `ENABLE_AUTH=false` or `AUTH_MODE=self_hosted_key`).
    *   If `ENABLE_AUTH=false`, the backend would not require login/registration and might:
        *   Still accept an API key provided by the user (via client's custom service settings) and validate it against a simple list/config if desired for basic protection.
        *   Or, operate without any API key validation if the user prefers.
*   **Client-Side**:
    *   The client already supports a "Custom Service" mode where the user provides the Server URL and API Key. This mode should remain fully functional.
    *   The "Custom Service" settings in the client essentially become the way for users to connect to their self-hosted backend, regardless of whether that self-hosted backend implements its own simple API key check or no check at all.

## 6. Integration Steps Summary

1.  **Backend**:
    *   Design and implement the user database schema.
    *   Develop the authentication endpoints (`/register`, `/login`, etc.).
    *   Implement API key generation and association logic.
    *   Modify the translation endpoint to check for authentication (token/API key) when official service mode is active or configured.
    *   Add configuration options for enabling/disabling mandatory authentication for self-hosting.
2.  **Client (`lingualink_client`)**:
    *   Enhance `AccountPageViewModel` to manage official login UI and custom server UI.
    *   Implement calls to the new backend authentication endpoints.
    *   Securely store and manage tokens/API keys for the official service.
    *   Modify `TranslationService` to include authentication headers when using the official service.
    *   Ensure the "Save" button on the Account page correctly saves either official credentials (if implemented) or custom server URL/API Key based on the selected mode.

This approach provides a clear path to a more robust service with user accounts while respecting the open-source nature of the project by allowing users to bypass official registration when self-hosting. 