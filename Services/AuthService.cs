using InventarioAPI.Models;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Data;
using System.Security.Cryptography;

namespace InventarioAPI.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task<bool> ValidateTokenAsync(string token);
        Task<UserInfo?> GetUserByTokenAsync(string token);
        Task UpdateLastAccessAsync(int userId);
        Task LogUserActivityAsync(int userId, string action, string description, string? metadata = null);
    }

    public class AuthService : IAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IConfiguration configuration, ILogger<AuthService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _connectionString = _configuration.GetConnectionString("InventarioConnection") 
                ?? throw new InvalidOperationException("Connection string not found");
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                _logger.LogInformation("Intento de login para usuario: {Usuario}", request.Usuario);

                // Validar credenciales en la base de datos
                var user = await ValidateUserCredentialsAsync(request.Usuario, request.Password);
                
                if (user == null)
                {
                    _logger.LogWarning("Login fallido para usuario: {Usuario} - Credenciales inválidas", request.Usuario);
                    
                    // Registrar intento fallido
                    await LogFailedLoginAttemptAsync(request.Usuario);
                    
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Usuario o contraseña incorrectos"
                    };
                }

                // Verificar si el usuario está activo
                if (!user.Activo)
                {
                    _logger.LogWarning("Login fallido para usuario: {Usuario} - Usuario inactivo", request.Usuario);
                    
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "La cuenta de usuario está desactivada"
                    };
                }

                // Generar token JWT
                var token = GenerateJwtToken(user);
                var expiresAt = DateTime.UtcNow.AddHours(
                    int.Parse(_configuration["JwtSettings:ExpirationHours"] ?? "8")
                );

                // Actualizar último acceso
                await UpdateLastAccessAsync(user.Id);

                // Registrar login exitoso
                await LogUserActivityAsync(user.Id, "login", $"Usuario {user.Usuario} inició sesión exitosamente", 
                                         $"{{\"ip\": \"{GetClientIpAddress()}\", \"userAgent\": \"{GetUserAgent()}\"}}");

                _logger.LogInformation("Login exitoso para usuario: {Usuario} (ID: {UserId})", user.Usuario, user.Id);

                return new LoginResponse
                {
                    Success = true,
                    Message = "Login exitoso",
                    Token = token,
                    User = user,
                    ExpiresAt = expiresAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la autenticación para usuario: {Usuario}", request.Usuario);
                
                return new LoginResponse
                {
                    Success = false,
                    Message = "Error durante la autenticación. Por favor, intente más tarde."
                };
            }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                if (string.IsNullOrEmpty(token))
                    return false;

                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtSettings = _configuration.GetSection("JwtSettings");
                var secretKey = jwtSettings["SecretKey"] ?? 
                    throw new InvalidOperationException("JWT Secret Key not found");

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = key,
                    ClockSkew = TimeSpan.Zero
                };

                SecurityToken validatedToken;
                var principal = tokenHandler.ValidateToken(token, validationParameters, out validatedToken);

                // Verificar que el usuario sigue activo
                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdClaim, out int userId))
                {
                    return await IsUserActiveAsync(userId);
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return false;
            }
        }

        public async Task<UserInfo?> GetUserByTokenAsync(string token)
        {
            try
            {
                if (!await ValidateTokenAsync(token))
                    return null;

                var tokenHandler = new JwtSecurityTokenHandler();
                var jwt = tokenHandler.ReadJwtToken(token);

                var userIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdClaim, out int userId))
                {
                    return await GetUserByIdAsync(userId);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by token");
                return null;
            }
        }

      public async Task UpdateLastAccessAsync(int userId)
{
    try
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string query = @"
            UPDATE USUARIOS 
            SET UltimoAcceso = GETDATE(), FechaActualizacion = GETDATE()
            WHERE ID = @UserId AND IsActive = 1";

        using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

        await command.ExecuteNonQueryAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error updating last access for user {UserId}", userId);
        // No lanzar excepción para no afectar el login
    }
}
        public async Task LogUserActivityAsync(int userId, string action, string description, string? metadata = null)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    INSERT INTO UserActivity (UserId, Action, Description, EntityType, Metadata, CreatedAt)
                    VALUES (@UserId, @Action, @Description, @EntityType, @Metadata, GETDATE())";

                using var command = new SqlCommand(query, connection);
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                command.Parameters.Add("@Action", SqlDbType.NVarChar, 50).Value = action;
                command.Parameters.Add("@Description", SqlDbType.NVarChar, 500).Value = description;
                command.Parameters.Add("@EntityType", SqlDbType.NVarChar, 50).Value = "auth";
                command.Parameters.Add("@Metadata", SqlDbType.NVarChar).Value = metadata ?? (object)DBNull.Value;

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging user activity for user {UserId}", userId);
                // No lanzar excepción para no afectar el flujo principal
            }
        }

        // Métodos privados
        private async Task<UserInfo?> ValidateUserCredentialsAsync(string usuario, string password)
{
    const string query = @"
        SELECT ID, USUARIO, NOMBRE, EMAIL, PERFIL, TIENDA, AREA, IsActive
        FROM USUARIOS 
        WHERE USUARIO = @Usuario AND PASSWORD = @Password AND IsActive = 1";

    using var connection = new SqlConnection(_connectionString);
    using var command = new SqlCommand(query, connection);
    
    command.Parameters.Add("@Usuario", SqlDbType.NVarChar, 50).Value = usuario;
    command.Parameters.Add("@Password", SqlDbType.NVarChar, 255).Value = HashPassword(password);
    
    await connection.OpenAsync();
    
    using var reader = await command.ExecuteReaderAsync();
    
    if (await reader.ReadAsync())
    {
        return new UserInfo
        {
            Id = Convert.ToInt32(reader["ID"]),
            Usuario = reader["USUARIO"]?.ToString() ?? "",
            Nombre = reader["NOMBRE"]?.ToString() ?? "",
            Email = reader["EMAIL"]?.ToString() ?? "",
            Perfil = reader["PERFIL"]?.ToString() ?? "",
            Tienda = reader["TIENDA"]?.ToString() ?? "",
            Area = reader["AREA"]?.ToString() ?? "",
            Activo = Convert.ToBoolean(reader["IsActive"])
        };
    }
    
    return null;
}

        private async Task<UserInfo?> GetUserByIdAsync(int userId)
        {
            const string query = @"
        SELECT ID, USUARIO, NOMBRE, EMAIL, PERFIL, TIENDA, AREA, IsActive
        FROM USUARIOS 
        WHERE ID = @UserId AND IsActive = 1";

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(query, connection);

            command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

            await connection.OpenAsync();

            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new UserInfo
                {
                    Id = Convert.ToInt32(reader["ID"]),
                    Usuario = reader["USUARIO"]?.ToString() ?? "",
                    Nombre = reader["NOMBRE"]?.ToString() ?? "",
                    Email = reader["EMAIL"]?.ToString() ?? "",
                    Perfil = reader["PERFIL"]?.ToString() ?? "",
                    Tienda = reader["TIENDA"]?.ToString() ?? "",
                    Area = reader["AREA"]?.ToString() ?? "",
                    Activo = Convert.ToBoolean(reader["IsActive"])
                };
            }

            return null;
        }
        private async Task<bool> IsUserActiveAsync(int userId)
        {
            const string query = "SELECT IsActive FROM USUARIOS WHERE ID = @UserId";

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(query, connection);

            command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

            await connection.OpenAsync();

            var result = await command.ExecuteScalarAsync();
            return result != null && Convert.ToBoolean(result);
        }

        private async Task LogFailedLoginAttemptAsync(string usuario)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                const string query = @"
                    INSERT INTO UserActivity (UserId, Action, Description, EntityType, Metadata, CreatedAt)
                    VALUES (0, @Action, @Description, @EntityType, @Metadata, GETDATE())";

                using var command = new SqlCommand(query, connection);
                command.Parameters.Add("@Action", SqlDbType.NVarChar, 50).Value = "login_failed";
                command.Parameters.Add("@Description", SqlDbType.NVarChar, 500).Value = $"Login fallido para usuario: {usuario}";
                command.Parameters.Add("@EntityType", SqlDbType.NVarChar, 50).Value = "auth";
                command.Parameters.Add("@Metadata", SqlDbType.NVarChar).Value =
                    $"{{\"usuario\": \"{usuario}\", \"ip\": \"{GetClientIpAddress()}\", \"timestamp\": \"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}\"}}";

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging failed login attempt");
            }
        }

        private string GenerateJwtToken(UserInfo user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"] ?? 
                throw new InvalidOperationException("JWT Secret Key not found");
            
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Usuario),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim("nombre", user.Nombre),
                new Claim("perfil", user.Perfil),
                new Claim("tienda", user.Tienda),
                new Claim("area", user.Area ?? ""),
                new Claim("activo", user.Activo.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, 
                    new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(), 
                    ClaimValueTypes.Integer64)
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(
                    int.Parse(jwtSettings["ExpirationHours"] ?? "8")
                ),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string HashPassword(string password)
        {
            // En producción, usar BCrypt, Argon2 o similar
            // Por ahora usamos SHA256 con salt para compatibilidad
            using var sha256 = SHA256.Create();
            var saltedPassword = password + (_configuration["Security:PasswordSalt"] ?? "default_salt_123");
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(saltedPassword));
            return Convert.ToBase64String(hashedBytes);
        }

        private string GetClientIpAddress()
        {
            // En un contexto real, esto se obtendría del HttpContext
            // Por ahora devolvemos un placeholder
            return "127.0.0.1";
        }

        private string GetUserAgent()
        {
            // En un contexto real, esto se obtendría del HttpContext
            // Por ahora devolvemos un placeholder
            return "InventarioAPI/1.0";
        }
    }
}